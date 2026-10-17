using System.ComponentModel.DataAnnotations;

namespace AgendaApp.Context
{
    public class Business
    {
        [Key]
        public Guid BusinessId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(150)]
        public string Email { get; set; } = null!;

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(250)]
        public string? Address { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        // Dirección pública de la página de reservas: /{Slug}
        [MaxLength(80)]
        public string? Slug { get; set; }

        // Horas mínimas de anticipación para que el cliente cancele en línea
        public int CancelLimitHours { get; set; } = 2;

        // Políticas de reserva (Fase 8). Antelación y días adelante solo limitan la reserva pública;
        // el margen entre turnos aplica siempre (panel y web).
        public int MinAdvanceMinutes { get; set; } = 30;

        public int BufferMinutes { get; set; } = 0;

        public int MaxDaysAhead { get; set; } = 8;

        // Al reprogramar: true = se recotiza con Horas Valle; false = precio de lista
        public bool ValleyOnReschedule { get; set; } = false;

        // Redes y contacto público (se muestran en la página de reservas del cliente)
        [MaxLength(30)]
        public string? WhatsApp { get; set; }

        [MaxLength(200)]
        public string? Instagram { get; set; }

        [MaxLength(200)]
        public string? Facebook { get; set; }

        [MaxLength(200)]
        public string? TikTok { get; set; }

        // Imágenes (URL de IAlmacenArchivos); null = sin imagen, se usa la inicial / el degradado.
        [MaxLength(300)]
        public string? LogoUrl { get; set; }

        [MaxLength(300)]
        public string? CoverUrl { get; set; }

        [Required]
        public int PlanId { get; set; } = 1;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Política de flexibilidad (CU-05): permite crear citas fuera del horario declarado
        // del colaborador en estado "PendingOutOfHours", sujetas a aprobación.
        public bool AllowOutOfHoursBooking { get; set; } = false;

        // 🔗 Relación con Users
        public ICollection<User> Users { get; set; } = new List<User>();


        // 🔗 Relaciones
        //public ICollection<User> Users { get; set; } = new List<User>();
        public ICollection<Client> Clients { get; set; } = new List<Client>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<BusinessSchedule> BusinessSchedules { get; set; } = new List<BusinessSchedule>();
    }
}
