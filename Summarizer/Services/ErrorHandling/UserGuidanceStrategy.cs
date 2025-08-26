using Microsoft.Extensions.Logging;
using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services.ErrorHandling
{
    /// <summary>
    /// 使用者指導策略
    /// 適用於使用者可以自行解決的問題，提供詳細的操作指南和步驟
    /// </summary>
    public class UserGuidanceStrategy : BaseErrorHandlingStrategy
    {
        /// <summary>
        /// 初始化使用者指導策略
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        /// <param name="notificationService">批次處理進度通知服務</param>
        /// <param name="cancellationService">取消操作服務</param>
        /// <param name="partialResultHandler">部分結果處理器</param>
        public UserGuidanceStrategy(
            ILogger<BaseErrorHandlingStrategy> logger,
            IBatchProgressNotificationService notificationService,
            ICancellationService cancellationService,
            IPartialResultHandler partialResultHandler)
            : base(logger, notificationService, cancellationService, partialResultHandler)
        {
        }

        /// <summary>
        /// 策略類型
        /// </summary>
        public override ErrorHandlingStrategy StrategyType => ErrorHandlingStrategy.UserGuidance;

        /// <summary>
        /// 執行使用者指導策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        public override async Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error)
        {
            LogHandlingStart(error, "使用者指導策略");

            try
            {
                // 產生詳細的使用者指導
                var guidance = await GenerateUserGuidanceAsync(error);

                // 建立互動式解決方案
                var interactiveSolution = CreateInteractiveSolution(error);

                // 準備常見問題解答
                var faq = await GenerateRelatedFAQAsync(error);

                // 發送指導通知
                await SendGuidanceNotificationAsync(error, guidance);

                // 記錄指導提供
                RecordGuidanceProvision(error);

                var result = CreateSuccessResult(
                    "已為使用者提供詳細的錯誤解決指導",
                    false,
                    "請依照提供的步驟進行操作");

                // 將指導資訊加入結果資料
                result.Data["Guidance"] = guidance;
                result.Data["InteractiveSolution"] = interactiveSolution;
                result.Data["FAQ"] = faq;
                result.Data["GuidanceProvidedAt"] = DateTime.UtcNow;

                LogHandlingComplete(error, result, "使用者指導策略");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行使用者指導策略時發生異常，錯誤ID: {ErrorId}", error.ErrorId);
                return CreateFailureResult(
                    $"使用者指導策略執行異常: {ex.Message}",
                    true,
                    "請聯絡技術支援尋求協助");
            }
        }

        /// <summary>
        /// 判斷是否適合提供使用者指導
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適合使用者指導</returns>
        public override bool CanHandle(ProcessingError error)
        {
            if (!base.CanHandle(error))
                return false;

            // 檢查錯誤類型是否適合使用者指導
            return IsUserGuidable(error);
        }

        /// <summary>
        /// 產生使用者指導
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>使用者指導物件</returns>
        private async Task<UserGuidance> GenerateUserGuidanceAsync(ProcessingError error)
        {
            await Task.Delay(50); // 模擬指導生成時間

            var guidance = new UserGuidance
            {
                ErrorId = error.ErrorId,
                Title = GetGuidanceTitle(error),
                Summary = GetGuidanceSummary(error),
                Steps = await GenerateDetailedStepsAsync(error),
                Tips = GetHelpfulTips(error),
                PrecautionWarnings = GetPrecautionWarnings(error),
                EstimatedTimeToResolve = EstimateResolutionTime(error),
                DifficultyLevel = DetermineDifficultyLevel(error),
                RequiredPermissions = GetRequiredPermissions(error),
                GeneratedAt = DateTime.UtcNow
            };

            return guidance;
        }

        /// <summary>
        /// 建立互動式解決方案
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>互動式解決方案</returns>
        private InteractiveSolution CreateInteractiveSolution(ProcessingError error)
        {
            return new InteractiveSolution
            {
                ErrorId = error.ErrorId,
                QuickFixActions = GetQuickFixActions(error),
                DiagnosticQuestions = GetDiagnosticQuestions(error),
                TroubleshootingTree = CreateTroubleshootingTree(error),
                HelpResources = GetHelpResources(error)
            };
        }

        /// <summary>
        /// 產生相關常見問題解答
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>常見問題解答列表</returns>
        private async Task<List<FAQItem>> GenerateRelatedFAQAsync(ProcessingError error)
        {
            await Task.Delay(30); // 模擬FAQ查詢時間

            return error.Category switch
            {
                ErrorCategory.Validation => GetValidationFAQ(),
                ErrorCategory.Network => GetNetworkFAQ(),
                ErrorCategory.Authorization => GetAuthorizationFAQ(),
                ErrorCategory.Configuration => GetConfigurationFAQ(),
                ErrorCategory.Processing => GetBusinessLogicFAQ(),
                _ => GetGeneralFAQ()
            };
        }

        /// <summary>
        /// 檢查錯誤是否適合使用者指導
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適合使用者指導</returns>
        private static bool IsUserGuidable(ProcessingError error)
        {
            // 根據錯誤類型判斷是否適合使用者指導
            return error.Category switch
            {
                ErrorCategory.Validation => true,       // 驗證錯誤通常使用者可以修正
                ErrorCategory.Authorization => true,    // 授權錯誤使用者可以調整權限
                ErrorCategory.Authentication => false,  // 認證錯誤通常需要技術支援
                ErrorCategory.Processing => true,         // 業務邏輯錯誤可以指導使用者
                ErrorCategory.Network => false,         // 網路錯誤通常需要技術處理（含逾時）
                ErrorCategory.Service => false,         // 服務錯誤通常不是使用者能解決的
                ErrorCategory.System => false,          // 系統錯誤需要管理員處理
                ErrorCategory.Storage => false,         // 儲存錯誤需要檢查
                _ => false
            };
        }

        /// <summary>
        /// 取得指導標題
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>指導標題</returns>
        private static string GetGuidanceTitle(ProcessingError error)
        {
            return error.Category switch
            {
                ErrorCategory.Validation => "輸入資料驗證問題解決指南",
                ErrorCategory.Authorization => "權限與授權問題解決指南",
                ErrorCategory.Configuration => "設定配置問題解決指南",
                ErrorCategory.Processing => "業務流程問題解決指南",
                _ => "問題解決指南"
            };
        }

        /// <summary>
        /// 取得指導摘要
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>指導摘要</returns>
        private static string GetGuidanceSummary(ProcessingError error)
        {
            return error.Category switch
            {
                ErrorCategory.Validation => "您提供的資料未通過驗證檢查。請依照以下步驟檢查並修正您的輸入內容。",
                ErrorCategory.Authorization => "您目前沒有足夠的權限執行此操作。請確認您的權限設定或聯絡管理員。",
                ErrorCategory.Configuration => "系統設定存在問題。請檢查相關的配置設定。",
                ErrorCategory.Processing => "操作過程中遇到業務規則限制。請依照指導調整您的操作方式。",
                _ => "系統遇到了問題，請依照以下指導進行處理。"
            };
        }

        /// <summary>
        /// 產生詳細步驟
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>詳細解決步驟</returns>
        private async Task<List<GuidanceStep>> GenerateDetailedStepsAsync(ProcessingError error)
        {
            await Task.Delay(20); // 模擬步驟生成時間

            return error.Category switch
            {
                ErrorCategory.Validation => GenerateValidationSteps(error),
                ErrorCategory.Authorization => GenerateAuthorizationSteps(error),
                ErrorCategory.Configuration => GenerateConfigurationSteps(error),
                ErrorCategory.Processing => GenerateBusinessLogicSteps(error),
                _ => GenerateGeneralSteps(error)
            };
        }

        /// <summary>
        /// 產生驗證錯誤解決步驟
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>驗證錯誤解決步驟</returns>
        private static List<GuidanceStep> GenerateValidationSteps(ProcessingError error)
        {
            return new List<GuidanceStep>
            {
                new() {
                    StepNumber = 1,
                    Title = "檢查輸入資料格式",
                    Description = "確認您輸入的資料符合要求的格式",
                    Action = "檢查檔案格式、欄位內容、資料類型等是否正確",
                    ExpectedResult = "資料格式符合系統要求"
                },
                new() {
                    StepNumber = 2,
                    Title = "驗證必要欄位",
                    Description = "確保所有必填欄位都已正確填寫",
                    Action = "檢查是否有空白或遺漏的必填欄位",
                    ExpectedResult = "所有必要資訊都已完整提供"
                },
                new() {
                    StepNumber = 3,
                    Title = "檢查資料長度限制",
                    Description = "確認輸入的文字長度不超過限制",
                    Action = "檢查文字內容是否過長，必要時進行修剪",
                    ExpectedResult = "所有輸入都在允許的長度範圍內"
                },
                new() {
                    StepNumber = 4,
                    Title = "重新提交資料",
                    Description = "修正問題後重新提交",
                    Action = "點擊提交按鈕重新執行操作",
                    ExpectedResult = "操作成功完成，不再出現驗證錯誤"
                }
            };
        }

        /// <summary>
        /// 產生授權錯誤解決步驟
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>授權錯誤解決步驟</returns>
        private static List<GuidanceStep> GenerateAuthorizationSteps(ProcessingError error)
        {
            return new List<GuidanceStep>
            {
                new() {
                    StepNumber = 1,
                    Title = "確認登入狀態",
                    Description = "檢查您是否已正確登入系統",
                    Action = "重新整理頁面或重新登入",
                    ExpectedResult = "顯示已登入的使用者資訊"
                },
                new() {
                    StepNumber = 2,
                    Title = "檢查權限設定",
                    Description = "確認您的帳號具有執行此操作的權限",
                    Action = "查看您的權限設定或聯絡管理員確認",
                    ExpectedResult = "確認擁有相關操作權限"
                },
                new() {
                    StepNumber = 3,
                    Title = "聯絡管理員",
                    Description = "如果權限不足，請聯絡系統管理員",
                    Action = "透過內部系統或郵件聯絡管理員申請權限",
                    ExpectedResult = "管理員協助調整權限設定"
                }
            };
        }

        /// <summary>
        /// 產生配置錯誤解決步驟
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>配置錯誤解決步驟</returns>
        private static List<GuidanceStep> GenerateConfigurationSteps(ProcessingError error)
        {
            return new List<GuidanceStep>
            {
                new() {
                    StepNumber = 1,
                    Title = "檢查配置設定",
                    Description = "確認相關的設定參數是否正確",
                    Action = "查看設定頁面，檢查各項參數值",
                    ExpectedResult = "所有設定參數都符合要求"
                },
                new() {
                    StepNumber = 2,
                    Title = "重設為預設值",
                    Description = "如果設定有問題，嘗試重設為系統預設值",
                    Action = "點擊重設按鈕或手動輸入預設值",
                    ExpectedResult = "設定恢復為可正常運作的狀態"
                }
            };
        }

        /// <summary>
        /// 產生業務邏輯錯誤解決步驟
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>業務邏輯錯誤解決步驟</returns>
        private static List<GuidanceStep> GenerateBusinessLogicSteps(ProcessingError error)
        {
            return new List<GuidanceStep>
            {
                new() {
                    StepNumber = 1,
                    Title = "了解業務規則",
                    Description = "確認您了解相關的業務規則和限制",
                    Action = "查看系統說明或業務流程文件",
                    ExpectedResult = "明確理解操作的業務規則"
                },
                new() {
                    StepNumber = 2,
                    Title = "調整操作方式",
                    Description = "根據業務規則調整您的操作",
                    Action = "修改操作步驟或順序以符合業務要求",
                    ExpectedResult = "操作符合系統的業務邏輯"
                }
            };
        }

        /// <summary>
        /// 產生一般解決步驟
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>一般解決步驟</returns>
        private static List<GuidanceStep> GenerateGeneralSteps(ProcessingError error)
        {
            return new List<GuidanceStep>
            {
                new() {
                    StepNumber = 1,
                    Title = "重新整理頁面",
                    Description = "嘗試重新整理瀏覽器頁面",
                    Action = "按 F5 或點擊重新整理按鈕",
                    ExpectedResult = "頁面重新載入，問題可能獲得解決"
                },
                new() {
                    StepNumber = 2,
                    Title = "重新嘗試操作",
                    Description = "稍等片刻後重新執行相同操作",
                    Action = "等待 30 秒後重新點擊執行",
                    ExpectedResult = "操作成功完成"
                },
                new() {
                    StepNumber = 3,
                    Title = "聯絡技術支援",
                    Description = "如果問題持續存在，請聯絡技術支援",
                    Action = "記錄錯誤訊息並聯絡支援團隊",
                    ExpectedResult = "獲得專業技術協助"
                }
            };
        }

        /// <summary>
        /// 取得有用提示
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>提示列表</returns>
        private static List<string> GetHelpfulTips(ProcessingError error)
        {
            return error.Category switch
            {
                ErrorCategory.Validation => new List<string>
                {
                    "💡 建議在提交前先檢查資料格式",
                    "📋 可以參考範例格式進行輸入",
                    "⚠️ 特殊字元可能造成驗證失敗"
                },
                ErrorCategory.Authorization => new List<string>
                {
                    "🔐 確保使用正確的帳號登入",
                    "👥 權限問題請聯絡您的主管或管理員",
                    "🔄 嘗試登出後重新登入"
                },
                _ => new List<string>
                {
                    "🔄 大部分問題可以透過重新操作解決",
                    "💾 建議先儲存您的工作進度",
                    "📞 遇到困難時請不要猶豫尋求幫助"
                }
            };
        }

        /// <summary>
        /// 取得注意警告
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>警告列表</returns>
        private static List<string> GetPrecautionWarnings(ProcessingError error)
        {
            return new List<string>
            {
                "⚠️ 在進行任何修改前，請確保已備份重要資料",
                "🔒 不要與他人分享您的登入憑證",
                "📱 如果問題持續發生，請記錄發生的時間和操作步驟"
            };
        }

        /// <summary>
        /// 估計解決時間
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>估計時間（分鐘）</returns>
        private static int EstimateResolutionTime(ProcessingError error)
        {
            return error.Category switch
            {
                ErrorCategory.Validation => 5,      // 驗證錯誤通常很快解決
                ErrorCategory.Authorization => 10,   // 權限問題可能需要聯絡管理員
                ErrorCategory.Configuration => 15,   // 配置問題需要更多時間
                ErrorCategory.Processing => 10,        // 業務邏輯問題中等時間
                _ => 20                              // 其他問題預估較長時間
            };
        }

        /// <summary>
        /// 確定難度等級
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>難度等級</returns>
        private static string DetermineDifficultyLevel(ProcessingError error)
        {
            return error.Category switch
            {
                ErrorCategory.Validation => "簡單",
                ErrorCategory.Authorization => "中等",
                ErrorCategory.Configuration => "中等",
                ErrorCategory.Processing => "中等",
                _ => "困難"
            };
        }

        /// <summary>
        /// 取得所需權限
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>權限列表</returns>
        private static List<string> GetRequiredPermissions(ProcessingError error)
        {
            return error.Category switch
            {
                ErrorCategory.Authorization => new List<string> { "需要相關功能的存取權限" },
                ErrorCategory.Configuration => new List<string> { "設定修改權限" },
                _ => new List<string>()
            };
        }

        // 以下省略其他輔助方法的實作，保持程式碼簡潔...
        private static List<QuickFixAction> GetQuickFixActions(ProcessingError error) => new();
        private static List<DiagnosticQuestion> GetDiagnosticQuestions(ProcessingError error) => new();
        private static TroubleshootingTree CreateTroubleshootingTree(ProcessingError error) => new() { ErrorId = error.ErrorId };
        private static List<HelpResource> GetHelpResources(ProcessingError error) => new();
        private static List<FAQItem> GetValidationFAQ() => new();
        private static List<FAQItem> GetNetworkFAQ() => new();
        private static List<FAQItem> GetAuthorizationFAQ() => new();
        private static List<FAQItem> GetConfigurationFAQ() => new();
        private static List<FAQItem> GetBusinessLogicFAQ() => new();
        private static List<FAQItem> GetGeneralFAQ() => new();

        /// <summary>
        /// 發送指導通知
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <param name="guidance">使用者指導</param>
        private async Task SendGuidanceNotificationAsync(ProcessingError error, UserGuidance guidance)
        {
            var message = $"📖 已為您準備詳細的問題解決指導，預估解決時間：{guidance.EstimatedTimeToResolve} 分鐘";
            await SendNotificationAsync(error, message);
        }

        /// <summary>
        /// 記錄指導提供
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        private static void RecordGuidanceProvision(ProcessingError error)
        {
            error.ErrorContext["GuidanceProvidedAt"] = DateTime.UtcNow;
            error.ErrorContext["GuidanceStrategy"] = "UserGuidance";
            error.ErrorContext["GuidanceId"] = Guid.NewGuid().ToString();
        }
    }

    // 相關資料模型（簡化版本）
    public class UserGuidance
    {
        public Guid ErrorId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<GuidanceStep> Steps { get; set; } = new();
        public List<string> Tips { get; set; } = new();
        public List<string> PrecautionWarnings { get; set; } = new();
        public int EstimatedTimeToResolve { get; set; }
        public string DifficultyLevel { get; set; } = string.Empty;
        public List<string> RequiredPermissions { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }

    public class GuidanceStep
    {
        public int StepNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ExpectedResult { get; set; } = string.Empty;
    }

    public class InteractiveSolution
    {
        public Guid ErrorId { get; set; }
        public List<QuickFixAction> QuickFixActions { get; set; } = new();
        public List<DiagnosticQuestion> DiagnosticQuestions { get; set; } = new();
        public TroubleshootingTree TroubleshootingTree { get; set; } = new();
        public List<HelpResource> HelpResources { get; set; } = new();
    }

    // 其他輔助類別（簡化定義）
    public class QuickFixAction { }
    public class DiagnosticQuestion { }
    public class TroubleshootingTree { public Guid ErrorId { get; set; } }
    public class HelpResource { }
    public class FAQItem { }
}