using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface ICategoryService
    {
        Task<List<CategoryResponseDto>> GetCategoriesAsync(Guid businessId, bool onlyActive);
        Task<ServiceResult<CategoryResponseDto>> CreateCategoryAsync(Guid businessId, CategorySaveDto dto);
        Task<ServiceResult<CategoryResponseDto>> UpdateCategoryAsync(Guid categoryId, CategorySaveDto dto);
        Task<ServiceResult<bool>> DeactivateCategoryAsync(Guid categoryId);
    }
}
