using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Summarizer.Configuration;
using Summarizer.Models.TextSegmentation;
using Summarizer.Services.Interfaces;

namespace Summarizer.Services;

/// <summary>
/// 文本分段服務實作
/// </summary>
public class TextSegmentationService : ITextSegmentationService
{
    private readonly TextSegmentationConfig _config;
    private readonly ISummaryService _summaryService;
    private readonly ILogger<TextSegmentationService> _logger;

    /// <summary>
    /// 構造函式
    /// </summary>
    /// <param name="config">分段配置</param>
    /// <param name="summaryService">摘要服務（用於 LLM 分段）</param>
    /// <param name="logger">日誌記錄器</param>
    public TextSegmentationService(
        IOptions<TextSegmentationConfig> config,
        ISummaryService summaryService,
        ILogger<TextSegmentationService> logger)
    {
        _config = config.Value ?? throw new ArgumentNullException(nameof(config));
        _summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 檢查文本是否需要分段處理
    /// </summary>
    /// <param name="text">待檢查的文本</param>
    /// <returns>是否需要分段</returns>
    public bool ShouldSegmentText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // 使用配置的觸發門檻來判斷
        var shouldSegment = text.Length > _config.TriggerLength;
        
        if (_config.LogDetailedInfo)
        {
            _logger.LogInformation("文本長度檢測：{Length} 字符，觸發門檻：{Threshold} 字符，需要分段：{ShouldSegment}",
                text.Length, _config.TriggerLength, shouldSegment);
        }

        return shouldSegment;
    }

    /// <summary>
    /// 執行文本分段
    /// </summary>
    /// <param name="request">分段請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分段結果</returns>
    public async Task<SegmentationResponse> SegmentTextAsync(SegmentationRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new SegmentationResponse
        {
            OriginalLength = request.Text.Length
        };

        try
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                response.ErrorMessage = "文本內容不能為空";
                response.ErrorCode = "EMPTY_TEXT";
                return response;
            }

            _logger.LogInformation("開始文本分段處理，原始文本長度：{Length} 字符", request.Text.Length);

            // 首先嘗試使用標點符號分段
            var segments = SegmentByPunctuation(request.Text, request.MaxSegmentLength, request.PreserveParagraphs);
            var segmentationMethod = "Punctuation";

            // 檢查分段品質，如果不佳且啟用 LLM 分段，則使用 LLM
            var qualityResult = ValidateSegmentQuality(segments, request.Text);
            
