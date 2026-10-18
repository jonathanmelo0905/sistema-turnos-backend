using AgendaApp.Dtos;
using AgendaApp.Middleware;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgendaApp.Controllers
{
    // API para el front de clientes (sin iniciar sesión).
    [ApiController]
    [Route("api/public")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicoLectura)]
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
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
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
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<ActionResult<PublicMyAppointmentsDto>> Lookup(string slug, [FromBody] PublicLookupDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.LookupAsync(slug, dto));
        }

        // Pide un código de acceso por correo. Siempre 200: no revela si el correo es de un cliente
        [HttpPost("businesses/{slug}/access/request")]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<IActionResult> AccessRequest(string slug, [FromBody] PublicAccessRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            await _public.RequestAccessCodeAsync(slug, dto.Email);
            return Ok(new { message = "Si ese correo tiene turnos aquí, te enviamos un código." });
        }

        // Verifica el código y devuelve mis turnos con el AccessToken del cliente
        [HttpPost("businesses/{slug}/access/verify")]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<ActionResult<PublicMyAppointmentsDto>> AccessVerify(string slug, [FromBody] PublicAccessVerifyDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.VerifyAccessCodeAsync(slug, dto));
        }

        // Entrar con correo o celular + contraseña (opcional, creada desde Mis turnos)
        [HttpPost("businesses/{slug}/access/password")]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<ActionResult<PublicMyAppointmentsDto>> AccessPassword(string slug, [FromBody] PublicAccessPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.AccessWithPasswordAsync(slug, dto));
        }

        // Crear o cambiar la contraseña con el código que guarda el navegador
        [HttpPost("businesses/{slug}/me/password")]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<IActionResult> SetPassword(string slug, [FromHeader(Name = "X-Client-Token")] string? token, [FromBody] PublicSetPasswordDto dto)
        {
            if (string.IsNullOrWhiteSpace(token)) return Unauthorized("Falta el código del cliente.");
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var r = await _public.SetPasswordAsync(slug, token, dto.Password);
            if (r.ErrorType == ServiceErrorType.NotFound) return NotFound(r.Error);
            if (r.ErrorType == ServiceErrorType.Unauthorized) return Unauthorized(r.Error);
            return Ok(new { message = "Listo: ya puedes entrar con tu contraseña." });
        }

        [HttpPost("businesses/{slug}/appointments/{id:guid}/cancel")]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<ActionResult<PublicAppointmentDto>> Cancel(string slug, Guid id, [FromBody] PublicCancelDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.CancelAsync(slug, id, dto));
        }

        // Calificar una atención (solo turnos atendidos, una vez)
        [HttpPost("businesses/{slug}/appointments/{id:guid}/review")]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<ActionResult<PublicAppointmentDto>> Review(string slug, Guid id, [FromBody] PublicReviewCreateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Map(await _public.ReviewAsync(slug, id, dto));
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
