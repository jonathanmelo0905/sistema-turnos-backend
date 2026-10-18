namespace AgendaApp.Dtos
{
    public class BusinessResponseDto
    {
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? LogoUrl { get; set; }
        public bool IsPublished { get; set; }
        public string? CoverUrl { get; set; }
        public string? Address { get; set; }
        public string? Description { get; set; }
        public string? WhatsApp { get; set; }
        public string? Instagram { get; set; }
        public string? Facebook { get; set; }
        public string? TikTok { get; set; }
        public bool AllowOutOfHoursBooking { get; set; }
        public string? Slug { get; set; }
        public int CancelLimitHours { get; set; }
        public int MinAdvanceMinutes { get; set; }
        public int BufferMinutes { get; set; }
        public int MaxDaysAhead { get; set; }
        public bool ValleyOnReschedule { get; set; }
        public int PlanId { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<UserResponseDto> Users { get; set; } = new();
    }

    // Guía "Configura tu empresa" (Inicio del dueño): qué pasos ya están hechos
    public class BusinessSetupDto
    {
        public bool Datos { get; set; }          // teléfono o WhatsApp, dirección y descripción
        public bool Imagenes { get; set; }       // logo y portada
        public bool Horario { get; set; }        // al menos un día abierto
        public bool Categorias { get; set; }     // al menos una activa
        public bool Servicios { get; set; }      // al menos uno activo
        public bool Equipo { get; set; }         // alguien activo con servicios asignados
        public bool Publicada { get; set; }
    }
}
