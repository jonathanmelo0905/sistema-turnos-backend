using AgendaApp.Dtos;
using AgendaApp.Middleware;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AvailabilityController : ControllerBase
    {
        private readonly AvailabilityService _availabilityService;

        public AvailabilityController(AvailabilityService availabilityService)
        {
            _availabilityService = availabilityService;
        }

        // GET /api/availability?userId={guid}&serviceId={guid?}&startDate={date}&endDate={date}
        // Público: el cliente sin cuenta necesita consultar disponibilidad antes de agendar (igual que ServicesController.GetServices)
        [HttpGet]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.PublicoLectura)]
        public async Task<ActionResult<AvailabilityResponseDto>> GetAvailability(
            [FromQuery] Guid userId,
            [FromQuery] Guid? serviceId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            // Sin sesión = cliente: se aplican antelación mínima y días hacia adelante
            var publico = User.Identity?.IsAuthenticated != true;
            var result = await _availabilityService.GetAvailableSlotsAsync(userId, serviceId, startDate, endDate, publico);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                _ => Ok(result.Data)
            };
        }

        // GET /api/availability/service?businessId=&serviceId=&date=
        // Quién presta el servicio y a qué horas puede empezar ese día (reserva con "cualquiera")
        [HttpGet("service")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.PublicoLectura)]
        public async Task<ActionResult<List<StaffAvailabilityDto>>> GetServiceAvailability(
            [FromQuery] Guid businessId, [FromQuery] Guid serviceId, [FromQuery] DateTime date)
        {
            var publico = User.Identity?.IsAuthenticated != true;
            var result = await _availabilityService.GetServiceAvailabilityAsync(businessId, serviceId, date, publico);
            return result.ErrorType == ServiceErrorType.NotFound ? NotFound(result.Error) : Ok(result.Data);
        }
    }
}
