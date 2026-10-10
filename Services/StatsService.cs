using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public interface IStatsService
    {
        Task<StatsDto> GetAsync(Guid businessId, DateTime from, DateTime to, Guid? userId);
    }

    // Estadísticas del negocio. Volumen pequeño (decenas de miles de turnos como máximo):
    // se cargan los turnos del rango y se agregan en memoria.
    public class StatsService : IStatsService
    {
        private const int MaxDays = 366;
        private readonly AgendaProDbContext _context;

        public StatsService(AgendaProDbContext context)
        {
            _context = context;
        }

        private record Fila(Guid Id, Guid UserId, string UserName, Guid ClientId, Guid ServiceId, string ServiceName,
            DateTime Start, DateTime End, string Status, decimal Price, string Source, decimal? ListPrice, int? ValleyPct);

        public async Task<StatsDto> GetAsync(Guid businessId, DateTime from, DateTime to, Guid? userId)
        {
            from = from.Date;
            to = to.Date;
            if (to < from) (from, to) = (to, from);
            if ((to - from).TotalDays > MaxDays) from = to.AddDays(-MaxDays);
            var dias = (int)(to - from).TotalDays + 1;

            var filas = await LoadAsync(businessId, from, to, userId);
            var previas = await LoadAsync(businessId, from.AddDays(-dias), from.AddDays(-1), userId);

            var dto = new StatsDto
            {
                From = from,
                To = to,
                Totals = await TotalsAsync(businessId, filas, from, to),
                Previous = await TotalsAsync(businessId, previas, from.AddDays(-dias), from.AddDays(-1))
            };

            var activos = filas.Where(f => Ocupa(f.Status)).ToList();

            // Por día (todos los días del rango, incluso en cero)
            dto.ByDay = Enumerable.Range(0, dias).Select(i => from.AddDays(i)).Select(d =>
            {
                var del = activos.Where(f => f.Start.Date == d).ToList();
                return new StatsDayDto
                {
                    Date = d,
                    Count = del.Count,
                    Completed = del.Count(f => f.Status == AppointmentStatus.Completed),
                    Revenue = del.Where(f => f.Status == AppointmentStatus.Completed).Sum(f => f.Price)
                };
            }).ToList();

            // Servicios más pedidos
            dto.ByService = activos.GroupBy(f => new { f.ServiceId, f.ServiceName })
                .Select(g => new StatsServiceDto
                {
                    ServiceId = g.Key.ServiceId,
                    Name = g.Key.ServiceName,
                    Count = g.Count(),
                    Revenue = g.Where(f => f.Status == AppointmentStatus.Completed).Sum(f => f.Price)
                })
                .OrderByDescending(s => s.Count).ThenByDescending(s => s.Revenue).ToList();

            // Por colaborador, con ocupación y calificación
            var ratings = await _context.Reviews.AsNoTracking()
                .Where(r => r.BusinessId == businessId && r.Appointment.DateTimeStart >= from && r.Appointment.DateTimeStart < to.AddDays(1))
                .Select(r => new { r.UserId, r.Rating }).ToListAsync();
            var disponibles = await AvailableMinutesAsync(businessId, from, to);

            dto.ByStaff = activos.GroupBy(f => new { f.UserId, f.UserName })
                .Select(g =>
                {
                    var reservados = g.Sum(f => (f.End - f.Start).TotalMinutes);
                    var disp = disponibles.GetValueOrDefault(g.Key.UserId);
                    var rs = ratings.Where(r => r.UserId == g.Key.UserId).ToList();
                    return new StatsStaffDto
                    {
                        UserId = g.Key.UserId,
                        Name = g.Key.UserName,
                        Count = g.Count(),
                        Completed = g.Count(f => f.Status == AppointmentStatus.Completed),
                        NoShow = g.Count(f => f.Status == AppointmentStatus.NoShow),
                        Revenue = g.Where(f => f.Status == AppointmentStatus.Completed).Sum(f => f.Price),
                        OccupancyPct = disp > 0 ? Math.Round(Math.Min(100, reservados / disp * 100), 1) : 0,
                        Rating = rs.Count > 0 ? Math.Round(rs.Average(r => r.Rating), 1) : null,
                        RatingCount = rs.Count
                    };
                })
                .OrderByDescending(s => s.Count).ToList();

            // Mapa de calor día × hora, marcando las horas en que la empresa atiende
            var abierto = await OpenHoursAsync(_context, businessId);
            for (var dow = 1; dow <= 7; dow++)
                for (var h = 6; h <= 22; h++)
                    dto.Heatmap.Add(new StatsHeatCellDto
                    {
                        DayOfWeek = dow,
                        Hour = h,
                        Count = activos.Count(f => Dow(f.Start) == dow && f.Start.Hour == h),
                        Open = abierto.Contains((dow, h))
                    });

            // Horas muertas: las franjas abiertas con menos demanda (para ofertas)
            dto.SlowHours = dto.Heatmap.Where(c => c.Open)
                .OrderBy(c => c.Count).ThenBy(c => c.DayOfWeek).ThenBy(c => c.Hour)
                .Take(5).ToList();

            dto.Valley = await ValleyAsync(businessId, from, to, dias, userId);

            // Reseñas del periodo
            var dist = Enumerable.Range(1, 5).Select(i => ratings.Count(r => r.Rating == i)).ToArray();
            var reseñas = userId.HasValue ? ratings.Where(r => r.UserId == userId.Value).ToList() : ratings;
            dto.RatingCount = reseñas.Count;
            dto.RatingAverage = reseñas.Count > 0 ? Math.Round(reseñas.Average(r => r.Rating), 2) : 0;
            dto.RatingDistribution = userId.HasValue ? Enumerable.Range(1, 5).Select(i => reseñas.Count(r => r.Rating == i)).ToArray() : dist;

            return dto;
        }

        private async Task<List<Fila>> LoadAsync(Guid businessId, DateTime from, DateTime to, Guid? userId)
        {
            var q = _context.Appointments.AsNoTracking()
                .Where(a => a.BusinessId == businessId && a.DateTimeStart >= from && a.DateTimeStart < to.AddDays(1));
            if (userId.HasValue) q = q.Where(a => a.UserId == userId.Value);

            return await q.Select(a => new Fila(a.AppointmentId, a.UserId, a.User.Name, a.ClientId, a.serviceId, a.Service.Name,
                    a.DateTimeStart, a.DateTimeEnd, a.Status, a.Price ?? a.Service.Price, a.Source, a.ListPrice, a.ValleyDiscountPct))
                .ToListAsync();
        }

        private async Task<StatsTotalsDto> TotalsAsync(Guid businessId, List<Fila> filas, DateTime from, DateTime to)
        {
            var todos = filas.Where(f => f.Status != AppointmentStatus.RejectedOutOfHours).ToList();
            var activos = todos.Where(f => Ocupa(f.Status)).ToList();
            var completados = activos.Where(f => f.Status == AppointmentStatus.Completed).ToList();
            var cancelados = todos.Count(f => f.Status == AppointmentStatus.Cancelled);
            var noShow = activos.Count(f => f.Status == AppointmentStatus.NoShow);
            var debian = completados.Count + noShow;

            // Clientes nuevos: su primera atención de la historia cae en el rango
            var clientes = completados.Select(f => f.ClientId).Distinct().ToList();
            var primeras = await _context.Appointments.AsNoTracking()
                .Where(a => a.BusinessId == businessId && clientes.Contains(a.ClientId) && a.Status == AppointmentStatus.Completed)
                .GroupBy(a => a.ClientId)
                .Select(g => new { g.Key, Primera = g.Min(a => a.DateTimeStart) })
                .ToListAsync();
            var nuevos = primeras.Count(p => p.Primera >= from);

            return new StatsTotalsDto
            {
                Appointments = activos.Count,
                Completed = completados.Count,
                Cancelled = cancelados,
                NoShow = noShow,
                Upcoming = activos.Count(f => f.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.PendingOutOfHours or AppointmentStatus.InProgress),
                CancelRate = todos.Count > 0 ? Math.Round(cancelados * 100.0 / todos.Count, 1) : 0,
                NoShowRate = debian > 0 ? Math.Round(noShow * 100.0 / debian, 1) : 0,
                Revenue = completados.Sum(f => f.Price),
                Estimated = activos.Where(f => f.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.InProgress).Sum(f => f.Price),
                AvgTicket = completados.Count > 0 ? Math.Round(completados.Average(f => f.Price), 0) : 0,
                NewClients = nuevos,
                ReturningClients = clientes.Count - nuevos,
                WebShare = activos.Count > 0 ? Math.Round(activos.Count(f => f.Source == "Web") * 100.0 / activos.Count, 1) : 0
            };
        }

        // Horas Valle: cuántos turnos tomaron el descuento y qué tan rentable fue
        // Se mide por fecha de RESERVA (CreatedAt), no por fecha del turno: una promoción se toma hoy
        // para un turno de los próximos días y debe contar en el periodo en que se reservó.
        private async Task<List<Fila>> LoadBookedAsync(Guid businessId, DateTime from, DateTime to, Guid? userId)
        {
            var desde = from.ToUniversalTime();
            var hasta = to.AddDays(1).ToUniversalTime();
            var q = _context.Appointments.AsNoTracking()
                .Where(a => a.BusinessId == businessId && a.CreatedAt >= desde && a.CreatedAt < hasta
                    && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.RejectedOutOfHours);
            if (userId.HasValue) q = q.Where(a => a.UserId == userId.Value);
            return await q.Select(a => new Fila(a.AppointmentId, a.UserId, a.User.Name, a.ClientId, a.serviceId, a.Service.Name,
                    a.DateTimeStart, a.DateTimeEnd, a.Status, a.Price ?? a.Service.Price, a.Source, a.ListPrice, a.ValleyDiscountPct))
                .ToListAsync();
        }

        private async Task<StatsValleyDto> ValleyAsync(Guid businessId, DateTime from, DateTime to, int dias, Guid? userId)
        {
            var reservados = await LoadBookedAsync(businessId, from, to, userId);
            var previos = await LoadBookedAsync(businessId, from.AddDays(-dias), from.AddDays(-1), userId);
            var activos = reservados;
            var valle = reservados.Where(f => f.ValleyPct > 0).ToList();
            var porAtender = valle.Where(f => f.Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.InProgress or AppointmentStatus.PendingOutOfHours).ToList();
            var completados = valle.Where(f => f.Status == AppointmentStatus.Completed).ToList();
            var noShow = valle.Count(f => f.Status == AppointmentStatus.NoShow);

            // Clientes nuevos: su primer turno (no cancelado) en la empresa fue con descuento valle dentro del rango
            var clientes = valle.Select(f => f.ClientId).Distinct().ToList();
            var historia = await _context.Appointments.AsNoTracking()
                .Where(a => a.BusinessId == businessId && clientes.Contains(a.ClientId)
                    && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.RejectedOutOfHours)
                .Select(a => new { a.AppointmentId, a.ClientId, a.DateTimeStart })
                .ToListAsync();
            var primeros = historia.GroupBy(a => a.ClientId).Select(g => g.MinBy(a => a.DateTimeStart)!).ToList();

            return new StatsValleyDto
            {
                Enabled = await _context.ValleyConfigs.AnyAsync(v => v.BusinessId == businessId && v.Enabled),
                Bookings = valle.Count,
                PreviousBookings = previos.Count(f => f.ValleyPct > 0),
                SharePct = activos.Count > 0 ? Math.Round(valle.Count * 100.0 / activos.Count, 1) : 0,
                Completed = completados.Count,
                NoShowRate = completados.Count + noShow > 0 ? Math.Round(noShow * 100.0 / (completados.Count + noShow), 1) : 0,
                Revenue = completados.Sum(f => f.Price),
                DiscountGiven = valle.Sum(f => (f.ListPrice ?? f.Price) - f.Price),
                AvgDiscountPct = valle.Count > 0 ? Math.Round(valle.Average(f => f.ValleyPct!.Value), 1) : 0,
                NewClients = primeros.Count(p => valle.Any(v => v.Id == p.AppointmentId)),
                Upcoming = porAtender.Count,
                Estimated = porAtender.Sum(f => f.Price),
                WebShare = valle.Count > 0 ? Math.Round(valle.Count(f => f.Source == "Web") * 100.0 / valle.Count, 1) : 0,
                TopSlots = valle.GroupBy(f => (Dow(f.Start), f.Start.Hour))
                    .Select(g => new StatsHeatCellDto { DayOfWeek = g.Key.Item1, Hour = g.Key.Hour, Count = g.Count(), Open = true })
                    .OrderByDescending(c => c.Count).ThenBy(c => c.DayOfWeek).ThenBy(c => c.Hour).Take(5).ToList(),
                TopServices = valle.GroupBy(f => new { f.ServiceId, f.ServiceName })
                    .Select(g => new StatsServiceDto
                    {
                        ServiceId = g.Key.ServiceId,
                        Name = g.Key.ServiceName,
                        Count = g.Count(),
                        Revenue = g.Where(f => f.Status == AppointmentStatus.Completed).Sum(f => f.Price)
                    })
                    .OrderByDescending(s => s.Count).Take(5).ToList(),
                ByWeek = valle.GroupBy(f => f.Start.Date.AddDays(-(Dow(f.Start) - 1)))
                    .OrderBy(g => g.Key)
                    .Select(g => new StatsDayDto
                    {
                        Date = g.Key,
                        Count = g.Count(),
                        Completed = g.Count(f => f.Status == AppointmentStatus.Completed),
                        Revenue = g.Where(f => f.Status == AppointmentStatus.Completed).Sum(f => f.Price)
                    }).ToList()
            };
        }

        // Minutos de jornada por colaborador en el rango (empresa ∩ colaborador − pausa − ausencias aprobadas de día completo)
        private async Task<Dictionary<Guid, double>> AvailableMinutesAsync(Guid businessId, DateTime from, DateTime to)
        {
            var empresa = await _context.BusinessSchedules.AsNoTracking().Where(b => b.BusinessId == businessId).ToListAsync();
            var jornadas = await _context.UserSchedules.AsNoTracking()
                .Where(s => s.User.BusinessId == businessId && s.IsAvailable).ToListAsync();
            var ausencias = await _context.TimeOffRequests.AsNoTracking()
                .Where(t => t.BusinessId == businessId && t.Status == TimeOffStatus.Approved && t.StartTime == null && t.StartDate <= to && t.EndDate >= from)
                .ToListAsync();

            var resultado = new Dictionary<Guid, double>();
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                var dow = Dow(d);
                var b = empresa.FirstOrDefault(x => x.DayOfWeek == dow);
                if (b == null || !b.IsOpen || !TimeSpan.TryParse(b.OpenTime, out var bo) || !TimeSpan.TryParse(b.CloseTime, out var bc)) continue;

                foreach (var j in jornadas.Where(x => x.DayOfWeek == dow))
                {
                    if (ausencias.Any(a => a.UserId == j.UserId && a.StartDate <= d && a.EndDate >= d)) continue;
                    if (!TimeSpan.TryParse(j.StartTime, out var js) || !TimeSpan.TryParse(j.EndTime, out var je)) continue;
                    var ini = js > bo ? js : bo;
                    var fin = je < bc ? je : bc;
                    var min = (fin - ini).TotalMinutes;
                    if (TimeSpan.TryParse(j.BreakStart, out var ps) && TimeSpan.TryParse(j.BreakEnd, out var pe) && pe > ps)
                        min -= (pe - ps).TotalMinutes;
                    if (min > 0) resultado[j.UserId] = resultado.GetValueOrDefault(j.UserId) + min;
                }
            }
            return resultado;
        }

        // Franjas (día, hora) en que la empresa atiende; también las usa ValleyService
        internal static async Task<HashSet<(int, int)>> OpenHoursAsync(AgendaProDbContext context, Guid businessId)
        {
            var empresa = await context.BusinessSchedules.AsNoTracking().Where(b => b.BusinessId == businessId && b.IsOpen).ToListAsync();
            var set = new HashSet<(int, int)>();
            foreach (var b in empresa)
            {
                if (!TimeSpan.TryParse(b.OpenTime, out var o) || !TimeSpan.TryParse(b.CloseTime, out var c)) continue;
                for (var h = o.Hours; h < (c.Minutes > 0 ? c.Hours + 1 : c.Hours); h++) set.Add((b.DayOfWeek, h));
            }
            return set;
        }

        internal static bool Ocupa(string status) => status != AppointmentStatus.Cancelled && status != AppointmentStatus.RejectedOutOfHours;
        internal static int Dow(DateTime d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;
    }
}
