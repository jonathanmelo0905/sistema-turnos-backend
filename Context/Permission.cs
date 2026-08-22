using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    public class Permission
    {
        [Key]
        public int PermissionId { get; set; }

        [Required, MaxLength(60)]
        public string Code { get; set; } = null!;

        [Required, MaxLength(200)]
        public string Description { get; set; } = null!;

        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }
}
