using System;
using System.Collections.Generic;

namespace Summarizer.Models.BatchProcessing
{
    /// <summary>
    /// 系統恢復結果模型，包含恢復過程的詳細資訊和狀態
    /// </summary>
    public class SystemRecoveryResult
    {
        /// <summary>
        /// 恢復操作的唯一識別碼
        /// </summary>
        public Guid RecoveryId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 關聯的批次處理識別碼
        /// </summary>
        public Guid BatchId { get; set; }

        /// <summary>
        /// 恢復是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 恢復原因
        /// </summary>
        public RecoveryReason Reason { get; set; }

        /// <summary>
        /// 恢復開始時間
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 恢復完成時間
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 恢復持續時間
        /// </summary>
        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;

        /// <summary>
        /// 執行的恢復步驟列表
        /// </summary>
        public List<RecoveryStep> Steps { get; set; } = new();

        /// <summary>
        /// 恢復過程中發生的錯誤
        /// </summary>
        public List<ProcessingError> Errors { get; set; } = new();

        /// <summary>
        /// 恢復過程的詳細日誌
        /// </summary>
        public List<string> Logs { get; set; } = new();

        /// <summary>
        /// 恢復統計資訊
        /// </summary>
        public Dictionary<string, object> Statistics { get; set; } = new();

        /// <summary>
        /// 恢復後的系統狀態
        /// </summary>
        public SystemState PostRecoveryState { get; set; } = SystemState.Unknown;

        /// <summary>
        /// 恢復建議和後續步驟
        /// </summary>
        public List<string> Recommendations { get; set; } = new();

        /// <summary>
        /// 受影響的資源清單
        /// </summary>
        public List<string> AffectedResources { get; set; } = new();

        /// <summary>
        /// 額外的恢復內容資訊
        /// </summary>
        public Dictionary<string, object> RecoveryContext { get; set; } = new();
    }

    /// <summary>
    /// 恢復步驟詳細資訊
    /// </summary>
    public class RecoveryStep
    {
        /// <summary>
        /// 步驟名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 步驟描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 步驟狀態
        /// </summary>
        public StepStatus Status { get; set; }

        /// <summary>
        /// 步驟開始時間
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// 步驟完成時間
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 步驟執行時間
        /// </summary>
        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;

        /// <summary>
        /// 步驟結果訊息
        /// </summary>
        public string? ResultMessage { get; set; }

        /// <summary>
        /// 步驟錯誤訊息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 步驟執行內容
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// 系統健康檢查結果模型
    /// </summary>
    public class SystemHealthCheckResult
    {
        /// <summary>
        /// 健康檢查的唯一識別碼
        /// </summary>
        public Guid HealthCheckId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 檢查執行時間
        /// </summary>
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 整體健康狀態
        /// </summary>
        public HealthStatus OverallStatus { get; set; }

        /// <summary>
        /// 各系統組件的健康狀態
        /// </summary>
        public Dictionary<string, ComponentHealthStatus> Components { get; set; } = new();

        /// <summary>
        /// 發現的問題列表
        /// </summary>
        public List<HealthIssue> Issues { get; set; } = new();

        /// <summary>
        /// 系統效能指標
        /// </summary>
        public SystemPerformanceMetrics Performance { get; set; } = new();

        /// <summary>
        /// 建議的修復措施
        /// </summary>
        public List<string> RecommendedActions { get; set; } = new();

        /// <summary>
        /// 下次檢查建議時間
        /// </summary>
        public DateTime? NextCheckRecommendedAt { get; set; }

        /// <summary>
        /// 健康檢查的詳細報告
        /// </summary>
        public string DetailedReport { get; set; } = string.Empty;
    }

    /// <summary>
    /// 系統組件健康狀態
    /// </summary>
    public class ComponentHealthStatus
    {
        /// <summary>
        /// 組件名稱
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 健康狀態
        /// </summary>
        public HealthStatus Status { get; set; }

        /// <summary>
        /// 狀態描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 最後檢查時間
        /// </summary>
        public DateTime LastCheckedAt { get; set; }

