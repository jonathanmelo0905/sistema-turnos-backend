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

        public AppointmentsController(IAppointmentService appointmentService)
        {
            _appointmentService = appointmentService;
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
    }
}
