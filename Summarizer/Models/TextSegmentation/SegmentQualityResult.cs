namespace Summarizer.Models.TextSegmentation;

/// <summary>
/// 分段品質評估結果
/// </summary>
public class SegmentQualityResult
{
    /// <summary>
    /// 整體品質評分（0-100）
    /// </summary>
    public double OverallScore { get; set; }

    /// <summary>
    /// 語義完整性評分（0-100）
    /// </summary>
    public double SemanticIntegrityScore { get; set; }

    /// <summary>
    /// 段落完整性評分（0-100）
    /// </summary>
    public double ParagraphIntegrityScore { get; set; }

    /// <summary>
    /// 長度分配均勻性評分（0-100）
    /// </summary>
    public double LengthBalanceScore { get; set; }

    /// <summary>
    /// 是否通過品質檢查
    /// </summary>
    public bool IsQualityAcceptable { get; set; }

    /// <summary>
    /// 品質問題列表
    /// </summary>
    public List<string> QualityIssues { get; set; } = new();

    /// <summary>
    /// 建議改進項目
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// 統計資訊
    /// </summary>
    public SegmentStatistics Statistics { get; set; } = new();
}

/// <summary>
/// 分段統計資訊
/// </summary>
public class SegmentStatistics
{
    /// <summary>
    /// 平均分段長度
    /// </summary>
    public double AverageSegmentLength { get; set; }

    /// <summary>
    /// 最長分段長度
    /// </summary>
    public int MaxSegmentLength { get; set; }

    /// <summary>
    /// 最短分段長度
    /// </summary>
    public int MinSegmentLength { get; set; }

    /// <summary>
    /// 分段長度標準差
    /// </summary>
    public double LengthStandardDeviation { get; set; }

    /// <summary>
    /// 總字符數
    /// </summary>
    public int TotalCharacters { get; set; }

    /// <summary>
    /// 分段數量
    /// </summary>
    public int SegmentCount { get; set; }
}