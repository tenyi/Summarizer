using Summarizer.Models.TextSegmentation;

namespace Summarizer.Services.Interfaces;

/// <summary>
/// 文本分段服務介面
/// </summary>
public interface ITextSegmentationService
{
    /// <summary>
    /// 檢查文本是否需要分段處理
    /// </summary>
    /// <param name="text">待檢查的文本</param>
    /// <returns>是否需要分段</returns>
    bool ShouldSegmentText(string text);

    /// <summary>
    /// 執行文本分段
    /// </summary>
    /// <param name="request">分段請求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分段結果</returns>
    Task<SegmentationResponse> SegmentTextAsync(SegmentationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用標點符號進行文本分段
    /// </summary>
    /// <param name="text">待分段文本</param>
    /// <param name="maxSegmentLength">最大分段長度</param>
    /// <param name="preserveParagraphs">是否保留段落邊界</param>
    /// <returns>分段結果列表</returns>
    List<SegmentResult> SegmentByPunctuation(string text, int maxSegmentLength, bool preserveParagraphs = true);

    /// <summary>
    /// 使用 LLM 進行智能文本分段
    /// </summary>
    /// <param name="text">待分段文本</param>
    /// <param name="maxSegmentLength">最大分段長度</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分段結果列表</returns>
    Task<List<SegmentResult>> SegmentByLlmAsync(string text, int maxSegmentLength, CancellationToken cancellationToken = default);

    /// <summary>
    /// 為分段生成標題
    /// </summary>
    /// <param name="content">分段內容</param>
    /// <param name="segmentIndex">分段索引</param>
    /// <returns>生成的標題</returns>
    string GenerateSegmentTitle(string content, int segmentIndex);

    /// <summary>
    /// 驗證分段品質
    /// </summary>
    /// <param name="segments">分段結果列表</param>
    /// <param name="originalText">原始文本</param>
    /// <returns>品質評估結果</returns>
    SegmentQualityResult ValidateSegmentQuality(List<SegmentResult> segments, string originalText);
}