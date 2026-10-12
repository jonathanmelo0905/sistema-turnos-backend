using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    // Aviso dentro del panel para una persona del equipo (la campanita).
    // La tabla "Notifications" del script original es para mensajes salientes al cliente (v2).
    public class StaffNotification
    {
        [Key]
        public Guid NotificationId { get; set; }

        public Guid BusinessId { get; set; }

        // Destinatario
        public Guid UserId { get; set; }

        [Required, MaxLength(40)]
        public string Type { get; set; } = null!;

        [Required, MaxLength(150)]
        public string Title { get; set; } = null!;

        [MaxLength(300)]
        public string? Body { get; set; }

        // Ruta del panel a la que lleva (ej. "turnos?fecha=2026-10-12")
        [MaxLength(200)]
        public string? Link { get; set; }

        public Guid? AppointmentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReadAt { get; set; }
    }

    public static class NotificationTypes
    {
        public const string WebBooking = "WebBooking";
        public const string ClientCancelled = "ClientCancelled";
        public const string OutOfHours = "OutOfHours";
        public const string TimeOffRequested = "TimeOffRequested";
        public const string TimeOffReviewed = "TimeOffReviewed";
        public const string Reassigned = "Reassigned";
        public const string NewReview = "NewReview";
        public const string Reminder = "Reminder";
    }
}
