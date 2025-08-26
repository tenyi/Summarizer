namespace Summarizer.Models.Responses;

/// <summary>
/// 基礎 API 回應資料模型
/// </summary>
public class ApiResponse
{
    /// <summary>
    /// 請求是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 錯誤訊息（如果失敗）
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// 錯誤代碼（如果失敗）
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// 時間戳記
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 通用 API 回應資料模型
/// </summary>
/// <typeparam name="T">資料類型</typeparam>
public class ApiResponse<T> : ApiResponse
{
    /// <summary>
    /// 回應資料
    /// </summary>
    public T? Data { get; set; }
}