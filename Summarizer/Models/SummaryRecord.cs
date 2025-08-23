//===============================================================
// 檔案：Models/SummaryRecord.cs
// 說明：定義摘要記錄的資料模型。
//===============================================================

using System.ComponentModel.DataAnnotations;

namespace Summarizer.Models
{
    public class SummaryRecord
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "原始文本不能為空")]
        [StringLength(50000, ErrorMessage = "原始文本長度不能超過 50,000 字元")]
        public string OriginalText { get; set; } = string.Empty;

        [StringLength(10000, ErrorMessage = "摘要文本長度不能超過 10,000 字元")]  
        public string SummaryText { get; set; } = string.Empty;

        [Required(ErrorMessage = "建立時間為必填項目")]
        public DateTime CreatedAt { get; set; }

        [StringLength(100, ErrorMessage = "使用者 ID 長度不能超過 100 字元")]
        public string? UserId { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "原始文本長度必須為非負數")]
        public int OriginalLength { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "摘要文本長度必須為非負數")]
        public int SummaryLength { get; set; }

        [Range(0.0, double.MaxValue, ErrorMessage = "處理時間必須為非負數")]
        public double ProcessingTimeMs { get; set; }

        [StringLength(1000, ErrorMessage = "錯誤訊息長度不能超過 1,000 字元")]
        public string? ErrorMessage { get; set; }
    }
}
