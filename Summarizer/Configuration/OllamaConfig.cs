using System.ComponentModel.DataAnnotations;

namespace Summarizer.Configuration;

/// <summary>
/// Ollama API 配置設定類別
/// </summary>
public class OllamaConfig
{
    /// <summary>
    /// Ollama API 端點 URL
    /// </summary>
    [Required]
    public string Endpoint { get; set; } = string.Empty;
    
    /// <summary>
    /// 使用的模型名稱
    /// </summary>
    [Required]
    public string Model { get; set; } = string.Empty;
    
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
    /// API 金鑰（可選）
    /// </summary>
    public string? ApiKey { get; set; }
    
    /// <summary>
    /// 是否啟用 SSL 憑證驗證
    /// </summary>
    public bool ValidateSslCertificate { get; set; } = true;
}