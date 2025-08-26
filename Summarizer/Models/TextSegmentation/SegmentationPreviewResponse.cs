namespace Summarizer.Models.TextSegmentation;

/// <summary>
/// 分段預覽回應資料模型
/// </summary>
public class SegmentationPreviewResponse
{
    /// <summary>
    /// 分段預覽列表
    /// </summary>
    public List<SegmentPreview> SegmentPreviews { get; set; } = new();

    /// <summary>
    /// 總分段數量
    /// </summary>
    public int TotalSegments { get; set; }

    /// <summary>
    /// 原始文本長度
    /// </summary>
    public int OriginalLength { get; set; }

    /// <summary>
    /// 處理時間（毫秒）
    /// </summary>
    public double ProcessingTimeMs { get; set; }

    /// <summary>
    /// 處理成功標誌
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 錯誤訊息（如果處理失敗）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 錯誤代碼（如果處理失敗）
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 使用的分段方法
    /// </summary>
    public string SegmentationMethod { get; set; } = string.Empty;

    /// <summary>
    /// 平均分段長度
    /// </summary>
    public double AverageSegmentLength { get; set; }
}

/// <summary>
/// 分段預覽資料模型（不包含完整內容）
/// </summary>
public class SegmentPreview
{
    /// <summary>
    /// 分段索引
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// 分段標題
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 分段字符數
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// 分段類型
    /// </summary>
    public SegmentType Type { get; set; }
}