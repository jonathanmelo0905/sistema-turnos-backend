namespace AgendaApp.Dtos
{
    public class BusinessResponseDto
    {
        public Guid BusinessId { get; set; }

        public string Name { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public int PlanId { get; set; }

        public DateTime CreatedAt { get; set; }

        // 👇 Opción 1: Mostrar resumen de los usuarios asociados (solo datos básicos)
        public List<UserResponseDto> Users { get; set; } = new();
        
    }
}
