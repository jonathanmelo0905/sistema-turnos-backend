using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public interface IPlatformService
    {
        Task<List<PlatformBusinessDto>> ListAsync(string? status);
        Task<ServiceResult<PlatformBusinessDto>> ApproveAsync(Guid businessId);
        Task<ServiceResult<PlatformBusinessDto>> RejectAsync(Guid businessId, string reason);
    }

    // Panel de plataforma (T9): el operador aprueba o rechaza las empresas registradas desde /registro
    public class PlatformService : IPlatformService
    {
        private readonly AgendaProDbContext _context;
        private readonly IEmailService _email;
        private readonly IConfiguration _config;
        private readonly ILogger<PlatformService> _logger;

        public PlatformService(AgendaProDbContext context, IEmailService email, IConfiguration config, ILogger<PlatformService> logger)
        {
            _context = context;
            _email = email;
            _config = config;
            _logger = logger;
        }

        public async Task<List<PlatformBusinessDto>> ListAsync(string? status)
        {
            var q = _context.Businesses.AsNoTracking()
                .Where(b => b.Status == BusinessStatus.Pending || b.Status == BusinessStatus.Approved || b.Status == BusinessStatus.Rejected);
            if (BusinessStatus.EsRevisable(status))
                q = q.Where(b => b.Status == status);

            return await Proyectar(q)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<ServiceResult<PlatformBusinessDto>> ApproveAsync(Guid businessId)
        {
            var b = await _context.Businesses.FirstOrDefaultAsync(x => x.BusinessId == businessId);
            if (b == null || !BusinessStatus.EsRevisable(b.Status))
                return ServiceResult<PlatformBusinessDto>.NotFound("No encontramos la empresa.");
            if (b.Status == BusinessStatus.Approved)
                return ServiceResult<PlatformBusinessDto>.Conflict("La empresa ya está aprobada.");

            b.Status = BusinessStatus.Approved;
            b.ReviewedAt = DateTime.UtcNow;
            b.RejectionReason = null;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Empresa {BusinessId} aprobada.", businessId);

            var dto = await UnaAsync(businessId);
            await AvisarDuenoAsync(dto, aprobada: true);
            return ServiceResult<PlatformBusinessDto>.Ok(dto);
        }

        public async Task<ServiceResult<PlatformBusinessDto>> RejectAsync(Guid businessId, string reason)
        {
            var b = await _context.Businesses.FirstOrDefaultAsync(x => x.BusinessId == businessId);
            if (b == null || !BusinessStatus.EsRevisable(b.Status))
                return ServiceResult<PlatformBusinessDto>.NotFound("No encontramos la empresa.");
            if (b.Status == BusinessStatus.Rejected)
                return ServiceResult<PlatformBusinessDto>.Conflict("La empresa ya está rechazada.");

            b.Status = BusinessStatus.Rejected;
            b.ReviewedAt = DateTime.UtcNow;
            b.RejectionReason = reason.Trim();
            await _context.SaveChangesAsync();

            // Las sesiones abiertas del personal dejan de renovarse
            await _context.RefreshTokens.Where(r => r.User.BusinessId == businessId && !r.Revoked)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Revoked, true));
            _logger.LogInformation("Empresa {BusinessId} rechazada.", businessId);

            var dto = await UnaAsync(businessId);
            await AvisarDuenoAsync(dto, aprobada: false);
            return ServiceResult<PlatformBusinessDto>.Ok(dto);
        }

        private Task<PlatformBusinessDto> UnaAsync(Guid businessId) =>
            Proyectar(_context.Businesses.AsNoTracking().Where(b => b.BusinessId == businessId)).FirstAsync();

        private static IQueryable<PlatformBusinessDto> Proyectar(IQueryable<Business> q) => q.Select(b => new PlatformBusinessDto
        {
            BusinessId = b.BusinessId,
            Name = b.Name,
            Email = b.Email,
            Phone = b.Phone,
            Address = b.Address,
            Slug = b.Slug,
            Status = b.Status,
            CreatedAt = b.CreatedAt,
            TermsAcceptedAt = b.TermsAcceptedAt,
            ReviewedAt = b.ReviewedAt,
            RejectionReason = b.RejectionReason,
            OwnerName = b.Users.Where(u => u.Role == Roles.Owner).OrderBy(u => u.CreatedAt).Select(u => u.Name).FirstOrDefault(),
            OwnerEmail = b.Users.Where(u => u.Role == Roles.Owner).OrderBy(u => u.CreatedAt).Select(u => u.Email).FirstOrDefault(),
            OwnerPhone = b.Users.Where(u => u.Role == Roles.Owner).OrderBy(u => u.CreatedAt).Select(u => u.Phone).FirstOrDefault(),
            UsersCount = b.Users.Count(u => u.Active),
            IsPublished = b.IsPublished
        });

        // Correo al dueño con el resultado; nunca interrumpe la operación
        private async Task AvisarDuenoAsync(PlatformBusinessDto b, bool aprobada)
        {
            if (string.IsNullOrWhiteSpace(b.OwnerEmail)) return;
            static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var nombre = (b.OwnerName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            var adminUrl = (_config["App:AdminUrl"] ?? "http://localhost:4200").TrimEnd('/');

            var cuerpo = aprobada
                ? $"""
                  <p>Hola {H(nombre)}, <strong>{H(b.Name)}</strong> ya está aprobada.</p>
                  <p>Entra al panel con <strong>{H(b.OwnerEmail)}</strong> y la contraseña que elegiste para configurar tu horario, servicios y equipo.
                  Tu página queda oculta hasta que la publiques desde <strong>Mi empresa</strong>, así tus clientes no la ven a medio armar.</p>
                  <p style="margin:22px 0"><a href="{adminUrl}/login" style="background:#4f46e5;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;font-weight:600">Iniciar sesión</a></p>
                  """
                : $"""
                  <p>Hola {H(nombre)}, revisamos la solicitud de <strong>{H(b.Name)}</strong> y por ahora no la pudimos aprobar.</p>
                  <p><strong>Motivo:</strong> {H(b.RejectionReason)}</p>
                  <p>Si crees que es un error o quieres corregir los datos, responde a este correo.</p>
                  """;
            var titulo = aprobada ? "¡Tu empresa está aprobada!" : "Solicitud no aprobada";

            try
            {
                await _email.SendAsync(b.OwnerEmail!, aprobada ? $"{b.Name} ya está aprobada" : $"Sobre la solicitud de {b.Name}", $"""
                    <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;padding:28px;color:#0f172a">
                      <h2 style="margin:0 0 12px;color:#4f46e5">{titulo}</h2>
                      {cuerpo}
                      <p style="margin:24px 0 0;font-size:12px;color:#64748b">Turnos · agenda en línea para negocios de servicios</p>
                    </div>
                    """, replyTo: _config["Plataforma:CorreoOperador"]);
            }
            catch (Exception ex) { _logger.LogError(ex, "No se pudo avisar al dueño de la empresa {BusinessId}.", b.BusinessId); }
        }
    }
}
