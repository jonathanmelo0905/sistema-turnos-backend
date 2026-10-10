using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    // Bitácora de cada turno: creación, cambios de estado, reprogramaciones y cesiones.
    public class AppointmentHistory
    {
        [Key]
        public Guid HistoryId { get; set; }

        public Guid AppointmentId { get; set; }

        [Required, MaxLength(30)]
        public string Action { get; set; } = null!; // Created, Status, Rescheduled, Reassigned

        [MaxLength(30)]
        public string? FromStatus { get; set; }

        [MaxLength(30)]
        public string? ToStatus { get; set; }

        public Guid? ByUserId { get; set; }

        public DateTime At { get; set; } = DateTime.UtcNow;

        [MaxLength(300)]
        public string? Note { get; set; }

        public Appointment Appointment { get; set; } = null!;
    }
}
