using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Summarizer.Configuration;
using Summarizer.Models.TextSegmentation;
using Summarizer.Services;
using Summarizer.Services.Interfaces;
using Xunit;

namespace Tests.Services;

/// <summary>
/// 文本分段服務測試類別
/// </summary>
public class TextSegmentationServiceTests
{
    private readonly Mock<ILogger<TextSegmentationService>> _mockLogger;
    private readonly Mock<ISummaryService> _mockSummaryService;
    private readonly TextSegmentationConfig _config;
    private readonly TextSegmentationService _service;

    public TextSegmentationServiceTests()
    {
        _mockLogger = new Mock<ILogger<TextSegmentationService>>();
        _mockSummaryService = new Mock<ISummaryService>();
        _config = new TextSegmentationConfig
        {
            TriggerLength = 2048,
            MaxSegmentLength = 2000,
            ContextLimitBuffer = 0.8,
            PreserveParagraphs = true,
            EnableLlmSegmentation = true,
            SentenceEndMarkers = new[] { ".", "。", "!", "！", "?" },
            GenerateAutoTitles = true,
            LogDetailedInfo = false  // 關閉詳細日誌以避免測試中的噪音
        };

        var options = Options.Create(_config);
        _service = new TextSegmentationService(options, _mockSummaryService.Object, _mockLogger.Object);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    [InlineData("這是一個短文本", false)]
    [InlineData("這是一個短文本。", false)]
    public void ShouldSegmentText_短文本_應該返回False(string text, bool expected)
    {
        // Act
        var result = _service.ShouldSegmentText(text);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldSegmentText_長文本_應該返回True()
    {
        // Arrange
        var longText = new string('測', 3000); // 3000個字符的文本

        // Act
        var result = _service.ShouldSegmentText(longText);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldSegmentText_正好觸發門檻_應該返回False()
    {
        // Arrange
        var text = new string('測', _config.TriggerLength); // 正好2048字符

        // Act
        var result = _service.ShouldSegmentText(text);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldSegmentText_超過觸發門檻一個字符_應該返回True()
    {
        // Arrange
        var text = new string('測', _config.TriggerLength + 1); // 2049字符

        // Act
        var result = _service.ShouldSegmentText(text);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void SegmentByPunctuation_簡單句子_應該正確分段()
    {
        // Arrange
        var text = "這是第一個句子。這是第二個句子。這是第三個句子。";
        var maxLength = 20;

        // Act
        var result = _service.SegmentByPunctuation(text, maxLength, false);

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, segment => Assert.True(segment.CharacterCount <= maxLength));
        Assert.All(result, segment => Assert.False(string.IsNullOrEmpty(segment.Content)));
    }

    [Fact]
    public void SegmentByPunctuation_長段落_應該保持字數限制()
    {
        // Arrange
        var longSentence = "這是一個非常長的句子" + new string('測', 500) + "。"; // 超長句子
        var maxLength = 200;

        // Act
        var result = _service.SegmentByPunctuation(longSentence, maxLength, false);

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, segment => Assert.True(segment.CharacterCount <= maxLength));
    }

    [Fact]
    public void SegmentByPunctuation_多段落文本_應該保留段落邊界()
    {
        // Arrange
        var text = "第一段的內容。\n\n第二段的內容。\n\n第三段的內容。";
        var maxLength = 50;

        // Act
        var result = _service.SegmentByPunctuation(text, maxLength, true);

        // Assert
        Assert.NotEmpty(result);
        Assert.All(result, segment => Assert.True(segment.CharacterCount <= maxLength));
    }

    [Fact]
    public void GenerateSegmentTitle_有內容的分段_應該生成基於首句的標題()
    {
        // Arrange
        var content = "這是一個測試句子。這是第二個句子。";
        var segmentIndex = 0;

        // Act
        var result = _service.GenerateSegmentTitle(content, segmentIndex);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.DoesNotContain("分段 1", result); // 不應該是默認標題
    }

    [Fact]
    public void GenerateSegmentTitle_空內容_應該生成默認標題()
    {
        // Arrange
        var content = "";
        var segmentIndex = 0;

        // Act
        var result = _service.GenerateSegmentTitle(content, segmentIndex);

        // Assert
        Assert.Equal("分段 1", result);
    }

    [Fact]
    public void GenerateSegmentTitle_超長首句_應該截取前30字符()
    {
        // Arrange
        var longContent = "這是一個非常非常非常非常非常非常長的句子用來測試標題生成功能是否能正確截取。";
        var segmentIndex = 0;

        // Act
        var result = _service.GenerateSegmentTitle(longContent, segmentIndex);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length <= 33); // 30字符 + "..."
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void ValidateSegmentQuality_空分段列表_應該返回品質不可接受()
    {
        // Arrange
        var segments = new List<SegmentResult>();
        var originalText = "測試文本";

        // Act
        var result = _service.ValidateSegmentQuality(segments, originalText);

        // Assert
        Assert.False(result.IsQualityAcceptable);
        Assert.Contains("無分段結果", result.QualityIssues);
    }

    [Fact]
    public void ValidateSegmentQuality_均勻分段_應該有高品質評分()
    {
        // Arrange
        var segments = new List<SegmentResult>
        {
            new() { Content = "第一段內容。", CharacterCount = 5, SegmentIndex = 0 },
            new() { Content = "第二段內容。", CharacterCount = 5, SegmentIndex = 1 },
            new() { Content = "第三段內容。", CharacterCount = 5, SegmentIndex = 2 }
        };
        var originalText = "第一段內容。第二段內容。第三段內容。";

        // Act
        var result = _service.ValidateSegmentQuality(segments, originalText);

        // Assert
        Assert.True(result.OverallScore > 50); // 應該有合理的評分
        Assert.NotNull(result.Statistics);
        Assert.Equal(3, result.Statistics.SegmentCount);
    }

    [Fact]
    public async Task SegmentTextAsync_空文本_應該返回錯誤()
    {
        // Arrange
        var request = new SegmentationRequest { Text = "" };

        // Act
        var result = await _service.SegmentTextAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("EMPTY_TEXT", result.ErrorCode);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SegmentTextAsync_正常文本_應該成功分段()
    {
        // Arrange
        var text = "這是第一個句子。這是第二個句子。這是第三個句子。這是第四個句子。";
        var request = new SegmentationRequest 
        { 
            Text = text,
            MaxSegmentLength = 20,
            PreserveParagraphs = true,
            GenerateTitles = true,
            EnableLlmSegmentation = false  // 關閉 LLM 以避免網路調用
        };

        // Act
        var result = await _service.SegmentTextAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Segments);
        Assert.True(result.TotalSegments > 0);
        Assert.Equal(text.Length, result.OriginalLength);
        Assert.True(result.ProcessingTimeMs >= 0);
        Assert.Equal("Punctuation", result.SegmentationMethod);
    }

    [Fact]
    public async Task SegmentTextAsync_啟用標題生成_所有分段應該有標題()
    {
        // Arrange
        var text = "這是第一個句子。這是第二個句子。這是第三個句子。";
        var request = new SegmentationRequest 
        { 
            Text = text,
            MaxSegmentLength = 15,
            GenerateTitles = true,
            EnableLlmSegmentation = false
        };

        // Act
        var result = await _service.SegmentTextAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Segments);
        Assert.All(result.Segments, segment => Assert.False(string.IsNullOrEmpty(segment.Title)));
    }

    [Fact]
    public async Task SegmentTextAsync_關閉標題生成_分段應該沒有標題()
    {
        // Arrange
        var text = "這是第一個句子。這是第二個句子。這是第三個句子。";
        var request = new SegmentationRequest 
        { 
            Text = text,
            MaxSegmentLength = 15,
            GenerateTitles = false,
            EnableLlmSegmentation = false
        };

        // Act
        var result = await _service.SegmentTextAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.Segments);
        // 由於沒有生成標題，標題應該是空字符串
        Assert.All(result.Segments, segment => Assert.True(string.IsNullOrEmpty(segment.Title)));
    }

    [Fact]
    public async Task SegmentTextAsync_LLM分段失敗_應該退回到標點符號分段()
    {
        // Arrange
        var text = "這是一個測試文本。";
        var request = new SegmentationRequest 
        { 
            Text = text,
            EnableLlmSegmentation = true
        };

        // 模擬 LLM 服務拋出異常
        _mockSummaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new Exception("LLM 服務錯誤"));

        // Act
        var result = await _service.SegmentTextAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Punctuation", result.SegmentationMethod);
    }
}