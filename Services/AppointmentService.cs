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
            var validacion = await _availabilityService.IsUserAvailable(dto.UserId, dto.DateTimeStart, dto.DateTimeEnd, dto.serviceId);
            if (!validacion.Status)
                return ServiceResult<AppointmentDto>.BadRequest(validacion.Mensaje);

            if (dto.DateTimeEnd <= dto.DateTimeStart)
                return ServiceResult<AppointmentDto>.BadRequest("La fecha de finalización debe ser posterior a la fecha de inicio.");

            var businessExists = await _context.Businesses.AnyAsync(b => b.BusinessId == dto.BusinessId);
            if (!businessExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un negocio con el ID {dto.BusinessId}.");

            var clientExists = await _context.Clients.AnyAsync(c => c.ClientId == dto.ClientId);
            if (!clientExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un cliente con el ID {dto.ClientId}.");

            var userExists = await _context.Users.AnyAsync(u => u.UserId == dto.UserId);
            if (!userExists)
                return ServiceResult<AppointmentDto>.NotFound($"No existe un usuario con el ID {dto.UserId}.");

            bool overlapBusiness = await _context.Appointments.AnyAsync(a =>
                a.BusinessId == dto.BusinessId &&
                (
                    (dto.DateTimeStart >= a.DateTimeStart && dto.DateTimeStart < a.DateTimeEnd) ||
                    (dto.DateTimeEnd > a.DateTimeStart && dto.DateTimeEnd <= a.DateTimeEnd) ||
                    (dto.DateTimeStart <= a.DateTimeStart && dto.DateTimeEnd >= a.DateTimeEnd)
                )
            );
            if (overlapBusiness)
                return ServiceResult<AppointmentDto>.Conflict("Ya existe una cita en ese horario para el negocio seleccionado.");

            bool overlapClient = await _context.Appointments.AnyAsync(a =>
                a.ClientId == dto.ClientId &&
                (
                    (dto.DateTimeStart >= a.DateTimeStart && dto.DateTimeStart < a.DateTimeEnd) ||
                    (dto.DateTimeEnd > a.DateTimeStart && dto.DateTimeEnd <= a.DateTimeEnd) ||
                    (dto.DateTimeStart <= a.DateTimeStart && dto.DateTimeEnd >= a.DateTimeEnd)
                )
            );
            if (overlapClient)
                return ServiceResult<AppointmentDto>.Conflict("El cliente ya tiene una cita programada en ese horario.");

            bool overlapUser = await _context.Appointments.AnyAsync(a =>
                a.UserId == dto.UserId &&
                (
                    (dto.DateTimeStart >= a.DateTimeStart && dto.DateTimeStart < a.DateTimeEnd) ||
                    (dto.DateTimeEnd > a.DateTimeStart && dto.DateTimeEnd <= a.DateTimeEnd) ||
                    (dto.DateTimeStart <= a.DateTimeStart && dto.DateTimeEnd >= a.DateTimeEnd)
                )
            );
            if (overlapUser)
                return ServiceResult<AppointmentDto>.Conflict("El profesional ya tiene una cita programada en ese horario.");

            var appointment = new Appointment
            {
                AppointmentId = Guid.NewGuid(),
                BusinessId = dto.BusinessId,
                ClientId = dto.ClientId,
                UserId = dto.UserId,
                DateTimeStart = dto.DateTimeStart,
                DateTimeEnd = dto.DateTimeEnd,
                Status = "Pending",
                serviceId = dto.serviceId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cita {AppointmentId} creada para negocio {BusinessId}, cliente {ClientId}, profesional {UserId}.",
                appointment.AppointmentId, appointment.BusinessId, appointment.ClientId, appointment.UserId);

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

            bool overlap = await _context.Appointments.AnyAsync(a =>
                a.AppointmentId != id &&
                (
                    (a.BusinessId == dto.BusinessId ||
                     a.ClientId == dto.ClientId ||
                     a.UserId == dto.UserId)
                ) &&
                (
                    (dto.DateTimeStart >= a.DateTimeStart && dto.DateTimeStart < a.DateTimeEnd) ||
                    (dto.DateTimeEnd > a.DateTimeStart && dto.DateTimeEnd <= a.DateTimeEnd) ||
                    (dto.DateTimeStart <= a.DateTimeStart && dto.DateTimeEnd >= a.DateTimeEnd)
                )
            );
            if (overlap)
                return ServiceResult<AppointmentDto>.Conflict("El horario se solapa con otra cita existente para el negocio, cliente o profesional.");

            appointment.BusinessId = dto.BusinessId;
            appointment.ClientId = dto.ClientId;
            appointment.UserId = dto.UserId;
            appointment.DateTimeStart = dto.DateTimeStart;
            appointment.DateTimeEnd = dto.DateTimeEnd;
            appointment.Status = appointment.Status ?? "Pending";
            appointment.serviceId = dto.serviceId;

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

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
    }
}
