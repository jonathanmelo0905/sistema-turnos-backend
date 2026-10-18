using AgendaApp.Context;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    // "dotnet run -- crear-operador <correo> <contraseña> [nombre]": crea o actualiza un operador (PlatformAdmin).
    // Los operadores viven en una empresa interna (Status Internal) porque Users.BusinessId es obligatorio.
    public static class OperadorSeeder
    {
        private const string CorreoEmpresaInterna = "plataforma@turnos.internal";

        public static async Task<int> RunAsync(AgendaProDbContext context, ILogger logger, string[] args)
        {
            var i = Array.IndexOf(args, "crear-operador");
            var correo = args.ElementAtOrDefault(i + 1)?.Trim().ToLowerInvariant();
            var clave = args.ElementAtOrDefault(i + 2);
            var nombre = args.ElementAtOrDefault(i + 3)?.Trim();

            if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@') || string.IsNullOrEmpty(clave) || clave.Length < 8)
            {
                logger.LogError("Uso: dotnet run -- crear-operador <correo> <contraseña de 8 o más> [nombre]");
                return 1;
            }

            var interna = await context.Businesses.FirstOrDefaultAsync(b => b.Status == BusinessStatus.Internal);
            if (interna == null)
            {
                interna = new Business
                {
                    BusinessId = Guid.NewGuid(),
                    Name = "Plataforma",
                    Email = CorreoEmpresaInterna,
                    Status = BusinessStatus.Internal,
                    PlanId = 1,
                    CreatedAt = DateTime.UtcNow
                };
                context.Businesses.Add(interna);
            }

            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == correo);
            if (user != null && user.Role != Roles.PlatformAdmin)
            {
                logger.LogError("El correo {Correo} ya es de un usuario de empresa; usa otro.", correo);
                return 1;
            }

            if (user == null)
            {
                user = new User
                {
                    UserId = Guid.NewGuid(),
                    BusinessId = interna.BusinessId,
                    Email = correo,
                    Name = string.IsNullOrWhiteSpace(nombre) ? "Operador" : nombre,
                    Role = Roles.PlatformAdmin,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(clave),
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(user);
            }
            else
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(clave);
                user.Active = true;
                if (!string.IsNullOrWhiteSpace(nombre)) user.Name = nombre;
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Operador {Correo} listo. Entra al panel con ese correo y verás /plataforma/empresas.", correo);
            return 0;
        }
    }
}
