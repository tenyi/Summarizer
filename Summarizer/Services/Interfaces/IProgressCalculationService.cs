using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 進度計算服務介面，負責計算處理進度和時間預估
/// </summary>
public interface IProgressCalculationService
{
    /// <summary>
    /// 計算整體進度百分比
    /// </summary>
    /// <param name="currentStage">當前處理階段</param>
    /// <param name="stageProgress">階段內進度（0-100）</param>
    /// <param name="completedSegments">已完成分段數</param>
    /// <param name="totalSegments">總分段數</param>
    /// <returns>整體進度百分比（0-100）</returns>
    double CalculateOverallProgress(
        ProcessingStage currentStage,
        double stageProgress,
        int completedSegments,
        int totalSegments);

    /// <summary>
    /// 預估剩餘處理時間
    /// </summary>
    /// <param name="elapsedTimeMs">已經花費的時間（毫秒）</param>
    /// <param name="completedSegments">已完成分段數</param>
    /// <param name="totalSegments">總分段數</param>
    /// <param name="currentStage">當前處理階段</param>
    /// <returns>預估剩餘時間（毫秒），如果無法預估則返回null</returns>
    long? EstimateRemainingTime(
        long elapsedTimeMs,
        int completedSegments,
        int totalSegments,
        ProcessingStage currentStage);

    /// <summary>
    /// 計算處理速度統計
    /// </summary>
    /// <param name="segmentStatuses">分段狀態列表</param>
    /// <param name="elapsedTimeMs">總經過時間</param>
    /// <returns>處理速度統計</returns>
    ProcessingSpeed CalculateProcessingSpeed(
        IEnumerable<SegmentStatus> segmentStatuses,
        long elapsedTimeMs);

    /// <summary>
    /// 計算階段進度
    /// </summary>
    /// <param name="stage">處理階段</param>
    /// <param name="completedSegments">已完成分段數</param>
    /// <param name="totalSegments">總分段數</param>
    /// <param name="currentSegmentProgress">當前分段進度（0-100）</param>
    /// <returns>階段進度百分比（0-100）</returns>
    double CalculateStageProgress(
        ProcessingStage stage,
        int completedSegments,
        int totalSegments,
        double currentSegmentProgress = 0);

    /// <summary>
    /// 取得階段的時間乘數（用於調整不同階段的時間預估）
    /// </summary>
    /// <param name="stage">處理階段</param>
    /// <returns>時間乘數</returns>
    double GetStageTimeMultiplier(ProcessingStage stage);

    /// <summary>
    /// 更新處理進度資料
    /// </summary>
    /// <param name="currentProgress">當前進度資料</param>
    /// <param name="segmentStatuses">分段狀態列表</param>
    /// <returns>更新後的進度資料</returns>
    ProcessingProgress UpdateProgress(
        ProcessingProgress currentProgress,
        IEnumerable<SegmentStatus> segmentStatuses);
}