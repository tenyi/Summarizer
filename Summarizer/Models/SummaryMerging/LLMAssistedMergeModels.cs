namespace Summarizer.Models.SummaryMerging;

/// <summary>
/// LLM 輔助合併結果
/// </summary>
public class LLMAssistedMergeResult
{
    /// <summary>
    /// 合併工作識別碼
    /// </summary>
    public Guid MergeJobId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 最終合併摘要
    /// </summary>
    public string FinalSummary { get; set; } = string.Empty;

    /// <summary>
    /// 使用的 LLM 模型資訊
    /// </summary>
    public LLMModelInfo ModelInfo { get; set; } = new();

    /// <summary>
    /// 使用的提示詞
    /// </summary>
    public string UsedPrompt { get; set; } = string.Empty;

    /// <summary>
    /// LLM 回應的原始內容
    /// </summary>
    public string RawResponse { get; set; } = string.Empty;

    /// <summary>
    /// 合併統計資訊
    /// </summary>
    public LLMMergeStatistics Statistics { get; set; } = new();

    /// <summary>
    /// 品質評估結果
    /// </summary>
    public MergeQualityAssessment? QualityAssessment { get; set; }

    /// <summary>
    /// 處理時間（毫秒）
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// 合併信心分數 (0-1)
    /// </summary>
    public double ConfidenceScore { get; set; }

    /// <summary>
    /// 建立時間
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 錯誤資訊（如果有）
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// 是否需要人工審查
    /// </summary>
    public bool RequiresHumanReview { get; set; }
}

/// <summary>
/// LLM 模型資訊
/// </summary>
public class LLMModelInfo
{
    /// <summary>
    /// 模型名稱
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// 模型版本
    /// </summary>
    public string ModelVersion { get; set; } = string.Empty;

    /// <summary>
    /// 服務提供者 (OpenAI, Ollama, etc.)
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 使用的參數設定
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// 模型性能指標
    /// </summary>
    public ModelPerformanceMetrics? PerformanceMetrics { get; set; }
}

/// <summary>
/// 模型性能指標
/// </summary>
public class ModelPerformanceMetrics
{
    /// <summary>
    /// 輸入令牌數量
    /// </summary>
    public int InputTokens { get; set; }

    /// <summary>
    /// 輸出令牌數量
    /// </summary>
    public int OutputTokens { get; set; }

    /// <summary>
    /// 總令牌數量
    /// </summary>
    public int TotalTokens => InputTokens + OutputTokens;

    /// <summary>
    /// 回應時間（毫秒）
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// 每秒令牌處理速度
    /// </summary>
    public double TokensPerSecond => ResponseTimeMs > 0 ? (double)TotalTokens / (ResponseTimeMs / 1000.0) : 0;
}

/// <summary>
/// LLM 合併統計資訊
/// </summary>
public class LLMMergeStatistics
{
    /// <summary>
    /// 原始摘要數量
    /// </summary>
    public int OriginalSummaryCount { get; set; }

    /// <summary>
    /// 原始總字數
    /// </summary>
    public int OriginalTotalWords { get; set; }

    /// <summary>
    /// 合併後字數
    /// </summary>
    public int MergedWords { get; set; }

    /// <summary>
    /// 壓縮比率 (0-1)
    /// </summary>
    public double CompressionRatio => OriginalTotalWords > 0 ? (double)MergedWords / OriginalTotalWords : 0;

    /// <summary>
    /// 資訊保留度評估 (0-1)
    /// </summary>
    public double InformationRetention { get; set; }

    /// <summary>
    /// 邏輯一致性評分 (0-1)
    /// </summary>
    public double LogicalConsistency { get; set; }

    /// <summary>
    /// 語言流暢度評分 (0-1)
    /// </summary>
    public double LanguageFluency { get; set; }
}

/// <summary>
/// 合併品質評估結果
/// </summary>
public class MergeQualityAssessment
{
    /// <summary>
    /// 整體品質分數 (0-1)
    /// </summary>
    public double OverallQualityScore { get; set; }

