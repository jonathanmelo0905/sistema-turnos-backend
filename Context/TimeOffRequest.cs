using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    // Ausencia de un colaborador (descanso, permiso, vacaciones, incapacidad).
    // Solo las aprobadas bloquean la disponibilidad. Una ausencia con turnos agendados
    // en su rango no se puede aprobar hasta que esos turnos se resuelvan.
    public class TimeOffRequest
    {
        [Key]
        public Guid TimeOffId { get; set; }

        [Required]
        public Guid BusinessId { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required, MaxLength(20)]
        public string Type { get; set; } = TimeOffTypes.Descanso;

        [Column(TypeName = "date")]
        public DateTime StartDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime EndDate { get; set; }

        // Solo para ausencias de un único día y parciales (ej. cita médica 14:00–16:00)
        [Column(TypeName = "varchar(8)")]
        public string? StartTime { get; set; }

        [Column(TypeName = "varchar(8)")]
        public string? EndTime { get; set; }

        [MaxLength(250)]
        public string? Reason { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = TimeOffStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Guid CreatedByUserId { get; set; }

        public Guid? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }

        [MaxLength(250)]
        public string? ReviewNote { get; set; }

        public User User { get; set; } = null!;

        public bool IsPartial => !string.IsNullOrWhiteSpace(StartTime) && !string.IsNullOrWhiteSpace(EndTime);
    }

    public static class TimeOffStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";
    }

    public static class TimeOffTypes
    {
        public const string Descanso = "Descanso";
        public const string Permiso = "Permiso";
        public const string Vacaciones = "Vacaciones";
        public const string Incapacidad = "Incapacidad";

        public static readonly IReadOnlyCollection<string> All = new[] { Descanso, Permiso, Vacaciones, Incapacidad };
    }

    // Estados de cita que ya no ocupan la agenda
    public static class AppointmentFreeStatus
    {
        public static readonly string[] All = { "Cancelled", "RejectedOutOfHours" };
    }
}
