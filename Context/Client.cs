using System.ComponentModel.DataAnnotations.Schema;

namespace AgendaApp.Context
{
    public class Client
    {
        public Guid ClientId { get; set; }
        public Guid BusinessId { get; set; }
        public string? Name { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Notes { get; set; }

        // Código privado con el que el navegador del cliente "lo recuerda" (sin cuenta)
        [System.ComponentModel.DataAnnotations.MaxLength(64)]
        public string? AccessToken { get; set; }

        public DateTime? CreatedAt { get; set; }

        // Código de acceso por correo: solo el hash; vence a los 10 min y admite 5 intentos
        [System.ComponentModel.DataAnnotations.MaxLength(64)]
        public string? AccessCodeHash { get; set; }
        public DateTime? AccessCodeExpiresAt { get; set; }
        public int AccessCodeAttempts { get; set; }

        // Última vez que aceptó términos y privacidad al reservar por la web
        public DateTime? TermsAcceptedAt { get; set; }


        // 👇 Relación uno-a-muchos con Appointment
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        // 🔗 Relaciones
        [ForeignKey("BusinessId")]
        public Business Business { get; set; } = null!;
    }
}
