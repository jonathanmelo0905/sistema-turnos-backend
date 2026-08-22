using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgendaApp.Dtos;
using AgendaApp.Services;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/users/{userId}/[controller]")]
    [Authorize(Policy = "collaborators.manage")]
    public class SchedulesController : ControllerBase
    {
        private readonly ISchedulesService _schedulesService;

        public SchedulesController(ISchedulesService schedulesService)
        {
            _schedulesService = schedulesService;
        }

        [HttpGet("schedule")]
        public async Task<IActionResult> GetWeeklySchedule(Guid userId)
        {
            var schedules = await _schedulesService.GetWeeklyScheduleAsync(userId);
            return Ok(schedules);
        }

        [HttpPost("schedule")]
        public async Task<IActionResult> UpsertSchedule(Guid userId, [FromBody] UserScheduleDto dto)
        {
            await _schedulesService.UpsertScheduleAsync(userId, dto);
            return Ok("Horario actualizado correctamente.");
        }

        [HttpPost("exceptions")]
        public async Task<IActionResult> AddException(Guid userId, [FromBody] UserScheduleExceptionCreateDto dto)
        {
            await _schedulesService.AddExceptionAsync(userId, dto);
            return Ok("Excepción agregada correctamente.");
        }

        [HttpDelete("exceptions/{id}")]
        public async Task<IActionResult> DeleteException(Guid userId, Guid id)
        {
            var result = await _schedulesService.DeleteExceptionAsync(userId, id);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok("Excepción eliminada correctamente.")
            };
        }
    }
}
