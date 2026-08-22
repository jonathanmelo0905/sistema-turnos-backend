using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AgendaApp.Dtos;
using AgendaApp.Services;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PlansController : ControllerBase
    {
        private readonly IPlanService _planService;

        public PlansController(IPlanService planService)
        {
            _planService = planService;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<IEnumerable<PlansDto>>> Get()
        {
            var plans = await _planService.GetPlansAsync();

            if (plans.Count == 0)
                return NoContent();

            return Ok(plans);
        }
    }
}
