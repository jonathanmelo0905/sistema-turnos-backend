using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    // ---- Página pública de la empresa ----
    public class PublicBusinessDto
    {
        public Guid BusinessId { get; set; }
        public string Slug { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? WhatsApp { get; set; }
        public string? Instagram { get; set; }
        public string? Facebook { get; set; }
        public string? TikTok { get; set; }
        public int CancelLimitHours { get; set; }
        public List<BusinessScheduleDayDto> Schedule { get; set; } = new();
        public List<PublicCategoryDto> Categories { get; set; } = new();
        public List<PublicServiceDto> Services { get; set; } = new();
        public List<PublicStaffDto> Staff { get; set; } = new();
    }

    public class PublicCategoryDto
    {
        public Guid CategoryId { get; set; }
        public string Name { get; set; } = null!;
    }

    public class PublicServiceDto
    {
        public Guid ServiceId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public int DurationMin { get; set; }
        public decimal Price { get; set; }
        public Guid? CategoryId { get; set; }
        public List<Guid> StaffIds { get; set; } = new();
    }

    public class PublicStaffDto
    {
        public Guid UserId { get; set; }
        public string Name { get; set; } = null!;
        public List<Guid> ServiceIds { get; set; } = new();
    }

    public class PublicBusinessListItemDto
    {
        public string Slug { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public int ServiceCount { get; set; }
    }

    // ---- Reserva como invitado ----
    public class PublicClientDto
    {
        [Required, MinLength(2), MaxLength(150)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(30)]
        public string Phone { get; set; } = null!;

        [EmailAddress, MaxLength(150)]
        public string? Email { get; set; }
    }

    public class PublicBookDto
    {
        [Required]
        public Guid ServiceId { get; set; }

        // Vacío = "cualquiera": se asigna quien tenga libre esa hora
        public Guid? UserId { get; set; }

        [Required]
        public DateTime Start { get; set; }

        [Required]
        public PublicClientDto Client { get; set; } = null!;

        [MaxLength(300)]
        public string? Notes { get; set; }

        // Si el cliente ya fue recordado por este navegador
        public string? AccessToken { get; set; }
    }

    public class PublicAppointmentDto
    {
        public Guid AppointmentId { get; set; }
        public string Code { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public decimal? Price { get; set; }
        public Guid UserId { get; set; }
        public string StaffName { get; set; } = null!;
        public bool CanCancel { get; set; }
        public string? CancelReason { get; set; }
    }

    public class PublicBookResultDto
    {
        public PublicAppointmentDto Appointment { get; set; } = null!;
        public string AccessToken { get; set; } = null!;
        public string ClientName { get; set; } = null!;
    }

    // ---- Mis turnos ----
    public class PublicLookupDto
    {
        [Required, MaxLength(150)]
        public string Contact { get; set; } = null!;   // correo o teléfono

        [Required, MinLength(6), MaxLength(6)]
        public string Code { get; set; } = null!;      // código de cualquiera de sus reservas
    }

    public class PublicSuggestionDto
    {
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = null!;
        public Guid UserId { get; set; }
        public string StaffName { get; set; } = null!;
        public DateTime? NextSlot { get; set; }
    }

    public class PublicMyAppointmentsDto
    {
        public string ClientName { get; set; } = null!;
        public string AccessToken { get; set; } = null!;
        public List<PublicAppointmentDto> Upcoming { get; set; } = new();
        public List<PublicAppointmentDto> Past { get; set; } = new();
        public PublicSuggestionDto? Suggestion { get; set; }
    }

    public class PublicCancelDto
    {
        [Required]
        public string AccessToken { get; set; } = null!;

        [MaxLength(250)]
        public string? Reason { get; set; }
    }
}
