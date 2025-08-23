//===============================================================
// 檔案：Tests/Repositories/SummaryRepositoryTests.cs
// 說明：SummaryRepository 的單元測試。
//===============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Summarizer.Data;
using Summarizer.Models;
using Summarizer.Repositories;
using Xunit;

namespace Tests.Repositories
{
    public class SummaryRepositoryTests : IDisposable
    {
        private readonly SummarizerDbContext _context;
        private readonly Mock<ILogger<SummaryRepository>> _mockLogger;
        private readonly SummaryRepository _repository;

        public SummaryRepositoryTests()
        {
            // 使用 In-Memory 資料庫進行測試
            var options = new DbContextOptionsBuilder<SummarizerDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _context = new SummarizerDbContext(options);
            _mockLogger = new Mock<ILogger<SummaryRepository>>();
            _repository = new SummaryRepository(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task CreateAsync_ValidRecord_ReturnsCreatedRecord()
        {
            // Arrange
            var record = new SummaryRecord
            {
                OriginalText = "測試原始文本",
                SummaryText = "測試摘要",
                CreatedAt = DateTime.UtcNow,
                UserId = "test-user",
                OriginalLength = 6,
                SummaryLength = 4,
                ProcessingTimeMs = 1000.0
            };

            // Act
            var result = await _repository.CreateAsync(record);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);
            Assert.Equal(record.OriginalText, result.OriginalText);
            Assert.Equal(record.SummaryText, result.SummaryText);
        }

        [Fact]
        public async Task GetByIdAsync_ExistingRecord_ReturnsRecord()
        {
            // Arrange
            var record = new SummaryRecord
            {
                OriginalText = "測試原始文本",
                SummaryText = "測試摘要",
                CreatedAt = DateTime.UtcNow,
                UserId = "test-user",
                OriginalLength = 6,
                SummaryLength = 4,
                ProcessingTimeMs = 1000.0
            };
            
            var createdRecord = await _repository.CreateAsync(record);

            // Act
            var result = await _repository.GetByIdAsync(createdRecord.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(createdRecord.Id, result.Id);
            Assert.Equal(record.OriginalText, result.OriginalText);
        }

        [Fact]
        public async Task GetByIdAsync_NonExistingRecord_ReturnsNull()
        {
            // Act
            var result = await _repository.GetByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetRecentAsync_WithRecords_ReturnsOrderedRecords()
        {
            // Arrange
            var records = new[]
            {
                new SummaryRecord
                {
                    OriginalText = "第一筆",
                    SummaryText = "摘要1",
                    CreatedAt = DateTime.UtcNow.AddHours(-2),
                    UserId = "user1",
                    OriginalLength = 3,
                    SummaryLength = 3,
                    ProcessingTimeMs = 1000.0
                },
                new SummaryRecord
                {
                    OriginalText = "第二筆",
                    SummaryText = "摘要2",
                    CreatedAt = DateTime.UtcNow.AddHours(-1),
                    UserId = "user2",
                    OriginalLength = 3,
                    SummaryLength = 3,
                    ProcessingTimeMs = 1000.0
                },
                new SummaryRecord
                {
                    OriginalText = "第三筆",
                    SummaryText = "摘要3",
                    CreatedAt = DateTime.UtcNow,
                    UserId = "user3",
                    OriginalLength = 3,
                    SummaryLength = 3,
                    ProcessingTimeMs = 1000.0
                }
            };

            foreach (var record in records)
            {
                await _repository.CreateAsync(record);
            }

            // Act
            var result = await _repository.GetRecentAsync(2);

            // Assert
            Assert.Equal(2, result.Count());
            var resultList = result.ToList();
            Assert.Equal("第三筆", resultList[0].OriginalText); // 最新的在前面
            Assert.Equal("第二筆", resultList[1].OriginalText);
        }

        [Fact]
        public async Task GetTotalCountAsync_WithRecords_ReturnsCorrectCount()
        {
            // Arrange
            var records = new[]
            {
                new SummaryRecord
                {
                    OriginalText = "記錄1",
                    SummaryText = "摘要1",
                    CreatedAt = DateTime.UtcNow,
                    UserId = "user1",
                    OriginalLength = 3,
                    SummaryLength = 3,
                    ProcessingTimeMs = 1000.0
                },
                new SummaryRecord
                {
                    OriginalText = "記錄2",
                    SummaryText = "摘要2",
                    CreatedAt = DateTime.UtcNow,
                    UserId = "user2",
                    OriginalLength = 3,
                    SummaryLength = 3,
                    ProcessingTimeMs = 1000.0
                }
            };

            foreach (var record in records)
            {
                await _repository.CreateAsync(record);
            }

            // Act
            var result = await _repository.GetTotalCountAsync();

            // Assert
            Assert.Equal(2, result);
        }

        [Fact]
        public async Task HealthCheckAsync_WithValidContext_ReturnsTrue()
        {
            // Act
            var result = await _repository.HealthCheckAsync();

            // Assert
            Assert.True(result);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}