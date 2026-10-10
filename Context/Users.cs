using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    public class User
    {
        [Key]
        public Guid UserId { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(150)]
        public string Email { get; set; } = null!;

        [Required, MaxLength(250)]
        public string PasswordHash { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Role { get; set; } = Roles.Owner;

        [MaxLength(30)]
        public string? Phone { get; set; }

        // Borrado lógico: un usuario inactivo no puede iniciar sesión ni recibir turnos.
        public bool Active { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 🔗 Relaciones
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<UserService> UserServices { get; set; } = new List<UserService>();
        public ICollection<UserSchedule> UserSchedules { get; set; } = new List<UserSchedule>();
        public ICollection<UserScheduleException> UserScheduleExceptions { get; set; } = new List<UserScheduleException>();
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }
}
