//===============================================================
// 檔案：Data/SummarizerDbContext.cs
// 說明：定義應用程式的資料庫上下文。
//===============================================================

using Microsoft.EntityFrameworkCore;
using Summarizer.Models;
using Summarizer.Models.BatchProcessing;
using System.Text.Json;

namespace Summarizer.Data
{
    public class SummarizerDbContext : DbContext
    {
        public SummarizerDbContext(DbContextOptions<SummarizerDbContext> options)
            : base(options)
        {
        }

        public DbSet<SummaryRecord> SummaryRecords { get; set; }
        public DbSet<PartialResult> PartialResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置 PartialResult 表格
            modelBuilder.Entity<PartialResult>(entity =>
            {
                entity.HasKey(e => e.PartialResultId);
                
                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasMaxLength(256);

                entity.Property(e => e.PartialSummary)
                    .IsRequired()
                    .HasMaxLength(50000);

                entity.Property(e => e.OriginalTextSample)
                    .HasMaxLength(5000);

                entity.Property(e => e.UserComment)
                    .HasMaxLength(2000);

                // 將複雜物件序列化為 JSON
                entity.Property(e => e.CompletedSegments)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<SegmentSummaryTask>>(v, (JsonSerializerOptions?)null) ?? new List<SegmentSummaryTask>());

                entity.Property(e => e.Quality)
                    .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<PartialResultQuality>(v, (JsonSerializerOptions?)null) ?? new PartialResultQuality());

                // 索引設定
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.BatchId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CancellationTime);
            });
        }
    }
}
