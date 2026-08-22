using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<ActionResult<AvailabilityResponseDto>> GetAvailability(
            [FromQuery] Guid userId,
            [FromQuery] Guid? serviceId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            var result = await _availabilityService.GetAvailableSlotsAsync(userId, serviceId, startDate, endDate);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                _ => Ok(result.Data)
            };
        }
    }
}