            if (!qualityResult.IsQualityAcceptable && request.EnableLlmSegmentation && _config.EnableLlmSegmentation)
            {
                _logger.LogInformation("標點符號分段品質不佳（評分：{Score}），嘗試 LLM 輔助分段", qualityResult.OverallScore);
                
                try
                {
                    var llmSegments = await SegmentByLlmAsync(request.Text, request.MaxSegmentLength, cancellationToken);
                    var llmQuality = ValidateSegmentQuality(llmSegments, request.Text);
                    
                    // 如果 LLM 分段品質更好，則使用 LLM 結果
                    if (llmQuality.OverallScore > qualityResult.OverallScore)
                    {
                        segments = llmSegments;
                        segmentationMethod = "LLM";
                        qualityResult = llmQuality;
                        _logger.LogInformation("LLM 分段品質更佳（評分：{Score}），採用 LLM 分段結果", llmQuality.OverallScore);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LLM 分段失敗，將使用標點符號分段結果");
                }
            }

            // 生成分段標題（如果需要）
            if (request.GenerateTitles)
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    if (string.IsNullOrEmpty(segments[i].Title))
                    {
                        segments[i].Title = GenerateSegmentTitle(segments[i].Content, i);
                    }
                }
            }

            stopwatch.Stop();

            // 設定回應資料
            response.Success = true;
            response.Segments = segments;
            response.TotalSegments = segments.Count;
            response.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            response.SegmentationMethod = segmentationMethod;
            response.AverageSegmentLength = segments.Count > 0 ? segments.Average(s => s.CharacterCount) : 0;

            _logger.LogInformation("文本分段完成，分段數量：{Count}，處理時間：{ProcessingTime}ms，方法：{Method}",
                response.TotalSegments, response.ProcessingTimeMs, segmentationMethod);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "文本分段處理時發生錯誤");
            
            response.Success = false;
            response.ErrorMessage = "分段處理失敗：" + ex.Message;
            response.ErrorCode = "SEGMENTATION_ERROR";
            response.ProcessingTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            
            return response;
        }
    }

    /// <summary>
    /// 使用標點符號進行文本分段
    /// </summary>
    /// <param name="text">待分段文本</param>
    /// <param name="maxSegmentLength">最大分段長度</param>
    /// <param name="preserveParagraphs">是否保留段落邊界</param>
    /// <returns>分段結果列表</returns>
    public List<SegmentResult> SegmentByPunctuation(string text, int maxSegmentLength, bool preserveParagraphs = true)
    {
        var segments = new List<SegmentResult>();
        var normalizedText = NormalizeText(text);
        
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return segments;
        }

        _logger.LogDebug("開始標點符號分段，文本長度：{Length}，最大分段長度：{MaxLength}", 
            normalizedText.Length, maxSegmentLength);

        try
        {
            // 首先按段落邊界分割（如果啟用）
            var paragraphs = preserveParagraphs ? 
                SplitByParagraphs(normalizedText) : 
                new List<string> { normalizedText };

            int currentPosition = 0;
            int segmentIndex = 0;

            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                {
                    currentPosition += paragraph.Length;
                    continue;
                }

                // 如果段落長度在限制內，直接作為一個分段
                if (paragraph.Length <= maxSegmentLength)
                {
                    segments.Add(CreateSegmentResult(paragraph, segmentIndex++, currentPosition, 
                        currentPosition + paragraph.Length));
                    currentPosition += paragraph.Length;
                }
                else
                {
                    // 需要進一步分割的長段落
                    var subSegments = SplitLongParagraph(paragraph, maxSegmentLength);
                    int paragraphPosition = currentPosition;

                    foreach (var subSegment in subSegments)
                    {
                        segments.Add(CreateSegmentResult(subSegment, segmentIndex++, paragraphPosition, 
                            paragraphPosition + subSegment.Length));
                        paragraphPosition += subSegment.Length;
                    }
                    currentPosition = paragraphPosition;
                }
            }

            _logger.LogDebug("標點符號分段完成，生成 {Count} 個分段", segments.Count);
            return segments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "標點符號分段時發生錯誤");
            throw;
        }
    }

    /// <summary>
    /// 使用 LLM 進行智能文本分段
    /// </summary>
    /// <param name="text">待分段文本</param>
    /// <param name="maxSegmentLength">最大分段長度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分段結果列表</returns>
    public async Task<List<SegmentResult>> SegmentByLlmAsync(string text, int maxSegmentLength, CancellationToken cancellationToken = default)
    {
        var segments = new List<SegmentResult>();
        
        try
        {
            _logger.LogDebug("開始 LLM 智能分段，文本長度：{Length}，最大分段長度：{MaxLength}", 
                text.Length, maxSegmentLength);

            // 構建 LLM 提示詞
            var prompt = _config.LlmSegmentationPrompt
                .Replace("{maxLength}", maxSegmentLength.ToString())
                .Replace("{text}", text);

            // 調用 LLM 服務
            var llmResponse = await _summaryService.SummarizeAsync(prompt, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(llmResponse))
            {
                throw new InvalidOperationException("LLM 返回空響應");
            }

            // 解析 LLM 響應，分割分段
            var segmentTexts = llmResponse.Split(new[] { "---SEGMENT---" }, StringSplitOptions.RemoveEmptyEntries);
            
            int currentPosition = 0;
            
            for (int i = 0; i < segmentTexts.Length; i++)
            {
                var segmentText = segmentTexts[i].Trim();
                if (!string.IsNullOrEmpty(segmentText))
                {
                    segments.Add(CreateSegmentResult(segmentText, i, currentPosition, 
                        currentPosition + segmentText.Length));
                    currentPosition += segmentText.Length;
                }
            }

            _logger.LogDebug("LLM 智能分段完成，生成 {Count} 個分段", segments.Count);
            return segments;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM 智能分段時發生錯誤");
            throw;
        }
    }

    /// <summary>
    /// 為分段生成標題
    /// </summary>
    /// <param name="content">分段內容</param>
    /// <param name="segmentIndex">分段索引</param>
    /// <returns>生成的標題</returns>
    public string GenerateSegmentTitle(string content, int segmentIndex)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return $"分段 {segmentIndex + 1}";
        }

        try
        {
            // 取得內容的第一句作為標題基礎
            var firstSentence = GetFirstSentence(content);
            
            // 如果第一句太長，則截取前 30 個字符
            if (firstSentence.Length > 30)
            {
                firstSentence = firstSentence.Substring(0, 30) + "...";
            }
            
            return string.IsNullOrEmpty(firstSentence) ? $"分段 {segmentIndex + 1}" : firstSentence;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "生成分段標題時發生錯誤，分段索引：{Index}", segmentIndex);
            return $"分段 {segmentIndex + 1}";
        }
    }

    /// <summary>
    /// 驗證分段品質
    /// </summary>
    /// <param name="segments">分段結果列表</param>
    /// <param name="originalText">原始文本</param>
    /// <returns>品質評估結果</returns>
    public SegmentQualityResult ValidateSegmentQuality(List<SegmentResult> segments, string originalText)
    {
        var result = new SegmentQualityResult();
        
        try
        {
            if (segments == null || segments.Count == 0)
            {
                result.QualityIssues.Add("無分段結果");
                return result;
            }

            // 計算統計資訊
            var lengths = segments.Select(s => s.CharacterCount).ToList();
            result.Statistics = new SegmentStatistics
            {
                SegmentCount = segments.Count,
                TotalCharacters = lengths.Sum(),
                AverageSegmentLength = lengths.Average(),
                MaxSegmentLength = lengths.Max(),
                MinSegmentLength = lengths.Min(),
                LengthStandardDeviation = CalculateStandardDeviation(lengths)
            };

            // 語義完整性評分（檢查分段是否在句子中間切斷）
            result.SemanticIntegrityScore = CalculateSemanticIntegrityScore(segments);

            // 段落完整性評分（檢查是否保留了段落結構）
            result.ParagraphIntegrityScore = CalculateParagraphIntegrityScore(segments, originalText);

            // 長度分配均勻性評分
            result.LengthBalanceScore = CalculateLengthBalanceScore(lengths);

            // 計算整體評分
            result.OverallScore = (result.SemanticIntegrityScore + result.ParagraphIntegrityScore + result.LengthBalanceScore) / 3;

            // 判定品質是否可接受（整體評分 >= 70）
            result.IsQualityAcceptable = result.OverallScore >= 70;

            // 收集品質問題和建議
            CollectQualityIssuesAndRecommendations(result, segments);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證分段品質時發生錯誤");
            result.QualityIssues.Add($"品質評估失敗：{ex.Message}");
            return result;
        }
    }

    // 私有輔助方法

    /// <summary>
    /// 標準化文本（統一換行符、去除多餘空白）
    /// </summary>
    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // 統一換行符為 \n
        var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // 移除連續的空白行，但保留段落分隔
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");

        return normalized.Trim();
    }

    /// <summary>
    /// 按段落邊界分割文本
    /// </summary>
    private static List<string> SplitByParagraphs(string text)
    {
        // 按雙換行符分割段落
        return text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList();
    }

    /// <summary>
    /// 分割長段落
    /// </summary>
    private List<string> SplitLongParagraph(string paragraph, int maxSegmentLength)
    {
        var segments = new List<string>();
        var sentences = SplitIntoSentences(paragraph);
        var currentSegment = new StringBuilder();

        foreach (var sentence in sentences)
        {
            // 如果加入這個句子會超過限制
            if (currentSegment.Length + sentence.Length > maxSegmentLength)
            {
                // 如果當前分段不為空，先儲存它
                if (currentSegment.Length > 0)
                {
                    segments.Add(currentSegment.ToString().Trim());
                    currentSegment.Clear();
                }

                // 如果單個句子就超過限制，需要強制分割
                if (sentence.Length > maxSegmentLength)
                {
                    segments.AddRange(ForceSplitLongSentence(sentence, maxSegmentLength));
                }
                else
                {
                    currentSegment.Append(sentence);
                }
            }
            else
            {
                currentSegment.Append(sentence);
            }
        }

        // 加入最後的分段
        if (currentSegment.Length > 0)
        {
            segments.Add(currentSegment.ToString().Trim());
        }

        return segments;
    }

    /// <summary>
    /// 將文本分割成句子
    /// </summary>
    private List<string> SplitIntoSentences(string text)
    {
        var sentences = new List<string>();
        var endMarkers = _config.SentenceEndMarkers;
        var currentSentence = new StringBuilder();

        for (int i = 0; i < text.Length; i++)
        {
            currentSentence.Append(text[i]);

            // 檢查是否遇到句子結束符
            if (endMarkers.Contains(text[i].ToString()))
            {
                // 檢查下一個字符是否為空白或結尾
                if (i == text.Length - 1 || char.IsWhiteSpace(text[i + 1]))
                {
                    sentences.Add(currentSentence.ToString());
                    currentSentence.Clear();
                }
            }
        }

        // 加入最後的句子片段（如果有）
        if (currentSentence.Length > 0)
        {
            sentences.Add(currentSentence.ToString());
        }

        return sentences.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
    }

    /// <summary>
    /// 強制分割超長句子
    /// </summary>
    private static List<string> ForceSplitLongSentence(string sentence, int maxLength)
    {
        var segments = new List<string>();
        
        for (int i = 0; i < sentence.Length; i += maxLength)
        {
            var length = Math.Min(maxLength, sentence.Length - i);
            segments.Add(sentence.Substring(i, length));
        }

        return segments;
    }

    /// <summary>
    /// 創建分段結果物件
    /// </summary>
    private static SegmentResult CreateSegmentResult(string content, int index, int startPos, int endPos)
    {
        return new SegmentResult
        {
            SegmentIndex = index,
            Content = content.Trim(),
            CharacterCount = content.Trim().Length,
            StartPosition = startPos,
            EndPosition = endPos,
            Type = SegmentType.Paragraph,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 獲取內容的第一句話
    /// </summary>
    private string GetFirstSentence(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var sentences = SplitIntoSentences(content);
        return sentences.FirstOrDefault()?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// 計算標準差
    /// </summary>
    private static double CalculateStandardDeviation(List<int> values)
    {
        if (values.Count <= 1) return 0;

        var average = values.Average();
        var sumOfSquares = values.Sum(v => Math.Pow(v - average, 2));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    /// <summary>
    /// 計算語義完整性評分
    /// </summary>
    private double CalculateSemanticIntegrityScore(List<SegmentResult> segments)
    {
        // 簡單的語義完整性檢查：檢查分段是否以完整的句子結束
        var completeSegments = 0;
        var endMarkers = _config.SentenceEndMarkers;

        foreach (var segment in segments)
        {
            var trimmedContent = segment.Content.TrimEnd();
            if (!string.IsNullOrEmpty(trimmedContent) && 
                endMarkers.Any(marker => trimmedContent.EndsWith(marker)))
            {
                completeSegments++;
            }
        }

        return segments.Count > 0 ? (double)completeSegments / segments.Count * 100 : 0;
    }

    /// <summary>
    /// 計算段落完整性評分
    /// </summary>
    private static double CalculateParagraphIntegrityScore(List<SegmentResult> segments, string originalText)
    {
        // 檢查是否保持了原始文本的段落結構
        var originalParagraphs = originalText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        // 如果原始文本沒有明確的段落分割，給予基本評分
        if (originalParagraphs.Length <= 1)
        {
            return 80; // 基本評分
        }

        // 計算分段數與原始段落數的比例合理性
        var ratio = (double)segments.Count / originalParagraphs.Length;
        
        // 理想比例在 1-3 之間
        if (ratio >= 1 && ratio <= 3)
        {
            return 100;
        }
        else if (ratio < 1)
        {
            return Math.Max(50, 100 * ratio); // 分段太少
        }
        else
        {
            return Math.Max(30, 100 / (ratio - 2)); // 分段太多
        }
    }

    /// <summary>
    /// 計算長度分配均勻性評分
    /// </summary>
    private static double CalculateLengthBalanceScore(List<int> lengths)
    {
        if (lengths.Count <= 1) return 100;

        var average = lengths.Average();
        var standardDeviation = CalculateStandardDeviation(lengths);
        
        // 變異係數（標準差/平均值）
        var coefficientOfVariation = average > 0 ? standardDeviation / average : 1;
        
        // 變異係數越小，均勻性越好
        // 0.2 以下為優秀，0.5 以上為較差
        if (coefficientOfVariation <= 0.2)
        {
            return 100;
        }
        else if (coefficientOfVariation >= 0.5)
        {
            return 50;
        }
        else
        {
            return 100 - (coefficientOfVariation - 0.2) / 0.3 * 50;
        }
    }

    /// <summary>
    /// 收集品質問題和建議
    /// </summary>
    private static void CollectQualityIssuesAndRecommendations(SegmentQualityResult result, List<SegmentResult> segments)
    {
        // 檢查品質問題
        if (result.SemanticIntegrityScore < 70)
        {
            result.QualityIssues.Add("語義完整性不足，部分分段未以完整句子結束");
            result.Recommendations.Add("建議使用 LLM 輔助分段以提高語義完整性");
        }

        if (result.ParagraphIntegrityScore < 70)
        {
            result.QualityIssues.Add("段落完整性不足，分段數量與原始段落結構不匹配");
            result.Recommendations.Add("建議調整分段參數或啟用段落邊界保留");
        }

        if (result.LengthBalanceScore < 70)
        {
            result.QualityIssues.Add("分段長度分配不均勻");
            result.Recommendations.Add("建議調整最大分段長度參數");
        }

        if (result.Statistics.MaxSegmentLength > result.Statistics.AverageSegmentLength * 2)
        {
            result.QualityIssues.Add("存在過長的分段");
            result.Recommendations.Add("建議降低最大分段長度限制");
        }

        if (result.Statistics.MinSegmentLength < result.Statistics.AverageSegmentLength * 0.3)
        {
            result.QualityIssues.Add("存在過短的分段");
            result.Recommendations.Add("建議調整分段算法以避免產生過短的分段");
        }
    }
}