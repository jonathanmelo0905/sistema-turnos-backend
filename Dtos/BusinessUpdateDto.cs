using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    // Perfil y políticas de la empresa (PUT /api/businesses/{id})
    public class BusinessUpdateDto
    {
        [Required, MinLength(3), MaxLength(150)]
        public string Name { get; set; } = null!;

        [Required, EmailAddress, MaxLength(150)]
        public string Email { get; set; } = null!;

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(30)]
        public string? WhatsApp { get; set; }

        [MaxLength(200)]
        public string? Instagram { get; set; }

        [MaxLength(200)]
        public string? Facebook { get; set; }

        [MaxLength(200)]
        public string? TikTok { get; set; }

        public bool AllowOutOfHoursBooking { get; set; }

        // Dirección pública (/{slug}); vacío = se conserva la actual
        [MaxLength(60)]
        public string? Slug { get; set; }

        [Range(0, 72)]
        public int CancelLimitHours { get; set; } = 2;
    }
}
