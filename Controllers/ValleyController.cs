using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    // Horas Valle de la empresa del usuario: configuración + franjas calculadas (vista previa).
    // Requiere "Configurar Horas Valle" (el dueño siempre).
    [ApiController]
    [Route("api/valley")]
    [Authorize(Policy = "valley.manage")]
    public class ValleyController : ControllerBase
    {
        private readonly IValleyService _valley;
        private readonly ITenantGuard _tenant;

        public ValleyController(IValleyService valley, ITenantGuard tenant)
        {
            _valley = valley;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult<ValleyStateDto>> Get() => Ok(await _valley.GetStateAsync(_tenant.BusinessId));

        [HttpPut]
        public async Task<ActionResult<ValleyStateDto>> Save([FromBody] ValleyConfigDto dto)
        {
            var result = await _valley.SaveConfigAsync(_tenant.BusinessId, dto);
            return result.Success ? Ok(result.Data) : BadRequest(result.Error);
        }
    }
}
