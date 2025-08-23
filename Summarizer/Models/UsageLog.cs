using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Summarizer.Models
{
    [Table("UsageLog")]
    public class UsageLog
    {
        /// <summary>
        /// 全域唯一識別碼，作為 API 呼叫記錄主鍵
        /// </summary>
        [Key]
        [Column("Uid")]
        public Guid Uid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 自動編號流水號（唯一約束，不作為主鍵）
        /// </summary>
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// 員工編號
        /// </summary>
        [Required]
        [StringLength(20)]
        public string EmpNo { get; set; } = string.Empty;

        /// <summary>
        /// 員工姓名
        /// </summary>
        [Required]
        [StringLength(10)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 部門編號
        /// </summary>
        [Required]
        public int DeptNo { get; set; }

        /// <summary>
        /// 部門名稱
        /// </summary>
        [Required]
        [StringLength(32)]
        public string Department { get; set; } = string.Empty;

        /// <summary>
        /// 使用者來源 IP 位址
        /// </summary>
        [Required]
        [StringLength(64)]
        public string IP { get; set; } = string.Empty;


        /// <summary>
        /// 使用者電腦/機器名稱
        /// </summary>
        [StringLength(64)]
        public string? Hostname { get; set; } = null;

        /// <summary>
        /// 呼叫時間，記錄 API 呼叫的時間
        /// </summary>
        [Required]
        public DateTime CallTime { get; set; } = DateTime.Now;

        /// <summary>
        /// API 名稱 (例如：Query、Download等)
        /// </summary>
        [Required]
        [StringLength(64)]
        public string APIName { get; set; } = string.Empty;

        /// <summary>
        /// 錯誤訊息 (若 API 呼叫失敗則記錄錯誤原因)
        /// </summary>
        [StringLength(512)]
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// 翻譯結果是否成功
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// 翻譯結果成功或失敗的訊息
        /// </summary>
        [StringLength(512)]
        public string? Result { get; set; } = null;

        /// <summary>
        /// 翻譯結果的總字數
        /// </summary>
        public int TotalChars { get; set; } = 0;

        /// <summary>
        /// 資料建立時間
        /// </summary>
        [Required]
        public DateTime CreatedTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 資料更新時間
        /// </summary>
        public DateTime? ResponseTime { get; set; } = null;

        /// <summary>
        /// 備註欄，可放置額外補充資訊
        /// </summary>
        [StringLength(512)]
        public string? Filename { get; set; } = null;
    }
}
