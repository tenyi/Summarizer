using System;
using System.Collections.Generic;

namespace Summarizer.Models.BatchProcessing
{
    /// <summary>
    /// 處理錯誤的詳細資訊模型，包含錯誤分類、嚴重程度和解決建議
    /// </summary>
    public class ProcessingError
    {
        /// <summary>
        /// 錯誤唯一識別碼
        /// </summary>
        public Guid ErrorId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 關聯的批次處理識別碼
        /// </summary>
        public Guid? BatchId { get; set; }

        /// <summary>
        /// 錯誤類別
        /// </summary>
        public ErrorCategory Category { get; set; }

        /// <summary>
        /// 錯誤嚴重程度
        /// </summary>
        public ErrorSeverity Severity { get; set; }

        /// <summary>
        /// 錯誤代碼，用於程式化處理
        /// </summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>
        /// 技術錯誤訊息
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// 使用者友善的錯誤訊息
        /// </summary>
        public string UserFriendlyMessage { get; set; } = string.Empty;

        /// <summary>
        /// 建議的解決方案列表
        /// </summary>
        public List<string> SuggestedActions { get; set; } = new();

        /// <summary>
        /// 錯誤發生的內容資訊
        /// </summary>
        public Dictionary<string, object> ErrorContext { get; set; } = new();

        /// <summary>
        /// 錯誤發生時間
        /// </summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 是否為可恢復的錯誤
        /// </summary>
        public bool IsRecoverable { get; set; }

        /// <summary>
        /// 已嘗試的重試次數
        /// </summary>
        public int RetryAttempts { get; set; }

        /// <summary>
        /// 最大重試次數
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// 錯誤處理策略類型
        /// </summary>
        public ErrorHandlingStrategy HandlingStrategy { get; set; }

        /// <summary>
        /// 影響的使用者識別碼
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// 錯誤來源服務或元件
        /// </summary>
        public string? Source { get; set; }

        /// <summary>
        /// 額外的診斷資訊
        /// </summary>
        public string? DiagnosticInfo { get; set; }

        /// <summary>
        /// 是否已通知使用者
        /// </summary>
        public bool UserNotified { get; set; }

        /// <summary>
        /// 是否已記錄到日誌
        /// </summary>
        public bool Logged { get; set; }
    }

    /// <summary>
    /// 錯誤類別枚舉
    /// </summary>
    public enum ErrorCategory
    {
        /// <summary>
        /// 驗證錯誤 - 使用者輸入或參數驗證失敗
        /// </summary>
        Validation,

        /// <summary>
        /// 認證錯誤 - 身份驗證失敗
        /// </summary>
        Authentication,

        /// <summary>
        /// 授權錯誤 - 權限不足
        /// </summary>
        Authorization,

        /// <summary>
        /// 網路錯誤 - 網路連線或通訊問題
        /// </summary>
        Network,

        /// <summary>
        /// 服務錯誤 - 外部服務（如 AI API）錯誤
        /// </summary>
        Service,

        /// <summary>
        /// 處理錯誤 - 業務邏輯處理過程中的錯誤
        /// </summary>
        Processing,

        /// <summary>
        /// 儲存錯誤 - 資料庫或檔案系統錯誤
        /// </summary>
        Storage,

        /// <summary>
        /// 系統錯誤 - 系統級錯誤，如記憶體不足
        /// </summary>
        System,

        /// <summary>
        /// 設定錯誤 - 設定檔或環境設定問題
        /// </summary>
        Configuration,

        /// <summary>
        /// 超時錯誤 - 操作超時
        /// </summary>
        Timeout
    }

    /// <summary>
    /// 錯誤嚴重程度枚舉
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// 資訊 - 僅供參考
        /// </summary>
        Info,

        /// <summary>
        /// 警告 - 可能的問題，但不影響核心功能
        /// </summary>
        Warning,

        /// <summary>
        /// 錯誤 - 影響特定功能，但系統仍可運行
        /// </summary>
        Error,

        /// <summary>
        /// 嚴重 - 影響主要功能，需要立即處理
        /// </summary>
        Critical,

        /// <summary>
        /// 致命 - 系統無法繼續運行
        /// </summary>
        Fatal
    }

    /// <summary>
    /// 錯誤處理策略枚舉
    /// </summary>
    public enum ErrorHandlingStrategy
    {
        /// <summary>
        /// 重試策略 - 自動重試操作
        /// </summary>
        Retry,

        /// <summary>
        /// 升級策略 - 提交給管理員或更高層處理
        /// </summary>
        Escalate,

        /// <summary>
        /// 使用者指導策略 - 提供使用者指導和建議
        /// </summary>
        UserGuidance,

        /// <summary>
        /// 恢復策略 - 嘗試自動恢復或提供部分結果
        /// </summary>
        Recovery,

        /// <summary>
        /// 備援策略 - 切換到備援服務或降級服務
        /// </summary>
        Fallback,

        /// <summary>
        /// 記錄並忽略策略 - 記錄錯誤但不中斷流程
        /// </summary>
        LogAndIgnore,

        /// <summary>
        /// 立即停止策略 - 立即停止所有相關操作
        /// </summary>
        ImmediateStop
    }
}