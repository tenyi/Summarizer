namespace Summarizer.Configuration;

/// <summary>
/// 文本分段配置類別
/// </summary>
public class TextSegmentationConfig
{
    /// <summary>
    /// 配置節點名稱
    /// </summary>
    public const string SectionName = "TextSegmentation";

    /// <summary>
    /// 觸發分段的文本長度門檻，預設為 2048 字符
    /// </summary>
    public int TriggerLength { get; set; } = 2048;

    /// <summary>
    /// 每個分段的最大字符長度，預設為 2000 字符
    /// </summary>
    public int MaxSegmentLength { get; set; } = 2000;

    /// <summary>
    /// Context 限制緩衝區百分比，預設為 0.8（80%）
    /// </summary>
    public double ContextLimitBuffer { get; set; } = 0.8;

    /// <summary>
    /// 是否保留段落邊界，預設為 true
    /// </summary>
    public bool PreserveParagraphs { get; set; } = true;

    /// <summary>
    /// 是否啟用 LLM 輔助分段，預設為 true
    /// </summary>
    public bool EnableLlmSegmentation { get; set; } = true;

    /// <summary>
    /// 句子結束符號列表
    /// </summary>
    public string[] SentenceEndMarkers { get; set; } = { ".", "。", "!", "！", "?" };

    /// <summary>
    /// 是否生成自動標題，預設為 true
    /// </summary>
    public bool GenerateAutoTitles { get; set; } = true;

    /// <summary>
    /// 是否記錄詳細資訊，預設為 true
    /// </summary>
    public bool LogDetailedInfo { get; set; } = true;

    /// <summary>
    /// 允許的特殊結構類型
    /// </summary>
    public string[] AllowedSpecialStructures { get; set; } = { "code", "table", "list", "quote" };

    /// <summary>
    /// 分段重試次數，預設為 3
    /// </summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>
    /// LLM 分段的提示詞模板
    /// </summary>
    public string LlmSegmentationPrompt { get; set; } = 
        "請將以下文本智能分割成多個段落，每個段落不超過 {maxLength} 字符，保持語義完整性。請直接返回分段後的文本，段落之間用 '---SEGMENT---' 分隔：\n\n{text}";
}