using AgendaApp.Context;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace AgendaApp.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionService _permissionService;

        public PermissionAuthorizationHandler(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
            if (role == Roles.Owner)
            {
                context.Succeed(requirement);
                return;
            }

            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
                return;

            var grantedCodes = await _permissionService.GetGrantedCodesAsync(userId);
            if (grantedCodes.Contains(requirement.PermissionCode))
                context.Succeed(requirement);
        }
    }
}
