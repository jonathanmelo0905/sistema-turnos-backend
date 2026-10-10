using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BusinessesController : ControllerBase
    {
        private readonly IBusinessService _businessService;
        private readonly IBusinessScheduleService _businessScheduleService;
        private readonly ITenantGuard _tenant;

        public BusinessesController(IBusinessService businessService, IBusinessScheduleService businessScheduleService, ITenantGuard tenant)
        {
            _businessService = businessService;
            _businessScheduleService = businessScheduleService;
            _tenant = tenant;
        }

        [HttpGet("{businessId:guid}")]
        [Authorize]
        public async Task<ActionResult<BusinessResponseDto>> Get(Guid businessId)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var result = await _businessService.GetBusinessAsync(businessId);
            if (result.ErrorType == ServiceErrorType.NotFound)
                return NotFound(new { message = result.Error });

            return Ok(result.Data);
        }

        // Perfil público, contacto y políticas de la empresa
        [HttpPut("{businessId:guid}")]
        [Authorize(Policy = "business.edit")]
        public async Task<ActionResult<BusinessResponseDto>> Update(Guid businessId, [FromBody] BusinessUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var result = await _businessService.UpdateBusinessAsync(businessId, dto);
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpGet("{businessId:guid}/schedule")]
        [Authorize]
        public async Task<ActionResult<BusinessScheduleResponseDto>> GetSchedule(Guid businessId)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var result = await _businessScheduleService.GetScheduleAsync(businessId);

            if (result.ErrorType == ServiceErrorType.NotFound)
                return NotFound(result.Error);

            return Ok(result.Data);
        }

        [HttpPut("{businessId:guid}/schedule")]
        [Authorize(Policy = "business.edit")]
        public async Task<ActionResult<BusinessScheduleResponseDto>> UpdateSchedule(Guid businessId, [FromBody] BusinessScheduleUpdateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var result = await _businessScheduleService.UpdateScheduleAsync(businessId, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                _ => Ok(result.Data)
            };
        }
    }
}
