namespace AgendaApp.Dtos
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
        public required string RefreshToken { get; set; }
        public Guid BusinessId { get; set; }
        public Guid UserId { get; set; }
        public DateTime RefreshTokenExpiration { get; set; }
    }
}
