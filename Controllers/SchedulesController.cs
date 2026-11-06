using AgendaApp.Authorization;
using AgendaApp.Context;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Controllers
{
    // Horario semanal y excepciones de un colaborador. El propio colaborador gestiona el suyo;
    // para el de otros se necesita collaborators.manage (o ser Owner).
    [ApiController]
    [Route("api/users/{userId:guid}/[controller]")]
    [Authorize]
    public class SchedulesController : ControllerBase
    {
        private readonly ISchedulesService _schedulesService;
        private readonly ITenantGuard _tenant;
        private readonly AgendaProDbContext _context;

        public SchedulesController(ISchedulesService schedulesService, ITenantGuard tenant, AgendaProDbContext context)
        {
            _schedulesService = schedulesService;
            _tenant = tenant;
            _context = context;
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetWeeklySchedule(Guid userId)
        {
            if (!await _tenant.CanViewUserAsync(userId))
                return Forbid();

            return Ok(await _schedulesService.GetWeeklyScheduleAsync(userId));
        }

        // PUT /api/users/{userId}/schedules/week — guarda los 7 días validados contra la empresa
        [HttpPut("week")]
        public async Task<IActionResult> SaveWeek(Guid userId, [FromBody] WeeklyScheduleDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await SinPermiso(userId) is { } prohibido) return prohibido;

            var result = await _schedulesService.SaveWeekAsync(userId, _tenant.BusinessId, dto);
            return result.ErrorType == ServiceErrorType.BadRequest ? BadRequest(result.Error) : Ok(result.Data);
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> UpsertSchedule(Guid userId, [FromBody] UserScheduleDto dto)
        {
            if (await SinPermiso(userId) is { } prohibido) return prohibido;

            var result = await _schedulesService.UpsertScheduleAsync(userId, _tenant.BusinessId, dto);
            return result.ErrorType == ServiceErrorType.BadRequest
                ? BadRequest(result.Error)
                : Ok(new { message = "Horario actualizado correctamente." });
        }

        // GET /api/users/{userId}/schedules/exceptions — desde hoy en adelante
        [HttpGet("exceptions")]
        public async Task<IActionResult> GetExceptions(Guid userId)
        {
            if (!await _tenant.CanViewUserAsync(userId))
                return Forbid();

            return Ok(await _schedulesService.GetExceptionsAsync(userId, DateTime.Today));
        }

        // Horas extra (IsAvailable=true): uno mismo si la empresa lo permite; quien gestiona el equipo, siempre.
        // Bloqueos (IsAvailable=false): solo quien gestiona el equipo; el colaborador debe solicitar una ausencia.
        [HttpPost("exceptions")]
        public async Task<IActionResult> AddException(Guid userId, [FromBody] UserScheduleExceptionCreateDto dto)
        {
            if (await SinPermiso(userId) is { } prohibido) return prohibido;

            var gestiona = await _tenant.HasPermissionAsync(PermissionCodes.CollaboratorsManage);
            if (!gestiona)
            {
                if (!dto.IsAvailable)
                    return BadRequest("Para bloquear tiempo de tu agenda, solicita una ausencia.");

                var permite = await _context.Businesses.AsNoTracking()
                    .Where(b => b.BusinessId == _tenant.BusinessId)
                    .Select(b => b.AllowOutOfHoursBooking)
                    .FirstOrDefaultAsync();
                if (!permite)
                    return BadRequest("Tu empresa no permite abrir horas extra. Pídelo al dueño o encargado.");
            }

            var result = await _schedulesService.AddExceptionAsync(userId, _tenant.BusinessId, dto);
            return result.ErrorType == ServiceErrorType.BadRequest ? BadRequest(result.Error) : Ok(result.Data);
        }

        [HttpDelete("exceptions/{id:guid}")]
        public async Task<IActionResult> DeleteException(Guid userId, Guid id)
        {
            if (await SinPermiso(userId) is { } prohibido) return prohibido;

            var result = await _schedulesService.DeleteExceptionAsync(userId, id);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { message = "Excepción eliminada correctamente." })
            };
        }

        // Gestionar a otra persona: collaborators.manage; al dueño solo otro dueño
        private async Task<IActionResult?> SinPermiso(Guid userId)
        {
            if (await _tenant.CanManageUserAsync(userId)) return null;
            if (await _tenant.EsDuenoAjenoAsync(userId)) return StatusCode(403, new { message = "El horario del dueño solo lo cambia un dueño." });
            return Forbid();
        }
    }
}
