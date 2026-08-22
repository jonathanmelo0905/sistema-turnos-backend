using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public class RegisterBusinessResultDto
    {
        public Guid BusinessId { get; set; }
        public Guid UserId { get; set; }
    }

    public class RefreshResultDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime RefreshTokenExpiration { get; set; }
    }

    public interface IAuthService
    {
        Task<ServiceResult<RegisterBusinessResultDto>> RegisterBusinessAsync(RegisterBusinessDto dto);
        Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto dto);
        Task<ServiceResult<RefreshResultDto>> RefreshTokenAsync(string refreshToken);
        Task<ServiceResult<bool>> LogoutAsync(string refreshToken);
    }
}
