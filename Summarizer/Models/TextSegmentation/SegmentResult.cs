namespace Summarizer.Models.TextSegmentation;

/// <summary>
/// 文本分段結果資料模型
/// </summary>
public class SegmentResult
{
    /// <summary>
    /// 分段索引，從 0 開始
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// 分段標題（基於首句或關鍵詞自動生成）
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 分段內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 分段字符數
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// 分段在原始文本中的起始位置
    /// </summary>
    public int StartPosition { get; set; }

    /// <summary>
    /// 分段在原始文本中的結束位置
    /// </summary>
    public int EndPosition { get; set; }

    /// <summary>
    /// 分段類型
    /// </summary>
    public SegmentType Type { get; set; }

    /// <summary>
    /// 分段創建時間
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 分段類型列舉
/// </summary>
public enum SegmentType
{
    /// <summary>
    /// 一般段落
    /// </summary>
    Paragraph,

    /// <summary>
    /// 代碼段落
    /// </summary>
    CodeBlock,

    /// <summary>
    /// 清單項目
    /// </summary>
    List,

    /// <summary>
    /// 表格內容
    /// </summary>
    Table,

    /// <summary>
    /// 引用內容
    /// </summary>
    Quote
}