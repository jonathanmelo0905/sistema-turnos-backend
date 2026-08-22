using System.Security.Claims;
using AgendaApp.Context;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly IPermissionService _permissionService;

        public PermissionsController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        // GET /api/permissions — catálogo estático, cualquier usuario autenticado
        [HttpGet("/api/permissions")]
        public ActionResult<List<PermissionDto>> GetCatalog()
        {
            return Ok(_permissionService.GetCatalog());
        }

        // GET /api/users/{userId}/permissions
        // Owner viendo a otro usuario -> lista con auditoría (GrantedAt/GrantedByUserId).
        // Un usuario viendo sus propios permisos -> solo la lista plana de códigos.
        [HttpGet("/api/users/{userId:guid}/permissions")]
        public async Task<IActionResult> GetUserPermissions(Guid userId)
        {
            var result = await _permissionService.GetUserPermissionsAsync(userId);

            if (result.ErrorType == ServiceErrorType.NotFound)
                return NotFound(result.Error);

            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var callerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            var isSelf = Guid.TryParse(callerIdClaim, out var callerId) && callerId == userId;

            if (callerRole == Roles.Owner && !isSelf)
                return Ok(result.Data);

            if (isSelf)
                return Ok(result.Data!.Select(p => p.Code).ToList());

            // No es Owner y no está consultando sus propios permisos
            return Forbid();
        }

        // POST /api/users/{userId}/permissions/{permissionCode} — solo Owner
        [HttpPost("/api/users/{userId:guid}/permissions/{permissionCode}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> GrantPermission(Guid userId, string permissionCode)
        {
            var callerIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
            if (!Guid.TryParse(callerIdClaim, out var callerId))
                return Unauthorized("Token inválido: no contiene el identificador del usuario.");

            var result = await _permissionService.GrantAsync(userId, permissionCode, callerId);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { message = "Permiso otorgado correctamente." })
            };
        }

        // DELETE /api/users/{userId}/permissions/{permissionCode} — solo Owner
        [HttpDelete("/api/users/{userId:guid}/permissions/{permissionCode}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> RevokePermission(Guid userId, string permissionCode)
        {
            var result = await _permissionService.RevokeAsync(userId, permissionCode);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { message = "Permiso revocado correctamente." })
            };
        }
    }
}
