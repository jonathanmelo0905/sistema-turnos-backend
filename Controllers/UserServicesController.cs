using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgendaApp.Dtos;
using AgendaApp.Services;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/users/{userId}/services")]
    [Authorize(Policy = "collaborators.manage")]
    public class UserServicesController : ControllerBase
    {
        private readonly IUserServicesService _userServicesService;

        public UserServicesController(IUserServicesService userServicesService)
        {
            _userServicesService = userServicesService;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserServices(Guid userId)
        {
            var services = await _userServicesService.GetUserServicesAsync(userId);
            return Ok(services);
        }

        [HttpPost]
        public async Task<IActionResult> AssignServices(Guid userId, [FromBody] AssignUserServicesDto dto)
        {
            var result = await _userServicesService.AssignServicesAsync(userId, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                _ => Ok(new { message = "Servicios asignados correctamente." })
            };
        }

        [HttpDelete("{serviceId}")]
        public async Task<IActionResult> RemoveUserService(Guid userId, Guid serviceId)
        {
            var result = await _userServicesService.RemoveUserServiceAsync(userId, serviceId);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok("Servicio eliminado correctamente del usuario.")
            };
        }
    }
}
