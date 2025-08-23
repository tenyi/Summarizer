//===============================================================
// 檔案：Controllers/HistoryController.cs
// 說明：提供查詢摘要記錄的功能。
//===============================================================

using Microsoft.AspNetCore.Mvc;
using Summarizer.Repositories.Interfaces;

namespace Summarizer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HistoryController : ControllerBase
    {
        private readonly ISummaryRepository _summaryRepository;

        public HistoryController(ISummaryRepository summaryRepository)
        {
            _summaryRepository = summaryRepository;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var records = await _summaryRepository.GetRecentAsync();
            return Ok(records);
        }
    }
}
