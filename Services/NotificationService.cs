using AgendaApp.Context;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public interface INotificationService
    {
        // Agrega los avisos al contexto; se guardan con el SaveChanges de quien llama
        Task NotifyAsync(Guid businessId, IEnumerable<Guid> userIds, string type, string title, string? body, string? link, Guid? appointmentId = null, Guid? exceptUserId = null);

        Task<List<Guid>> OwnersAsync(Guid businessId);
        Task<List<Guid>> WithPermissionAsync(Guid businessId, string permissionCode);
    }

    public class NotificationService : INotificationService
    {
        private readonly AgendaProDbContext _context;

        public NotificationService(AgendaProDbContext context)
        {
            _context = context;
        }

        public Task NotifyAsync(Guid businessId, IEnumerable<Guid> userIds, string type, string title, string? body, string? link, Guid? appointmentId = null, Guid? exceptUserId = null)
        {
            foreach (var userId in userIds.Distinct().Where(u => u != Guid.Empty && u != exceptUserId))
            {
                _context.StaffNotifications.Add(new StaffNotification
                {
                    NotificationId = Guid.NewGuid(),
                    BusinessId = businessId,
                    UserId = userId,
                    Type = type,
                    Title = Cut(title, 150)!,
                    Body = Cut(body, 300),
                    Link = Cut(link, 200),
                    AppointmentId = appointmentId,
                    CreatedAt = DateTime.UtcNow
                });
            }
            return Task.CompletedTask;
        }

        public Task<List<Guid>> OwnersAsync(Guid businessId) =>
            _context.Users.Where(u => u.BusinessId == businessId && u.Active && u.Role == Roles.Owner)
                .Select(u => u.UserId).ToListAsync();

        // Dueños + quienes tienen el permiso
        public Task<List<Guid>> WithPermissionAsync(Guid businessId, string permissionCode) =>
            _context.Users.Where(u => u.BusinessId == businessId && u.Active &&
                    (u.Role == Roles.Owner || u.UserPermissions.Any(p => p.Permission.Code == permissionCode)))
                .Select(u => u.UserId).ToListAsync();

        private static string? Cut(string? s, int max) => s == null ? null : (s.Length <= max ? s : s[..(max - 1)] + "…");
    }
}
