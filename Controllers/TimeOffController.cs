using AgendaApp.Authorization;
using AgendaApp.Context;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    // Ausencias del personal. Cada quien solicita las suyas; aprueba quien tiene
    // flexibility.approve (o el dueño). Quien gestiona el equipo puede registrarlas para otros.
    [ApiController]
    [Route("api/time-off")]
    [Authorize]
    public class TimeOffController : ControllerBase
    {
        private readonly ITimeOffService _timeOff;
        private readonly ITenantGuard _tenant;

        public TimeOffController(ITimeOffService timeOff, ITenantGuard tenant)
        {
            _timeOff = timeOff;
            _tenant = tenant;
        }

        // GET /api/time-off?userId=&status=&from=&to=
        // Sin permisos de equipo/aprobación solo se ven las propias.
        [HttpGet]
        public async Task<ActionResult<List<TimeOffResponseDto>>> List(
            [FromQuery] Guid? userId, [FromQuery] string? status, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        {
            var veTodo = await _tenant.HasPermissionAsync(PermissionCodes.FlexibilityApprove)
                      || await _tenant.HasPermissionAsync(PermissionCodes.CollaboratorsManage);

            if (!veTodo) userId = _tenant.UserId;
            else if (userId.HasValue && !await _tenant.UserInBusinessAsync(userId.Value)) return NotFound();

            return Ok(await _timeOff.ListAsync(_tenant.BusinessId, userId, status, from, to));
        }

        // Vista previa: turnos que quedarían en conflicto con una ausencia
        // GET /api/time-off/conflicts?userId=&startDate=&endDate=&startTime=&endTime=
        [HttpGet("conflicts")]
        public async Task<ActionResult<List<TimeOffConflictDto>>> Preview(
            [FromQuery] Guid? userId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate,
            [FromQuery] string? startTime, [FromQuery] string? endTime)
        {
            var target = userId ?? _tenant.UserId;
            if (!await _tenant.CanManageUserAsync(target)) return Forbid();
            if (endDate < startDate) return BadRequest("La fecha final no puede ser anterior a la inicial.");

            return Ok(await _timeOff.ConflictsAsync(target, startDate, endDate, startTime, endTime));
        }

        [HttpGet("{id:guid}/conflicts")]
        public async Task<ActionResult<List<TimeOffConflictDto>>> Conflicts(Guid id)
        {
            var t = await _timeOff.FindAsync(id);
            if (t == null || t.BusinessId != _tenant.BusinessId) return NotFound();
            if (!await PuedeVer(t)) return Forbid();

            return Ok(await _timeOff.ConflictsAsync(t.UserId, t.StartDate, t.EndDate, t.StartTime, t.EndTime));
        }

        [HttpPost]
        public async Task<ActionResult<TimeOffResponseDto>> Create([FromBody] TimeOffCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var target = dto.UserId ?? _tenant.UserId;
            if (!await _tenant.CanManageUserAsync(target)) return Forbid();

            var autoApprove = await _tenant.HasPermissionAsync(PermissionCodes.FlexibilityApprove);
            var result = await _timeOff.CreateAsync(_tenant.BusinessId, target, _tenant.UserId, dto, autoApprove);
            return Map(result);
        }

        [HttpPost("{id:guid}/approve")]
        [Authorize(Policy = "flexibility.approve")]
        public async Task<ActionResult<TimeOffResponseDto>> Approve(Guid id, [FromBody] TimeOffReviewDto? dto)
        {
            var t = await _timeOff.FindAsync(id);
            if (t == null || t.BusinessId != _tenant.BusinessId) return NotFound();
            return Map(await _timeOff.ReviewAsync(id, _tenant.UserId, true, dto?.Note));
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize(Policy = "flexibility.approve")]
        public async Task<ActionResult<TimeOffResponseDto>> Reject(Guid id, [FromBody] TimeOffReviewDto? dto)
        {
            var t = await _timeOff.FindAsync(id);
            if (t == null || t.BusinessId != _tenant.BusinessId) return NotFound();
            return Map(await _timeOff.ReviewAsync(id, _tenant.UserId, false, dto?.Note));
        }

        // El propio colaborador cancela su solicitud; quien gestiona el equipo también puede.
        [HttpPost("{id:guid}/cancel")]
        public async Task<ActionResult<TimeOffResponseDto>> Cancel(Guid id)
        {
            var t = await _timeOff.FindAsync(id);
            if (t == null || t.BusinessId != _tenant.BusinessId) return NotFound();
            if (!await _tenant.CanManageUserAsync(t.UserId) && !await _tenant.HasPermissionAsync(PermissionCodes.FlexibilityApprove))
                return Forbid();

            return Map(await _timeOff.CancelAsync(id, _tenant.UserId));
        }

        private async Task<bool> PuedeVer(TimeOffRequest t) =>
            await _tenant.CanManageUserAsync(t.UserId) || await _tenant.HasPermissionAsync(PermissionCodes.FlexibilityApprove);

        private ActionResult<TimeOffResponseDto> Map(ServiceResult<TimeOffResponseDto> r) => r.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(r.Error),
            ServiceErrorType.BadRequest => BadRequest(r.Error),
            ServiceErrorType.Conflict => Conflict(r.Error),
            _ => Ok(r.Data)
        };
    }
}
