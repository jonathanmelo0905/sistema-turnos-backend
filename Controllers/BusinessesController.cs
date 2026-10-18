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
        private readonly IBusinessImageService _imagenes;

        public BusinessesController(IBusinessService businessService, IBusinessScheduleService businessScheduleService, ITenantGuard tenant, IBusinessImageService imagenes)
        {
            _businessService = businessService;
            _businessScheduleService = businessScheduleService;
            _tenant = tenant;
            _imagenes = imagenes;
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

        // GET: api/businesses/{id}/setup — pasos de la guía "Configura tu empresa"
        [HttpGet("{businessId:guid}/setup")]
        [Authorize(Policy = "business.edit")]
        public async Task<ActionResult<BusinessSetupDto>> Setup(Guid businessId)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var result = await _businessService.GetSetupAsync(businessId);
            return result.Success ? Ok(result.Data) : NotFound(result.Error);
        }

        // PUT: api/businesses/{id}/visibility { published } — mostrar u ocultar la empresa a los clientes
        [HttpPut("{businessId:guid}/visibility")]
        [Authorize(Policy = "business.edit")]
        public async Task<ActionResult<BusinessResponseDto>> SetVisibility(Guid businessId, [FromBody] BusinessVisibilityDto dto)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var result = await _businessService.SetVisibilityAsync(businessId, dto.Published);
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                _ => Ok(result.Data)
            };
        }

        // POST: api/businesses/{id}/logo y /cover — multipart "file" (JPG, PNG o WEBP; logo 2 MB, portada 5 MB)
        [HttpPost("{businessId:guid}/logo")]
        [Authorize(Policy = "business.edit")]
        [RequestSizeLimit(BusinessImageService.LogoMaximo + 64 * 1024)]
        public Task<IActionResult> UploadLogo(Guid businessId, IFormFile file) => SubirImagen(businessId, BusinessImage.Logo, file);

        [HttpPost("{businessId:guid}/cover")]
        [Authorize(Policy = "business.edit")]
        [RequestSizeLimit(BusinessImageService.PortadaMaxima + 64 * 1024)]
        public Task<IActionResult> UploadCover(Guid businessId, IFormFile file) => SubirImagen(businessId, BusinessImage.Cover, file);

        [HttpDelete("{businessId:guid}/logo")]
        [Authorize(Policy = "business.edit")]
        public Task<IActionResult> DeleteLogo(Guid businessId) => QuitarImagen(businessId, BusinessImage.Logo);

        [HttpDelete("{businessId:guid}/cover")]
        [Authorize(Policy = "business.edit")]
        public Task<IActionResult> DeleteCover(Guid businessId) => QuitarImagen(businessId, BusinessImage.Cover);

        private async Task<IActionResult> SubirImagen(Guid businessId, BusinessImage tipo, IFormFile file)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var result = await _imagenes.SubirAsync(businessId, tipo, file);
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { url = result.Data })
            };
        }

        private async Task<IActionResult> QuitarImagen(Guid businessId, BusinessImage tipo)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            return await _imagenes.QuitarAsync(businessId, tipo) ? NoContent() : NotFound();
        }

        // GET: api/businesses/{id}/holidays?from=yyyy-MM-dd&days=365 — festivos de Colombia del periodo
        [HttpGet("{businessId:guid}/holidays")]
        [Authorize]
        public ActionResult<List<HolidayDto>> GetHolidays(Guid businessId, [FromQuery] DateTime? from, [FromQuery] int days = 365)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            var desde = (from ?? DateTime.Today).Date;
            return Ok(FestivosColombia.Entre(desde, desde.AddDays(Math.Clamp(days, 1, 730)))
                .Select(f => new HolidayDto { Date = f.Fecha, Name = f.Nombre }).ToList());
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
