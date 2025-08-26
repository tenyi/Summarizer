using Summarizer.Models.BatchProcessing;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services;

/// <summary>
/// 進度計算服務實作，提供進度計算和時間預估功能
/// </summary>
public class ProgressCalculationService : IProgressCalculationService
{
    /// <summary>
    /// 各階段的預設權重（用於整體進度計算）
    /// </summary>
    private readonly Dictionary<ProcessingStage, double> _stageWeights = new()
    {
        { ProcessingStage.Initializing, 5.0 },
        { ProcessingStage.Segmenting, 10.0 },
        { ProcessingStage.BatchProcessing, 70.0 },
        { ProcessingStage.Merging, 10.0 },
        { ProcessingStage.Finalizing, 5.0 }
    };

    /// <summary>
    /// 各階段的時間乘數（用於時間預估調整）
    /// </summary>
    private readonly Dictionary<ProcessingStage, double> _stageTimeMultipliers = new()
    {
        { ProcessingStage.Initializing, 0.1 },
        { ProcessingStage.Segmenting, 0.2 },
        { ProcessingStage.BatchProcessing, 1.0 },
        { ProcessingStage.Merging, 0.3 },
        { ProcessingStage.Finalizing, 0.1 }
    };

    public double CalculateOverallProgress(
        ProcessingStage currentStage,
        double stageProgress,
        int completedSegments,
        int totalSegments)
    {
        // 如果已完成，直接返回100%
        if (currentStage == ProcessingStage.Completed)
            return 100.0;

        // 如果失敗，根據已完成的分段計算進度
        if (currentStage == ProcessingStage.Failed)
            return totalSegments > 0 ? (double)completedSegments / totalSegments * 100 : 0;

        // 計算已完成階段的進度
        double baseProgress = 0;
        foreach (var stage in _stageWeights.Keys)
        {
            if (stage == currentStage) break;
            baseProgress += _stageWeights[stage];
        }

        // 計算當前階段的貢獻
        var currentStageWeight = _stageWeights.GetValueOrDefault(currentStage, 0);
        var normalizedStageProgress = Math.Max(0, Math.Min(100, stageProgress));
        var stageContribution = (currentStageWeight * normalizedStageProgress) / 100.0;

        // 總進度不能超過100%
        return Math.Min(100.0, baseProgress + stageContribution);
    }

    public long? EstimateRemainingTime(
        long elapsedTimeMs,
        int completedSegments,
        int totalSegments,
        ProcessingStage currentStage)
    {
        // 無法預估的情況
        if (completedSegments == 0 || totalSegments == 0 || elapsedTimeMs <= 0)
            return null;

        // 已完成的情況
        if (currentStage == ProcessingStage.Completed || completedSegments >= totalSegments)
            return 0;

        // 計算平均每分段處理時間
        var avgTimePerSegment = (double)elapsedTimeMs / completedSegments;
        var remainingSegments = totalSegments - completedSegments;

        // 根據當前階段調整時間預估
        var stageMultiplier = GetStageTimeMultiplier(currentStage);
        var estimatedTime = avgTimePerSegment * remainingSegments * stageMultiplier;

        // 新增一些緩衝時間（10%）來提高預估準確性
        estimatedTime *= 1.1;

        return (long)Math.Max(0, estimatedTime);
    }

    public ProcessingSpeed CalculateProcessingSpeed(
        IEnumerable<SegmentStatus> segmentStatuses,
        long elapsedTimeMs)
    {
        var completedSegments = segmentStatuses.Where(s => s.IsCompleted).ToList();
        var elapsedMinutes = elapsedTimeMs / 60000.0;
        var elapsedSeconds = elapsedTimeMs / 1000.0;

        var speed = new ProcessingSpeed
        {
            LastCalculated = DateTime.UtcNow,
            CalculationWindowMs = elapsedTimeMs
        };

        if (elapsedMinutes > 0)
        {
            // 每分鐘處理的分段數量
            speed.SegmentsPerMinute = completedSegments.Count / elapsedMinutes;

            // 計算字符處理速度
            var totalCharacters = completedSegments.Sum(s => s.ContentLength);
            if (elapsedSeconds > 0)
            {
                speed.CharactersPerSecond = totalCharacters / elapsedSeconds;
                speed.CurrentThroughput = speed.CharactersPerSecond;
            }

            // 計算處理延遲統計
            var processingTimes = completedSegments
                .Where(s => s.ProcessingTimeMs.HasValue)
                .Select(s => (double)s.ProcessingTimeMs.Value)
                .ToList();

            if (processingTimes.Any())
            {
                speed.AverageLatencyMs = processingTimes.Average();
                speed.MaxLatencyMs = processingTimes.Max();
                speed.MinLatencyMs = processingTimes.Min();

                // 效率計算（相對於理想速度，這裡假設理想速度是平均延遲的倒數）
                var idealThroughput = 1000.0 / speed.AverageLatencyMs; // 每秒理想處理數量
                var actualThroughput = speed.SegmentsPerMinute / 60.0; // 每秒實際處理數量
                speed.EfficiencyPercentage = Math.Min(100, actualThroughput / idealThroughput * 100);
            }
        }

        return speed;
    }

