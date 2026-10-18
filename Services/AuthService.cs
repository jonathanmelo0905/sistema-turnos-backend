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
        private readonly IEmailService _email;

        public AuthService(AgendaProDbContext context, IConfiguration config, ILogger<AuthService> logger, IEmailService email)
        {
            _context = context;
            _config = config;
            _logger = logger;
            _email = email;
        }

        // Registro desde /registro: la empresa queda Pending hasta que el operador la apruebe (T9)
        public async Task<ServiceResult<RegisterBusinessResultDto>> RegisterBusinessAsync(RegisterBusinessDto dto)
        {
            if (!dto.AcceptTerms)
                return ServiceResult<RegisterBusinessResultDto>.BadRequest("Acepta los términos y la política de privacidad para enviar la solicitud.");

            var correoEmpresa = dto.BusinessEmail.Trim().ToLowerInvariant();
            var correoDueno = dto.AdminEmail.Trim().ToLowerInvariant();

            if (await _context.Businesses.AnyAsync(b => b.Email == correoEmpresa))
                return ServiceResult<RegisterBusinessResultDto>.Conflict("Ya hay una empresa registrada con ese correo. Si es la tuya, inicia sesión.");

            if (await _context.Users.AnyAsync(u => u.Email == correoDueno))
                return ServiceResult<RegisterBusinessResultDto>.Conflict("Ya hay una cuenta con tu correo. Inicia sesión o usa otro correo.");

            var business = new Business
            {
                BusinessId = Guid.NewGuid(),
                Name = dto.BusinessName.Trim(),
                Email = correoEmpresa,
                Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
                PlanId = 1,
                Slug = await SlugHelper.UniqueAsync(_context, dto.BusinessName),
                Status = BusinessStatus.Pending,
                TermsAcceptedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var user = new User
            {
                UserId = Guid.NewGuid(),
                BusinessId = business.BusinessId,
                Name = dto.AdminName.Trim(),
                Email = correoDueno,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Role = Roles.Owner,
                CreatedAt = DateTime.UtcNow,
                Business = business
            };

            _context.Businesses.Add(business);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Negocio {BusinessId} y usuario admin {UserId} registrados (pendiente de aprobación).", business.BusinessId, user.UserId);
            await AvisarRegistroAsync(business, user);

            return ServiceResult<RegisterBusinessResultDto>.Ok(new RegisterBusinessResultDto
            {
                BusinessId = business.BusinessId,
                UserId = user.UserId
            });
        }

        private Task<string?> EstadoEmpresaAsync(Guid businessId) =>
            _context.Businesses.Where(b => b.BusinessId == businessId).Select(b => b.Status).FirstOrDefaultAsync();

        private static string MensajeEstado(string? estado) => estado switch
        {
            BusinessStatus.Pending => "Tu empresa está en revisión. Te avisaremos por correo cuando la aprobemos (normalmente en menos de un día hábil).",
            BusinessStatus.Rejected => "La solicitud de tu empresa no fue aprobada. Revisa el correo que te enviamos o escríbenos para más información.",
            _ => "No encontramos tu empresa. Escríbenos para revisarlo."
        };

        // Correos del registro: al dueño ("recibimos tu solicitud") y al operador. Nunca interrumpen el registro
        private async Task AvisarRegistroAsync(Business b, User dueno)
        {
            static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            string Caja(string titulo, string cuerpo) => $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;padding:28px;color:#0f172a">
                  <h2 style="margin:0 0 12px;color:#4f46e5">{titulo}</h2>
                  {cuerpo}
                  <p style="margin:24px 0 0;font-size:12px;color:#64748b">Turnos · agenda en línea para negocios de servicios</p>
                </div>
                """;

            try
            {
                var nombre = dueno.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                await _email.SendAsync(dueno.Email, "Recibimos la solicitud de " + b.Name, Caja("Recibimos tu solicitud",
                    $"""
                    <p>Hola {H(nombre)}, gracias por registrar <strong>{H(b.Name)}</strong>.</p>
                    <p>Vamos a revisar los datos y te escribiremos a este correo cuando la empresa esté aprobada, normalmente en menos de un día hábil.
                    Desde ese momento podrás entrar al panel con <strong>{H(dueno.Email)}</strong> y la contraseña que elegiste.</p>
                    """));
            }
            catch (Exception ex) { _logger.LogError(ex, "No se pudo enviar el correo de registro al dueño {UserId}.", dueno.UserId); }

            var operador = _config["Plataforma:CorreoOperador"] ?? _config["Smtp:From"];
            if (string.IsNullOrWhiteSpace(operador)) return;
            try
            {
                await _email.SendAsync(operador, "Nueva empresa por aprobar: " + b.Name, Caja("Nueva empresa por aprobar",
                    $"""
                    <p><strong>{H(b.Name)}</strong> · {H(b.Email)}{(b.Phone != null ? " · " + H(b.Phone) : "")}</p>
                    <p>Dirección: {H(b.Address ?? "—")}</p>
                    <p>Dueño: {H(dueno.Name)} · {H(dueno.Email)}</p>
                    <p>Apruébala o recházala desde el panel de plataforma.</p>
                    """));
            }
            catch (Exception ex) { _logger.LogError(ex, "No se pudo avisar al operador del registro de {BusinessId}.", b.BusinessId); }
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

            var estado = await EstadoEmpresaAsync(user.BusinessId);
            if (estado != BusinessStatus.Approved)
            {
                _logger.LogWarning("Login de {UserId} con empresa en estado {Estado}.", user.UserId, estado);
                return ServiceResult<AuthResponseDto>.Unauthorized(MensajeEstado(estado));
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

            if (existingToken == null || existingToken.Expiration < DateTime.UtcNow || !existingToken.User.Active
                || await EstadoEmpresaAsync(existingToken.User.BusinessId) != BusinessStatus.Approved)
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
