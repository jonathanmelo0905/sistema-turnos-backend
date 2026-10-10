using AgendaApp.Authorization;
using AgendaApp.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Controllers
{
    // La campanita del panel: avisos de la persona que inició sesión.
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private readonly AgendaProDbContext _context;
        private readonly ITenantGuard _tenant;

        public NotificationsController(AgendaProDbContext context, ITenantGuard tenant)
        {
            _context = context;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int take = 30)
        {
            var mias = _context.StaffNotifications.AsNoTracking().Where(n => n.UserId == _tenant.UserId);
            var items = await mias.OrderByDescending(n => n.CreatedAt).Take(Math.Clamp(take, 1, 100))
                .Select(n => new { n.NotificationId, n.Type, n.Title, n.Body, n.Link, n.AppointmentId, n.CreatedAt, n.ReadAt })
                .ToListAsync();
            var unread = await mias.CountAsync(n => n.ReadAt == null);
            return Ok(new { unread, items });
        }

        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> Read(Guid id)
        {
            var n = await _context.StaffNotifications.FirstOrDefaultAsync(x => x.NotificationId == id && x.UserId == _tenant.UserId);
            if (n == null) return NotFound();
            n.ReadAt ??= DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> ReadAll()
        {
            var ahora = DateTime.UtcNow;
            var pendientes = await _context.StaffNotifications.Where(n => n.UserId == _tenant.UserId && n.ReadAt == null).ToListAsync();
            pendientes.ForEach(n => n.ReadAt = ahora);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