    public double CalculateStageProgress(
        ProcessingStage stage,
        int completedSegments,
        int totalSegments,
        double currentSegmentProgress = 0)
    {
        if (totalSegments == 0) return 0;

        double progress = stage switch
        {
            ProcessingStage.Initializing => 100, // 初始化是瞬間完成的
            ProcessingStage.Segmenting => 100,   // 分段也是相對快速的
            ProcessingStage.BatchProcessing => CalculateBatchProcessingProgress(completedSegments, totalSegments, currentSegmentProgress),
            ProcessingStage.Merging => CalculateMergingProgress(completedSegments, totalSegments),
            ProcessingStage.Finalizing => 100,   // 完成處理也是相對快速的
            ProcessingStage.Completed => 100,
            ProcessingStage.Failed => 0,
            _ => 0
        };

        return Math.Max(0, Math.Min(100, progress));
    }

    public double GetStageTimeMultiplier(ProcessingStage stage)
    {
        return _stageTimeMultipliers.GetValueOrDefault(stage, 1.0);
    }

    public ProcessingProgress UpdateProgress(
        ProcessingProgress currentProgress,
        IEnumerable<SegmentStatus> segmentStatuses)
    {
        var segmentList = segmentStatuses.ToList();
        var completedCount = segmentList.Count(s => s.Status == SegmentProcessingStatus.Completed);
        var failedCount = segmentList.Count(s => s.Status == SegmentProcessingStatus.Failed);
        var processingSegment = segmentList.FirstOrDefault(s => s.IsProcessing);

        // 更新基本統計
        currentProgress.CompletedSegments = completedCount;
        currentProgress.FailedSegments = failedCount;
        currentProgress.CurrentSegment = processingSegment?.Index ?? completedCount;
        currentProgress.CurrentSegmentTitle = processingSegment?.Title;

        // 計算階段進度
        var stageProgress = CalculateStageProgress(
            currentProgress.CurrentStage,
            completedCount,
            currentProgress.TotalSegments);
        currentProgress.StageProgress = stageProgress;

        // 計算整體進度
        currentProgress.OverallProgress = CalculateOverallProgress(
            currentProgress.CurrentStage,
            stageProgress,
            completedCount,
            currentProgress.TotalSegments);

        // 更新時間統計
        var elapsedTime = DateTime.UtcNow - currentProgress.StartTime;
        currentProgress.ElapsedTimeMs = (long)elapsedTime.TotalMilliseconds;

        // 預估剩餘時間
        currentProgress.EstimatedRemainingTimeMs = EstimateRemainingTime(
            currentProgress.ElapsedTimeMs,
            completedCount,
            currentProgress.TotalSegments,
            currentProgress.CurrentStage);

        // 計算平均分段時間
        var completedSegments = segmentList.Where(s => s.Status == SegmentProcessingStatus.Completed);
        var avgTime = completedSegments.Any() 
            ? completedSegments.Where(s => s.ProcessingTimeMs.HasValue).Average(s => (double)s.ProcessingTimeMs.Value)
            : 0;
        currentProgress.AverageSegmentTimeMs = avgTime;

        // 更新處理速度
        currentProgress.ProcessingSpeed = CalculateProcessingSpeed(segmentList, currentProgress.ElapsedTimeMs);

        // 更新最後修改時間
        currentProgress.LastUpdated = DateTime.UtcNow;

        return currentProgress;
    }

    /// <summary>
    /// 計算批次處理階段的進度
    /// </summary>
    private double CalculateBatchProcessingProgress(int completedSegments, int totalSegments, double currentSegmentProgress)
    {
        if (totalSegments == 0) return 100;

        // 基礎進度：已完成分段的百分比
        var baseProgress = (double)completedSegments / totalSegments * 100;

        // 當前分段的貢獻：當前分段進度 / 總分段數
        var currentSegmentContribution = currentSegmentProgress / totalSegments;

        return Math.Min(100, baseProgress + currentSegmentContribution);
    }

    /// <summary>
    /// 計算合併階段的進度
    /// </summary>
    private double CalculateMergingProgress(int completedSegments, int totalSegments)
    {
        if (totalSegments == 0) return 100;

        // 合併進度基於已完成分段的比例
        return Math.Min(100, (double)completedSegments / totalSegments * 100);
    }
}