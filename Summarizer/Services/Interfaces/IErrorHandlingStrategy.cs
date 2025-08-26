using System.Threading.Tasks;
using Summarizer.Models.BatchProcessing;

namespace Summarizer.Services.Interfaces
{
    /// <summary>
    /// 錯誤處理策略介面
    /// 定義不同類型錯誤處理策略的通用契約
    /// </summary>
    public interface IErrorHandlingStrategy
    {
        /// <summary>
        /// 策略類型
        /// </summary>
        ErrorHandlingStrategy StrategyType { get; }

        /// <summary>
        /// 執行錯誤處理策略
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>處理結果</returns>
        Task<ErrorHandlingResult> ExecuteAsync(ProcessingError error);

        /// <summary>
        /// 判斷此策略是否適用於指定的錯誤
        /// </summary>
        /// <param name="error">錯誤資訊</param>
        /// <returns>是否適用</returns>
        bool CanHandle(ProcessingError error);
    }
}