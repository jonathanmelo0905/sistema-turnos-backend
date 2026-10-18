using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Dtos
{
    public class RegisterBusinessDto
    {
        // Business
        [Required(ErrorMessage = "Escribe el nombre de la empresa."), MaxLength(150)]
        public string BusinessName { get; set; } = null!;

        [Required(ErrorMessage = "Escribe el correo de la empresa."), EmailAddress(ErrorMessage = "El correo de la empresa no es válido."), MaxLength(150)]
        public string BusinessEmail { get; set; } = null!;

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        // Usuario Admin
        [Required(ErrorMessage = "Escribe tu nombre."), MaxLength(100)]
        public string AdminName { get; set; } = null!;

        [Required(ErrorMessage = "Escribe tu correo."), EmailAddress(ErrorMessage = "Tu correo no es válido."), MaxLength(150)]
        public string AdminEmail { get; set; } = null!;

        [Required(ErrorMessage = "Escribe una contraseña."), MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres."), MaxLength(100)]
        public string Password { get; set; } = null!;

        public bool AcceptTerms { get; set; }

        // Cloudflare Turnstile (T13): hoy no se valida
        public string? CaptchaToken { get; set; }
    }
}
