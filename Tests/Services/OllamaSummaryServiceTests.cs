using FakeItEasy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Summarizer.Configuration;
using Summarizer.Exceptions;
using Summarizer.Services;
using System.Net;
using System.Text.Json;

namespace Tests.Services;

/// <summary>
/// OllamaSummaryService 單元測試
/// </summary>
public class OllamaSummaryServiceTests
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<OllamaConfig> _config;
    private readonly ILogger<OllamaSummaryService> _logger;
    private readonly OllamaSummaryService _service;
    private readonly OllamaConfig _ollamaConfig;

    public OllamaSummaryServiceTests()
    {
        _httpClient = A.Fake<HttpClient>();
        _config = A.Fake<IOptions<OllamaConfig>>();
        _logger = A.Fake<ILogger<OllamaSummaryService>>();
        
        _ollamaConfig = new OllamaConfig
        {
            Endpoint = "http://localhost:11434",
            Model = "gemma3",
            Timeout = 30000,
            RetryCount = 3,
            RetryDelayMs = 1000,
            ValidateSslCertificate = true
        };
        
        A.CallTo(() => _config.Value).Returns(_ollamaConfig);
        _service = new OllamaSummaryService(_httpClient, _config, _logger);
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OllamaSummaryService(null!, _config, _logger));
        
        Assert.Equal("httpClient", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullConfig_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OllamaSummaryService(_httpClient, null!, _logger));
        
        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new OllamaSummaryService(_httpClient, _config, null!));
        
        Assert.Equal("logger", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SummarizeAsync_WithInvalidText_ThrowsArgumentException(string? text)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.SummarizeAsync(text!));
    }

    [Fact]
    public async Task SummarizeAsync_WithValidText_ReturnsSuccessResponse()
    {
        // Arrange
        var inputText = "這是一段測試文本，需要進行摘要處理。";
        var expectedSummary = "這是摘要結果。";
        
        var responseContent = JsonSerializer.Serialize(new { response = expectedSummary });
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseContent)
        };
        
        A.CallTo(() => _httpClient.PostAsync(A<string>._, A<HttpContent>._, A<CancellationToken>._))
            .Returns(Task.FromResult(httpResponse));

        // Act
        var result = await _service.SummarizeAsync(inputText);

        // Assert
        Assert.Equal(expectedSummary, result);
    }

    [Fact]
    public async Task IsHealthyAsync_WhenServiceIsHealthy_ReturnsTrue()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };
        
        A.CallTo(() => _httpClient.GetAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(httpResponse));

        // Act
        var result = await _service.IsHealthyAsync();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsHealthyAsync_WhenServiceIsUnhealthy_ReturnsFalse()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        
        A.CallTo(() => _httpClient.GetAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(httpResponse));

        // Act
        var result = await _service.IsHealthyAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task TestConnectionAsync_CallsIsHealthyAsync()
    {
        // Arrange
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]")
        };
        
        A.CallTo(() => _httpClient.GetAsync(A<string>._, A<CancellationToken>._))
            .Returns(Task.FromResult(httpResponse));

        // Act
        var result = await _service.TestConnectionAsync();

        // Assert
        Assert.True(result);
        A.CallTo(() => _httpClient.GetAsync(A<string>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }
}