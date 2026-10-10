using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServicesController : ControllerBase
    {
        private readonly IServiceService _serviceService;
        private readonly ITenantGuard _tenant;

        public ServicesController(IServiceService serviceService, ITenantGuard tenant)
        {
            _serviceService = serviceService;
            _tenant = tenant;
        }

        // Público: catálogo visible para clientes finales sin login (solo activos).
        // El personal del propio negocio ve también los inactivos.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetServices([FromQuery] Guid businessId)
        {
            var services = await _serviceService.GetServicesAsync(businessId);
            if (!_tenant.IsOwnBusiness(businessId))
                services = services.Where(s => s.Active).ToList();
            return Ok(services);
        }

        [HttpPost]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> CreateService([FromBody] ServiceCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            dto.BusinessId = _tenant.BusinessId;
            if (dto.CategoryId != null && !await _tenant.CategoryInBusinessAsync(dto.CategoryId.Value))
                return BadRequest("La categoría no pertenece a tu empresa.");

            var result = await _serviceService.CreateServiceAsync(dto);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => CreatedAtAction(nameof(GetServices), new { businessId = dto.BusinessId }, result.Data)
            };
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> UpdateService(Guid id, [FromBody] ServiceUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await _tenant.ServiceInBusinessAsync(id))
                return NotFound("Servicio no encontrado.");

            if (dto.CategoryId != null && !await _tenant.CategoryInBusinessAsync(dto.CategoryId.Value))
                return BadRequest("La categoría no pertenece a tu empresa.");

            var result = await _serviceService.UpdateServiceAsync(id, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> DeactivateService(Guid id)
        {
            if (!await _tenant.ServiceInBusinessAsync(id))
                return NotFound("Servicio no encontrado.");

            var result = await _serviceService.DeactivateServiceAsync(id);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { message = "Servicio desactivado correctamente." })
            };
        }
    }
}
