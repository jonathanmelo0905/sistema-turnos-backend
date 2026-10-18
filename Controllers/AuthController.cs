using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AgendaApp.Middleware;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPasswordResetService _reset;

        public AuthController(IAuthService authService, IPasswordResetService reset)
        {
            _authService = authService;
            _reset = reset;
        }

        // Restablecer contraseña con el enlace que envía soporte: primero se valida, luego se cambia
        [HttpPost("password-reset/check")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<ActionResult<PasswordResetCheckDto>> PasswordResetCheck([FromBody] PasswordResetTokenDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var r = await _reset.CheckAsync(dto.Token);
            return r.Success ? Ok(r.Data) : BadRequest(r.Error);
        }

        [HttpPost("password-reset")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<IActionResult> PasswordReset([FromBody] PasswordResetDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var r = await _reset.ResetAsync(dto.Token, dto.Password);
            return r.Success ? Ok(new { message = "Listo, ya puedes iniciar sesión con tu contraseña nueva." }) : BadRequest(r.Error);
        }

        // Registro público de una empresa: queda pendiente de aprobación por el operador
        [HttpPost("register-business")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.PublicoEscritura)]
        public async Task<IActionResult> RegisterBusiness([FromBody] RegisterBusinessDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RegisterBusinessAsync(dto);

            if (result.ErrorType == ServiceErrorType.BadRequest)
                return BadRequest(result.Error);
            if (result.ErrorType == ServiceErrorType.Conflict)
                return Conflict(result.Error);

            return Ok(new
            {
                message = "Recibimos tu solicitud. Te avisaremos por correo cuando la empresa esté aprobada.",
                businessId = result.Data!.BusinessId,
                userId = result.Data.UserId
            });
        }

        // 🔐 LOGIN
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto dto)
        {
            var result = await _authService.LoginAsync(dto);

            if (result.ErrorType == ServiceErrorType.Unauthorized)
                return Unauthorized(result.Error);

            return Ok(result.Data);
        }

        // ♻️ RENOVAR TOKEN
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken);

            if (result.ErrorType == ServiceErrorType.Unauthorized)
                return Unauthorized(result.Error);

            return Ok(new
            {
                token = result.Data!.Token,
                refreshToken = result.Data.RefreshToken,
                refreshTokenExpiration = result.Data.RefreshTokenExpiration
            });
        }

        // 🚪 LOGOUT (revoca token)
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
        {
            var result = await _authService.LogoutAsync(dto.RefreshToken);

            if (result.ErrorType == ServiceErrorType.NotFound)
                return NotFound(result.Error);

            return Ok("Sesión cerrada correctamente.");
        }
    }
}
