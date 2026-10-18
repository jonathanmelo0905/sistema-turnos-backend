using System.Security.Cryptography;
using System.Text;
using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public interface IPasswordResetService
    {
        // Soporte: envía al dueño de la empresa el enlace para restablecer su contraseña
        Task<ServiceResult<PasswordResetSentDto>> SendToOwnerAsync(Guid businessId);
        Task<ServiceResult<PasswordResetCheckDto>> CheckAsync(string token);
        Task<ServiceResult<bool>> ResetAsync(string token, string password);
    }

    // T10: restablecer la contraseña del dueño desde soporte (token de un solo uso, 24 h, solo el hash en la base)
    public class PasswordResetService : IPasswordResetService
    {
        private static readonly TimeSpan Vigencia = TimeSpan.FromHours(24);
        private const string Invalido = "El enlace no es válido o ya se usó. Pide uno nuevo a soporte.";
        private const string Vencido = "El enlace venció (dura 24 horas). Pide uno nuevo a soporte.";

        private readonly AgendaProDbContext _context;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly IWebHostEnvironment _env;

        public PasswordResetService(AgendaProDbContext context, IEmailService email, IConfiguration config, ILogger<PasswordResetService> logger, IWebHostEnvironment env)
        {
            _env = env;
            _context = context;
            _email = email;
            _config = config;
            _logger = logger;
        }

        public async Task<ServiceResult<PasswordResetSentDto>> SendToOwnerAsync(Guid businessId)
        {
            var b = await _context.Businesses.AsNoTracking().FirstOrDefaultAsync(x => x.BusinessId == businessId);
            if (b == null || !BusinessStatus.EsRevisable(b.Status))
                return ServiceResult<PasswordResetSentDto>.NotFound("No encontramos la empresa.");

            var dueno = await _context.Users.Where(u => u.BusinessId == businessId && u.Role == Roles.Owner)
                .OrderBy(u => u.CreatedAt).FirstOrDefaultAsync();
            if (dueno == null)
                return ServiceResult<PasswordResetSentDto>.NotFound("La empresa no tiene dueño registrado.");

            // Un enlace vigente a la vez: los anteriores sin usar dejan de servir
            var ahora = DateTime.UtcNow;
            await _context.PasswordResetTokens.Where(t => t.UserId == dueno.UserId && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, ahora));

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            _context.PasswordResetTokens.Add(new PasswordResetToken
            {
                UserId = dueno.UserId,
                TokenHash = Hash(token),
                ExpiresAt = ahora + Vigencia,
                CreatedAt = ahora
            });
            await _context.SaveChangesAsync();

            var enlace = $"{(_config["App:AdminUrl"] ?? "http://localhost:4200").TrimEnd('/')}/restablecer?token={token}";
            var enviado = await EnviarAsync(dueno, b.Name, enlace);
            _logger.LogInformation("Enlace de restablecimiento creado para el dueño {UserId} de {BusinessId}.", dueno.UserId, businessId);

            if (!enviado)
                return ServiceResult<PasswordResetSentDto>.BadRequest("No pudimos enviar el correo. Revisa la configuración de SMTP e inténtalo de nuevo.");

            return ServiceResult<PasswordResetSentDto>.Ok(new PasswordResetSentDto
            {
                Email = dueno.Email,
                ExpiresAt = ahora + Vigencia,
                DevLink = _env.IsDevelopment() ? enlace : null
            });
        }

        public async Task<ServiceResult<PasswordResetCheckDto>> CheckAsync(string token)
        {
            var (t, error) = await BuscarAsync(token);
            if (t == null) return ServiceResult<PasswordResetCheckDto>.BadRequest(error!);
            return ServiceResult<PasswordResetCheckDto>.Ok(new PasswordResetCheckDto { Name = t.User.Name, Email = t.User.Email });
        }

        public async Task<ServiceResult<bool>> ResetAsync(string token, string password)
        {
            var (t, error) = await BuscarAsync(token);
            if (t == null) return ServiceResult.BadRequest(error!);

            t.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            t.UsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Se cierran todas sus sesiones abiertas
            await _context.RefreshTokens.Where(r => r.UserId == t.UserId && !r.Revoked)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Revoked, true));

            _logger.LogInformation("Contraseña restablecida para el usuario {UserId}.", t.UserId);
            return ServiceResult.Ok();
        }

        private async Task<(PasswordResetToken?, string?)> BuscarAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return (null, Invalido);
            var hash = Hash(token.Trim());
            var t = await _context.PasswordResetTokens.Include(x => x.User).FirstOrDefaultAsync(x => x.TokenHash == hash);
            if (t == null || t.UsedAt != null || !t.User.Active) return (null, Invalido);
            if (t.ExpiresAt < DateTime.UtcNow) return (null, Vencido);
            return (t, null);
        }

        private static string Hash(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private async Task<bool> EnviarAsync(User dueno, string empresa, string enlace)
        {
            static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var nombre = dueno.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            try
            {
                await _email.SendAsync(dueno.Email, "Restablece tu contraseña de " + empresa, $"""
                    <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;padding:28px;color:#0f172a">
                      <h2 style="margin:0 0 12px;color:#4f46e5">Restablece tu contraseña</h2>
                      <p>Hola {H(nombre)}, nos pediste ayuda para entrar al panel de <strong>{H(empresa)}</strong>.</p>
                      <p>Usa este botón para elegir una contraseña nueva. El enlace sirve una sola vez y vence en 24 horas.</p>
                      <p style="margin:22px 0"><a href="{enlace}" style="background:#4f46e5;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:600">Elegir contraseña nueva</a></p>
                      <p style="font-size:13px;color:#64748b">Si no lo pediste, ignora este correo: tu contraseña actual sigue funcionando.</p>
                      <p style="margin:24px 0 0;font-size:12px;color:#64748b">Turnos · agenda en línea para negocios de servicios</p>
                    </div>
                    """, replyTo: _config["Plataforma:CorreoOperador"]);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar el enlace de restablecimiento al usuario {UserId}.", dueno.UserId);
                return false;
            }
        }
    }
}
