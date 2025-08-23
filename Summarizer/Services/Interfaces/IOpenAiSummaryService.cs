namespace Summarizer.Services.Interfaces;

/// <summary>
/// OpenAI 摘要服務介面
/// </summary>
public interface IOpenAiSummaryService : ISummaryService
{
    /// <summary>
    /// 測試與 OpenAI API 的連接
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>連接測試結果</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
}