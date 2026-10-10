using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AgendaApp.Services
{
    public interface IValleyService
    {
        Task<ValleyStateDto> GetStateAsync(Guid businessId);
        Task<ServiceResult<ValleyStateDto>> SaveConfigAsync(Guid businessId, ValleyConfigDto dto);
        ValleyQuoteDto? Quote(ValleyStateDto state, DateTime start, int durationMin);
        Task<ValleyQuoteDto?> QuoteAsync(Guid businessId, DateTime start, int durationMin);
    }

    // Horas Valle: detecta las franjas día × hora con menos reservas de los últimos 30 días
    // y calcula el descuento de un servicio según cuánto de su duración cae en ellas.
    public class ValleyService : IValleyService
    {
        public const int Days = 30;
        public const int MinData = 20;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

        private readonly AgendaProDbContext _context;
        private readonly IMemoryCache _cache;

        public ValleyService(AgendaProDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private static string Key(Guid businessId) => $"valley:{businessId}";

        public async Task<ValleyStateDto> GetStateAsync(Guid businessId)
        {
            if (_cache.TryGetValue(Key(businessId), out ValleyStateDto? cached) && cached != null) return cached;

            var cfg = await _context.ValleyConfigs.AsNoTracking().FirstOrDefaultAsync(v => v.BusinessId == businessId)
                      ?? new ValleyConfig { BusinessId = businessId };

            var hasta = DateTime.Today;
            var desde = hasta.AddDays(-Days);
            var inicios = await _context.Appointments.AsNoTracking()
                .Where(a => a.BusinessId == businessId && a.DateTimeStart >= desde && a.DateTimeStart < hasta
                    && a.Status != AppointmentStatus.Cancelled && a.Status != AppointmentStatus.RejectedOutOfHours)
                .Select(a => a.DateTimeStart)
                .ToListAsync();

            var total = inicios.Count;
            var porFranja = inicios.GroupBy(s => (StatsService.Dow(s), s.Hour)).ToDictionary(g => g.Key, g => g.Count());
            var abierto = await StatsService.OpenHoursAsync(_context, businessId);

            var state = new ValleyStateDto
            {
                Config = ToDto(cfg),
                Total = total,
                MinData = MinData,
                EnoughData = total >= MinData,
                Days = Days
            };

            // Umbral expresado en citas: en modo % depende del total del periodo
            var limite = cfg.Mode == ValleyModes.Count ? (double)cfg.Threshold : (double)cfg.Threshold * total / 100;

            foreach (var (dow, h) in abierto.OrderBy(x => x.Item1).ThenBy(x => x.Item2))
            {
                var count = porFranja.GetValueOrDefault((dow, h));
                var esValle = state.EnoughData && count <= limite;
                state.Cells.Add(new ValleyCellDto
                {
                    DayOfWeek = dow,
                    Hour = h,
                    Count = count,
                    SharePct = total > 0 ? Math.Round(count * 100.0 / total, 1) : 0,
                    IsValley = esValle,
                    Pct = esValle ? Intensidad(cfg, count, limite) : 0
                });
            }

            _cache.Set(Key(businessId), state, CacheTtl);
            return state;
        }

        // Máximo con 0 citas, mínimo justo en el umbral, lineal entre medio
        private static int Intensidad(ValleyConfig cfg, int count, double limite)
        {
            if (limite <= 0) return cfg.DiscountMax;
            var t = Math.Min(1, count / limite);
            return (int)Math.Round(cfg.DiscountMax - (cfg.DiscountMax - cfg.DiscountMin) * t, MidpointRounding.AwayFromZero);
        }

        public ValleyQuoteDto? Quote(ValleyStateDto state, DateTime start, int durationMin)
        {
            if (!state.Config.Enabled || !state.EnoughData || durationMin <= 0) return null;

            var dow = StatsService.Dow(start);
            var fin = start.AddMinutes(durationMin);
            double enValle = 0, ponderado = 0;

            // Se recorre el servicio por tramos de hora reloj
            for (var t = start; t < fin;)
            {
                var finHora = t.Date.AddHours(t.Hour + 1);
                var tramoFin = finHora < fin ? finHora : fin;
                var min = (tramoFin - t).TotalMinutes;
                var celda = state.Cells.FirstOrDefault(c => c.DayOfWeek == dow && c.Hour == t.Hour);
                if (celda is { IsValley: true })
                {
                    enValle += min;
                    ponderado += min * celda.Pct;
                }
                t = tramoFin;
            }

            if (enValle <= 0) return null;

            var basePct = (int)Math.Round(ponderado / enValle, MidpointRounding.AwayFromZero);
            var solape = (int)Math.Floor(enValle * 100 / durationMin);
            var completo = solape >= state.Config.MinOverlapPct;
            var pct = completo ? basePct
                : state.Config.Proportional ? (int)Math.Round(basePct * solape / 100.0, MidpointRounding.AwayFromZero)
                : 0;

            return new ValleyQuoteDto { Start = start, Pct = pct, BasePct = basePct, OverlapPct = solape, Full = completo };
        }

        public async Task<ValleyQuoteDto?> QuoteAsync(Guid businessId, DateTime start, int durationMin) =>
            Quote(await GetStateAsync(businessId), start, durationMin);

        public async Task<ServiceResult<ValleyStateDto>> SaveConfigAsync(Guid businessId, ValleyConfigDto dto)
        {
            if (dto.DiscountMin > dto.DiscountMax)
                return ServiceResult<ValleyStateDto>.BadRequest("El descuento mínimo no puede ser mayor que el máximo.");
            if (dto.Mode == ValleyModes.Percent && dto.Threshold > 100)
                return ServiceResult<ValleyStateDto>.BadRequest("En el criterio por porcentaje el umbral debe estar entre 0 y 100.");

            var cfg = await _context.ValleyConfigs.FirstOrDefaultAsync(v => v.BusinessId == businessId);
            if (cfg == null)
            {
                cfg = new ValleyConfig { BusinessId = businessId };
                _context.ValleyConfigs.Add(cfg);
            }
            cfg.Enabled = dto.Enabled;
            cfg.Mode = dto.Mode;
            cfg.Threshold = dto.Threshold;
            cfg.DiscountMin = dto.DiscountMin;
            cfg.DiscountMax = dto.DiscountMax;
            cfg.MinOverlapPct = dto.MinOverlapPct;
            cfg.Proportional = dto.Proportional;
            cfg.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _cache.Remove(Key(businessId));
            return ServiceResult<ValleyStateDto>.Ok(await GetStateAsync(businessId));
        }

        private static ValleyConfigDto ToDto(ValleyConfig c) => new()
        {
            Enabled = c.Enabled,
            Mode = c.Mode,
            Threshold = c.Threshold,
            DiscountMin = c.DiscountMin,
            DiscountMax = c.DiscountMax,
            MinOverlapPct = c.MinOverlapPct,
            Proportional = c.Proportional,
            UpdatedAt = c.UpdatedAt
        };
    }
}
