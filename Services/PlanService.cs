using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class PlanService : IPlanService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<PlanService> _logger;

        public PlanService(AgendaProDbContext context, ILogger<PlanService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PlansDto>> GetPlansAsync()
        {
            var plans = await _context.Plans
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(x => new PlansDto
                {
                    PlanId = x.PlanId,
                    MaxAppointments = x.MaxAppointments,
                    MaxUsers = x.MaxUsers,
                    Name = x.Name,
                    Price = x.Price
                })
                .ToListAsync();

            _logger.LogInformation("Se obtuvieron {Count} planes.", plans.Count);
            return plans;
        }
    }
}
