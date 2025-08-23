using System.ComponentModel.DataAnnotations;

namespace Summarizer.Models.Requests;

/// <summary>
/// 摘要請求模型
/// </summary>
public class SummarizeRequest
{
    /// <summary>
    /// 待摘要的文本內容
    /// </summary>
    [Required(ErrorMessage = "文本內容不能為空")]
    [StringLength(50000, ErrorMessage = "文本長度不能超過 50,000 字元")]
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// 摘要選項
    /// </summary>
    public SummaryOptions? Options { get; set; }
}

/// <summary>
/// 摘要選項
/// </summary>
public class SummaryOptions
{
    /// <summary>
    /// 摘要長度類型
    /// </summary>
    public SummaryLength Length { get; set; } = SummaryLength.Medium;
    
    /// <summary>
    /// 語言設定
    /// </summary>
    public string Language { get; set; } = "zh-TW";
}

/// <summary>
/// 摘要長度列舉
/// </summary>
public enum SummaryLength
{
    /// <summary>
    /// 簡短摘要
    /// </summary>
    Short,
    
    /// <summary>
    /// 中等長度摘要
    /// </summary>
    Medium,
    
    /// <summary>
    /// 詳細摘要
    /// </summary>
    Long
}