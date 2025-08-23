//===============================================================
// 檔案：Data/SummarizerDbContext.cs
// 說明：定義應用程式的資料庫上下文。
//===============================================================

using Microsoft.EntityFrameworkCore;
using Summarizer.Models;

namespace Summarizer.Data
{
    public class SummarizerDbContext : DbContext
    {
        public SummarizerDbContext(DbContextOptions<SummarizerDbContext> options)
            : base(options)
        {
        }

        public DbSet<SummaryRecord> SummaryRecords { get; set; }
    }
}
