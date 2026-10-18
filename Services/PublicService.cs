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
        Task RequestAccessCodeAsync(string slug, string email);
        Task<ServiceResult<PublicMyAppointmentsDto>> VerifyAccessCodeAsync(string slug, PublicAccessVerifyDto dto);
        Task<ServiceResult<PublicMyAppointmentsDto>> AccessWithPasswordAsync(string slug, PublicAccessPasswordDto dto);
        Task<ServiceResult<bool>> SetPasswordAsync(string slug, string accessToken, string password);
        Task<ServiceResult<PublicAppointmentDto>> CancelAsync(string slug, Guid appointmentId, PublicCancelDto dto);
        Task<ServiceResult<PublicAppointmentDto>> RescheduleAsync(string slug, Guid appointmentId, PublicRescheduleDto dto);
        Task<ServiceResult<PublicAppointmentDto>> ReviewAsync(string slug, Guid appointmentId, PublicReviewCreateDto dto);
    }

    public class PublicService : IPublicService
    {
        private readonly AgendaProDbContext _context;
        private readonly IAppointmentService _appointments;
        private readonly AvailabilityService _availability;
        private readonly ILogger<PublicService> _logger;
        private readonly INotificationService _notify;
        private readonly IValleyService _valley;
        private readonly ICorreoCitasService _correos;

        public PublicService(AgendaProDbContext context, IAppointmentService appointments, AvailabilityService availability, ILogger<PublicService> logger, INotificationService notify, IValleyService valley, ICorreoCitasService correos)
        {
            _context = context;
            _appointments = appointments;
            _availability = availability;
            _logger = logger;
            _notify = notify;
            _valley = valley;
            _correos = correos;
        }

        public async Task<List<PublicBusinessListItemDto>> ListBusinessesAsync() =>
            await _context.Businesses.AsNoTracking()
                .Where(b => b.Slug != null)
                .OrderBy(b => b.Name)
                .Select(b => new PublicBusinessListItemDto
                {
                    Slug = b.Slug!,
                    Name = b.Name,
                    LogoUrl = b.LogoUrl,
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
                    PhotoUrl = u.PhotoUrl,
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

            var visibles = _context.Reviews.AsNoTracking().Where(r => r.BusinessId == b.BusinessId && !r.Hidden);
            var ratingCount = await visibles.CountAsync();
            var ratingAvg = ratingCount > 0 ? await visibles.AverageAsync(r => r.Rating) : 0;
            var ultimas = await visibles.OrderByDescending(r => r.Comment != null).ThenByDescending(r => r.CreatedAt).Take(6)
                .Select(r => new PublicReviewDto
                {
                    Rating = r.Rating,
                    Comment = r.Comment,
                    ClientName = r.Client.Name ?? "Cliente",
                    ServiceName = r.Appointment.Service.Name,
                    StaffName = r.Appointment.User.Name,
                    CreatedAt = r.CreatedAt,
                    Reply = r.Reply
                }).ToListAsync();
            // Privacidad: solo el primer nombre del cliente y del profesional
            ultimas.ForEach(r => { r.ClientName = r.ClientName.Split(' ')[0]; r.StaffName = r.StaffName?.Split(' ')[0]; });

            // Horas Valle: solo las franjas con descuento, para la sugerencia y la explicación
            var valle = await _valley.GetStateAsync(b.BusinessId);
            var ventanas = valle.Cells.Where(c => c.IsValley && c.Pct > 0).ToList();
            var valley = valle.Config.Enabled && ventanas.Count > 0
                ? new PublicValleyDto
                {
                    MinOverlapPct = valle.Config.MinOverlapPct,
                    Proportional = valle.Config.Proportional,
                    DiscountMax = ventanas.Max(c => c.Pct),
                    Days = valle.Days,
                    Windows = ventanas
                }
                : null;

            return new PublicBusinessDto
            {
                Valley = valley,
                RatingAverage = Math.Round(ratingAvg, 1),
                RatingCount = ratingCount,
                Reviews = ultimas,
                BusinessId = b.BusinessId,
                Slug = b.Slug!,
                Name = b.Name,
                LogoUrl = b.LogoUrl,
                CoverUrl = b.CoverUrl,
                Description = b.Description,
                Address = b.Address,
                Phone = b.Phone,
                Email = b.Email,
                WhatsApp = b.WhatsApp,
                Instagram = b.Instagram,
                Facebook = b.Facebook,
                TikTok = b.TikTok,
                CancelLimitHours = b.CancelLimitHours,
                MaxDaysAhead = b.MaxDaysAhead,
                ClosedHolidays = b.ClosedOnHolidays
                    ? FestivosColombia.Entre(DateTime.Today, DateTime.Today.AddDays(b.MaxDaysAhead - 1))
                        .Select(f => new HolidayDto { Date = f.Fecha, Name = f.Nombre }).ToList()
                    : new List<HolidayDto>(),
                MinAdvanceMinutes = b.MinAdvanceMinutes,
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

            if (!dto.AcceptTerms)
                return ServiceResult<PublicBookResultDto>.BadRequest("Acepta los términos y la política de privacidad para reservar.");
            var digits = new string((dto.Client.Phone ?? "").Where(char.IsDigit).ToArray());
            var correo = dto.Client.Email?.Trim().ToLowerInvariant();
            if (digits.Length == 0 && string.IsNullOrEmpty(correo))
                return ServiceResult<PublicBookResultDto>.BadRequest("Escribe tu correo o tu WhatsApp para poder avisarte de tu cita.");
            if (digits.Length > 0 && digits.Length < 7) return ServiceResult<PublicBookResultDto>.BadRequest("Escribe un WhatsApp válido.");
            if (dto.Start < DateTime.Now.AddMinutes(b.MinAdvanceMinutes))
                return ServiceResult<PublicBookResultDto>.BadRequest($"Esta empresa recibe reservas con al menos {TextoAntelacion(b.MinAdvanceMinutes)} de anticipación. Elige una hora más adelante.");
            if (dto.Start.Date > DateTime.Today.AddDays(b.MaxDaysAhead - 1))
                return ServiceResult<PublicBookResultDto>.BadRequest($"Solo se puede reservar hasta {b.MaxDaysAhead} días adelante.");

            // Quién reserva. Un correo o WhatsApp que ya tiene cuenta solo se usa con su sesión
            // (si no, cualquiera podría reservar a nombre de otro y ver o cambiar sus datos).
            var nombre = dto.Client.Name.Trim();
            var tel = digits == "" ? null : digits;
            var email = string.IsNullOrEmpty(correo) ? null : correo;
            var propio = string.IsNullOrWhiteSpace(dto.AccessToken) ? null
                : await _context.Clients.FirstOrDefaultAsync(c => c.BusinessId == b.BusinessId && c.AccessToken == dto.AccessToken);
            var conCuenta = await _context.Clients
                .Where(c => c.BusinessId == b.BusinessId && ((email != null && c.Email == email) || (tel != null && c.Phone == tel)))
                .Select(c => c.ClientId).ToListAsync();
            if (conCuenta.Any(id => id != propio?.ClientId))
                return ServiceResult<PublicBookResultDto>.Ok(new PublicBookResultDto { Requires = "Acceso" });

            if (propio != null)
            {
                var difiere = !MismoNombre(propio.Name, nombre) || (tel != null && propio.Phone != tel) || (email != null && propio.Email != email);
                if (difiere && dto.UpdateMyData == null)
                    return ServiceResult<PublicBookResultDto>.Ok(new PublicBookResultDto
                    {
                        Requires = "ConfirmarDatos",
                        Saved = new PublicClientDto { Name = propio.Name ?? "", Phone = propio.Phone, Email = propio.Email }
                    });
                if (difiere && dto.UpdateMyData == true)
                {
                    propio.Name = nombre;
                    if (tel != null) propio.Phone = tel;
                    if (email != null) propio.Email = email;
                    await _context.SaveChangesAsync();
                }
            }

            // "Cualquiera": quien tenga libre esa hora y menos turnos ese día
            var userId = dto.UserId;
            if (userId == null)
            {
                var opciones = await _availability.GetServiceAvailabilityAsync(b.BusinessId, dto.ServiceId, dto.Start.Date, publico: true);
                var libres = opciones.Data?.Where(p => p.Starts.Any(s => s == dto.Start)).Select(p => p.UserId).ToList() ?? new();
                if (libres.Count == 0) return ServiceResult<PublicBookResultDto>.Conflict("Esa hora ya no está disponible. Elige otra.");

                var dia = dto.Start.Date;
                userId = await _context.Users.Where(u => libres.Contains(u.UserId))
                    .OrderBy(u => u.Appointments.Count(a => a.DateTimeStart >= dia && a.DateTimeStart < dia.AddDays(1) && !AppointmentFreeStatus.All.Contains(a.Status)))
                    .Select(u => (Guid?)u.UserId)
                    .FirstAsync();
            }

            // Con sesión se usa ese cliente; si no, el correo y el WhatsApp son nuevos: se crea
            var clientId = propio?.ClientId;

            var result = await _appointments.BookAsync(b.BusinessId, null, "Web", new AppointmentBookDto
            {
                ClientId = clientId,
                NewClient = clientId == null ? new NewClientDto { Name = nombre, Phone = tel, Email = email } : null,
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
            client.TermsAcceptedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Reserva web {Id} en {Slug} para cliente {ClientId}.", result.Data!.AppointmentId, slug, client.ClientId);

            var id = result.Data.AppointmentId;
            await _correos.EnviarAsync(id, TipoCorreoCita.Confirmacion);
            var a = (await LoadAsync(b.BusinessId, b.CancelLimitHours, x => x.AppointmentId == id)).First();
            return ServiceResult<PublicBookResultDto>.Ok(new PublicBookResultDto { Appointment = a, AccessToken = client.AccessToken, ClientName = client.Name ?? "", HasPassword = client.PasswordHash != null });
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

        private const int MinutosCodigo = 10;
        private const int IntentosCodigo = 5;

        public async Task RequestAccessCodeAsync(string slug, string email)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return;
            var correo = email.Trim().ToLowerInvariant();
            var client = await _context.Clients
                .Where(c => c.BusinessId == b.BusinessId && c.Email == correo)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync();
            if (client == null) { _logger.LogInformation("Código de acceso pedido para un correo sin cliente en {Slug}.", slug); return; }

            // Evita reenvíos seguidos: como máximo un código nuevo por minuto
            var ahora = DateTime.UtcNow;
            if (client.AccessCodeExpiresAt.HasValue && client.AccessCodeExpiresAt.Value > ahora.AddMinutes(MinutosCodigo - 1)) return;

            var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            client.AccessCodeHash = HashCodigo(client.ClientId, codigo);
            client.AccessCodeExpiresAt = ahora.AddMinutes(MinutosCodigo);
            client.AccessCodeAttempts = 0;
            await _context.SaveChangesAsync();

            await _correos.EnviarCodigoAccesoAsync(b, client, codigo, MinutosCodigo);
        }

        public async Task<ServiceResult<PublicMyAppointmentsDto>> VerifyAccessCodeAsync(string slug, PublicAccessVerifyDto dto)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicMyAppointmentsDto>.NotFound("Empresa no encontrada.");

            var correo = dto.Email.Trim().ToLowerInvariant();
            var client = await _context.Clients
                .Where(c => c.BusinessId == b.BusinessId && c.Email == correo && c.AccessCodeHash != null)
                .OrderByDescending(c => c.AccessCodeExpiresAt)
                .FirstOrDefaultAsync();
            if (client == null || client.AccessCodeExpiresAt < DateTime.UtcNow || client.AccessCodeAttempts >= IntentosCodigo)
                return ServiceResult<PublicMyAppointmentsDto>.Unauthorized("El código no es válido o ya venció. Pide uno nuevo.");

            var esperado = Convert.FromHexString(client.AccessCodeHash!);
            var recibido = Convert.FromHexString(HashCodigo(client.ClientId, dto.Code.Trim()));
            if (!CryptographicOperations.FixedTimeEquals(esperado, recibido))
            {
                client.AccessCodeAttempts++;
                await _context.SaveChangesAsync();
                var quedan = IntentosCodigo - client.AccessCodeAttempts;
                return ServiceResult<PublicMyAppointmentsDto>.Unauthorized(quedan > 0
                    ? $"Código incorrecto. Te quedan {quedan} intento(s)."
                    : "Superaste los intentos. Pide un código nuevo.");
            }

            client.AccessCodeHash = null;
            client.AccessCodeExpiresAt = null;
            client.AccessCodeAttempts = 0;
            client.AccessToken ??= NewToken();
            await _context.SaveChangesAsync();
            return ServiceResult<PublicMyAppointmentsDto>.Ok(await BuildMyAsync(b, client));
        }

        public async Task<ServiceResult<PublicMyAppointmentsDto>> AccessWithPasswordAsync(string slug, PublicAccessPasswordDto dto)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicMyAppointmentsDto>.NotFound("Empresa no encontrada.");

            var contacto = dto.Contact.Trim().ToLowerInvariant();
            var digitos = new string(contacto.Where(char.IsDigit).ToArray());
            var candidatos = await _context.Clients
                .Where(c => c.BusinessId == b.BusinessId && c.PasswordHash != null &&
                    ((c.Email != null && c.Email == contacto) || (!contacto.Contains('@') && digitos.Length >= 7 && c.Phone != null && c.Phone.Contains(digitos))))
                .ToListAsync();

            var client = candidatos.FirstOrDefault(c => BCrypt.Net.BCrypt.Verify(dto.Password, c.PasswordHash));
            if (client == null)
                return ServiceResult<PublicMyAppointmentsDto>.Unauthorized("Correo, celular o contraseña incorrectos.");

            client.AccessToken ??= NewToken();
            await _context.SaveChangesAsync();
            return ServiceResult<PublicMyAppointmentsDto>.Ok(await BuildMyAsync(b, client));
        }

        // Crear o cambiar la contraseña con la sesión del navegador (AccessToken)
        public async Task<ServiceResult<bool>> SetPasswordAsync(string slug, string accessToken, string password)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<bool>.NotFound("Empresa no encontrada.");

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.BusinessId == b.BusinessId && c.AccessToken == accessToken && accessToken != "");
            if (client == null) return ServiceResult<bool>.Unauthorized("No te reconocimos. Vuelve a entrar a tus turnos.");

            client.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            await _context.SaveChangesAsync();
            return ServiceResult<bool>.Ok(true);
        }

        // Compara nombres sin importar mayúsculas ni espacios de más
        private static bool MismoNombre(string? a, string b) =>
            string.Equals(string.Join(' ', (a ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries)),
                string.Join(' ', b.Split(' ', StringSplitOptions.RemoveEmptyEntries)), StringComparison.OrdinalIgnoreCase);

        private static string HashCodigo(Guid clientId, string codigo) =>
            Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{clientId:N}:{codigo}")));

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

        // Otra hora con la misma persona: reglas de la reserva sin sesión y el límite de cancelación
        public async Task<ServiceResult<PublicAppointmentDto>> RescheduleAsync(string slug, Guid appointmentId, PublicRescheduleDto dto)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicAppointmentDto>.NotFound("Empresa no encontrada.");

            var a = await _context.Appointments.AsNoTracking().Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId && x.BusinessId == b.BusinessId);
            if (a == null || a.Client.AccessToken != dto.AccessToken)
                return ServiceResult<PublicAppointmentDto>.NotFound("Turno no encontrado.");

            if (a.Status != AppointmentStatus.Pending && a.Status != AppointmentStatus.Confirmed && a.Status != AppointmentStatus.PendingOutOfHours)
                return ServiceResult<PublicAppointmentDto>.BadRequest("Este turno ya no se puede reprogramar.");
            if (a.DateTimeStart < DateTime.Now.AddHours(b.CancelLimitHours))
                return ServiceResult<PublicAppointmentDto>.BadRequest($"Solo puedes reprogramar en línea hasta {b.CancelLimitHours} h antes. Escríbele a la empresa.");
            if (dto.Start == a.DateTimeStart)
                return ServiceResult<PublicAppointmentDto>.BadRequest("Elige una hora distinta a la actual.");
            if (dto.Start < DateTime.Now.AddMinutes(b.MinAdvanceMinutes))
                return ServiceResult<PublicAppointmentDto>.BadRequest($"Esta empresa recibe reservas con al menos {TextoAntelacion(b.MinAdvanceMinutes)} de anticipación. Elige una hora más adelante.");
            if (dto.Start.Date > DateTime.Today.AddDays(b.MaxDaysAhead - 1))
                return ServiceResult<PublicAppointmentDto>.BadRequest($"Solo se puede reservar hasta {b.MaxDaysAhead} días adelante.");

            var r = await _appointments.RescheduleAsync(b.BusinessId, appointmentId, Guid.Empty,
                new AppointmentRescheduleDto { Start = dto.Start, Note = "Reprogramado por el cliente." });
            if (!r.Success)
                return r.ErrorType == ServiceErrorType.Conflict
                    ? ServiceResult<PublicAppointmentDto>.Conflict("Esa hora acaba de ser tomada. Elige otra.")
                    : ServiceResult<PublicAppointmentDto>.BadRequest(r.Error!);

            _logger.LogInformation("Turno {Id} reprogramado por el cliente en {Slug}.", appointmentId, slug);
            return ServiceResult<PublicAppointmentDto>.Ok((await LoadAsync(b.BusinessId, b.CancelLimitHours, x => x.AppointmentId == appointmentId)).First());
        }

        public async Task<ServiceResult<PublicAppointmentDto>> ReviewAsync(string slug, Guid appointmentId, PublicReviewCreateDto dto)
        {
            var b = await FindBusinessAsync(slug);
            if (b == null) return ServiceResult<PublicAppointmentDto>.NotFound("Empresa no encontrada.");

            var a = await _context.Appointments.Include(x => x.Client)
                .FirstOrDefaultAsync(x => x.AppointmentId == appointmentId && x.BusinessId == b.BusinessId);
            if (a == null || a.Client.AccessToken != dto.AccessToken)
                return ServiceResult<PublicAppointmentDto>.NotFound("Turno no encontrado.");
            if (a.Status != AppointmentStatus.Completed)
                return ServiceResult<PublicAppointmentDto>.BadRequest("Solo puedes calificar una atención que ya se realizó.");
            if (await _context.Reviews.AnyAsync(r => r.AppointmentId == appointmentId))
                return ServiceResult<PublicAppointmentDto>.Conflict("Ya calificaste esta atención. ¡Gracias!");

            _context.Reviews.Add(new Review
            {
                ReviewId = Guid.NewGuid(),
                BusinessId = b.BusinessId,
                AppointmentId = appointmentId,
                ClientId = a.ClientId,
                UserId = a.UserId,
                ServiceId = a.serviceId,
                Rating = dto.Rating,
                Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
                CreatedAt = DateTime.UtcNow
            });
            await _notifyReview(b.BusinessId, a, dto.Rating);
            await _context.SaveChangesAsync();

            return ServiceResult<PublicAppointmentDto>.Ok((await LoadAsync(b.BusinessId, b.CancelLimitHours, x => x.AppointmentId == appointmentId)).First());
        }

        private async Task _notifyReview(Guid businessId, Appointment a, int rating)
        {
            var estrellas = new string('★', rating) + new string('☆', 5 - rating);
            await _notify.NotifyAsync(businessId, (await _notify.OwnersAsync(businessId)).Append(a.UserId), NotificationTypes.NewReview,
                $"Nueva reseña {estrellas}", $"{a.Client.Name} calificó su visita.", "resenas", a.AppointmentId);
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
                Email = client.Email,
                Phone = client.Phone,
                HasPassword = client.PasswordHash != null,
                Upcoming = todos.Where(a => a.End >= ahora && abiertos.Contains(a.Status)).OrderBy(a => a.Start).ToList(),
                Past = todos.Where(a => !(a.End >= ahora && abiertos.Contains(a.Status))).OrderByDescending(a => a.Start).Take(20).ToList(),
                Suggestion = await SuggestAsync(b.BusinessId, client.ClientId)
            };
        }

        private static string TextoAntelacion(int min) => min switch
        {
            >= 1440 when min % 1440 == 0 => min == 1440 ? "1 día" : $"{min / 1440} días",
            >= 60 => min == 60 ? "1 hora" : $"{min / 60.0:0.#} horas",
            _ => $"{min} minutos"
        };

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

            var huecos = await _availability.GetAvailableSlotsAsync(ultimo.UserId, ultimo.serviceId, DateTime.Today, DateTime.Today.AddDays(13), publico: true);
            var proximo = huecos.Data?.Days.SelectMany(d => d.Starts).OrderBy(s => s).FirstOrDefault();

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
                    ListPrice = a.ListPrice,
                    ValleyDiscountPct = a.ValleyDiscountPct,
                    UserId = a.UserId,
                    StaffName = a.User.Name,
                    CancelReason = a.CancelReason,
                    CanCancel = (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.PendingOutOfHours)
                        && a.DateTimeStart >= limite,
                    CanReschedule = (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.PendingOutOfHours)
                        && a.DateTimeStart >= limite,
                    MyRating = _context.Reviews.Where(r => r.AppointmentId == a.AppointmentId).Select(r => (int?)r.Rating).FirstOrDefault(),
                    CanReview = a.Status == AppointmentStatus.Completed && !_context.Reviews.Any(r => r.AppointmentId == a.AppointmentId)
                })
                .ToListAsync();
            lista.ForEach(a => a.Code = Code(a.AppointmentId));
            return lista;
        }

        public static string Code(Guid id) => id.ToString("N")[..6].ToUpperInvariant();

        private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    }
}
