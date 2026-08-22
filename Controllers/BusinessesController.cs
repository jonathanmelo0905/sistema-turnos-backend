using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgendaApp.Dtos;
using AgendaApp.Services;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BusinessesController : ControllerBase
    {
        private readonly IBusinessService _businessService;
        private readonly IBusinessScheduleService _businessScheduleService;

        public BusinessesController(IBusinessService businessService, IBusinessScheduleService businessScheduleService)
        {
            _businessService = businessService;
            _businessScheduleService = businessScheduleService;
        }

        [HttpGet("{businessId:Guid}")]
        [Authorize]
        public async Task<ActionResult<BusinessResponseDto>> Get(Guid businessId)
        {
            var result = await _businessService.GetBusinessAsync(businessId);

            if (result.ErrorType == ServiceErrorType.NotFound)
                return NotFound(new { message = result.Error });

            return Ok(result.Data);
        }

        [HttpGet("{businessId:guid}/schedule")]
        [Authorize]
        public async Task<ActionResult<BusinessScheduleResponseDto>> GetSchedule(Guid businessId)
        {
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
