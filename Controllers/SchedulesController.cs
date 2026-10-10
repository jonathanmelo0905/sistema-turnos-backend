using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public SchedulesController(ISchedulesService schedulesService, ITenantGuard tenant)
        {
            _schedulesService = schedulesService;
            _tenant = tenant;
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetWeeklySchedule(Guid userId)
        {
            if (!await _tenant.CanManageUserAsync(userId))
                return Forbid();

            return Ok(await _schedulesService.GetWeeklyScheduleAsync(userId));
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> UpsertSchedule(Guid userId, [FromBody] UserScheduleDto dto)
        {
            if (!await _tenant.CanManageUserAsync(userId))
                return Forbid();

            await _schedulesService.UpsertScheduleAsync(userId, dto);
            return Ok(new { message = "Horario actualizado correctamente." });
        }

        [HttpPost("exceptions")]
        public async Task<IActionResult> AddException(Guid userId, [FromBody] UserScheduleExceptionCreateDto dto)
        {
            if (!await _tenant.CanManageUserAsync(userId))
                return Forbid();

            await _schedulesService.AddExceptionAsync(userId, dto);
            return Ok(new { message = "Excepción agregada correctamente." });
        }

        [HttpDelete("exceptions/{id:guid}")]
        public async Task<IActionResult> DeleteException(Guid userId, Guid id)
        {
            if (!await _tenant.CanManageUserAsync(userId))
                return Forbid();

            var result = await _schedulesService.DeleteExceptionAsync(userId, id);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { message = "Excepción eliminada correctamente." })
            };
        }
    }
}
