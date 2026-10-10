using AgendaApp.Authorization;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITenantGuard _tenant;

        public UsersController(IUserService userService, ITenantGuard tenant)
        {
            _userService = userService;
            _tenant = tenant;
        }

        // GET: api/users — usuarios del negocio del token
        [HttpGet]
        [Authorize(Policy = "collaborators.manage")]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
        {
            return Ok(await _userService.GetUsersByBusinessAsync(_tenant.BusinessId));
        }

        // GET: api/users/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "collaborators.manage")]
        public async Task<ActionResult<UserResponseDto>> GetUser(Guid id)
        {
            if (!await _tenant.UserInBusinessAsync(id))
                return NotFound();

            var user = await _userService.GetUserAsync(id);
            return user == null ? NotFound() : Ok(user);
        }

        [HttpGet("colaboradores/{businessId:guid}")]
        [Authorize(Policy = "collaborators.manage")]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUserXBusiness(Guid businessId)
        {
            if (!_tenant.IsOwnBusiness(businessId))
                return Forbid();

            return Ok(await _userService.GetUsersByBusinessAsync(businessId));
        }

        // POST: api/users (solo Owner). El negocio siempre es el del token.
        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult<UserResponseDto>> CreateUser(CreateUserDto dto)
        {
            dto.BusinessId = _tenant.BusinessId;
            var result = await _userService.CreateUserAsync(dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => CreatedAtAction(nameof(GetUser), new { id = result.Data!.UserId }, result.Data)
            };
        }

        // PUT: api/users/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> UpdateUser(Guid id, UpdateUserDto dto)
        {
            if (!await _tenant.UserInBusinessAsync(id))
                return NotFound("Usuario no encontrado.");

            if (id == _tenant.UserId && (!dto.Active || dto.Role != Context.Roles.Owner))
                return BadRequest("No puedes desactivarte ni quitarte el rol de dueño a ti mismo.");

            var result = await _userService.UpdateUserAsync(id, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => NoContent()
            };
        }

        // DELETE: api/users/{id} — desactiva (borrado lógico)
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            if (!await _tenant.UserInBusinessAsync(id))
                return NotFound();

            if (id == _tenant.UserId)
                return BadRequest("No puedes desactivar tu propio usuario.");

            return await _userService.DeleteUserAsync(id) ? NoContent() : NotFound();
        }
    }
}
