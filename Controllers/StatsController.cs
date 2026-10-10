using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    // GET /api/stats?from=&to=&userId= — requiere "Ver estadísticas" (el dueño siempre)
    [ApiController]
    [Route("api/stats")]
    [Authorize(Policy = "stats.view")]
    public class StatsController : ControllerBase
    {
        private readonly IStatsService _stats;
        private readonly ITenantGuard _tenant;

        public StatsController(IStatsService stats, ITenantGuard tenant)
        {
            _stats = stats;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult<StatsDto>> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? userId)
        {
            if (userId.HasValue && !await _tenant.UserInBusinessAsync(userId.Value)) return NotFound();
            var hasta = to ?? DateTime.Today;
            var desde = from ?? hasta.AddDays(-29);
            return Ok(await _stats.GetAsync(_tenant.BusinessId, desde, hasta, userId));
        }
    }
}
