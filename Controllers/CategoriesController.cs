using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ITenantGuard _tenant;

        public CategoriesController(ICategoryService categoryService, ITenantGuard tenant)
        {
            _categoryService = categoryService;
            _tenant = tenant;
        }

        // Público (página de reservas): solo activas. El personal del negocio ve todas.
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories([FromQuery] Guid businessId)
        {
            var own = _tenant.IsOwnBusiness(businessId);
            return Ok(await _categoryService.GetCategoriesAsync(businessId, onlyActive: !own));
        }

        [HttpPost]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> CreateCategory([FromBody] CategorySaveDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _categoryService.CreateCategoryAsync(_tenant.BusinessId, dto);
            return result.ErrorType switch
            {
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpPut("{id:guid}")]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategorySaveDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await _tenant.CategoryInBusinessAsync(id))
                return NotFound("Categoría no encontrada.");

            var result = await _categoryService.UpdateCategoryAsync(id, dto);
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "services.manage")]
        public async Task<IActionResult> DeactivateCategory(Guid id)
        {
            if (!await _tenant.CategoryInBusinessAsync(id))
                return NotFound("Categoría no encontrada.");

            await _categoryService.DeactivateCategoryAsync(id);
            return Ok(new { message = "Categoría desactivada correctamente." });
        }
    }
}
