using Summarizer.Models.BatchProcessing;

namespace Summarizer.Models.SummaryMerging;

/// <summary>
/// 來源追溯結果
/// </summary>
public class SourceTrackingResult
{
    /// <summary>
    /// 追溯 ID
    /// </summary>
    public Guid TrackingId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 合併任務 ID
    /// </summary>
    public Guid MergeJobId { get; set; }

    /// <summary>
    /// 最終摘要內容
    /// </summary>
    public string FinalSummary { get; set; } = string.Empty;

    /// <summary>
    /// 段落來源對應
    /// </summary>
    public List<ParagraphSourceMapping> ParagraphMappings { get; set; } = new();

    /// <summary>
    /// 來源完整性分數
    /// </summary>
    public double SourceIntegrityScore { get; set; }

    /// <summary>
    /// 追溯品質分數
    /// </summary>
    public double TraceabilityScore { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 驗證狀態
    /// </summary>
    public TrackingValidationStatus ValidationStatus { get; set; } = TrackingValidationStatus.Pending;
}

/// <summary>
/// 段落來源對應
/// </summary>
public class ParagraphSourceMapping
{
    /// <summary>
    /// 段落索引
    /// </summary>
    public int ParagraphIndex { get; set; }

    /// <summary>
    /// 段落內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 段落開始位置
    /// </summary>
    public int StartPosition { get; set; }

    /// <summary>
    /// 段落結束位置
    /// </summary>
    public int EndPosition { get; set; }

    /// <summary>
    /// 來源分段引用列表
    /// </summary>
    public List<SourceReference> SourceReferences { get; set; } = new();

    /// <summary>
    /// 信心分數
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// 合併方法
    /// </summary>
    public string MergeMethod { get; set; } = string.Empty;
}

/// <summary>
/// 來源引用資訊
/// </summary>
public class SourceReference
{
    /// <summary>
    /// 來源分段索引
    /// </summary>
    public int SegmentIndex { get; set; }

    /// <summary>
    /// 來源分段標題
    /// </summary>
    public string SegmentTitle { get; set; } = string.Empty;

    /// <summary>
    /// 來源分段內容摘錄
    /// </summary>
    public string ContentExcerpt { get; set; } = string.Empty;

    /// <summary>
    /// 貢獻度權重
    /// </summary>
    public double ContributionWeight { get; set; }

    /// <summary>
    /// 相似度分數
    /// </summary>
    public double SimilarityScore { get; set; }

    /// <summary>
    /// 引用類型
    /// </summary>
    public SourceReferenceType ReferenceType { get; set; }

    /// <summary>
    /// 原始分段內容
    /// </summary>
    public string OriginalSegmentContent { get; set; } = string.Empty;

    /// <summary>
    /// 分段摘要內容
    /// </summary>
    public string SummaryContent { get; set; } = string.Empty;
}

/// <summary>
/// 來源驗證結果
/// </summary>
public class SourceValidationResult
{
    /// <summary>
    /// 驗證是否通過
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 完整性分數
    /// </summary>
    public double IntegrityScore { get; set; }

    /// <summary>
    /// 準確性分數
    /// </summary>
    public double AccuracyScore { get; set; }

    /// <summary>
    /// 涵蓋度分數
    /// </summary>
    public double CoverageScore { get; set; }

    /// <summary>
    /// 驗證問題列表
    /// </summary>
    public List<ValidationIssue> Issues { get; set; } = new();

    /// <summary>
    /// 驗證時間
    /// </summary>
    public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 驗證問題
/// </summary>
public class ValidationIssue
{
    /// <summary>
    /// 問題類型
    /// </summary>
    public ValidationIssueType IssueType { get; set; }

    /// <summary>
    /// 問題描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 問題嚴重性
    /// </summary>
    public IssueSeverity Severity { get; set; }

    /// <summary>
    /// 相關段落索引
    /// </summary>
    public int? ParagraphIndex { get; set; }

    /// <summary>
    /// 相關來源分段索引
    /// </summary>
    public int? SourceSegmentIndex { get; set; }

    /// <summary>
    /// 建議修正動作
    /// </summary>
    public string SuggestedAction { get; set; } = string.Empty;
}

/// <summary>
/// 來源引用自動生成選項
/// </summary>
public class ReferenceGenerationOptions
{
    /// <summary>
    /// 引用格式
    /// </summary>
    public ReferenceFormat Format { get; set; } = ReferenceFormat.InText;

