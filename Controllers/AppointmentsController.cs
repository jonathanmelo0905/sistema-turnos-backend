using AgendaApp.Authorization;
using AgendaApp.Context;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    // Turnos de la empresa del token. Cada colaborador gestiona los suyos; quien tiene
    // collaborators.manage (o el dueño) gestiona los de todo el equipo.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentService _appointments;
        private readonly ITenantGuard _tenant;
        private readonly IReminderService _reminders;
        private readonly IWebHostEnvironment _env;

        public AppointmentsController(IAppointmentService appointments, ITenantGuard tenant, IReminderService reminders, IWebHostEnvironment env)
        {
            _appointments = appointments;
            _tenant = tenant;
            _reminders = reminders;
            _env = env;
        }

        private Task<bool> GestionaEquipo() => _tenant.HasPermissionAsync(PermissionCodes.CollaboratorsManage);

        // GET /api/appointments?from=&to=&userId=&status=
        [HttpGet]
        public async Task<ActionResult<List<AppointmentDto>>> List(
            [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? userId, [FromQuery] string? status)
        {
            if (!await GestionaEquipo()) userId = _tenant.UserId;
            var desde = from ?? DateTime.Today;
            var hasta = to ?? desde;
            if (hasta < desde) return BadRequest("La fecha final no puede ser anterior a la inicial.");

            return Ok(await _appointments.ListAsync(_tenant.BusinessId, desde, hasta, userId, status));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<AppointmentDetailDto>> Get(Guid id)
        {
            var a = await _appointments.GetDetailAsync(_tenant.BusinessId, id);
            if (a == null) return NotFound();
            if (a.UserId != _tenant.UserId && !await GestionaEquipo()) return Forbid();
            return Ok(a);
        }

        // POST /api/appointments/book — reserva desde el panel (mostrador)
        [HttpPost("book")]
        public async Task<ActionResult<AppointmentDto>> Book([FromBody] AppointmentBookDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (dto.UserId != _tenant.UserId && !await GestionaEquipo()) return Forbid();

            return Map(await _appointments.BookAsync(_tenant.BusinessId, _tenant.UserId, "Mostrador", dto));
        }

        // Contrato anterior (fin explícito): se mantiene por compatibilidad
        [HttpPost]
        public async Task<ActionResult<AppointmentDto>> Create([FromBody] AppointmentCreateDto dto)
        {
            if (dto == null) return BadRequest("Los datos de la cita son requeridos.");
            if (dto.UserId != _tenant.UserId && !await GestionaEquipo()) return Forbid();

            dto.BusinessId = _tenant.BusinessId;
            return Map(await _appointments.CreateAppointmentAsync(dto));
        }

        // POST /api/appointments/{id}/reschedule — otra hora y/o ceder a otro colaborador
        [HttpPost("{id:guid}/reschedule")]
        public async Task<ActionResult<AppointmentDto>> Reschedule(Guid id, [FromBody] AppointmentRescheduleDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var actual = await _appointments.GetDetailAsync(_tenant.BusinessId, id);
            if (actual == null) return NotFound();

            var gestiona = await GestionaEquipo();
            if (actual.UserId != _tenant.UserId && !gestiona) return Forbid();
            // Ceder a otra persona: la propia colaboradora puede ceder sus turnos a una compañera
            if (dto.UserId.HasValue && !await _tenant.UserInBusinessAsync(dto.UserId.Value)) return NotFound("Colaborador no encontrado.");

            return Map(await _appointments.RescheduleAsync(_tenant.BusinessId, id, _tenant.UserId, dto));
        }

        // POST /api/appointments/{id}/status { status, note }
        [HttpPost("{id:guid}/status")]
        public async Task<ActionResult<AppointmentDto>> ChangeStatus(Guid id, [FromBody] AppointmentStatusDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var actual = await _appointments.GetDetailAsync(_tenant.BusinessId, id);
            if (actual == null) return NotFound();
            if (actual.UserId != _tenant.UserId && !await GestionaEquipo()) return Forbid();

            return Map(await _appointments.ChangeStatusAsync(_tenant.BusinessId, id, _tenant.UserId, dto));
        }

        // Cancelar (antes era un borrado físico): DELETE /api/appointments/{id}?reason=
        [HttpDelete("{id:guid}")]
        public Task<ActionResult<AppointmentDto>> Cancel(Guid id, [FromQuery] string? reason) =>
            ChangeStatus(id, new AppointmentStatusDto { Status = AppointmentStatus.Cancelled, Note = reason ?? "Cancelado desde el panel." });

        // GET /api/appointments/to-contact — turnos por avisar al cliente (propios o del equipo)
        [HttpGet("to-contact")]
        public async Task<ActionResult<List<AppointmentDto>>> ToContact()
        {
            Guid? userId = await GestionaEquipo() ? null : _tenant.UserId;
            return Ok(await _appointments.ToContactAsync(_tenant.BusinessId, userId));
        }

        // POST /api/appointments/{id}/contacted { note }
        [HttpPost("{id:guid}/contacted")]
        public async Task<ActionResult<AppointmentDto>> Contacted(Guid id, [FromBody] AppointmentStatusDto? dto)
        {
            var actual = await _appointments.GetDetailAsync(_tenant.BusinessId, id);
            if (actual == null) return NotFound();
            if (actual.UserId != _tenant.UserId && !await GestionaEquipo()) return Forbid();
            return Map(await _appointments.MarkContactedAsync(_tenant.BusinessId, id, _tenant.UserId, dto?.Note));
        }

        // POST /api/appointments/{id}/reminder { channel: "WhatsApp" | "Email" } — recordatorio del día anterior
        [HttpPost("{id:guid}/reminder")]
        public async Task<ActionResult<AppointmentDto>> Reminder(Guid id, [FromBody] AppointmentReminderDto dto)
        {
            var actual = await _appointments.GetDetailAsync(_tenant.BusinessId, id);
            if (actual == null) return NotFound();
            if (actual.UserId != _tenant.UserId && !await GestionaEquipo()) return Forbid();
            return Map(await _reminders.RecordarAsync(_tenant.BusinessId, id, _tenant.UserId, dto.Channel));
        }

        // POST /api/appointments/reminders/run — solo Development: corre el job de recordatorios (pruebas)
        [HttpPost("reminders/run")]
        public async Task<ActionResult<object>> RunReminders()
        {
            if (!_env.IsDevelopment()) return NotFound();
            return Ok(new { created = await _reminders.GenerarAsync() });
        }

        // Aprobar / rechazar turnos fuera de horario: el colaborador del turno o quien tenga flexibility.approve
        [HttpPost("{id:guid}/approve")]
        public async Task<ActionResult<AppointmentDto>> ApproveOutOfHours(Guid id)
        {
            var actual = await _appointments.GetDetailAsync(_tenant.BusinessId, id);
            if (actual == null) return NotFound();
            var puede = await _tenant.HasPermissionAsync(PermissionCodes.FlexibilityApprove);
            return Map(await _appointments.ApproveOutOfHoursAsync(id, _tenant.UserId, puede));
        }

        [HttpPost("{id:guid}/reject")]
        public async Task<ActionResult<AppointmentDto>> RejectOutOfHours(Guid id)
        {
            var actual = await _appointments.GetDetailAsync(_tenant.BusinessId, id);
            if (actual == null) return NotFound();
            var puede = await _tenant.HasPermissionAsync(PermissionCodes.FlexibilityApprove);
            return Map(await _appointments.RejectOutOfHoursAsync(id, _tenant.UserId, puede));
        }

        private ActionResult<AppointmentDto> Map(ServiceResult<AppointmentDto> r) => r.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(r.Error),
            ServiceErrorType.BadRequest => BadRequest(r.Error),
            ServiceErrorType.Conflict => Conflict(r.Error),
            ServiceErrorType.Unauthorized => Forbid(),
            _ => Ok(r.Data)
        };
    }
}
