using System.ComponentModel.DataAnnotations;

namespace Summarizer.Configuration;

/// <summary>
/// OpenAI API 配置設定類別
/// </summary>
public class OpenAiConfig
{
    /// <summary>
    /// OpenAI API 金鑰
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// 使用的模型名稱
    /// </summary>
    [Required]
    public string Model { get; set; } = "gpt-3.5-turbo";
    
    /// <summary>
    /// API 端點 URL（可選，預設使用官方端點）
    /// </summary>
    public string? Endpoint { get; set; }
    
    /// <summary>
    /// API 呼叫超時時間（毫秒）
    /// </summary>
    [Range(1000, 300000)]
    public int Timeout { get; set; } = 30000;
    
    /// <summary>
    /// 重試次數
    /// </summary>
    [Range(0, 10)]
    public int RetryCount { get; set; } = 3;
    
    /// <summary>
    /// 重試延遲時間（毫秒）
    /// </summary>
    [Range(100, 10000)]
    public int RetryDelayMs { get; set; } = 1000;
    
    /// <summary>
    /// 最大輸入 token 數量
    /// </summary>
    [Range(100, 128000)]
    public int MaxTokens { get; set; } = 4000;
    
    /// <summary>
    /// 組織 ID（可選）
    /// </summary>
    public string? OrganizationId { get; set; }
}