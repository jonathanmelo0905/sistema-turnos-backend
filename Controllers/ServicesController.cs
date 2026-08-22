using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgendaApp.Dtos;
using AgendaApp.Services;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly IServiceService _serviceService;

        public ServicesController(IServiceService serviceService)
        {
            _serviceService = serviceService;
        }

        // Público: catálogo de servicios visible para clientes finales sin login
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetServices([FromQuery] Guid businessId)
        {
            var services = await _serviceService.GetServicesAsync(businessId);
            return Ok(services);
        }

        [HttpPost]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> CreateService([FromBody] ServiceCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _serviceService.CreateServiceAsync(dto);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => CreatedAtAction(nameof(GetServices), new { businessId = dto.BusinessId }, result.Data)
            };
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> UpdateService(Guid id, [FromBody] ServiceUpdateDto dto)
        {
            var result = await _serviceService.UpdateServiceAsync(id, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> DeactivateService(Guid id)
        {
            var result = await _serviceService.DeactivateServiceAsync(id);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok("Servicio desactivado correctamente.")
            };
        }
    }
}
