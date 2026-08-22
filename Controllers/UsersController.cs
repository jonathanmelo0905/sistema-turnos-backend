using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgendaApp.Dtos;
using AgendaApp.Services;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // GET: api/users (requiere estar autenticado; filtra por el negocio del usuario autenticado)
        [HttpGet]
        [Authorize(Policy = "collaborators.manage")]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers()
        {
            var businessIdClaim = User.FindFirst("BusinessId")?.Value;
            if (!Guid.TryParse(businessIdClaim, out var businessId))
                return Unauthorized("Token inválido: no contiene BusinessId.");

            var users = await _userService.GetUsersByBusinessAsync(businessId);
            return Ok(users);
        }

        // GET: api/users/{id}
        [HttpGet("{id:guid}")]
        [Authorize(Policy = "collaborators.manage")]
        public async Task<ActionResult<UserResponseDto>> GetUser(Guid id)
        {
            var user = await _userService.GetUserAsync(id);

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        [HttpGet("colaboradores/{businessId:guid}")]
        [Authorize(Policy = "collaborators.manage")]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUserXBusiness(Guid businessId)
        {
            var users = await _userService.GetUsersByBusinessAsync(businessId);
            return Ok(users);
        }

        // POST: api/users (solo Admin puede crear)
        [HttpPost]
        [Authorize(Roles = "Owner")]
        public async Task<ActionResult<UserResponseDto>> CreateUser(CreateUserDto dto)
        {
            var result = await _userService.CreateUserAsync(dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                _ => CreatedAtAction(nameof(GetUser), new { id = result.Data!.UserId }, result.Data)
            };
        }

        // PUT: api/users/{id}
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> UpdateUser(Guid id, UpdateUserDto dto)
        {
            var result = await _userService.UpdateUserAsync(id, dto);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => NoContent()
            };
        }

        // DELETE: api/users/{id}
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> DeleteUser(Guid id)
        {
            var deleted = await _userService.DeleteUserAsync(id);

            if (!deleted)
                return NotFound();

            return NoContent();
        }
    }
}
