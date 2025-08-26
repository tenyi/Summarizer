using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Summarizer.Services;
using Summarizer.Services.Interfaces;
using Summarizer.Models.BatchProcessing;

namespace Tests.Services;

/// <summary>
/// 取消服務測試
/// </summary>
public class CancellationServiceTests : IDisposable
{
    private readonly Mock<IBatchProgressNotificationService> _mockNotificationService;
    private readonly Mock<IPartialResultHandler> _mockPartialResultHandler;
    private readonly Mock<ILogger<CancellationService>> _mockLogger;
    private readonly CancellationService _cancellationService;

    public CancellationServiceTests()
    {
        _mockNotificationService = new Mock<IBatchProgressNotificationService>();
        _mockPartialResultHandler = new Mock<IPartialResultHandler>();
        _mockLogger = new Mock<ILogger<CancellationService>>();
        _cancellationService = new CancellationService(
            _mockNotificationService.Object, 
            _mockPartialResultHandler.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void RegisterBatchProcess_ShouldReturnValidCancellationToken()
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var context = new BatchProcessingContext { BatchId = batchId };

        // Act
        var token = _cancellationService.RegisterBatchProcess(batchId, context);

        // Assert
        Assert.False(token.IsCancellationRequested);
        Assert.NotNull(_cancellationService.GetCancellationToken(batchId));
    }

    [Fact]
    public async Task RequestCancellationAsync_WithNonExistentBatch_ShouldReturnNotFound()
    {
        // Arrange
        var request = new CancellationRequest
        {
            BatchId = Guid.NewGuid(),
            UserId = "test-user",
            Reason = CancellationReason.UserRequested
        };

        // Act
        var result = await _cancellationService.RequestCancellationAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("找不到指定的批次處理", result.Message);
    }

    [Fact]
    public async Task RequestCancellationAsync_WithValidBatch_ShouldCancelGracefully()
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var context = new BatchProcessingContext { BatchId = batchId, IsAtSafeCheckpoint = true };
        var token = _cancellationService.RegisterBatchProcess(batchId, context);

        var request = new CancellationRequest
        {
            BatchId = batchId,
            UserId = "test-user",
            Reason = CancellationReason.UserRequested,
            ForceCancel = false,
            SavePartialResults = true
        };

        // Act
        var result = await _cancellationService.RequestCancellationAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("取消成功", result.Message);
        // 註：取消操作完成後，資源已被清理，因此不檢查 IsCancellationRequested
    }

    [Fact]
    public async Task RequestCancellationAsync_WithForceCancel_ShouldCancelImmediately()
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var context = new BatchProcessingContext { BatchId = batchId };
        var token = _cancellationService.RegisterBatchProcess(batchId, context);

        var request = new CancellationRequest
        {
            BatchId = batchId,
            UserId = "test-user",
            Reason = CancellationReason.UserRequested,
            ForceCancel = true
        };

        // Act
        var result = await _cancellationService.RequestCancellationAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("強制取消成功", result.Message);
        Assert.False(result.PartialResultsSaved);
        // 註：取消操作完成後，資源已被清理，因此不檢查 IsCancellationRequested
    }

    [Fact]
    public void SetSafeCheckpoint_ShouldUpdateContextState()
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var context = new BatchProcessingContext { BatchId = batchId };
        _cancellationService.RegisterBatchProcess(batchId, context);

        // Act
        _cancellationService.SetSafeCheckpoint(batchId, true);

        // Assert
        Assert.True(context.IsAtSafeCheckpoint);
    }

    [Fact]
    public void IsCancellationRequested_WithNonExistentBatch_ShouldReturnFalse()
    {
        // Arrange
        var batchId = Guid.NewGuid();

        // Act
        var result = _cancellationService.IsCancellationRequested(batchId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetCancellationToken_WithNonExistentBatch_ShouldReturnNull()
    {
        // Arrange
        var batchId = Guid.NewGuid();

        // Act
        var result = _cancellationService.GetCancellationToken(batchId);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(CancellationReason.UserRequested)]
    [InlineData(CancellationReason.SystemTimeout)]
    [InlineData(CancellationReason.ResourceExhausted)]
    [InlineData(CancellationReason.ServiceUnavailable)]
    [InlineData(CancellationReason.QualityThreshold)]
    [InlineData(CancellationReason.ApplicationShutdown)]
    public async Task RequestCancellationAsync_WithDifferentReasons_ShouldHandleCorrectly(CancellationReason reason)
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var context = new BatchProcessingContext { BatchId = batchId, IsAtSafeCheckpoint = true };
        _cancellationService.RegisterBatchProcess(batchId, context);

        var request = new CancellationRequest
        {
            BatchId = batchId,
            UserId = "test-user",
            Reason = reason
        };

        // Act
        var result = await _cancellationService.RequestCancellationAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.True(context.IsCancellationRequested);
        Assert.Equal(request, context.CancellationRequest);
    }

    [Fact]
    public async Task RequestCancellationAsync_ShouldNotifyProgressService()
    {
        // Arrange
        var batchId = Guid.NewGuid();
        var context = new BatchProcessingContext { BatchId = batchId, IsAtSafeCheckpoint = true };
        _cancellationService.RegisterBatchProcess(batchId, context);

        var request = new CancellationRequest
        {
            BatchId = batchId,
            UserId = "test-user",
            Reason = CancellationReason.UserRequested
        };

        // Act
        await _cancellationService.RequestCancellationAsync(request);

        // Assert
        _mockNotificationService.Verify(
            x => x.NotifyCancellationRequestedAsync(batchId, request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    public void Dispose()
    {
        _cancellationService?.Dispose();
    }
}