using System.ComponentModel.DataAnnotations;

namespace Summarizer.Models.TextSegmentation;

/// <summary>
/// 分段檢查請求資料模型
/// </summary>
public class SegmentationCheckRequest
{
    /// <summary>
    /// 待檢查的文本
    /// </summary>
    [Required(ErrorMessage = "文本內容不能為空")]
    public string Text { get; set; } = string.Empty;
}