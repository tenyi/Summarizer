using FakeItEasy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Summarizer.Controllers;
using Summarizer.Exceptions;
using Summarizer.Models.Requests;
using Summarizer.Models.Responses;
using Summarizer.Services.Interfaces;
using Summarizer.Repositories.Interfaces;

namespace Tests.Controllers;

/// <summary>
/// SummarizeController 單元測試
/// </summary>
public class SummarizeControllerTests
{
    private readonly IOllamaSummaryService _ollamaService;
    private readonly IOpenAiSummaryService _openAiService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SummarizeController> _logger;
    private readonly ISummaryRepository _summaryRepository;
    private readonly ITextSegmentationService _textSegmentationService;
    private readonly IBatchSummaryProcessingService _batchProcessingService;
    private readonly SummarizeController _controller;

    public SummarizeControllerTests()
    {
        _ollamaService = A.Fake<IOllamaSummaryService>();
        _openAiService = A.Fake<IOpenAiSummaryService>();
        _configuration = A.Fake<IConfiguration>();
        _logger = A.Fake<ILogger<SummarizeController>>();
        _summaryRepository = A.Fake<ISummaryRepository>();
        _textSegmentationService = A.Fake<ITextSegmentationService>();
        _batchProcessingService = A.Fake<IBatchSummaryProcessingService>();
        
        _controller = new SummarizeController(_ollamaService, _openAiService, _textSegmentationService, _batchProcessingService, _configuration, _logger, _summaryRepository);
        
        // 設定 HttpContext
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task SummarizeAsync_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new SummarizeRequest
        {
            Text = "這是一段需要摘要的測試文本。"
        };
        
        var expectedSummary = "這是摘要結果。";
        
        A.CallTo(() => _configuration.GetValue<string>("AiProvider"))
            .Returns("ollama");
        
        A.CallTo(() => _ollamaService.SummarizeAsync(request.Text, A<CancellationToken>._))
            .Returns(Task.FromResult(expectedSummary));

        // Act
        var result = await _controller.SummarizeAsync(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SummarizeResponse>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.Equal(expectedSummary, response.Summary);
        Assert.Equal(request.Text.Length, response.OriginalLength);
        Assert.Equal(expectedSummary.Length, response.SummaryLength);
        Assert.True(response.ProcessingTimeMs > 0);
    }

    [Fact]
    public async Task SummarizeAsync_WithEmptyText_ReturnsBadRequest()
    {
        // Arrange
        var request = new SummarizeRequest
        {
            Text = ""
        };
        
        _controller.ModelState.AddModelError("Text", "文本內容不能為空");

        // Act
        var result = await _controller.SummarizeAsync(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<SummarizeResponse>(badRequestResult.Value);
        
        Assert.False(response.Success);
        Assert.Equal("VALIDATION_ERROR", response.ErrorCode);
    }

    [Fact]
    public async Task SummarizeAsync_WhenServiceTimeout_ReturnsRequestTimeout()
    {
        // Arrange
        var request = new SummarizeRequest
        {
            Text = "測試文本"
        };
        
        A.CallTo(() => _configuration.GetValue<string>("AiProvider"))
            .Returns("ollama");
        
        A.CallTo(() => _ollamaService.SummarizeAsync(request.Text, A<CancellationToken>._))
            .Throws<ApiTimeoutException>();

        // Act
        var result = await _controller.SummarizeAsync(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status408RequestTimeout, statusResult.StatusCode);
        
        var response = Assert.IsType<SummarizeResponse>(statusResult.Value);
        Assert.False(response.Success);
        Assert.Equal("TIMEOUT_ERROR", response.ErrorCode);
    }

    [Fact]
    public async Task SummarizeAsync_WhenServiceUnavailable_ReturnsServiceUnavailable()
    {
        // Arrange
        var request = new SummarizeRequest
        {
            Text = "測試文本"
        };
        
        A.CallTo(() => _configuration.GetValue<string>("AiProvider"))
            .Returns("ollama");
        
        A.CallTo(() => _ollamaService.SummarizeAsync(request.Text, A<CancellationToken>._))
            .Throws<ApiServiceUnavailableException>();

        // Act
        var result = await _controller.SummarizeAsync(request);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
        
        var response = Assert.IsType<SummarizeResponse>(statusResult.Value);
        Assert.False(response.Success);
        Assert.Equal("SERVICE_UNAVAILABLE", response.ErrorCode);
    }

    [Fact]
    public async Task SummarizeAsync_WithOpenAiProvider_UsesOpenAiService()
    {
        // Arrange
        var request = new SummarizeRequest
        {
            Text = "測試文本"
        };
        
        var expectedSummary = "OpenAI 摘要結果";
        
        A.CallTo(() => _configuration.GetValue<string>("AiProvider"))
            .Returns("openai");
        
        A.CallTo(() => _openAiService.SummarizeAsync(request.Text, A<CancellationToken>._))
            .Returns(Task.FromResult(expectedSummary));

        // Act
        var result = await _controller.SummarizeAsync(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SummarizeResponse>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.Equal(expectedSummary, response.Summary);
        
        A.CallTo(() => _openAiService.SummarizeAsync(request.Text, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _ollamaService.SummarizeAsync(A<string>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task HealthCheckAsync_WhenServiceIsHealthy_ReturnsOk()
    {
        // Arrange
        A.CallTo(() => _configuration.GetValue<string>("AiProvider"))
            .Returns("ollama");
        
        A.CallTo(() => _ollamaService.IsHealthyAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(true));

        // Act
        var result = await _controller.HealthCheckAsync();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(okResult.Value);
        
        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task HealthCheckAsync_WhenServiceIsUnhealthy_ReturnsServiceUnavailable()
    {
        // Arrange
        A.CallTo(() => _configuration.GetValue<string>("AiProvider"))
            .Returns("ollama");
        
        A.CallTo(() => _ollamaService.IsHealthyAsync(A<CancellationToken>._))
            .Returns(Task.FromResult(false));

        // Act
        var result = await _controller.HealthCheckAsync();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
        
        var response = Assert.IsType<ApiResponse<object>>(statusResult.Value);
        Assert.False(response.Success);
        Assert.Equal("SERVICE_UNHEALTHY", response.ErrorCode);
    }
}