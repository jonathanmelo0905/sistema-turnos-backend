using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    // Servicios que presta cada colaborador. El propio colaborador gestiona los suyos;
    // para los de otros se necesita collaborators.manage (o ser Owner).
    [ApiController]
    [Route("api/users/{userId:guid}/services")]
    [Authorize]
    public class UserServicesController : ControllerBase
    {
        private readonly IUserServicesService _userServicesService;
        private readonly ITenantGuard _tenant;

        public UserServicesController(IUserServicesService userServicesService, ITenantGuard tenant)
        {
            _userServicesService = userServicesService;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserServices(Guid userId)
        {
            if (!await _tenant.CanManageUserAsync(userId))
                return Forbid();

            return Ok(await _userServicesService.GetUserServicesAsync(userId));
        }

        [HttpPost]
        public async Task<IActionResult> AssignServices(Guid userId, [FromBody] AssignUserServicesDto dto)
        {
            if (!await _tenant.CanManageUserAsync(userId))
                return Forbid();

            foreach (var serviceId in dto.ServiceIds)
                if (!await _tenant.ServiceInBusinessAsync(serviceId))
                    return BadRequest("Uno de los servicios no pertenece a tu empresa.");

            var result = await _userServicesService.AssignServicesAsync(userId, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                _ => Ok(new { message = "Servicios asignados correctamente." })
            };
        }

        [HttpDelete("{serviceId:guid}")]
        public async Task<IActionResult> RemoveUserService(Guid userId, Guid serviceId)
        {
            if (!await _tenant.CanManageUserAsync(userId))
                return Forbid();

            var result = await _userServicesService.RemoveUserServiceAsync(userId, serviceId);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { message = "Servicio quitado correctamente." })
            };
        }
    }
}
