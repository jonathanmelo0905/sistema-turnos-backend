using System.Security.Claims;
using AgendaApp.Context;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPermissionService _permissionService;

        public AppointmentsController(IAppointmentService appointmentService, IPermissionService permissionService)
        {
            _appointmentService = appointmentService;
            _permissionService = permissionService;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<AppointmentDto>>> GetAppointments([FromQuery] Guid businessId)
        {
            if (businessId == Guid.Empty)
                return BadRequest("El parámetro 'businessId' es obligatorio.");

            var result = await _appointmentService.GetAppointmentsAsync(businessId);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateAppointment([FromBody] AppointmentCreateDto dto)
        {
            if (dto == null)
                return BadRequest("Los datos de la cita son requeridos.");

            var result = await _appointmentService.CreateAppointmentAsync(dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => CreatedAtAction(nameof(GetAppointments), new { businessId = dto.BusinessId }, result.Data)
            };
        }

        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> UpdateAppointment(Guid id, [FromBody] AppointmentCreateDto dto)
        {
            if (dto == null)
                return BadRequest("Los datos de la cita son requeridos.");

            var result = await _appointmentService.UpdateAppointmentAsync(id, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> DeleteAppointment(Guid id)
        {
            var result = await _appointmentService.DeleteAppointmentAsync(id);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => NoContent()
            };
        }

        // Puede aprobar/rechazar: el colaborador dueño de la cita, o quien tenga flexibility.approve
        // (Owner incluido por el bypass de rol).
        [HttpPost("{id:guid}/approve")]
        [Authorize]
        public async Task<IActionResult> ApproveOutOfHours(Guid id)
        {
            var (callerId, canActOnBehalf) = await ResolveCallerAsync();
            if (callerId == null)
                return Unauthorized("Token inválido: no contiene el identificador del usuario.");

            var result = await _appointmentService.ApproveOutOfHoursAsync(id, callerId.Value, canActOnBehalf);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.Unauthorized => Forbid(),
                _ => Ok(result.Data)
            };
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize]
        public async Task<IActionResult> RejectOutOfHours(Guid id)
        {
            var (callerId, canActOnBehalf) = await ResolveCallerAsync();
            if (callerId == null)
                return Unauthorized("Token inválido: no contiene el identificador del usuario.");

            var result = await _appointmentService.RejectOutOfHoursAsync(id, callerId.Value, canActOnBehalf);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.Unauthorized => Forbid(),
                _ => Ok(result.Data)
            };
        }

        // Resuelve el UserId del caller y si puede actuar en nombre de otro colaborador
        // (Owner por bypass de rol, o Encargado con flexibility.approve otorgado).
        private async Task<(Guid? CallerId, bool CanActOnBehalf)> ResolveCallerAsync()
        {
            var callerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(callerIdClaim, out var callerId))
                return (null, false);

            var role = User.FindFirst(ClaimTypes.Role)?.Value;
            if (role == Roles.Owner)
                return (callerId, true);

            var grantedCodes = await _permissionService.GetGrantedCodesAsync(callerId);
            return (callerId, grantedCodes.Contains(PermissionCodes.FlexibilityApprove));
        }
    }
}
