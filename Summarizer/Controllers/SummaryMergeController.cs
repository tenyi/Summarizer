using Microsoft.AspNetCore.Mvc;
using Summarizer.Models.BatchProcessing;
using Summarizer.Models.SummaryMerging;
using Summarizer.Models.Responses;
using Summarizer.Services.Interfaces;

namespace Summarizer.Controllers;

/// <summary>
/// 摘要合併控制器
/// 提供摘要合併相關的 API 端點
/// </summary>
[ApiController]
[Route("api/summarize")]
public class SummaryMergeController : ControllerBase
{
    private readonly ISummaryMergerService _summaryMergerService;
    private readonly IMergeStrategySelector _strategySelector;
    private readonly ILLMAssistedMergeService _llmMergeService;
    private readonly IBatchSummaryProcessingService _batchProcessingService;
    private readonly ILogger<SummaryMergeController> _logger;

    public SummaryMergeController(
        ISummaryMergerService summaryMergerService,
        IMergeStrategySelector strategySelector,
        ILLMAssistedMergeService llmMergeService,
        IBatchSummaryProcessingService batchProcessingService,
        ILogger<SummaryMergeController> logger)
    {
        _summaryMergerService = summaryMergerService ?? throw new ArgumentNullException(nameof(summaryMergerService));
        _strategySelector = strategySelector ?? throw new ArgumentNullException(nameof(strategySelector));
        _llmMergeService = llmMergeService ?? throw new ArgumentNullException(nameof(llmMergeService));
        _batchProcessingService = batchProcessingService ?? throw new ArgumentNullException(nameof(batchProcessingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 執行摘要合併
    /// </summary>
    /// <param name="request">合併請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併結果</returns>
    [HttpPost("merge")]
    public async Task<ActionResult<MergeApiResponse>> MergeAsync(
        [FromBody] MergeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始摘要合併，任務數量: {TaskCount}，策略: {Strategy}", 
                request.SummaryTasks?.Count ?? 0, request.Strategy);

            // 驗證請求
            var validation = ValidateMergeRequest(request);
            if (!validation.IsValid)
            {
                return BadRequest(new MergeApiResponse
                {
                    Success = false,
                    Error = validation.ErrorMessage,
                    ValidationErrors = validation.Errors
                });
            }

            // 執行合併
            var result = await _summaryMergerService.MergeSummariesAsync(
                request.SummaryTasks!,
                request.Strategy,
                request.Parameters,
                cancellationToken);

            // 如果啟用 LLM 輔助，進行 LLM 合併
            MergeResult? finalResult = result;
            if (request.EnableLLMAssist)
            {
                var llmResult = await _llmMergeService.MergeWithLLMAsync(
                    request.SummaryTasks!,
                    request.Strategy,
                    request.UserPreferences,
                    cancellationToken);

                // 融合規則式和 LLM 結果
                finalResult = await _llmMergeService.FuseMergeResultsAsync(
                    result, llmResult, request.FusionStrategy, cancellationToken);
            }

            return Ok(new MergeApiResponse
            {
                Success = true,
                MergeResult = finalResult,
                ProcessingTimeMs = finalResult.ProcessingTime.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "摘要合併過程發生錯誤");
            return StatusCode(500, new MergeApiResponse
            {
                Success = false,
                Error = "摘要合併處理失敗：" + ex.Message
            });
        }
    }

    /// <summary>
    /// 合併預覽
    /// 返回合併預覽而不實際執行完整合併
    /// </summary>
    /// <param name="request">預覽請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併預覽結果</returns>
    [HttpPost("merge/preview")]
    public async Task<ActionResult<MergePreviewResponse>> PreviewMergeAsync(
        [FromBody] MergePreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("開始合併預覽，任務數量: {TaskCount}", 
                request.SummaryTasks?.Count ?? 0);

            // 驗證請求
            if (request.SummaryTasks == null || !request.SummaryTasks.Any())
            {
                return BadRequest(new MergePreviewResponse
                {
                    Success = false,
                    Error = "摘要任務列表不能為空"
                });
            }

            // 分析內容特徵
            var characteristics = await _strategySelector.AnalyzeContentCharacteristicsAsync(
                request.SummaryTasks, cancellationToken);

            // 獲取策略建議
            var strategyRecommendation = await _strategySelector.SelectOptimalStrategyAsync(
                request.SummaryTasks, request.UserPreferences, cancellationToken);

            // 生成預覽摘要（使用快速模式）
            var previewParameters = new MergeParameters
            {
                TargetLength = Math.Min(request.PreviewLength ?? 200, 500),
                EnableLLMAssist = false, // 預覽不使用 LLM
                PreserveStructure = false
            };

            var previewResult = await _summaryMergerService.MergeSummariesAsync(
                request.SummaryTasks,
                strategyRecommendation.RecommendedStrategy,
                previewParameters,
                cancellationToken);

            return Ok(new MergePreviewResponse
            {
                Success = true,
                PreviewSummary = previewResult.FinalSummary,
                RecommendedStrategy = strategyRecommendation.RecommendedStrategy,
                StrategyReason = strategyRecommendation.Reason,
                ContentCharacteristics = characteristics,
                EstimatedProcessingTime = TimeSpan.FromMilliseconds(previewResult.ProcessingTime.TotalMilliseconds * 3),
                QualityScore = previewResult.QualityMetrics.OverallQuality
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合併預覽過程發生錯誤");
            return StatusCode(500, new MergePreviewResponse
            {
                Success = false,
                Error = "預覽生成失敗：" + ex.Message
            });
        }
    }

    /// <summary>
    /// 獲取可用的合併策略
    /// </summary>
    /// <param name="summaryTasks">摘要任務列表（可選）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>策略選項列表</returns>
    [HttpPost("merge/strategies")]
    public async Task<ActionResult<MergeStrategiesResponse>> GetMergeStrategiesAsync(
        [FromBody] List<SegmentSummaryTask>? summaryTasks = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var strategies = new List<StrategyOption>();

            // 基本策略選項
            foreach (MergeStrategy strategy in Enum.GetValues<MergeStrategy>())
            {
                var option = new StrategyOption
                {
                    Strategy = strategy,
                    Name = GetStrategyDisplayName(strategy),
                    Description = GetStrategyDescription(strategy),
                    IsRecommended = false
                };

                strategies.Add(option);
            }

            // 如果提供了摘要任務，分析並標記推薦策略
            if (summaryTasks?.Any() == true)
            {
                var recommendation = await _strategySelector.SelectOptimalStrategyAsync(
                    summaryTasks, cancellationToken: cancellationToken);

                var recommendedOption = strategies.FirstOrDefault(s => s.Strategy == recommendation.RecommendedStrategy);
                if (recommendedOption != null)
                {
                    recommendedOption.IsRecommended = true;
                    recommendedOption.RecommendationReason = recommendation.Reason;
                    recommendedOption.ConfidenceScore = recommendation.ConfidenceScore;
                }
            }

            return Ok(new MergeStrategiesResponse
            {
                Success = true,
                Strategies = strategies
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取合併策略時發生錯誤");
            return StatusCode(500, new MergeStrategiesResponse
            {
                Success = false,
                Error = "獲取策略選項失敗：" + ex.Message
            });
        }
    }

    /// <summary>
    /// 從批次處理結果執行合併
    /// </summary>
    /// <param name="request">批次合併請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併結果</returns>
    [HttpPost("merge/from-batch")]
    public async Task<ActionResult<MergeApiResponse>> MergeFromBatchAsync(
        [FromBody] BatchMergeRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("從批次處理結果執行合併，批次ID: {BatchId}", request.BatchId);

            // 獲取批次處理狀態
            var batchProgress = await _batchProcessingService.GetBatchProgressAsync(request.BatchId);
            if (batchProgress == null)
            {
                return NotFound(new MergeApiResponse
                {
                    Success = false,
                    Error = "找不到指定的批次處理任務"
                });
            }

            if (batchProgress.Status != BatchProcessingStatus.Completed)
            {
                return BadRequest(new MergeApiResponse
                {
                    Success = false,
                    Error = $"批次處理尚未完成，當前狀態：{batchProgress.Status}"
                });
            }

            // 獲取批次處理的摘要結果
            var batchResult = await _batchProcessingService.GetBatchResultAsync(request.BatchId);
            if (batchResult == null)
            {
                return BadRequest(new MergeApiResponse
                {
                    Success = false,
                    Error = "無法取得批次處理結果"
                });
            }

            var summaryTasks = batchResult.Tasks
                .Where(t => t.Status == SegmentTaskStatus.Completed)
                .ToList();

            if (!summaryTasks.Any())
            {
                return BadRequest(new MergeApiResponse
                {
                    Success = false,
                    Error = "批次處理中沒有成功完成的摘要任務"
                });
            }

            // 建立合併請求
            var mergeRequest = new MergeRequest
            {
                SummaryTasks = summaryTasks,
                Strategy = request.Strategy ?? MergeStrategy.Balanced,
                Parameters = request.Parameters ?? new MergeParameters(),
                EnableLLMAssist = request.EnableLLMAssist,
                UserPreferences = request.UserPreferences,
                FusionStrategy = request.FusionStrategy ?? FusionStrategy.Intelligent
            };

            // 執行合併
            return await MergeAsync(mergeRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "從批次結果合併時發生錯誤，批次ID: {BatchId}", request.BatchId);
            return StatusCode(500, new MergeApiResponse
            {
                Success = false,
                Error = "批次合併處理失敗：" + ex.Message
            });
        }
    }

    /// <summary>
    /// 儲存合併結果
    /// </summary>
    /// <param name="request">儲存請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>儲存結果</returns>
    [HttpPost("merge/save")]
    public async Task<ActionResult<SaveMergeResultResponse>> SaveMergeResultAsync(
        [FromBody] SaveMergeResultRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("儲存合併結果，合併ID: {MergeJobId}", request.MergeJobId);

            // TODO: 實作合併結果的儲存邏輯
            // 這裡可以儲存到資料庫、檔案系統或其他持久化存儲
            await Task.CompletedTask; // 保持異步特性，將來可添加真正的異步操作
            
            var savedId = Guid.NewGuid(); // 模擬儲存後的 ID

            return Ok(new SaveMergeResultResponse
            {
                Success = true,
                SavedId = savedId,
                Message = "合併結果已成功儲存"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "儲存合併結果時發生錯誤，合併ID: {MergeJobId}", request.MergeJobId);
            return StatusCode(500, new SaveMergeResultResponse
            {
                Success = false,
                Error = "儲存合併結果失敗：" + ex.Message
            });
        }
    }

    /// <summary>
    /// 檢索已儲存的合併結果
    /// </summary>
    /// <param name="savedId">儲存的結果 ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>合併結果</returns>
    [HttpGet("merge/retrieve/{savedId}")]
    public async Task<ActionResult<RetrieveMergeResultResponse>> RetrieveMergeResultAsync(
        Guid savedId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("檢索合併結果，儲存ID: {SavedId}", savedId);

            // TODO: 實作從持久化存儲檢索合併結果的邏輯
            await Task.CompletedTask; // 保持異步特性，將來可添加真正的異步操作
            
            return NotFound(new RetrieveMergeResultResponse
            {
                Success = false,
                Error = "找不到指定的合併結果"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "檢索合併結果時發生錯誤，儲存ID: {SavedId}", savedId);
            return StatusCode(500, new RetrieveMergeResultResponse
            {
                Success = false,
                Error = "檢索合併結果失敗：" + ex.Message
            });
        }
    }

    #region 私有輔助方法

    /// <summary>
    /// 驗證合併請求
    /// </summary>
    private MergeRequestValidation ValidateMergeRequest(MergeRequest request)
    {
        if (request == null)
        {
            return new MergeRequestValidation
            {
                IsValid = false,
                ErrorMessage = "請求不能為空"
            };
        }

        if (request.SummaryTasks == null || !request.SummaryTasks.Any())
        {
            return new MergeRequestValidation
            {
                IsValid = false,
                ErrorMessage = "摘要任務列表不能為空"
            };
        }

        var errors = new List<string>();

        // 檢查每個摘要任務
        for (int i = 0; i < request.SummaryTasks.Count; i++)
        {
            var task = request.SummaryTasks[i];
            if (string.IsNullOrWhiteSpace(task.SummaryResult))
            {
                errors.Add($"摘要任務 {i + 1} 的摘要內容不能為空");
            }
        }

        // 檢查參數
        if (request.Parameters?.TargetLength < 50)
        {
            errors.Add("目標長度不能小於 50 個字");
        }

        return new MergeRequestValidation
        {
            IsValid = !errors.Any(),
            ErrorMessage = errors.Any() ? string.Join("; ", errors) : null,
            Errors = errors
        };
    }

    /// <summary>
    /// 獲取策略顯示名稱
    /// </summary>
    private static string GetStrategyDisplayName(MergeStrategy strategy)
    {
        return strategy switch
        {
            MergeStrategy.Concise => "簡潔式合併",
            MergeStrategy.Detailed => "詳細式合併",
            MergeStrategy.Structured => "結構化合併",
            MergeStrategy.Balanced => "平衡式合併",
            MergeStrategy.Custom => "自訂合併",
            _ => strategy.ToString()
        };
    }

    /// <summary>
    /// 獲取策略描述
    /// </summary>
    private static string GetStrategyDescription(MergeStrategy strategy)
    {
        return strategy switch
        {
            MergeStrategy.Concise => "重點摘取，簡明扼要，適合快速瀏覽",
            MergeStrategy.Detailed => "保留細節，完整敘述，適合深入了解",
            MergeStrategy.Structured => "分類整理，層次清晰，適合結構化展示",
            MergeStrategy.Balanced => "在簡潔和詳細之間取得平衡，適合一般用途",
            MergeStrategy.Custom => "根據自訂偏好進行合併，靈活調整",
            _ => "未知策略"
        };
    }

    #endregion
}