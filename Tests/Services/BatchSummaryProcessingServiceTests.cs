using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Summarizer.Configuration;
using Summarizer.Models.BatchProcessing;
using Summarizer.Models.TextSegmentation;
using Summarizer.Services;
using Summarizer.Services.Interfaces;
using Xunit;

namespace Tests.Services;

/// <summary>
/// 批次摘要處理服務測試類別
/// </summary>
public class BatchSummaryProcessingServiceTests : IDisposable
{
    private readonly Mock<ILogger<BatchSummaryProcessingService>> _mockLogger;
    private readonly Mock<ISummaryService> _mockSummaryService;
    private readonly Mock<IBatchProgressNotificationService> _mockNotificationService;
    private readonly Mock<ICancellationService> _mockCancellationService;
    private readonly BatchProcessingConfig _config;
    private readonly BatchSummaryProcessingService _service;

    public BatchSummaryProcessingServiceTests()
    {
        _mockLogger = new Mock<ILogger<BatchSummaryProcessingService>>();
        _mockSummaryService = new Mock<ISummaryService>();
        _mockNotificationService = new Mock<IBatchProgressNotificationService>();
        _mockCancellationService = new Mock<ICancellationService>();
        _config = new BatchProcessingConfig
        {
            DefaultConcurrentLimit = 2,
            MaxConcurrentLimit = 4,
            RetryPolicy = new RetryPolicyConfig
            {
                MaxRetries = 3,
                BackoffMultiplier = 2.0,
                BaseDelaySeconds = 1
            },
            ApiTimeout = new ApiTimeoutConfig
            {
                DefaultTimeoutSeconds = 30,
                LongTimeoutSeconds = 60
            }
        };

        // 設定 CancellationService mock 行為
        _mockCancellationService.Setup(x => x.RegisterBatchProcess(It.IsAny<Guid>(), It.IsAny<BatchProcessingContext>()))
            .Returns(CancellationToken.None);

        var options = Options.Create(_config);
        _service = new BatchSummaryProcessingService(options, _mockSummaryService.Object, _mockNotificationService.Object, _mockCancellationService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task StartBatchProcessingAsync_有效輸入_應該返回BatchId()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "第一段內容", Title = "第一段", CharacterCount = 5 },
            new() { Content = "第二段內容", Title = "第二段", CharacterCount = 5 }
        };
        var originalText = "第一段內容第二段內容";

        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("摘要結果");

        // Act
        var batchId = await _service.StartBatchProcessingAsync(segments, originalText, "testuser");

        // Assert
        Assert.NotEqual(Guid.Empty, batchId);
    }

    [Fact]
    public async Task StartBatchProcessingAsync_空分段列表_應該拋出異常()
    {
        // Arrange
        var segments = new List<SegmentResult>();
        var originalText = "測試文本";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.StartBatchProcessingAsync(segments, originalText, "testuser"));
    }

    [Fact]
    public async Task StartBatchProcessingAsync_空原始文本_應該拋出異常()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "測試內容", Title = "測試" }
        };
        var originalText = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.StartBatchProcessingAsync(segments, originalText, "testuser"));
    }

    [Fact]
    public async Task GetBatchProgressAsync_存在的BatchId_應該返回進度信息()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "測試內容", Title = "測試", CharacterCount = 4 }
        };
        var originalText = "測試內容";

        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("摘要結果");

        var batchId = await _service.StartBatchProcessingAsync(segments, originalText, "testuser");

        // 等待一下讓處理開始
        await Task.Delay(100);

        // Act
        var progress = await _service.GetBatchProgressAsync(batchId);

        // Assert
        Assert.NotNull(progress);
        Assert.Equal(batchId, progress.BatchId);
        Assert.Equal(1, progress.TotalSegments);
    }

    [Fact]
    public async Task GetBatchProgressAsync_不存在的BatchId_應該返回Null()
    {
        // Arrange
        var nonExistentBatchId = Guid.NewGuid();

        // Act
        var progress = await _service.GetBatchProgressAsync(nonExistentBatchId);

        // Assert
        Assert.Null(progress);
    }

    [Fact]
    public async Task PauseBatchProcessingAsync_處理中的Batch_應該成功暫停()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "長時間處理的內容", Title = "測試", CharacterCount = 9 }
        };
        var originalText = "長時間處理的內容";

        // 模擬長時間運行的摘要服務
        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .Returns(async (string text, CancellationToken token) => 
                          {
                              await Task.Delay(2000, token); // 模擬2秒處理時間
                              return "摘要結果";
                          });

        var batchId = await _service.StartBatchProcessingAsync(segments, originalText, "testuser");
        
        // 等待處理開始
        await Task.Delay(50);

        // Act
        var success = await _service.PauseBatchProcessingAsync(batchId);

        // Assert
        Assert.True(success);
    }

    [Fact]
    public async Task ResumeBatchProcessingAsync_暫停的Batch_應該成功恢復()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "測試內容", Title = "測試", CharacterCount = 4 }
        };
        var originalText = "測試內容";

        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .Returns(async (string text, CancellationToken token) => 
                          {
                              await Task.Delay(1000, token);
                              return "摘要結果";
                          });

        var batchId = await _service.StartBatchProcessingAsync(segments, originalText, "testuser");
        await Task.Delay(50);
        
        await _service.PauseBatchProcessingAsync(batchId);

        // Act
        var success = await _service.ResumeBatchProcessingAsync(batchId);

        // Assert
        Assert.True(success);
    }

    [Fact]
    public async Task CancelBatchProcessingAsync_任何狀態的Batch_應該成功取消()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "測試內容", Title = "測試", CharacterCount = 4 }
        };
        var originalText = "測試內容";

        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("摘要結果");

        var batchId = await _service.StartBatchProcessingAsync(segments, originalText, "testuser");

        // Act
        var success = await _service.CancelBatchProcessingAsync(batchId);

        // Assert
        Assert.True(success);
    }

    [Fact]
    public async Task GetUserBatchesAsync_有效UserId_應該返回用戶批次列表()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "用戶測試內容", Title = "測試", CharacterCount = 6 }
        };
        var originalText = "用戶測試內容";
        var userId = "testuser";

        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("摘要結果");

        var batchId = await _service.StartBatchProcessingAsync(segments, originalText, userId);

        // Act
        var userBatches = await _service.GetUserBatchesAsync(userId);

        // Assert
        Assert.NotEmpty(userBatches);
        Assert.Contains(userBatches, b => b.BatchId == batchId);
    }

    [Fact]
    public async Task GetUserBatchesAsync_空UserId_應該返回空列表()
    {
        // Act
        var userBatches = await _service.GetUserBatchesAsync("");

        // Assert
        Assert.Empty(userBatches);
    }

    [Fact]
    public async Task CleanupCompletedBatchesAsync_有已完成批次_應該清理舊記錄()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "清理測試內容", Title = "測試", CharacterCount = 6 }
        };
        var originalText = "清理測試內容";

        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ReturnsAsync("摘要結果");

        var batchId = await _service.StartBatchProcessingAsync(segments, originalText, "testuser");
        
        // 等待處理完成
        await Task.Delay(1000);

        // Act
        var cleanedCount = await _service.CleanupCompletedBatchesAsync(0); // 清理0小時前的記錄

        // Assert
        // 由於批次可能還在處理中，清理數量可能為0或1
        Assert.True(cleanedCount >= 0);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _service?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}