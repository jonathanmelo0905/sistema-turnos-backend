namespace AgendaApp.Dtos
{
    public class UserResponseDto
    {
        public Guid UserId { get; set; }
        public Guid BusinessId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public string? PhotoUrl { get; set; }
        public string Role { get; set; } = null!;
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