        /// <summary>
        /// 回應時間（毫秒）
        /// </summary>
        public long ResponseTimeMs { get; set; }

        /// <summary>
        /// 組件特定的指標
        /// </summary>
        public Dictionary<string, object> Metrics { get; set; } = new();

        /// <summary>
        /// 相關的警告或錯誤
        /// </summary>
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// 健康問題詳細資訊
    /// </summary>
    public class HealthIssue
    {
        /// <summary>
        /// 問題唯一識別碼
        /// </summary>
        public Guid IssueId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 問題類型
        /// </summary>
        public IssueType Type { get; set; }

        /// <summary>
        /// 問題嚴重程度
        /// </summary>
        public IssueSeverity Severity { get; set; }

        /// <summary>
        /// 問題標題
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 問題描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 受影響的組件
        /// </summary>
        public string Component { get; set; } = string.Empty;

        /// <summary>
        /// 發現時間
        /// </summary>
        public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 建議的解決措施
        /// </summary>
        public List<string> SuggestedFixes { get; set; } = new();

        /// <summary>
        /// 是否可自動修復
        /// </summary>
        public bool CanAutoFix { get; set; }

        /// <summary>
        /// 問題內容資訊
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// 系統效能指標
    /// </summary>
    public class SystemPerformanceMetrics
    {
        /// <summary>
        /// CPU 使用率（百分比）
        /// </summary>
        public double CpuUsagePercent { get; set; }

        /// <summary>
        /// 記憶體使用率（百分比）
        /// </summary>
        public double MemoryUsagePercent { get; set; }

        /// <summary>
        /// 磁碟使用率（百分比）
        /// </summary>
        public double DiskUsagePercent { get; set; }

        /// <summary>
        /// 平均回應時間（毫秒）
        /// </summary>
        public double AverageResponseTimeMs { get; set; }

        /// <summary>
        /// 活躍連線數
        /// </summary>
        public int ActiveConnections { get; set; }

        /// <summary>
        /// 處理中的批次數量
        /// </summary>
        public int ProcessingBatches { get; set; }

        /// <summary>
        /// 錯誤率（百分比）
        /// </summary>
        public double ErrorRatePercent { get; set; }

        /// <summary>
        /// 額外的效能指標
        /// </summary>
        public Dictionary<string, double> AdditionalMetrics { get; set; } = new();
    }

    /// <summary>
    /// 自我修復結果模型
    /// </summary>
    public class SelfRepairResult
    {
        /// <summary>
        /// 修復操作的唯一識別碼
        /// </summary>
        public Guid RepairId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 修復是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 修復開始時間
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 修復完成時間
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 修復持續時間
        /// </summary>
        public TimeSpan Duration => CompletedAt?.Subtract(StartedAt) ?? TimeSpan.Zero;

        /// <summary>
        /// 嘗試修復的問題列表
        /// </summary>
        public List<RepairAttempt> RepairAttempts { get; set; } = new();

        /// <summary>
        /// 成功修復的問題數量
        /// </summary>
        public int SuccessfulRepairs { get; set; }

        /// <summary>
        /// 失敗的修復數量
        /// </summary>
        public int FailedRepairs { get; set; }

        /// <summary>
        /// 無法自動修復的問題
        /// </summary>
        public List<HealthIssue> UnresolvableIssues { get; set; } = new();

        /// <summary>
        /// 修復過程中的日誌
        /// </summary>
        public List<string> Logs { get; set; } = new();

        /// <summary>
        /// 修復後的系統狀態
        /// </summary>
        public SystemState PostRepairState { get; set; } = SystemState.Unknown;

        /// <summary>
        /// 需要手動處理的建議
        /// </summary>
        public List<string> ManualActionRequired { get; set; } = new();
    }

    /// <summary>
    /// 修復嘗試詳細資訊
    /// </summary>
    public class RepairAttempt
    {
        /// <summary>
        /// 嘗試修復的問題ID
        /// </summary>
        public Guid IssueId { get; set; }

        /// <summary>
        /// 修復動作描述
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// 修復是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 修復開始時間
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// 修復完成時間
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// 修復結果訊息
        /// </summary>
        public string ResultMessage { get; set; } = string.Empty;

