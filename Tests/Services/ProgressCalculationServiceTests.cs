using Xunit;
using Summarizer.Services;
using Summarizer.Models.BatchProcessing;

namespace Tests.Services;

/// <summary>
/// 進度計算服務測試
/// </summary>
public class ProgressCalculationServiceTests
{
    private readonly ProgressCalculationService _service;

    /// <summary>
    /// 建構函式，初始化進度計算服務實例
    /// </summary>
    public ProgressCalculationServiceTests()
    {
        _service = new ProgressCalculationService();
    }

    /// <summary>
    /// 測試整體進度計算是否返回正確的進度值
    /// </summary>
    /// <param name="stage">處理階段</param>
    /// <param name="stageProgress">階段進度</param>
    /// <param name="completedSegments">已完成區段數</param>
    /// <param name="totalSegments">總區段數</param>
    /// <param name="expectedProgress">預期進度</param>
    [Theory]
    [InlineData(ProcessingStage.Initializing, 50, 0, 10, 2.5)]
    [InlineData(ProcessingStage.BatchProcessing, 50, 5, 10, 42.5)]
    [InlineData(ProcessingStage.Completed, 100, 10, 10, 100)]
    public void CalculateOverallProgress_ShouldReturnCorrectProgress(
        ProcessingStage stage,
        double stageProgress,
        int completedSegments,
        int totalSegments,
        double expectedProgress)
    {
        // 執行：呼叫計算整體進度方法
        var result = _service.CalculateOverallProgress(stage, stageProgress, completedSegments, totalSegments);

        // 斷言：驗證結果是否等於預期進度
        Assert.Equal(expectedProgress, result, 0.1);
    }

    /// <summary>
    /// 測試當已完成區段為0時，估計剩餘時間應返回null
    /// </summary>
    [Fact]
    public void EstimateRemainingTime_WithZeroCompleted_ShouldReturnNull()
    {
        // 執行：呼叫估計剩餘時間方法，傳入已完成區段為0
        var result = _service.EstimateRemainingTime(1000, 0, 10, ProcessingStage.BatchProcessing);

        // 斷言：驗證結果為null
        Assert.Null(result);
    }

    /// <summary>
    /// 測試使用有效資料估計剩餘時間應返回估計值
    /// </summary>
    /// <param name="elapsedMs">已耗費毫秒數</param>
    /// <param name="completed">已完成數</param>
    /// <param name="total">總數</param>
    /// <param name="stage">處理階段</param>
    /// <param name="expectedMs">預期毫秒數</param>
    [Theory]
    [InlineData(10000, 5, 10, ProcessingStage.BatchProcessing, 11000)] // 5500 * 2 * 1.1
    public void EstimateRemainingTime_WithValidData_ShouldReturnEstimate(
        long elapsedMs,
        int completed,
        int total,
        ProcessingStage stage,
        long expectedMs)
    {
        // 執行：呼叫估計剩餘時間方法
        var result = _service.EstimateRemainingTime(elapsedMs, completed, total, stage);

        // 斷言：驗證結果不為null且在允許誤差範圍內
        Assert.NotNull(result);
        Assert.True(Math.Abs(result.Value - expectedMs) < 1000); // 允許 1 秒誤差
    }
}