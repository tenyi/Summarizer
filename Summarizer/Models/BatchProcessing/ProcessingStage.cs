namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 處理階段枚舉，定義批次處理的各個階段
/// </summary>
public enum ProcessingStage
{
    /// <summary>
    /// 初始化階段 - 準備處理環境和資源
    /// </summary>
    Initializing,

    /// <summary>
    /// 分段階段 - 將文本分割成多個處理單元
    /// </summary>
    Segmenting,

    /// <summary>
    /// 批次處理階段 - 主要的AI處理工作
    /// </summary>
    BatchProcessing,

    /// <summary>
    /// 合併階段 - 整合各分段的處理結果
    /// </summary>
    Merging,

    /// <summary>
    /// 完成處理階段 - 最終化結果和清理工作
    /// </summary>
    Finalizing,

    /// <summary>
    /// 已完成 - 所有處理工作完成
    /// </summary>
    Completed,

    /// <summary>
    /// 處理失敗 - 遇到無法恢復的錯誤
    /// </summary>
    Failed
}