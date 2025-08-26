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

    public ProgressCalculationServiceTests()
    {
        _service = new ProgressCalculationService();
    }

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
        // Act
        var result = _service.CalculateOverallProgress(stage, stageProgress, completedSegments, totalSegments);

        // Assert
        Assert.Equal(expectedProgress, result, 0.1);
    }

    [Fact]
    public void EstimateRemainingTime_WithZeroCompleted_ShouldReturnNull()
    {
        // Act
        var result = _service.EstimateRemainingTime(1000, 0, 10, ProcessingStage.BatchProcessing);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(10000, 5, 10, ProcessingStage.BatchProcessing, 11000)] // 5500 * 2 * 1.1
    public void EstimateRemainingTime_WithValidData_ShouldReturnEstimate(
        long elapsedMs, 
        int completed, 
        int total, 
        ProcessingStage stage, 
        long expectedMs)
    {
        // Act
        var result = _service.EstimateRemainingTime(elapsedMs, completed, total, stage);

        // Assert
        Assert.NotNull(result);
        Assert.True(Math.Abs(result.Value - expectedMs) < 1000); // 允許 1 秒誤差
    }
}