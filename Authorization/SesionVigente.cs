using System.Security.Claims;
using AgendaApp.Context;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Authorization
{
    // Estado del usuario que se compara con el JWT en cada petición con sesión.
    public record EstadoSesion(bool Active, string Role, Guid BusinessId);

    // El JWT dura 2 h y trae el rol adentro. Para que desactivar a alguien, cambiarle el rol o rechazar
    // su empresa surta efecto al instante, cada petición con sesión se compara con la base (con la
    // caché corta de IPermissionCache, que se invalida en cada uno de esos cambios).
    // Si no coincide: 401 { message, code: "AccesoCambio" }. El front intenta renovar: si el usuario
    // sigue activo recibe un JWT con el rol nuevo; si no, vuelve al login.
    public static class SesionVigente
    {
        public const string Codigo = "AccesoCambio";
        public const string Mensaje = "Tu acceso cambió. Vuelve a iniciar sesión.";
        private const string ClaveItem = "sesion-no-vigente";

        public static async Task ValidarAsync(TokenValidatedContext ctx)
        {
            var principal = ctx.Principal;
            var userIdTxt = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal?.FindFirst("sub")?.Value;
            var businessIdTxt = principal?.FindFirst("BusinessId")?.Value;
            var rolToken = principal?.FindFirst(ClaimTypes.Role)?.Value;

            if (!Guid.TryParse(userIdTxt, out var userId) || !Guid.TryParse(businessIdTxt, out var businessId))
            {
                Rechazar(ctx, "token sin usuario o empresa");
                return;
            }

            var servicios = ctx.HttpContext.RequestServices;
            var cache = servicios.GetRequiredService<IPermissionCache>();
            var db = servicios.GetRequiredService<AgendaProDbContext>();

            var usuario = await cache.GetSesionAsync(userId, id => db.Users.AsNoTracking()
                .Where(u => u.UserId == id)
                .Select(u => new EstadoSesion(u.Active, u.Role, u.BusinessId))
                .FirstOrDefaultAsync());

            if (usuario == null || !usuario.Active)
            {
                Rechazar(ctx, "usuario inactivo o inexistente");
                return;
            }
            if (usuario.BusinessId != businessId || usuario.Role != rolToken)
            {
                Rechazar(ctx, $"el rol o la empresa cambiaron (token {rolToken}, base {usuario.Role})");
                return;
            }

            var estadoEmpresa = await cache.GetEstadoEmpresaAsync(businessId, id => db.Businesses.AsNoTracking()
                .Where(b => b.BusinessId == id)
                .Select(b => b.Status)
                .FirstOrDefaultAsync());

            var empresaOk = estadoEmpresa == BusinessStatus.Approved
                || (usuario.Role == Roles.PlatformAdmin && estadoEmpresa == BusinessStatus.Internal);
            if (!empresaOk)
            {
                Rechazar(ctx, $"empresa en estado {estadoEmpresa}");
                return;
            }
        }

        // Respuesta del 401 cuando el token es válido pero ya no corresponde al usuario de la base
        public static async Task ResponderAsync(JwtBearerChallengeContext ctx)
        {
            if (!ctx.HttpContext.Items.ContainsKey(ClaveItem)) return;
            ctx.HandleResponse();
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await ctx.Response.WriteAsJsonAsync(new { message = Mensaje, code = Codigo });
        }

        private static void Rechazar(TokenValidatedContext ctx, string motivo)
        {
            var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SesionVigente");
            log.LogInformation("Sesión no vigente: {Motivo}.", motivo);
            ctx.HttpContext.Items[ClaveItem] = true;
            ctx.Fail(Mensaje);
        }
    }
}
