namespace Summarizer.Models.Responses;

/// <summary>
/// 摘要回應模型
/// </summary>
public class SummarizeResponse
{
    /// <summary>
    /// 操作是否成功
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// 摘要結果內容
    /// </summary>
    public string? Summary { get; set; }
    
    /// <summary>
    /// 原文長度
    /// </summary>
    public int OriginalLength { get; set; }
    
    /// <summary>
    /// 摘要長度
    /// </summary>
    public int SummaryLength { get; set; }
    
    /// <summary>
    /// 處理時間（毫秒）
    /// </summary>
    public double ProcessingTimeMs { get; set; }
    
    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string? Error { get; set; }
    
    /// <summary>
    /// 錯誤代碼
    /// </summary>
    public string? ErrorCode { get; set; }
}
