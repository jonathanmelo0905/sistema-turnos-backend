using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    // Panel de plataforma: solo el operador (rol PlatformAdmin, policy "platform")
    [ApiController]
    [Route("api/platform")]
    [Authorize(Policy = "platform")]
    public class PlatformController : ControllerBase
    {
        private readonly IPlatformService _platform;
        private readonly IPasswordResetService _reset;

        public PlatformController(IPlatformService platform, IPasswordResetService reset)
        {
            _platform = platform;
            _reset = reset;
        }

        // GET: api/platform/businesses?status=Pending|Approved|Rejected (sin status: todas)
        [HttpGet("businesses")]
        public async Task<ActionResult<List<PlatformBusinessDto>>> List([FromQuery] string? status) =>
            Ok(await _platform.ListAsync(status));

        [HttpPost("businesses/{businessId:guid}/approve")]
        public async Task<ActionResult<PlatformBusinessDto>> Approve(Guid businessId) =>
            Responder(await _platform.ApproveAsync(businessId));

        [HttpPost("businesses/{businessId:guid}/reject")]
        public async Task<ActionResult<PlatformBusinessDto>> Reject(Guid businessId, [FromBody] PlatformRejectDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            return Responder(await _platform.RejectAsync(businessId, dto.Reason));
        }

        // Soporte: envía al dueño el enlace para restablecer su contraseña (24 h, un solo uso)
        [HttpPost("businesses/{businessId:guid}/owner-password-reset")]
        public async Task<ActionResult<PasswordResetSentDto>> OwnerPasswordReset(Guid businessId)
        {
            var r = await _reset.SendToOwnerAsync(businessId);
            return r.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(r.Error),
                ServiceErrorType.BadRequest => BadRequest(r.Error),
                _ => Ok(r.Data)
            };
        }

        private ActionResult<PlatformBusinessDto> Responder(ServiceResult<PlatformBusinessDto> r) => r.ErrorType switch
        {
            ServiceErrorType.NotFound => NotFound(r.Error),
            ServiceErrorType.Conflict => Conflict(r.Error),
            _ => Ok(r.Data)
        };
    }
}
