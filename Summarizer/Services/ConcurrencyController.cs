using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using System.Collections.Concurrent;

namespace Summarizer.Services;

/// <summary>
/// 併發控制器 - 管理 API 請求的併發數量和動態調整
/// </summary>
public class ConcurrencyController : IDisposable
{
    private readonly BatchProcessingConfig _config;
    private readonly ILogger<ConcurrencyController> _logger;
    
    private int _currentConcurrency;
    private readonly Queue<TimeSpan> _recentResponseTimes = new();
    private readonly Queue<bool> _recentApiResults = new();
    private readonly object _adjustmentLock = new();
    private readonly Timer _adjustmentTimer;
    private readonly SemaphoreSlim _concurrencyLimiter;
    
    private readonly ConcurrentDictionary<string, DateTime> _lastRequestTimes = new();
    private readonly ConcurrentDictionary<string, int> _activeRequests = new();
    
    private bool _disposed = false;

    /// <summary>
    /// 建構函式
    /// </summary>
    public ConcurrencyController(
        IOptions<BatchProcessingConfig> config,
        ILogger<ConcurrencyController> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _currentConcurrency = _config.DefaultConcurrentLimit;
        _concurrencyLimiter = new SemaphoreSlim(_currentConcurrency, _config.MaxConcurrentLimit);
        
        // 每 30 秒檢查一次併發調整
        _adjustmentTimer = new Timer(CheckAndAdjustConcurrency, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        _logger.LogInformation("併發控制器已初始化，初始併發數: {Concurrency}", _currentConcurrency);
    }

    /// <summary>
    /// 當前併發數
    /// </summary>
    public int CurrentConcurrency => _currentConcurrency;

    /// <summary>
    /// 最大併發數
    /// </summary>
    public int MaxConcurrency => _config.MaxConcurrentLimit;

    /// <summary>
    /// 活躍請求數
    /// </summary>
    public int ActiveRequests => _activeRequests.Values.Sum();

    /// <summary>
    /// 平均回應時間
    /// </summary>
    public TimeSpan AverageResponseTime 
    {
        get
        {
            lock (_adjustmentLock)
            {
                if (!_recentResponseTimes.Any()) return TimeSpan.Zero;
                return TimeSpan.FromTicks((long)_recentResponseTimes.Average(t => t.Ticks));
            }
        }
    }

    /// <summary>
    /// API 成功率
    /// </summary>
    public double ApiSuccessRate 
    {
        get
        {
            lock (_adjustmentLock)
            {
                if (!_recentApiResults.Any()) return 100.0;
                return _recentApiResults.Count(r => r) * 100.0 / _recentApiResults.Count;
            }
        }
    }

    /// <summary>
    /// 請求併發許可
    /// </summary>
    public async Task<IDisposable> AcquireConcurrencyPermitAsync(
        string batchId, 
        CancellationToken cancellationToken = default)
    {
        // 等待併發許可
        await _concurrencyLimiter.WaitAsync(cancellationToken);
        
        // 增加活躍請求計數
        _activeRequests.AddOrUpdate(batchId, 1, (key, count) => count + 1);
        
        // 返回釋放許可的 IDisposable
        return new ConcurrencyPermit(this, batchId);
    }

    /// <summary>
    /// 記錄 API 呼叫結果
    /// </summary>
    public void RecordApiCall(TimeSpan responseTime, bool success)
    {
        lock (_adjustmentLock)
        {
            // 記錄回應時間（最多保留 100 個）
            _recentResponseTimes.Enqueue(responseTime);
            if (_recentResponseTimes.Count > 100)
            {
                _recentResponseTimes.Dequeue();
            }
            
            // 記錄 API 結果（最多保留 100 個）
            _recentApiResults.Enqueue(success);
            if (_recentApiResults.Count > 100)
            {
                _recentApiResults.Dequeue();
            }
        }
        
        _logger.LogDebug("記錄 API 呼叫，回應時間: {ResponseTime}ms，成功: {Success}", 
            responseTime.TotalMilliseconds, success);
    }

    /// <summary>
    /// 檢查並調整併發數
    /// </summary>
    private void CheckAndAdjustConcurrency(object? state)
    {
        if (_disposed) return;

        try
        {
            lock (_adjustmentLock)
            {
                if (!_recentResponseTimes.Any() || !_recentApiResults.Any())
                {
                    return; // 沒有足夠的數據進行調整
                }

                var avgResponseTime = AverageResponseTime;
                var successRate = ApiSuccessRate;
                var previousConcurrency = _currentConcurrency;

                // 調整邏輯
                if (ShouldIncreaseConcurrency(avgResponseTime, successRate))
                {
                    IncreaseConcurrency();
                }
                else if (ShouldDecreaseConcurrency(avgResponseTime, successRate))
                {
                    DecreaseConcurrency();
                }

                if (_currentConcurrency != previousConcurrency)
                {
                    _logger.LogInformation(
                        "併發數已調整：{Previous} -> {Current}，平均回應時間: {ResponseTime}ms，成功率: {SuccessRate}%",
                        previousConcurrency, _currentConcurrency, avgResponseTime.TotalMilliseconds, successRate);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "併發數調整時發生錯誤");
        }
    }

    /// <summary>
    /// 判斷是否應該增加併發數
    /// </summary>
    private bool ShouldIncreaseConcurrency(TimeSpan avgResponseTime, double successRate)
    {
        return _currentConcurrency < _config.MaxConcurrentLimit &&
               avgResponseTime < TimeSpan.FromSeconds(3) &&
               successRate >= 95.0 &&
               _recentResponseTimes.Count >= 10; // 有足夠的數據樣本
    }

    /// <summary>
    /// 判斷是否應該減少併發數
    /// </summary>
    private bool ShouldDecreaseConcurrency(TimeSpan avgResponseTime, double successRate)
    {
        return _currentConcurrency > 1 &&
               (avgResponseTime > TimeSpan.FromSeconds(10) || successRate < 85.0);
    }

    /// <summary>
    /// 增加併發數
    /// </summary>
    private void IncreaseConcurrency()
    {
        if (_currentConcurrency < _config.MaxConcurrentLimit)
        {
            _currentConcurrency++;
            _concurrencyLimiter.Release(); // 增加一個許可
        }
    }

    /// <summary>
    /// 減少併發數
    /// </summary>
    private void DecreaseConcurrency()
    {
        if (_currentConcurrency > 1)
        {
            _currentConcurrency--;
            // 注意：我們不立即減少信號量許可，而是讓自然流程減少活躍請求
        }
    }

    /// <summary>
    /// 釋放併發許可
    /// </summary>
    internal void ReleaseConcurrencyPermit(string batchId)
    {
        _concurrencyLimiter.Release();
        
        // 減少活躍請求計數
        _activeRequests.AddOrUpdate(batchId, 0, (key, count) => Math.Max(0, count - 1));
        if (_activeRequests.TryGetValue(batchId, out var remainingCount) && remainingCount == 0)
        {
            _activeRequests.TryRemove(batchId, out _);
        }
    }

    /// <summary>
    /// 取得併發統計資訊
    /// </summary>
    public ConcurrencyStatistics GetStatistics()
    {
        lock (_adjustmentLock)
        {
            return new ConcurrencyStatistics
            {
                CurrentConcurrency = _currentConcurrency,
                MaxConcurrency = _config.MaxConcurrentLimit,
                ActiveRequests = ActiveRequests,
                AverageResponseTime = AverageResponseTime,
                ApiSuccessRate = ApiSuccessRate,
                TotalSamples = _recentResponseTimes.Count
            };
        }
    }

    /// <summary>
    /// 強制設定併發數（主要用於測試）
    /// </summary>
    public void SetConcurrency(int concurrency)
    {
        if (concurrency < 1 || concurrency > _config.MaxConcurrentLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(concurrency), 
                $"併發數必須在 1 到 {_config.MaxConcurrentLimit} 之間");
        }

        lock (_adjustmentLock)
        {
            var difference = concurrency - _currentConcurrency;
            _currentConcurrency = concurrency;

            if (difference > 0)
            {
                // 增加許可
                for (int i = 0; i < difference; i++)
                {
                    _concurrencyLimiter.Release();
                }
            }
            // 對於減少的情況，讓自然流程處理
        }

        _logger.LogInformation("併發數已手動設定為: {Concurrency}", concurrency);
    }

    /// <summary>
    /// 釋放資源
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _adjustmentTimer?.Dispose();
            _concurrencyLimiter?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// 併發許可證 - 自動釋放資源
/// </summary>
internal class ConcurrencyPermit : IDisposable
{
    private readonly ConcurrencyController _controller;
    private readonly string _batchId;
    private bool _disposed = false;

    internal ConcurrencyPermit(ConcurrencyController controller, string batchId)
    {
        _controller = controller;
        _batchId = batchId;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _controller.ReleaseConcurrencyPermit(_batchId);
            _disposed = true;
        }
    }
}

/// <summary>
/// 併發統計資訊
/// </summary>
public class ConcurrencyStatistics
{
    /// <summary>
    /// 當前併發數
    /// </summary>
    public int CurrentConcurrency { get; set; }

    /// <summary>
    /// 最大併發數
    /// </summary>
    public int MaxConcurrency { get; set; }

    /// <summary>
    /// 活躍請求數
    /// </summary>
    public int ActiveRequests { get; set; }

    /// <summary>
    /// 平均回應時間
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }

    /// <summary>
    /// API 成功率
    /// </summary>
    public double ApiSuccessRate { get; set; }

    /// <summary>
    /// 統計樣本數
    /// </summary>
    public int TotalSamples { get; set; }
}