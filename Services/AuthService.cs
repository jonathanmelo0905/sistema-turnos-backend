using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AgendaApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly AgendaProDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthService> _logger;

        public AuthService(AgendaProDbContext context, IConfiguration config, ILogger<AuthService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public async Task<ServiceResult<RegisterBusinessResultDto>> RegisterBusinessAsync(RegisterBusinessDto dto)
        {
            if (_context.Businesses.Any(b => b.Email == dto.BusinessEmail))
                return ServiceResult<RegisterBusinessResultDto>.BadRequest("Ya existe un negocio con ese email.");

            if (_context.Users.Any(u => u.Email == dto.AdminEmail))
                return ServiceResult<RegisterBusinessResultDto>.BadRequest("Ya existe un usuario con ese email.");

            var business = new Business
            {
                BusinessId = Guid.NewGuid(),
                Name = dto.BusinessName,
                Email = dto.BusinessEmail,
                Phone = dto.Phone,
                Address = dto.Address,
                PlanId = 1,
                CreatedAt = DateTime.UtcNow
            };

            var user = new User
            {
                UserId = Guid.NewGuid(),
                BusinessId = business.BusinessId,
                Name = dto.AdminName,
                Email = dto.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = Roles.Owner,
                CreatedAt = DateTime.UtcNow,
                Business = business
            };

            _context.Businesses.Add(business);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Negocio {BusinessId} y usuario admin {UserId} registrados.", business.BusinessId, user.UserId);

            return ServiceResult<RegisterBusinessResultDto>.Ok(new RegisterBusinessResultDto
            {
                BusinessId = business.BusinessId,
                UserId = user.UserId
            });
        }

        public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            {
                _logger.LogWarning("Intento de login fallido para el email {Email}.", dto.Email);
                return ServiceResult<AuthResponseDto>.Unauthorized("Email o contraseña incorrectos");
            }

            if (!user.Active)
            {
                _logger.LogWarning("Intento de login de usuario inactivo {UserId}.", user.UserId);
                return ServiceResult<AuthResponseDto>.Unauthorized("Tu usuario está inactivo. Habla con el administrador de tu empresa.");
            }

            var token = GenerateJwtToken(user);
            var refreshToken = await CreateRefreshTokenAsync(user);

            _logger.LogInformation("Login exitoso para usuario {UserId}.", user.UserId);

            return ServiceResult<AuthResponseDto>.Ok(new AuthResponseDto
            {
                Token = token,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                BusinessId = user.BusinessId,
                UserId = user.UserId,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiration = refreshToken.Expiration
            });
        }

        public async Task<ServiceResult<RefreshResultDto>> RefreshTokenAsync(string refreshToken)
        {
            var existingToken = await _context.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.Revoked);

            if (existingToken == null || existingToken.Expiration < DateTime.UtcNow || !existingToken.User.Active)
            {
                _logger.LogWarning("Intento de refresh con token inválido o expirado.");
                return ServiceResult<RefreshResultDto>.Unauthorized("Refresh token inválido o expirado.");
            }

            var newJwt = GenerateJwtToken(existingToken.User);
            var newRefresh = await CreateRefreshTokenAsync(existingToken.User);

            existingToken.Revoked = true;
            _context.RefreshTokens.Update(existingToken);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token rotado para usuario {UserId}.", existingToken.UserId);

            return ServiceResult<RefreshResultDto>.Ok(new RefreshResultDto
            {
                Token = newJwt,
                RefreshToken = newRefresh.Token,
                RefreshTokenExpiration = newRefresh.Expiration
            });
        }

        public async Task<ServiceResult<bool>> LogoutAsync(string refreshToken)
        {
            var token = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == refreshToken);
            if (token == null)
                return ServiceResult.NotFound("Refresh token no encontrado.");

            token.Revoked = true;
            _context.RefreshTokens.Update(token);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Logout: refresh token revocado para usuario {UserId}.", token.UserId);
            return ServiceResult.Ok();
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("BusinessId", user.BusinessId.ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<RefreshToken> CreateRefreshTokenAsync(User user)
        {
            var refresh = new RefreshToken
            {
                TokenId = Guid.NewGuid(),
                UserId = user.UserId,
                Token = Guid.NewGuid().ToString(),
                Expiration = DateTime.UtcNow.AddDays(7),
                Revoked = false
            };

            _context.RefreshTokens.Add(refresh);
            await _context.SaveChangesAsync();

            return refresh;
        }
    }
}
