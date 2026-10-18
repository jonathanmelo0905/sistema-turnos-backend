using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    // Empresa vista desde el panel de plataforma (operador)
    public class PlatformBusinessDto
    {
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Slug { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime? TermsAcceptedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? RejectionReason { get; set; }
        public string? OwnerName { get; set; }
        public string? OwnerEmail { get; set; }
        public string? OwnerPhone { get; set; }
        public int UsersCount { get; set; }
        public bool IsPublished { get; set; }
    }

    public class PlatformRejectDto
    {
        [Required(ErrorMessage = "Escribe el motivo del rechazo.")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "El motivo debe tener entre 5 y 500 caracteres.")]
        public string Reason { get; set; } = null!;
    }

    public class PasswordResetSentDto
    {
        public string Email { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        // Solo en Development, para los scripts de prueba (en producción el enlace solo va por correo)
        public string? DevLink { get; set; }
    }

    public class PasswordResetCheckDto
    {
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
    }

    public class PasswordResetTokenDto
    {
        [Required]
        public string Token { get; set; } = null!;
    }

    public class PasswordResetDto
    {
        [Required]
        public string Token { get; set; } = null!;

        [Required, MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        [MaxLength(100)]
        public string Password { get; set; } = null!;
    }
}
