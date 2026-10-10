using AgendaApp.Authorization;
using AgendaApp.Context;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Controllers
{
    // Sesión actual: el front la usa para armar el menú según los permisos efectivos.
    [ApiController]
    [Route("api/me")]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly AgendaProDbContext _context;
        private readonly IPermissionService _permissionService;
        private readonly ITenantGuard _tenant;

        public MeController(AgendaProDbContext context, IPermissionService permissionService, ITenantGuard tenant)
        {
            _context = context;
            _permissionService = permissionService;
            _tenant = tenant;
        }

        [HttpGet]
        public async Task<ActionResult<MeDto>> Get()
        {
            var user = await _context.Users
                .Include(u => u.Business)
                .FirstOrDefaultAsync(u => u.UserId == _tenant.UserId && u.Active);

            if (user == null)
                return Unauthorized();

            var permissions = user.Role == Roles.Owner
                ? PermissionCodes.Catalog.Keys.ToList()
                : (await _permissionService.GetGrantedCodesAsync(user.UserId)).ToList();

            return Ok(new MeDto
            {
                UserId = user.UserId,
                BusinessId = user.BusinessId,
                BusinessName = user.Business.Name,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Permissions = permissions
            });
        }
    }
}
