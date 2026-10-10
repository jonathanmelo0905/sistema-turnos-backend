using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly AgendaProDbContext _context;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(AgendaProDbContext context, ILogger<CategoryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<CategoryResponseDto>> GetCategoriesAsync(Guid businessId, bool onlyActive)
        {
            return await _context.Categories
                .Where(c => c.BusinessId == businessId && (!onlyActive || c.Active))
                .OrderBy(c => c.Name)
                .Select(c => new CategoryResponseDto
                {
                    CategoryId = c.CategoryId,
                    Name = c.Name,
                    Description = c.Description,
                    Active = c.Active,
                    ServiceCount = c.Services.Count(s => s.Active)
                })
                .ToListAsync();
        }

        public async Task<ServiceResult<CategoryResponseDto>> CreateCategoryAsync(Guid businessId, CategorySaveDto dto)
        {
            var name = dto.Name.Trim();
            if (await NameTakenAsync(businessId, name, null))
                return ServiceResult<CategoryResponseDto>.Conflict("Ya existe una categoría con ese nombre.");

            var category = new Category
            {
                CategoryId = Guid.NewGuid(),
                BusinessId = businessId,
                Name = name,
                Description = Clean(dto.Description),
                Active = dto.Active
            };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Categoría {CategoryId} creada para negocio {BusinessId}.", category.CategoryId, businessId);

            return ServiceResult<CategoryResponseDto>.Ok(ToDto(category, 0));
        }

        public async Task<ServiceResult<CategoryResponseDto>> UpdateCategoryAsync(Guid categoryId, CategorySaveDto dto)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category == null)
                return ServiceResult<CategoryResponseDto>.NotFound("Categoría no encontrada.");

            var name = dto.Name.Trim();
            if (await NameTakenAsync(category.BusinessId, name, categoryId))
                return ServiceResult<CategoryResponseDto>.Conflict("Ya existe una categoría con ese nombre.");

            category.Name = name;
            category.Description = Clean(dto.Description);
            category.Active = dto.Active;
            await _context.SaveChangesAsync();

            var count = await _context.Services.CountAsync(s => s.CategoryId == categoryId && s.Active);
            return ServiceResult<CategoryResponseDto>.Ok(ToDto(category, count));
        }

        public async Task<ServiceResult<bool>> DeactivateCategoryAsync(Guid categoryId)
        {
            var category = await _context.Categories.FindAsync(categoryId);
            if (category == null)
                return ServiceResult.NotFound("Categoría no encontrada.");

            category.Active = false;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Categoría {CategoryId} desactivada.", categoryId);
            return ServiceResult.Ok();
        }

        private Task<bool> NameTakenAsync(Guid businessId, string name, Guid? exceptId) =>
            _context.Categories.AnyAsync(c => c.BusinessId == businessId && c.Name == name && c.CategoryId != exceptId);

        private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static CategoryResponseDto ToDto(Category c, int count) => new()
        {
            CategoryId = c.CategoryId,
            Name = c.Name,
            Description = c.Description,
            Active = c.Active,
            ServiceCount = count
        };
    }
}