    /// <summary>
    /// 詳細品質指標
    /// </summary>
    public QualityMetrics Metrics { get; set; } = new();

    /// <summary>
    /// 評估建議
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// 問題識別
    /// </summary>
    public List<QualityIssue> Issues { get; set; } = new();

    /// <summary>
    /// 評估時間
    /// </summary>
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 是否通過品質門檻
    /// </summary>
    public bool PassesQualityThreshold { get; set; }
}

/// <summary>
/// 品質指標
/// </summary>
public class QualityMetrics
{
    /// <summary>
    /// 內容完整性 (0-1)
    /// </summary>
    public double ContentCompleteness { get; set; }

    /// <summary>
    /// 資訊準確性 (0-1)
    /// </summary>
    public double InformationAccuracy { get; set; }

    /// <summary>
    /// 語言品質 (0-1)
    /// </summary>
    public double LanguageQuality { get; set; }

    /// <summary>
    /// 結構合理性 (0-1)
    /// </summary>
    public double StructuralCoherence { get; set; }

    /// <summary>
    /// 重複內容檢測分數 (越低越好, 0-1)
    /// </summary>
    public double DuplicationScore { get; set; }

    /// <summary>
    /// 關鍵資訊保留度 (0-1)
    /// </summary>
    public double KeyInformationRetention { get; set; }
}


/// <summary>
/// 融合策略
/// </summary>
public enum FusionStrategy
{
    /// <summary>
    /// 優先使用 LLM 結果
    /// </summary>
    PreferLLM,

    /// <summary>
    /// 優先使用規則式結果
    /// </summary>
    PreferRuleBased,

    /// <summary>
    /// 智能融合（根據品質評估自動選擇）
    /// </summary>
    Intelligent,

    /// <summary>
    /// 加權融合（結合兩種結果）
    /// </summary>
    WeightedFusion,

    /// <summary>
    /// 階段式融合（不同部分使用不同策略）
    /// </summary>
    StagedFusion
}


/// <summary>
/// 後處理選項
/// </summary>
public class PostProcessingOptions
{
    /// <summary>
    /// 是否進行語言檢查
    /// </summary>
    public bool EnableLanguageCheck { get; set; } = true;

    /// <summary>
    /// 是否進行格式標準化
    /// </summary>
    public bool EnableFormatNormalization { get; set; } = true;

    /// <summary>
    /// 是否進行重複內容清理
    /// </summary>
    public bool EnableDuplicationCleaning { get; set; } = true;

    /// <summary>
    /// 是否進行長度調整
    /// </summary>
    public bool EnableLengthAdjustment { get; set; } = true;

    /// <summary>
    /// 目標長度限制
    /// </summary>
    public int? TargetLengthLimit { get; set; }

    /// <summary>
    /// 是否保留來源標記
    /// </summary>
    public bool PreserveSourceTags { get; set; } = true;

    /// <summary>
    /// 自訂後處理規則
    /// </summary>
    public List<string> CustomRules { get; set; } = new();
}

/// <summary>
/// 驗證結果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 是否通過驗證
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 驗證分數 (0-1)
    /// </summary>
    public double ValidationScore { get; set; }

    /// <summary>
    /// 驗證錯誤列表
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// 驗證警告列表
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// 驗證建議
    /// </summary>
    public List<string> Suggestions { get; set; } = new();

    /// <summary>
    /// 驗證時間
    /// </summary>
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 驗證錯誤
/// </summary>
public class ValidationError
{
    /// <summary>
    /// 錯誤類型
    /// </summary>
    public string ErrorType { get; set; } = string.Empty;

    /// <summary>
    /// 錯誤訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 錯誤位置
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 嚴重程度
    /// </summary>
    public QualityIssueSeverity Severity { get; set; } = QualityIssueSeverity.High;
}

/// <summary>
/// 驗證警告
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// 警告類型
    /// </summary>
    public string WarningType { get; set; } = string.Empty;

    /// <summary>
    /// 警告訊息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 警告位置
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 建議處理方式
    /// </summary>
    public string? Suggestion { get; set; }
}