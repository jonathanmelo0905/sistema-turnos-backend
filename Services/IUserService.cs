using AgendaApp.Dtos;

namespace AgendaApp.Services
{
    public interface IUserService
    {
        Task<List<UserResponseDto>> GetUsersByBusinessAsync(Guid businessId);
        Task<UserResponseDto?> GetUserAsync(Guid id);
        Task<ServiceResult<UserResponseDto>> CreateUserAsync(CreateUserDto dto);
        Task<ServiceResult<bool>> UpdateUserAsync(Guid id, UpdateUserDto dto);
        Task<bool> DeleteUserAsync(Guid id);
    }
}
