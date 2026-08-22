namespace AgendaApp.Dtos
{
    // DTO para mostrar los servicios asignados a un usuario
    public class UserServiceResponseDto
    {
        public Guid UserId { get; set; }
        public Guid ServiceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int DurationMin { get; set; }
        public bool Active { get; set; }
    }
}
