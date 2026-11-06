using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class AppointmentDto
    {
        public Guid AppointmentId { get; set; }
        public Guid BusinessId { get; set; }
        public string? BusinessName { get; set; }
        public Guid ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? ClientPhone { get; set; }
        public string? ClientEmail { get; set; }
        public Guid UserId { get; set; }
        public string? UserName { get; set; }
        public Guid ServiceId { get; set; }
        public string? ServiceName { get; set; }
        public int DurationMin { get; set; }
        public decimal? Price { get; set; }
        public decimal? ListPrice { get; set; }
        public int? ValleyDiscountPct { get; set; }
        public DateTime DateTimeStart { get; set; }
        public DateTime DateTimeEnd { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Notes { get; set; }
        public string? CancelReason { get; set; }
        public string Source { get; set; } = "Mostrador";
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool NeedsContact { get; set; }
        public string? ContactReason { get; set; }
        public DateTime? ContactedAt { get; set; }
        public DateTime? ReminderSentAt { get; set; }
        public string? ReminderChannel { get; set; }
    }

    public class AppointmentReminderDto
    {
        // "WhatsApp" | "Email"
        public string Channel { get; set; } = null!;
    }

    public class AppointmentDetailDto : AppointmentDto
    {
        public List<AppointmentHistoryDto> History { get; set; } = new();
        public int ClientVisits { get; set; }
    }

    public class AppointmentHistoryDto
    {
        public string Action { get; set; } = null!;
        public string? FromStatus { get; set; }
        public string? ToStatus { get; set; }
        public string? ByName { get; set; }
        public DateTime At { get; set; }
        public string? Note { get; set; }
    }

    public class NewClientDto
    {
        [Required, MinLength(2), MaxLength(150)]
        public string Name { get; set; } = null!;

        [MaxLength(30)]
        public string? Phone { get; set; }

        [EmailAddress, MaxLength(150)]
        public string? Email { get; set; }
    }

    // Reserva desde el panel (mostrador). El fin se calcula con la duración del servicio.
    public class AppointmentBookDto
    {
        public Guid? ClientId { get; set; }
        public NewClientDto? NewClient { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [Required]
        public Guid ServiceId { get; set; }

        [Required]
        public DateTime Start { get; set; }

        [MaxLength(300)]
        public string? Notes { get; set; }
    }

    public class AppointmentRescheduleDto
    {
        [Required]
        public DateTime Start { get; set; }

        // Otro colaborador (ceder el turno). Vacío = el mismo.
        public Guid? UserId { get; set; }

        [MaxLength(250)]
        public string? Note { get; set; }
    }

    // Cambiar el servicio de un turno (el cliente llegó y pidió otro)
    public class ChangeServicePreviewRequestDto
    {
        [Required]
        public Guid ServiceId { get; set; }
    }

    public class AppointmentChangeServiceDto
    {
        [Required]
        public Guid ServiceId { get; set; }

        // Con Horas Valle en esa hora: true cobra con descuento, false el precio completo
        public bool ApplyValley { get; set; }

        // Si se pisa con los siguientes: "Overlap" (quedan solapados) o "Shift" (se corren)
        [MaxLength(10)]
        public string? Overlap { get; set; }

        // Aviso a los clientes de los turnos corridos: "Email" o "WhatsApp"
        [MaxLength(10)]
        public string? Notify { get; set; }

        [MaxLength(200)]
        public string? Note { get; set; }
    }

    public class ChangeServiceTurnoDto
    {
        public Guid AppointmentId { get; set; }
        public string ClientName { get; set; } = "";
        public string ServiceName { get; set; } = "";
        public DateTime OldStart { get; set; }
        public DateTime OldEnd { get; set; }
        public DateTime NewStart { get; set; }
        public DateTime NewEnd { get; set; }
        public bool OutOfSchedule { get; set; }
        public bool HasEmail { get; set; }
    }

    public class ChangeServicePreviewDto
    {
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime NewEnd { get; set; }
        public int DurationMin { get; set; }
        public int OldDurationMin { get; set; }
        public decimal ListPrice { get; set; }
        public decimal? OldPrice { get; set; }
        public int? ValleyPct { get; set; }
        public decimal? ValleyPrice { get; set; }
        public bool EndsOutOfSchedule { get; set; }
        // Turnos con los que se pisa el nuevo horario
        public List<ChangeServiceTurnoDto> Conflicts { get; set; } = new();
        // Plan para correr los siguientes (en cascada)
        public List<ChangeServiceTurnoDto> Shift { get; set; } = new();
        public int ShiftMinutes { get; set; }
    }

    public class AppointmentStatusDto
    {
        [Required, MaxLength(30)]
        public string Status { get; set; } = null!;

        [MaxLength(250)]
        public string? Note { get; set; }
    }
}