    /// <summary>
    /// 顯示分段標題
    /// </summary>
    public bool ShowSegmentTitles { get; set; } = true;

    /// <summary>
    /// 顯示信心分數
    /// </summary>
    public bool ShowConfidenceScores { get; set; } = false;

    /// <summary>
    /// 最小信心分數閾值
    /// </summary>
    public double MinConfidenceThreshold { get; set; } = 0.6;

    /// <summary>
    /// 最大引用數量
    /// </summary>
    public int MaxReferencesPerParagraph { get; set; } = 5;

    /// <summary>
    /// 群組相似來源
    /// </summary>
    public bool GroupSimilarSources { get; set; } = true;

    /// <summary>
    /// 自訂引用格式模板
    /// </summary>
    public string? CustomFormatTemplate { get; set; }
}

/// <summary>
/// 追溯視覺化資料
/// </summary>
public class TraceabilityVisualizationData
{
    /// <summary>
    /// 視覺化 ID
    /// </summary>
    public Guid VisualizationId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 追溯結果 ID
    /// </summary>
    public Guid TrackingId { get; set; }

    /// <summary>
    /// 節點資料（段落和來源分段）
    /// </summary>
    public List<VisualizationNode> Nodes { get; set; } = new();

    /// <summary>
    /// 連接資料（段落與來源的關聯）
    /// </summary>
    public List<VisualizationLink> Links { get; set; } = new();

    /// <summary>
    /// 配置選項
    /// </summary>
    public VisualizationOptions Options { get; set; } = new();

    /// <summary>
    /// 生成時間
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 視覺化節點
/// </summary>
public class VisualizationNode
{
    /// <summary>
    /// 節點 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 節點類型
    /// </summary>
    public NodeType Type { get; set; }

    /// <summary>
    /// 節點標題
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 節點內容
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 節點大小（基於內容長度或重要性）
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// 節點顏色（基於類型或分數）
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// 位置資訊
    /// </summary>
    public NodePosition Position { get; set; } = new();

    /// <summary>
    /// 擴展屬性
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// 視覺化連接
/// </summary>
public class VisualizationLink
{
    /// <summary>
    /// 連接 ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 來源節點 ID
    /// </summary>
    public string SourceId { get; set; } = string.Empty;

    /// <summary>
    /// 目標節點 ID
    /// </summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// 連接強度（基於相似度或貢獻度）
    /// </summary>
    public double Strength { get; set; }

    /// <summary>
    /// 連接類型
    /// </summary>
    public LinkType Type { get; set; }

    /// <summary>
    /// 連接標籤
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 連接顏色
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// 連接寬度
    /// </summary>
    public double Width { get; set; }
}

/// <summary>
/// 節點位置資訊
/// </summary>
public class NodePosition
{
    /// <summary>
    /// X 座標
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y 座標
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Z 座標（3D 視覺化）
    /// </summary>
    public double Z { get; set; }
}

/// <summary>
/// 視覺化選項
/// </summary>
public class VisualizationOptions
{
    /// <summary>
    /// 視覺化類型
    /// </summary>
    public VisualizationType Type { get; set; } = VisualizationType.NetworkGraph;

    /// <summary>
    /// 佈局演算法
    /// </summary>
    public LayoutAlgorithm Layout { get; set; } = LayoutAlgorithm.ForceDirected;

    /// <summary>
    /// 顯示節點標籤
    /// </summary>
    public bool ShowNodeLabels { get; set; } = true;

    /// <summary>
    /// 顯示連接標籤
    /// </summary>
    public bool ShowLinkLabels { get; set; } = false;

    /// <summary>
    /// 互動式
    /// </summary>
    public bool Interactive { get; set; } = true;

    /// <summary>
    /// 動畫效果
    /// </summary>
    public bool EnableAnimation { get; set; } = true;

    /// <summary>
    /// 色彩主題
    /// </summary>
    public string ColorTheme { get; set; } = "default";
}

/// <summary>
/// 追溯驗證狀態
/// </summary>
public enum TrackingValidationStatus
{
    /// <summary>
    /// 待驗證
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 驗證中
    /// </summary>
    Validating = 2,

    /// <summary>
    /// 驗證通過
    /// </summary>
    Valid = 3,

    /// <summary>
    /// 驗證失敗
    /// </summary>
    Invalid = 4,

