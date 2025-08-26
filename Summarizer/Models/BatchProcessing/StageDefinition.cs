namespace Summarizer.Models.BatchProcessing;

/// <summary>
/// 處理階段定義，包含階段的基本資訊和預估時間
/// </summary>
public class StageDefinition
{
    /// <summary>
    /// 處理階段
    /// </summary>
    public ProcessingStage Stage { get; set; }

    /// <summary>
    /// 階段名稱
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 階段描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 階段圖示（CSS類別或字符）
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 預估階段耗時百分比（相對於總處理時間）
    /// </summary>
    public double EstimatedDurationPercentage { get; set; }

    /// <summary>
    /// 階段是否為關鍵路徑（影響整體完成時間）
    /// </summary>
    public bool IsCriticalPath { get; set; }

    /// <summary>
    /// 階段的顯示順序
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 階段是否可以並行處理
    /// </summary>
    public bool CanRunInParallel { get; set; }
}

/// <summary>
/// 預設的階段定義
/// </summary>
public static class DefaultStageDefinitions
{
    /// <summary>
    /// 取得預設的處理階段定義列表
    /// </summary>
    public static List<StageDefinition> GetDefault()
    {
        return new List<StageDefinition>
        {
            new StageDefinition
            {
                Stage = ProcessingStage.Initializing,
                Name = "初始化",
                Description = "準備處理環境和資源",
                Icon = "settings",
                EstimatedDurationPercentage = 5,
                Order = 1,
                IsCriticalPath = true,
                CanRunInParallel = false
            },
            new StageDefinition
            {
                Stage = ProcessingStage.Segmenting,
                Name = "文本分段",
                Description = "將長文本分割成處理單元",
                Icon = "cut",
                EstimatedDurationPercentage = 10,
                Order = 2,
                IsCriticalPath = true,
                CanRunInParallel = false
            },
            new StageDefinition
            {
                Stage = ProcessingStage.BatchProcessing,
                Name = "批次處理",
                Description = "AI 模型處理各個分段",
                Icon = "cpu",
                EstimatedDurationPercentage = 70,
                Order = 3,
                IsCriticalPath = true,
                CanRunInParallel = true
            },
            new StageDefinition
            {
                Stage = ProcessingStage.Merging,
                Name = "結果合併",
                Description = "整合各分段的處理結果",
                Icon = "merge",
                EstimatedDurationPercentage = 10,
                Order = 4,
                IsCriticalPath = true,
                CanRunInParallel = false
            },
            new StageDefinition
            {
                Stage = ProcessingStage.Finalizing,
                Name = "完成處理",
                Description = "最終化結果和清理工作",
                Icon = "check",
                EstimatedDurationPercentage = 5,
                Order = 5,
                IsCriticalPath = true,
                CanRunInParallel = false
            }
        };
    }
}