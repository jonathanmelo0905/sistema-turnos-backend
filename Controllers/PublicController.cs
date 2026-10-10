using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    // API para el front de clientes (sin iniciar sesión).
    [ApiController]
    [Route("api/public")]
    [AllowAnonymous]
    public class PublicController : ControllerBase
    {
        private readonly IPublicService _public;

        public PublicController(IPublicService publicService)
        {
            _public = publicService;
        }

        // Directorio de empresas
        [HttpGet("businesses")]
        public async Task<ActionResult<List<PublicBusinessListItemDto>>> List() => Ok(await _public.ListBusinessesAsync());

        // Página de la empresa: datos, horario, servicios y equipo
        [HttpGet("businesses/{slug}")]
        public async Task<ActionResult<PublicBusinessDto>> Get(string slug)
        {
            var b = await _public.GetBusinessAsync(slug);
            return b == null ? NotFound("No encontramos esta empresa.") : Ok(b);
        }

        // Reserva como invitado
        [HttpPost("businesses/{slug}/book")]
        public async Task<ActionResult<PublicBookResultDto>> Book(string slug, [FromBody] PublicBookDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.BookAsync(slug, dto));
        }

        // Mis turnos con el código que guarda el navegador
        [HttpGet("businesses/{slug}/me")]
        public async Task<ActionResult<PublicMyAppointmentsDto>> Me(string slug, [FromHeader(Name = "X-Client-Token")] string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return Unauthorized("Falta el código del cliente.");
            return Map(await _public.MyAppointmentsAsync(slug, token));
        }

        // Mis turnos con correo/teléfono + código de reserva
        [HttpPost("businesses/{slug}/lookup")]
        public async Task<ActionResult<PublicMyAppointmentsDto>> Lookup(string slug, [FromBody] PublicLookupDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.LookupAsync(slug, dto));
        }

        [HttpPost("businesses/{slug}/appointments/{id:guid}/cancel")]
        public async Task<ActionResult<PublicAppointmentDto>> Cancel(string slug, Guid id, [FromBody] PublicCancelDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.CancelAsync(slug, id, dto));
        }

        private ActionResult<T> Map<T>(ServiceResult<T> r) => r.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(r.Error),
            ServiceErrorType.BadRequest => BadRequest(r.Error),
            ServiceErrorType.Conflict => Conflict(r.Error),
            ServiceErrorType.Unauthorized => Unauthorized(r.Error),
            _ => Ok(r.Data)
        };
    }
}
