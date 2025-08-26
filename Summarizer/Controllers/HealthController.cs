//===============================================================
// 檔案：HealthController.cs
// 說明：提供應用程式健康狀況檢查的 API 端點。
//===============================================================

using Microsoft.AspNetCore.Mvc;

namespace Summarizer.Controllers
{
    /// <summary>
    /// 健康檢查控制器，提供一個簡單的 API 端點來確認應用程式是否正在運行。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        /// <summary>
        /// 執行一個簡單的健康檢查並返回應用程式狀態。
        /// </summary>
        /// <returns>一個包含應用程式狀態訊息的 JSON 物件。</returns>
        [HttpGet]
        public IActionResult Get()
        {
            // 返回一個包含健康狀態的物件，HTTP 狀態碼為 200 OK。
            return Ok(new { status = "healthy", time = DateTime.UtcNow });
        }
    }
}
