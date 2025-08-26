namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 處理速度統計資料模型
/// </summary>
public class ProcessingSpeed
{
    /// <summary>
    /// 每分鐘處理的分段數量
    /// </summary>
    public double SegmentsPerMinute { get; set; }

    /// <summary>
    /// 每秒處理的字符數量
    /// </summary>
    public double CharactersPerSecond { get; set; }

    /// <summary>
    /// 當前處理吞吐量（字符/秒）
    /// </summary>
    public double CurrentThroughput { get; set; }

    /// <summary>
    /// 平均處理延遲（毫秒）
    /// </summary>
    public double AverageLatencyMs { get; set; }

    /// <summary>
    /// 最大處理延遲（毫秒）
    /// </summary>
    public double MaxLatencyMs { get; set; }

    /// <summary>
    /// 最小處理延遲（毫秒）
    /// </summary>
    public double MinLatencyMs { get; set; }

    /// <summary>
    /// 處理效率百分比（相對於理想速度）
    /// </summary>
    public double EfficiencyPercentage { get; set; }

    /// <summary>
    /// 計算當前速度的時間窗口（毫秒）
    /// </summary>
    public long CalculationWindowMs { get; set; } = 60000; // 預設1分鐘

    /// <summary>
    /// 速度統計的最後更新時間
    /// </summary>
    public DateTime LastCalculated { get; set; } = DateTime.UtcNow;
}