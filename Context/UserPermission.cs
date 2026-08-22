using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    public class UserPermission
    {
        public Guid UserId { get; set; }
        public int PermissionId { get; set; }

        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public Guid GrantedByUserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [ForeignKey(nameof(PermissionId))]
        public Permission Permission { get; set; } = null!;

        [ForeignKey(nameof(GrantedByUserId))]
        public User GrantedByUser { get; set; } = null!;
    }
}
