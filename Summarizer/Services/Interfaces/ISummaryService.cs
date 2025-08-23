namespace Summarizer.Services.Interfaces;

/// <summary>
/// AI 摘要服務基礎介面
/// </summary>
public interface ISummaryService
{
    /// <summary>
    /// 生成文件摘要
    /// </summary>
    /// <param name="text">待摘要的文本</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>摘要結果</returns>
    Task<string> SummarizeAsync(string text, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 檢查服務健康狀態
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>服務是否健康</returns>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}