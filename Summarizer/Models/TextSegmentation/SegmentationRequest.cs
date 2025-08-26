using System.ComponentModel.DataAnnotations;

namespace Summarizer.Models.TextSegmentation;

/// <summary>
/// 文本分段請求資料模型
/// </summary>
public class SegmentationRequest
{
    /// <summary>
    /// 待分段的文本
    /// </summary>
    [Required(ErrorMessage = "文本內容不能為空")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 每個分段的最大字符長度，預設為 2000 字符
    /// </summary>
    [Range(500, 5000, ErrorMessage = "分段長度必須在 500 到 5000 字符之間")]
    public int MaxSegmentLength { get; set; } = 2000;

    /// <summary>
    /// 是否保留段落邊界，預設為 true
    /// </summary>
    public bool PreserveParagraphs { get; set; } = true;

    /// <summary>
    /// 是否生成自動標題，預設為 true
    /// </summary>
    public bool GenerateTitles { get; set; } = true;

    /// <summary>
    /// 是否啟用 LLM 輔助分段，預設為 false
    /// </summary>
    public bool EnableLlmSegmentation { get; set; } = false;
}