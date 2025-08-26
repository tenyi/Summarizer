namespace Summarizer.Configuration;

/// <summary>
/// 批次處理配置類別
/// </summary>
public class BatchProcessingConfig
{
    /// <summary>
    /// 配置節點名稱
    /// </summary>
    public const string SectionName = "BatchProcessing";

    /// <summary>
    /// 預設併發限制數量
    /// </summary>
    public int DefaultConcurrentLimit { get; set; } = 2;

    /// <summary>
    /// 最大併發限制數量
    /// </summary>
    public int MaxConcurrentLimit { get; set; } = 4;

    /// <summary>
    /// 重試政策設定
    /// </summary>
    public RetryPolicyConfig RetryPolicy { get; set; } = new();

    /// <summary>
    /// API 超時設定
    /// </summary>
    public ApiTimeoutConfig ApiTimeout { get; set; } = new();

    /// <summary>
    /// 進度回報設定
    /// </summary>
    public ProgressReportingConfig ProgressReporting { get; set; } = new();

    /// <summary>
    /// 持久化設定
    /// </summary>
    public PersistenceSettingsConfig PersistenceSettings { get; set; } = new();
}

/// <summary>
/// 重試政策配置
/// </summary>
public class RetryPolicyConfig
{
    /// <summary>
    /// 最大重試次數
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// 退避倍數
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// 基礎延遲秒數
    /// </summary>
    public int BaseDelaySeconds { get; set; } = 1;
}

/// <summary>
/// API 超時配置
/// </summary>
public class ApiTimeoutConfig
{
    /// <summary>
    /// 預設超時秒數
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 長超時秒數
    /// </summary>
    public int LongTimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// 進度回報配置
/// </summary>
public class ProgressReportingConfig
{
    /// <summary>
    /// 更新間隔秒數
    /// </summary>
    public int UpdateIntervalSeconds { get; set; } = 2;

    /// <summary>
    /// 是否啟用即時更新
    /// </summary>
    public bool EnableRealtimeUpdates { get; set; } = true;
}

/// <summary>
/// 持久化設定配置
/// </summary>
public class PersistenceSettingsConfig
{
    /// <summary>
    /// 保存進度間隔（秒）
    /// </summary>
    public int SaveProgressInterval { get; set; } = 10;

    /// <summary>
    /// 是否啟用檢查點功能
    /// </summary>
    public bool EnableCheckpoints { get; set; } = true;
}