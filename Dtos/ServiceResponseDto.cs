namespace AgendaApp.Dtos
{// DTO para mostrar un servicio (respuesta al cliente)
    public class ServiceResponseDto
    {
        public Guid ServiceId { get; set; }
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DurationMin { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }
    }
}
