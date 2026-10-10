using AgendaApp.Authorization;
using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Controllers
{
    // Reseñas en el panel. Cada colaborador ve y responde las suyas; con stats.view (o el dueño)
    // se ven las de todo el equipo. Ocultar de la página pública: dueño o stats.view.
    [ApiController]
    [Route("api/reviews")]
    [Authorize]
    public class ReviewsController : ControllerBase
    {
        private readonly AgendaProDbContext _context;
        private readonly ITenantGuard _tenant;

        public ReviewsController(AgendaProDbContext context, ITenantGuard tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        private Task<bool> VeTodo() => _tenant.HasPermissionAsync(PermissionCodes.StatsView);

        // GET /api/reviews?from=&to=&userId=&rating=
        [HttpGet]
        public async Task<ActionResult<ReviewListDto>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? userId, [FromQuery] int? rating)
        {
            if (!await VeTodo()) userId = _tenant.UserId;

            var q = _context.Reviews.AsNoTracking().Where(r => r.BusinessId == _tenant.BusinessId);
            if (userId.HasValue) q = q.Where(r => r.UserId == userId.Value);
            if (from.HasValue) q = q.Where(r => r.CreatedAt >= from.Value.Date);
            if (to.HasValue) q = q.Where(r => r.CreatedAt < to.Value.Date.AddDays(1));

            var conteo = await q.GroupBy(r => r.Rating).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            var total = conteo.Sum(c => c.N);
            var dist = Enumerable.Range(1, 5).Select(i => conteo.FirstOrDefault(c => c.Key == i)?.N ?? 0).ToArray();

            if (rating.HasValue) q = q.Where(r => r.Rating == rating.Value);
            var items = await q.OrderByDescending(r => r.CreatedAt).Take(200)
                .Select(r => new ReviewDto
                {
                    ReviewId = r.ReviewId,
                    AppointmentId = r.AppointmentId,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt,
                    Reply = r.Reply,
                    RepliedAt = r.RepliedAt,
                    Hidden = r.Hidden,
                    ClientName = r.Client.Name,
                    UserId = r.UserId,
                    StaffName = r.Appointment.User.Name,
                    ServiceName = r.Appointment.Service.Name,
                    AppointmentDate = r.Appointment.DateTimeStart
                })
                .ToListAsync();

            return Ok(new ReviewListDto
            {
                Count = total,
                Average = total == 0 ? 0 : Math.Round(dist.Select((n, i) => n * (i + 1)).Sum() / (double)total, 2),
                Distribution = dist,
                Items = items
            });
        }

        // Responder (público): quien atendió o quien ve todo
        [HttpPost("{id:guid}/reply")]
        public async Task<IActionResult> Reply(Guid id, [FromBody] ReviewReplyDto dto)
        {
            var r = await _context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id && x.BusinessId == _tenant.BusinessId);
            if (r == null) return NotFound();
            if (r.UserId != _tenant.UserId && !await VeTodo()) return Forbid();

            r.Reply = string.IsNullOrWhiteSpace(dto.Reply) ? null : dto.Reply.Trim();
            r.RepliedAt = r.Reply == null ? null : DateTime.UtcNow;
            r.RepliedByUserId = r.Reply == null ? null : _tenant.UserId;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // Ocultar / mostrar en la página pública
        [HttpPost("{id:guid}/visibility")]
        public async Task<IActionResult> Visibility(Guid id, [FromQuery] bool hidden)
        {
            if (!await VeTodo()) return Forbid();
            var r = await _context.Reviews.FirstOrDefaultAsync(x => x.ReviewId == id && x.BusinessId == _tenant.BusinessId);
            if (r == null) return NotFound();
            r.Hidden = hidden;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
