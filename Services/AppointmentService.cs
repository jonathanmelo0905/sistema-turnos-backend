using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly AgendaProDbContext _context;
        private readonly AvailabilityService _availabilityService;
        private readonly ILogger<AppointmentService> _logger;

        public AppointmentService(AgendaProDbContext context, AvailabilityService availabilityService, ILogger<AppointmentService> logger)
        {
            _context = context;
            _availabilityService = availabilityService;
            _logger = logger;
        }

        public async Task<ServiceResult<List<AppointmentDto>>> GetAppointmentsAsync(Guid businessId)
        {
            var appointments = await _context.Appointments
                .Where(a => a.BusinessId == businessId)
                .Include(a => a.Business)
                .Include(a => a.Client)
                .Include(a => a.User)
                .Select(a => new AppointmentDto
                {
                    AppointmentId = a.AppointmentId,
                    BusinessId = a.BusinessId,
                    BusinessName = a.Business.Name,
                    ClientId = a.ClientId,
                    ClientName = a.Client.Name,
                    UserId = a.UserId,
                    UserName = a.User.Name,
                    DateTimeStart = a.DateTimeStart,
                    DateTimeEnd = a.DateTimeEnd,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    ServiceId = a.serviceId
                })
                .ToListAsync();

            if (appointments.Count == 0)
                return ServiceResult<List<AppointmentDto>>.NotFound("No se encontraron citas para el negocio especificado.");

            return ServiceResult<List<AppointmentDto>>.Ok(appointments);
        }

        public async Task<ServiceResult<AppointmentDto>> CreateAppointmentAsync(AppointmentCreateDto dto)
        {
            if (dto.DateTimeEnd <= dto.DateTimeStart)
                return ServiceResult<AppointmentDto>.BadRequest("La fecha de finalización debe ser posterior a la fecha de inicio.");

            var business = await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == dto.BusinessId);
            if (business == null)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un negocio con el ID {dto.BusinessId}.");

            // 🕗 Bloque 1: chequeo de horario declarado. Este es el ÚNICO motivo que la
            // flexibilidad de CU-05 puede saltear, y solo si el negocio lo permite — el
            // solapamiento con otra cita (bloque 2, sección CU-04 más abajo) nunca se ve
            // afectado por este toggle.
            var validacion = await _availabilityService.IsUserAvailable(dto.UserId, dto.DateTimeStart, dto.DateTimeEnd, dto.serviceId);

            var isOutOfHoursPending = false;

            if (!validacion.Status)
            {
                if (validacion.Reason == AvailabilityReason.OutOfSchedule && business.AllowOutOfHoursBooking)
                    isOutOfHoursPending = true;
                else
                    return ServiceResult<AppointmentDto>.BadRequest(validacion.Mensaje);
            }

            var clientExists = await _context.Clients.AnyAsync(c => c.ClientId == dto.ClientId);
            if (!clientExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un cliente con el ID {dto.ClientId}.");

            var userExists = await _context.Users.AnyAsync(u => u.UserId == dto.UserId);
            if (!userExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un usuario con el ID {dto.UserId}.");

            // 🔒 Bloque 2: sección crítica bajo transacción + SELECT ... FOR UPDATE para cerrar
            // la carrera entre el chequeo de solapamiento y el INSERT (ver diseño CU-04). Se
            // ejecuta siempre, sin importar isOutOfHoursPending — el toggle del negocio nunca
            // afecta esta validación. Solo importa colaborador y cliente — un negocio con
            // varios colaboradores sí puede tener citas simultáneas con profesionales distintos.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var lockedConflicts = await _context.Appointments
                .FromSqlInterpolated($@"
                    SELECT * FROM Appointments
                    WHERE Status <> 'Cancelled' AND Status <> 'RejectedOutOfHours'
                      AND (UserId = {dto.UserId} OR ClientId = {dto.ClientId})
                      AND DateTimeStart < {dto.DateTimeEnd}
                      AND DateTimeEnd > {dto.DateTimeStart}
                    FOR UPDATE")
                .ToListAsync();

            if (lockedConflicts.Any(a => a.UserId == dto.UserId))
            {
                await transaction.RollbackAsync();
                return ServiceResult<AppointmentDto>.Conflict("El profesional ya tiene una cita programada en ese horario.");
            }

            if (lockedConflicts.Any(a => a.ClientId == dto.ClientId))
            {
                await transaction.RollbackAsync();
                return ServiceResult<AppointmentDto>.Conflict("El cliente ya tiene una cita programada en ese horario.");
            }

            var appointment = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                BusinessId = dto.BusinessId,
                ClientId = dto.ClientId,
                UserId = dto.UserId,
                DateTimeStart = dto.DateTimeStart,
                DateTimeEnd = dto.DateTimeEnd,
                Status = isOutOfHoursPending ? "PendingOutOfHours" : "Pending",
                serviceId = dto.serviceId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Cita {AppointmentId} creada para negocio {BusinessId}, cliente {ClientId}, profesional {UserId} (Status={Status}).",
                appointment.AppointmentId, appointment.BusinessId, appointment.ClientId, appointment.UserId, appointment.Status);

            return ServiceResult<AppointmentDto>.Ok(new AppointmentDto
            {
                AppointmentId = appointment.AppointmentId,
                BusinessId = appointment.BusinessId,
                ClientId = appointment.ClientId,
                UserId = appointment.UserId,
                DateTimeStart = appointment.DateTimeStart,
                DateTimeEnd = appointment.DateTimeEnd,
                Status = appointment.Status,
                CreatedAt = appointment.CreatedAt,
                ServiceId = appointment.serviceId
            });
        }

        public async Task<ServiceResult<AppointmentDto>> UpdateAppointmentAsync(Guid id, AppointmentCreateDto dto)
        {
            if (dto.DateTimeEnd <= dto.DateTimeStart)
                return ServiceResult<AppointmentDto>.BadRequest("La fecha de finalización debe ser posterior a la fecha de inicio.");

            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
                return ServiceResult<AppointmentDto>.NotFound("No se encontró la cita especificada.");

            var businessExists = await _context.Businesses.AnyAsync(b => b.BusinessId == dto.BusinessId);
            if (!businessExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un negocio con el ID {dto.BusinessId}.");

            var clientExists = await _context.Clients.AnyAsync(c => c.ClientId == dto.ClientId);
            if (!clientExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un cliente con el ID {dto.ClientId}.");

            var userExists = await _context.Users.AnyAsync(u => u.UserId == dto.UserId);
            if (!userExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un usuario con el ID {dto.UserId}.");

            // 🔒 Misma sección crítica que en CreateAppointmentAsync — ver diseño CU-04.
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var lockedConflicts = await _context.Appointments
                .FromSqlInterpolated($@"
                    SELECT * FROM Appointments
                    WHERE Status <> 'Cancelled' AND Status <> 'RejectedOutOfHours'
                      AND AppointmentId <> {id}
                      AND (UserId = {dto.UserId} OR ClientId = {dto.ClientId})
                      AND DateTimeStart < {dto.DateTimeEnd}
                      AND DateTimeEnd > {dto.DateTimeStart}
                    FOR UPDATE")
                .ToListAsync();

            if (lockedConflicts.Any(a => a.UserId == dto.UserId))
            {
                await transaction.RollbackAsync();
                return ServiceResult<AppointmentDto>.Conflict("El profesional ya tiene una cita programada en ese horario.");
            }

            if (lockedConflicts.Any(a => a.ClientId == dto.ClientId))
            {
                await transaction.RollbackAsync();
                return ServiceResult<AppointmentDto>.Conflict("El cliente ya tiene una cita programada en ese horario.");
            }

            appointment.BusinessId = dto.BusinessId;
            appointment.ClientId = dto.ClientId;
            appointment.UserId = dto.UserId;
            appointment.DateTimeStart = dto.DateTimeStart;
            appointment.DateTimeEnd = dto.DateTimeEnd;
            appointment.Status = appointment.Status ?? "Pending";
            appointment.serviceId = dto.serviceId;

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Cita {AppointmentId} actualizada.", id);

            return ServiceResult<AppointmentDto>.Ok(new AppointmentDto
            {
                AppointmentId = appointment.AppointmentId,
                BusinessId = appointment.BusinessId,
                ClientId = appointment.ClientId,
                UserId = appointment.UserId,
                DateTimeStart = appointment.DateTimeStart,
                DateTimeEnd = appointment.DateTimeEnd,
                Status = appointment.Status,
                CreatedAt = appointment.CreatedAt,
                ServiceId = appointment.serviceId
            });
        }

        public async Task<ServiceResult<bool>> DeleteAppointmentAsync(Guid id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
                return ServiceResult.NotFound("No se encontró la cita especificada.");

            _context.Appointments.Remove(appointment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cita {AppointmentId} eliminada.", id);
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult<AppointmentDto>> ApproveOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
                return ServiceResult<AppointmentDto>.NotFound("No se encontró la cita especificada.");

            if (appointment.UserId != callerId && !callerCanActOnBehalf)
                return ServiceResult<AppointmentDto>.Unauthorized("No tenés permiso para aprobar esta cita.");

            if (appointment.Status != "PendingOutOfHours")
                return ServiceResult<AppointmentDto>.BadRequest("La cita no está pendiente de aprobación por horario.");

            appointment.Status = "Confirmed";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cita {AppointmentId} aprobada (fuera de horario) por {CallerId}.", id, callerId);

            return ServiceResult<AppointmentDto>.Ok(ToDto(appointment));
        }

        public async Task<ServiceResult<AppointmentDto>> RejectOutOfHoursAsync(Guid id, Guid callerId, bool callerCanActOnBehalf)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
                return ServiceResult<AppointmentDto>.NotFound("No se encontró la cita especificada.");

            if (appointment.UserId != callerId && !callerCanActOnBehalf)
                return ServiceResult<AppointmentDto>.Unauthorized("No tenés permiso para rechazar esta cita.");

            if (appointment.Status != "PendingOutOfHours")
                return ServiceResult<AppointmentDto>.BadRequest("La cita no está pendiente de aprobación por horario.");

            appointment.Status = "RejectedOutOfHours";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cita {AppointmentId} rechazada (fuera de horario) por {CallerId}.", id, callerId);

            return ServiceResult<AppointmentDto>.Ok(ToDto(appointment));
        }

        private static AppointmentDto ToDto(Appointment appointment) => new()
        {
            AppointmentId = appointment.AppointmentId,
            BusinessId = appointment.BusinessId,
            ClientId = appointment.ClientId,
            UserId = appointment.UserId,
            DateTimeStart = appointment.DateTimeStart,
            DateTimeEnd = appointment.DateTimeEnd,
            Status = appointment.Status,
            CreatedAt = appointment.CreatedAt,
            ServiceId = appointment.serviceId
        };
    }
}
