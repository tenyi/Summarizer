namespace Summarizer.Exceptions;

/// <summary>
/// 摘要服務例外基底類別
/// </summary>
public abstract class SummaryServiceException : Exception
{
    protected SummaryServiceException(string message) : base(message) { }
    protected SummaryServiceException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// API 連接例外
/// </summary>
public class ApiConnectionException : SummaryServiceException
{
    public ApiConnectionException() : base("API 連接失敗") { }
    public ApiConnectionException(string message) : base(message) { }
    public ApiConnectionException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// API 超時例外
/// </summary>
public class ApiTimeoutException : SummaryServiceException
{
    public ApiTimeoutException() : base("API 呼叫超時") { }
    public ApiTimeoutException(string message) : base(message) { }
    public ApiTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// API 服務不可用例外
/// </summary>
public class ApiServiceUnavailableException : SummaryServiceException
{
    public ApiServiceUnavailableException() : base("API 服務不可用") { }
    public ApiServiceUnavailableException(string message) : base(message) { }
    public ApiServiceUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// API 回應解析例外
/// </summary>
public class ApiResponseParsingException : SummaryServiceException
{
    public ApiResponseParsingException(string message) : base(message) { }
    public ApiResponseParsingException(string message, Exception innerException) : base(message, innerException) { }
}