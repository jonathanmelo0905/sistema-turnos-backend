using System.Security.Cryptography;
using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    // Todo lo que ve y hace el cliente final sin iniciar sesión.
    public interface IPublicService
    {
        Task<List<PublicBusinessListItemDto>> ListBusinessesAsync();
        Task<PublicBusinessDto?> GetBusinessAsync(string slug);
        Task<ServiceResult<PublicBookResultDto>> BookAsync(string slug, PublicBookDto dto);
        Task<ServiceResult<PublicMyAppointmentsDto>> MyAppointmentsAsync(string slug, string accessToken);
        Task<ServiceResult<PublicMyAppointmentsDto>> LookupAsync(string slug, PublicLookupDto dto);
        Task<ServiceResult<PublicAppointmentDto>> CancelAsync(string slug, Guid appointmentId, PublicCancelDto dto);
    }

    public class PublicService : IPublicService
    {
        private readonly AgendaProDbContext _context;
        private readonly IAppointmentService _appointments;
        private readonly AvailabilityService _availability;
        private readonly ILogger<PublicService> _logger;

        public PublicService(AgendaProDbContext context, IAppointmentService appointments, AvailabilityService availability, ILogger<PublicService> logger)
        {
            _context = context;
            _appointments = appointments;
            _availability = availability;
            _logger = logger;
        }

        public async Task<List<PublicBusinessListItemDto>> ListBusinessesAsync() =>
            await _context.Businesses.AsNoTracking()
                .Where(b => b.Slug != null)
                .OrderBy(b => b.Name)
                .Select(b => new PublicBusinessListItemDto
                {
                    Slug = b.Slug!,
                    Name = b.Name,
                    Description = b.Description,
                    Address = b.Address,
                    ServiceCount = _context.Services.Count(s => s.BusinessId == b.BusinessId && s.Active)
                })
                .ToListAsync();

        public async Task<PublicBusinessDto?> GetBusinessAsync(string slug)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return null;

            var services = await _context.Services.AsNoTracking()
                .Where(s => s.BusinessId == b.BusinessId && s.Active)
                .OrderBy(s => s.Name)
                .Select(s => new PublicServiceDto
                {
                    ServiceId = s.ServiceId,
                    Name = s.Name,
                    Description = s.Description,
                    DurationMin = s.DurationMin,
                    Price = s.Price,
                    CategoryId = s.Category != null && s.Category.Active ? s.CategoryId : null,
                    StaffIds = s.UserServices.Where(us => us.User.Active).Select(us => us.UserId).ToList()
                })
                .ToListAsync();

            var staff = await _context.Users.AsNoTracking()
                .Where(u => u.BusinessId == b.BusinessId && u.Active && u.UserServices.Any(us => us.Service.Active))
                .OrderBy(u => u.Name)
                .Select(u => new PublicStaffDto
                {
                    UserId = u.UserId,
                    Name = u.Name,
                    ServiceIds = u.UserServices.Where(us => us.Service.Active).Select(us => us.ServiceId).ToList()
                })
                .ToListAsync();

            var categories = await _context.Categories.AsNoTracking()
                .Where(c => c.BusinessId == b.BusinessId && c.Active && c.Services.Any(s => s.Active))
                .OrderBy(c => c.Name)
                .Select(c => new PublicCategoryDto { CategoryId = c.CategoryId, Name = c.Name })
                .ToListAsync();

            var schedule = await _context.BusinessSchedules.AsNoTracking()
                .Where(s => s.BusinessId == b.BusinessId)
                .OrderBy(s => s.DayOfWeek)
                .Select(s => new BusinessScheduleDayDto { DayOfWeek = s.DayOfWeek, IsOpen = s.IsOpen, OpenTime = s.OpenTime, CloseTime = s.CloseTime })
                .ToListAsync();

            return new PublicBusinessDto
            {
                BusinessId = b.BusinessId,
                Slug = b.Slug!,
                Name = b.Name,
                Description = b.Description,
                Address = b.Address,
                Phone = b.Phone,
                Email = b.Email,
                WhatsApp = b.WhatsApp,
                Instagram = b.Instagram,
                Facebook = b.Facebook,
                TikTok = b.TikTok,
                CancelLimitHours = b.CancelLimitHours,
                Schedule = schedule,
                Categories = categories,
                // Solo servicios que alguien presta
                Services = services.Where(s => s.StaffIds.Count > 0).ToList(),
                Staff = staff
            };
        }

        public async Task<ServiceResult<PublicBookResultDto>> BookAsync(string slug, PublicBookDto dto)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicBookResultDto>.NotFound("Empresa no encontrada.");

            var digits = new string(dto.Client.Phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 7) return ServiceResult<PublicBookResultDto>.BadRequest("Escribe un teléfono válido.");
            if (dto.Start < DateTime.Now.AddMinutes(10)) return ServiceResult<PublicBookResultDto>.BadRequest("Elige una hora con al menos 10 minutos de anticipación.");

            // "Cualquiera": quien tenga libre esa hora y menos turnos ese día
            var userId = dto.UserId;
            if (userId == null)
            {
                var opciones = await _availability.GetServiceAvailabilityAsync(b.BusinessId, dto.ServiceId, dto.Start.Date);
                var libres = opciones.Data?.Where(p => p.Starts.Any(s => s == dto.Start)).Select(p => p.UserId).ToList() ?? new();
                if (libres.Count == 0) return ServiceResult<PublicBookResultDto>.Conflict("Esa hora ya no está disponible. Elige otra.");

                var dia = dto.Start.Date;
                userId = await _context.Users.Where(u => libres.Contains(u.UserId))
                    .OrderBy(u => u.Appointments.Count(a => a.DateTimeStart >= dia && a.DateTimeStart < dia.AddDays(1) && !AppointmentFreeStatus.All.Contains(a.Status)))
                    .Select(u => (Guid?)u.UserId)
                    .FirstAsync();
            }

            // Cliente recordado por el navegador → se usa ese; si no, se busca/crea por teléfono o correo
            Guid? clientId = null;
            if (!string.IsNullOrWhiteSpace(dto.AccessToken))
                clientId = await _context.Clients.Where(c => c.BusinessId == b.BusinessId && c.AccessToken == dto.AccessToken)
                    .Select(c => (Guid?)c.ClientId).FirstOrDefaultAsync();

            var result = await _appointments.BookAsync(b.BusinessId, null, "Web", new AppointmentBookDto
            {
                ClientId = clientId,
                NewClient = clientId == null ? new NewClientDto { Name = dto.Client.Name.Trim(), Phone = digits, Email = dto.Client.Email?.Trim() } : null,
                UserId = userId.Value,
                ServiceId = dto.ServiceId,
                Start = dto.Start,
                Notes = dto.Notes
            });

            if (!result.Success)
                return result.ErrorType switch
                {
                    ServiceErrorType.Conflict => ServiceResult<PublicBookResultDto>.Conflict("Esa hora acaba de ser tomada. Elige otra."),
                    ServiceErrorType.NotFound => ServiceResult<PublicBookResultDto>.NotFound(result.Error!),
                    _ => ServiceResult<PublicBookResultDto>.BadRequest(result.Error!)
                };

            var client = await _context.Clients.FirstAsync(c => c.ClientId == result.Data!.ClientId);
            client.AccessToken ??= NewToken();
            client.CreatedAt ??= DateTime.UtcNow;
            // Completa el correo si el cliente lo da ahora y antes no lo tenía
            if (string.IsNullOrWhiteSpace(client.Email) && !string.IsNullOrWhiteSpace(dto.Client.Email)) client.Email = dto.Client.Email.Trim().ToLowerInvariant();
            await _context.SaveChangesAsync();

            _logger.LogInformation("Reserva web {Id} en {Slug} para cliente {ClientId}.", result.Data!.AppointmentId, slug, client.ClientId);

            var id = result.Data.AppointmentId;
            var a = (await LoadAsync(b.BusinessId, b.CancelLimitHours, x => x.AppointmentId == id)).First();
            return ServiceResult<PublicBookResultDto>.Ok(new PublicBookResultDto { Appointment = a, AccessToken = client.AccessToken, ClientName = client.Name ?? "" });
        }

        public async Task<ServiceResult<PublicMyAppointmentsDto>> MyAppointmentsAsync(string slug, string accessToken)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicMyAppointmentsDto>.NotFound("Empresa no encontrada.");

            var client = await _context.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.BusinessId == b.BusinessId && c.AccessToken == accessToken && accessToken != "");
            if (client == null) return ServiceResult<PublicMyAppointmentsDto>.Unauthorized("No te reconocimos. Busca tus turnos con tu correo o teléfono.");

            return ServiceResult<PublicMyAppointmentsDto>.Ok(await BuildMyAsync(b, client));
        }

        public async Task<ServiceResult<PublicMyAppointmentsDto>> LookupAsync(string slug, PublicLookupDto dto)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicMyAppointmentsDto>.NotFound("Empresa no encontrada.");

            var contacto = dto.Contact.Trim().ToLowerInvariant();
            var digitos = new string(contacto.Where(char.IsDigit).ToArray());
            var codigo = dto.Code.Trim().ToLowerInvariant();

            var candidatos = await _context.Clients
                .Where(c => c.BusinessId == b.BusinessId &&
                    ((c.Email != null && c.Email == contacto) || (digitos.Length >= 7 && c.Phone != null && c.Phone.Contains(digitos))))
                .ToListAsync();

            foreach (var c in candidatos)
            {
                var ids = await _context.Appointments.Where(a => a.ClientId == c.ClientId).Select(a => a.AppointmentId).ToListAsync();
                if (!ids.Any(id => id.ToString("N").StartsWith(codigo))) continue;

                c.AccessToken ??= NewToken();
                await _context.SaveChangesAsync();
                return ServiceResult<PublicMyAppointmentsDto>.Ok(await BuildMyAsync(b, c));
            }

            return ServiceResult<PublicMyAppointmentsDto>.Unauthorized("No encontramos reservas con esos datos. Revisa el código que te dimos al reservar.");
        }

        public async Task<ServiceResult<PublicAppointmentDto>> CancelAsync(string slug, Guid appointmentId, PublicCancelDto dto)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicAppointmentDto>.NotFound("Empresa no encontrada.");

            var a = await _context.Appointments.Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId && x.BusinessId == b.BusinessId);
            if (a == null || a.Client.AccessToken != dto.AccessToken)
                return ServiceResult<PublicAppointmentDto>.NotFound("Turno no encontrado.");

            if (a.Status != AppointmentStatus.Pending && a.Status != AppointmentStatus.Confirmed && a.Status != AppointmentStatus.PendingOutOfHours)
                return ServiceResult<PublicAppointmentDto>.BadRequest("Este turno ya no se puede cancelar.");
            if (a.DateTimeStart < DateTime.Now.AddHours(b.CancelLimitHours))
                return ServiceResult<PublicAppointmentDto>.BadRequest($"Solo puedes cancelar en línea hasta {b.CancelLimitHours} h antes. Escríbele a la empresa.");

            var nota = "Cancelado por el cliente" + (string.IsNullOrWhiteSpace(dto.Reason) ? "." : $": {dto.Reason.Trim()}");
            var r = await _appointments.ChangeStatusAsync(b.BusinessId, appointmentId, Guid.Empty, new AppointmentStatusDto { Status = AppointmentStatus.Cancelled, Note = nota });
            if (!r.Success) return ServiceResult<PublicAppointmentDto>.BadRequest(r.Error!);

            return ServiceResult<PublicAppointmentDto>.Ok((await LoadAsync(b.BusinessId, b.CancelLimitHours, x => x.AppointmentId == appointmentId)).First());
        }

        // ---------------------------------------------------------------- Auxiliares

        private Task<Business?> FindBusinessAsync(string slug) =>
            _context.Businesses.AsNoTracking().FirstOrDefaultAsync(b => b.Slug == slug.ToLower());

        private async Task<PublicMyAppointmentsDto> BuildMyAsync(Business b, Client client)
        {
            var clientId = client.ClientId;
            var todos = await LoadAsync(b.BusinessId, b.CancelLimitHours, a => a.ClientId == clientId);

            var ahora = DateTime.Now;
            var abiertos = new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.PendingOutOfHours, AppointmentStatus.InProgress };

            return new PublicMyAppointmentsDto
            {
                ClientName = client.Name ?? "",
                AccessToken = client.AccessToken!,
                Upcoming = todos.Where(a => a.End >= ahora && abiertos.Contains(a.Status)).OrderBy(a => a.Start).ToList(),
                Past = todos.Where(a => !(a.End >= ahora && abiertos.Contains(a.Status))).OrderByDescending(a => a.Start).Take(20).ToList(),
                Suggestion = await SuggestAsync(b.BusinessId, client.ClientId)
            };
        }

        // "¿Repetir con la misma persona?": último servicio atendido y el próximo hueco libre
        private async Task<PublicSuggestionDto?> SuggestAsync(Guid businessId, Guid clientId)
        {
            var ultimo = await _context.Appointments.AsNoTracking()
                .Where(a => a.ClientId == clientId && a.Status == AppointmentStatus.Completed
                    && a.Service.Active && a.User.Active
                    && a.User.UserServices.Any(us => us.ServiceId == a.serviceId))
                .OrderByDescending(a => a.DateTimeStart)
                .Select(a => new { a.serviceId, ServiceName = a.Service.Name, a.UserId, StaffName = a.User.Name })
                .FirstOrDefaultAsync();
            if (ultimo == null) return null;

            var huecos = await _availability.GetAvailableSlotsAsync(ultimo.UserId, ultimo.serviceId, DateTime.Today, DateTime.Today.AddDays(13));
            var proximo = huecos.Data?.Days.SelectMany(d => d.Starts).Where(s => s > DateTime.Now.AddHours(1)).OrderBy(s => s).FirstOrDefault();

            return new PublicSuggestionDto
            {
                ServiceId = ultimo.serviceId,
                ServiceName = ultimo.ServiceName,
                UserId = ultimo.UserId,
                StaffName = ultimo.StaffName,
                NextSlot = proximo == default(DateTime) ? null : proximo
            };
        }

        private async Task<List<PublicAppointmentDto>> LoadAsync(Guid businessId, int cancelLimitHours,
            System.Linq.Expressions.Expression<Func<Appointment, bool>> filtro)
        {
            var limite = DateTime.Now.AddHours(cancelLimitHours);
            var lista = await _context.Appointments.AsNoTracking()
                .Where(a => a.BusinessId == businessId)
                .Where(filtro)
                .Select(a => new PublicAppointmentDto
                {
                    AppointmentId = a.AppointmentId,
                    Code = "",
                    Status = a.Status,
                    Start = a.DateTimeStart,
                    End = a.DateTimeEnd,
                    ServiceId = a.serviceId,
                    ServiceName = a.Service.Name,
                    Price = a.Price ?? a.Service.Price,
                    UserId = a.UserId,
                    StaffName = a.User.Name,
                    CancelReason = a.CancelReason,
                    CanCancel = (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.PendingOutOfHours)
                        && a.DateTimeStart >= limite
                })
                .ToListAsync();
            lista.ForEach(a => a.Code = Code(a.AppointmentId));
            return lista;
        }

        public static string Code(Guid id) => id.ToString("N")[..6].ToUpperInvariant();

        private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }
}
