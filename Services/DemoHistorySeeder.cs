using AgendaApp.Context;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    // Solo desarrollo: genera ~2 meses de historial para "Barbería Demo" (turnos atendidos,
    // cancelados, no asistidos y reseñas) para poder ver estadísticas con datos.
    // Uso: dotnet run -- seed-historial
    public static class DemoHistorySeeder
    {
        private const string Empresa = "Barbería Demo";
        private const string Marca = "[historial-demo]";

        public static async Task RunAsync(AgendaProDbContext db, ILogger logger)
        {
            var b = await db.Businesses.FirstOrDefaultAsync(x => x.Name == Empresa);
            if (b == null) { logger.LogWarning("No existe la empresa {Empresa}.", Empresa); return; }
            if (await db.Appointments.AnyAsync(a => a.BusinessId == b.BusinessId && a.Notes == Marca))
            {
                logger.LogInformation("El historial demo ya existe; no se duplica.");
                return;
            }

            var staff = await db.UserServices.Where(us => us.User.BusinessId == b.BusinessId && us.User.Active && us.Service.Active)
                .Select(us => new { us.UserId, us.ServiceId, us.Service.DurationMin, us.Service.Price }).ToListAsync();
            if (staff.Count == 0) return;

            var rnd = new Random(42);
            var nombres = new[] { "Andrés Pérez", "Camila Rojas", "Felipe Gómez", "Sofía Torres", "Juan Martínez", "Valeria Díaz", "Carlos Ruiz",
                "Natalia Herrera", "Diego Castro", "Paula Moreno", "Sebastián Ortiz", "Laura Jiménez", "Mateo Vargas", "Isabella Romero",
                "Santiago López", "Daniela Ríos", "Tomás Mejía", "Mariana Cárdenas", "Nicolás Suárez", "Gabriela Peña" };

            // Clientes demo (reutiliza los que existan por teléfono)
            var clientes = new List<Guid>();
            for (var i = 0; i < nombres.Length; i++)
            {
                var tel = $"31000{i:D5}";
                var c = await db.Clients.FirstOrDefaultAsync(x => x.BusinessId == b.BusinessId && x.Phone == tel);
                if (c == null)
                {
                    c = new Client { ClientId = Guid.NewGuid(), BusinessId = b.BusinessId, Name = nombres[i], Phone = tel, CreatedAt = DateTime.UtcNow.AddDays(-70) };
                    db.Clients.Add(c);
                }
                clientes.Add(c.ClientId);
            }
            await db.SaveChangesAsync();

            // Demanda realista: más tarde y fines de semana; mediodía y primeras horas flojas
            int[] pesoHora = { 0, 0, 0, 0, 0, 0, 0, 0, 2, 3, 4, 4, 1, 2, 3, 4, 5, 6, 5, 0 }; // índice = hora
            var comentarios = new[] { "Excelente servicio, muy puntual.", "Me encantó el resultado.", "Buena atención.", "Todo bien, volveré.",
                "Un poco de espera pero quedó perfecto.", null, null, "Muy amables.", "El mejor corte que me han hecho." };

            var creados = 0;
            var hoy = DateTime.Today;
            for (var d = hoy.AddDays(-60); d < hoy; d = d.AddDays(1))
            {
                var dow = d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
                if (dow == 7) continue;
                var cupos = dow == 6 ? rnd.Next(6, 10) : rnd.Next(3, 8);
                var ocupado = new HashSet<(Guid, int)>();
                for (var n = 0; n < cupos; n++)
                {
                    var hora = ElegirHora(rnd, pesoHora, dow == 6 ? 14 : 19);
                    var s = staff[rnd.Next(staff.Count)];
                    if (!ocupado.Add((s.UserId, hora))) continue;

                    var inicio = d.AddHours(hora).AddMinutes(rnd.Next(0, 2) * 30);
                    var r = rnd.NextDouble();
                    var estado = r < 0.08 ? AppointmentStatus.Cancelled : r < 0.13 ? AppointmentStatus.NoShow : AppointmentStatus.Completed;
                    var a = new Appointment
                    {
                        AppointmentId = Guid.NewGuid(),
                        BusinessId = b.BusinessId,
                        ClientId = clientes[(int)Math.Floor(Math.Pow(rnd.NextDouble(), 1.6) * clientes.Count)], // algunos vuelven seguido
                        UserId = s.UserId,
                        serviceId = s.ServiceId,
                        DateTimeStart = inicio,
                        DateTimeEnd = inicio.AddMinutes(s.DurationMin),
                        Price = s.Price,
                        Status = estado,
                        Source = rnd.NextDouble() < 0.35 ? "Web" : "Mostrador",
                        Notes = Marca,
                        CancelReason = estado == AppointmentStatus.Cancelled ? "Cliente canceló" : null,
                        CreatedAt = inicio.AddDays(-rnd.Next(1, 6)).ToUniversalTime(),
                        CompletedAt = estado == AppointmentStatus.Completed ? inicio.AddMinutes(s.DurationMin) : null
                    };
                    db.Appointments.Add(a);
                    creados++;

                    if (estado == AppointmentStatus.Completed && rnd.NextDouble() < 0.45)
                    {
                        var estrellas = rnd.NextDouble() switch { < 0.05 => 2, < 0.15 => 3, < 0.45 => 4, _ => 5 };
                        db.Reviews.Add(new Review
                        {
                            ReviewId = Guid.NewGuid(),
                            BusinessId = b.BusinessId,
                            AppointmentId = a.AppointmentId,
                            ClientId = a.ClientId,
                            UserId = a.UserId,
                            ServiceId = a.serviceId,
                            Rating = estrellas,
                            Comment = comentarios[rnd.Next(comentarios.Length)],
                            CreatedAt = inicio.AddHours(3).ToUniversalTime()
                        });
                    }
                }
            }
            await db.SaveChangesAsync();
            logger.LogInformation("Historial demo creado: {N} turnos en los últimos 60 días.", creados);
        }

        private static int ElegirHora(Random rnd, int[] peso, int cierre)
        {
            var opciones = Enumerable.Range(8, cierre - 8).SelectMany(h => Enumerable.Repeat(h, peso[h])).ToList();
            return opciones[rnd.Next(opciones.Count)];
        }
    }
}