    /// <summary>
    /// 部分有效
    /// </summary>
    PartiallyValid = 5
}

/// <summary>
/// 來源引用類型
/// </summary>
public enum SourceReferenceType
{
    /// <summary>
    /// 直接引用
    /// </summary>
    Direct = 1,

    /// <summary>
    /// 改寫引用
    /// </summary>
    Paraphrase = 2,

    /// <summary>
    /// 摘要引用
    /// </summary>
    Summary = 3,

    /// <summary>
    /// 合併引用
    /// </summary>
    Merged = 4,

    /// <summary>
    /// 推論引用
    /// </summary>
    Inferred = 5
}

/// <summary>
/// 驗證問題類型
/// </summary>
public enum ValidationIssueType
{
    /// <summary>
    /// 缺失來源
    /// </summary>
    MissingSource = 1,

    /// <summary>
    /// 不準確引用
    /// </summary>
    InaccurateReference = 2,

    /// <summary>
    /// 信心分數過低
    /// </summary>
    LowConfidence = 3,

    /// <summary>
    /// 重複引用
    /// </summary>
    DuplicateReference = 4,

    /// <summary>
    /// 無效連結
    /// </summary>
    BrokenLink = 5,

    /// <summary>
    /// 覆蓋度不足
    /// </summary>
    InsufficientCoverage = 6
}

/// <summary>
/// 問題嚴重性
/// </summary>
public enum IssueSeverity
{
    /// <summary>
    /// 資訊
    /// </summary>
    Info = 1,

    /// <summary>
    /// 警告
    /// </summary>
    Warning = 2,

    /// <summary>
    /// 錯誤
    /// </summary>
    Error = 3,

    /// <summary>
    /// 嚴重錯誤
    /// </summary>
    Critical = 4
}

/// <summary>
/// 引用格式
/// </summary>
public enum ReferenceFormat
{
    /// <summary>
    /// 內嵌引用
    /// </summary>
    InText = 1,

    /// <summary>
    /// 腳註
    /// </summary>
    Footnote = 2,

    /// <summary>
    /// 尾註
    /// </summary>
    Endnote = 3,

    /// <summary>
    /// 邊註
    /// </summary>
    Sidenote = 4,

    /// <summary>
    /// 工具提示
    /// </summary>
    Tooltip = 5,

    /// <summary>
    /// 自訂格式
    /// </summary>
    Custom = 6
}

/// <summary>
/// 節點類型
/// </summary>
public enum NodeType
{
    /// <summary>
    /// 最終摘要段落
    /// </summary>
    FinalParagraph = 1,

    /// <summary>
    /// 來源分段
    /// </summary>
    SourceSegment = 2,

    /// <summary>
    /// 原始文本區塊
    /// </summary>
    OriginalTextBlock = 3
}

/// <summary>
/// 連接類型
/// </summary>
public enum LinkType
{
    /// <summary>
    /// 直接來源
    /// </summary>
    DirectSource = 1,

    /// <summary>
    /// 間接來源
    /// </summary>
    IndirectSource = 2,

    /// <summary>
    /// 合併來源
    /// </summary>
    MergedSource = 3,

    /// <summary>
    /// 相似內容
    /// </summary>
    SimilarContent = 4
}

/// <summary>
/// 視覺化類型
/// </summary>
public enum VisualizationType
{
    /// <summary>
    /// 網絡圖
    /// </summary>
    NetworkGraph = 1,

    /// <summary>
    /// 樹狀圖
    /// </summary>
    TreeDiagram = 2,

    /// <summary>
    /// 流程圖
    /// </summary>
    FlowChart = 3,

    /// <summary>
    /// 桑基圖
    /// </summary>
    SankeyDiagram = 4,

    /// <summary>
    /// 弦圖
    /// </summary>
    ChordDiagram = 5
}

/// <summary>
/// 佈局演算法
/// </summary>
public enum LayoutAlgorithm
{
    /// <summary>
    /// 力導向佈局
    /// </summary>
    ForceDirected = 1,

    /// <summary>
    /// 階層佈局
    /// </summary>
    Hierarchical = 2,

    /// <summary>
    /// 圓形佈局
    /// </summary>
    Circular = 3,

    /// <summary>
    /// 網格佈局
    /// </summary>
    Grid = 4,

    /// <summary>
    /// 隨機佈局
    /// </summary>
    Random = 5
}