        /// <summary>
        /// 修復過程中的錯誤（如果有）
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 修復內容資訊
        /// </summary>
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// 恢復原因枚舉
    /// </summary>
    public enum RecoveryReason
    {
        /// <summary>
        /// 使用者取消操作
        /// </summary>
        UserCancellation,

        /// <summary>
        /// 系統錯誤
        /// </summary>
        SystemError,

        /// <summary>
        /// 應用程式異常終止
        /// </summary>
        ApplicationCrash,

        /// <summary>
        /// 逾時或超時
        /// </summary>
        Timeout,

        /// <summary>
        /// 手動恢復請求
        /// </summary>
        ManualRecovery,

        /// <summary>
        /// 系統維護
        /// </summary>
        SystemMaintenance
    }

    /// <summary>
    /// 恢復狀態枚舉
    /// </summary>
    public enum RecoveryStatus
    {
        /// <summary>
        /// 無需恢復
        /// </summary>
        NotRequired,

        /// <summary>
        /// 等待恢復
        /// </summary>
        Pending,

        /// <summary>
        /// 恢復中
        /// </summary>
        InProgress,

        /// <summary>
        /// 恢復完成
        /// </summary>
        Completed,

        /// <summary>
        /// 恢復失敗
        /// </summary>
        Failed
    }

    /// <summary>
    /// 步驟狀態枚舉
    /// </summary>
    public enum StepStatus
    {
        /// <summary>
        /// 等待執行
        /// </summary>
        Pending,

        /// <summary>
        /// 執行中
        /// </summary>
        InProgress,

        /// <summary>
        /// 已完成
        /// </summary>
        Completed,

        /// <summary>
        /// 已跳過
        /// </summary>
        Skipped,

        /// <summary>
        /// 執行失敗
        /// </summary>
        Failed
    }

    /// <summary>
    /// 系統狀態枚舉
    /// </summary>
    public enum SystemState
    {
        /// <summary>
        /// 未知狀態
        /// </summary>
        Unknown,

        /// <summary>
        /// 正常運行
        /// </summary>
        Healthy,

        /// <summary>
        /// 警告狀態（有問題但可運行）
        /// </summary>
        Warning,

        /// <summary>
        /// 降級狀態（功能受限）
        /// </summary>
        Degraded,

        /// <summary>
        /// 故障狀態
        /// </summary>
        Failed,

        /// <summary>
        /// 維護狀態
        /// </summary>
        Maintenance
    }

    /// <summary>
    /// 健康狀態枚舉
    /// </summary>
    public enum HealthStatus
    {
        /// <summary>
        /// 健康
        /// </summary>
        Healthy,

        /// <summary>
        /// 警告
        /// </summary>
        Warning,

        /// <summary>
        /// 不健康
        /// </summary>
        Unhealthy,

        /// <summary>
        /// 嚴重問題
        /// </summary>
        Critical,

        /// <summary>
        /// 無法檢查
        /// </summary>
        Unknown
    }

    /// <summary>
    /// 問題類型枚舉
    /// </summary>
    public enum IssueType
    {
        /// <summary>
        /// 效能問題
        /// </summary>
        Performance,

        /// <summary>
        /// 資源問題
        /// </summary>
        Resource,

        /// <summary>
        /// 連線問題
        /// </summary>
        Connectivity,

        /// <summary>
        /// 設定問題
        /// </summary>
        Configuration,

        /// <summary>
        /// 安全性問題
        /// </summary>
        Security,

        /// <summary>
        /// 資料問題
        /// </summary>
        Data,

        /// <summary>
        /// 服務問題
        /// </summary>
        Service,

        /// <summary>
        /// 其他問題
        /// </summary>
        Other
    }

    /// <summary>
    /// 問題嚴重程度枚舉
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>
        /// 低嚴重程度
        /// </summary>
        Low,

        /// <summary>
        /// 中等嚴重程度
        /// </summary>
        Medium,

        /// <summary>
        /// 高嚴重程度
        /// </summary>
        High,

        /// <summary>
        /// 嚴重
        /// </summary>
        Critical
    }
}