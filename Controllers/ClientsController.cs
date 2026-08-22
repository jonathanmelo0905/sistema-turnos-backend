using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgendaApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientsController : ControllerBase
    {
        private readonly IClientService _clientService;

        public ClientsController(IClientService clientService)
        {
            _clientService = clientService;
        }

        [HttpGet]
        [Authorize(Policy = "clients.contacts.view")]
        public async Task<ActionResult<IEnumerable<ClientsDto>>> Get([FromQuery] Guid businessId)
        {
            var clients = await _clientService.GetClientsAsync(businessId);

            if (!clients.Any())
                return NoContent();

            return Ok(clients);
        }

        [HttpGet("{clientId:guid}")]
        [Authorize(Policy = "clients.contacts.view")]
        public async Task<ActionResult<ClientsDto>> GetClient(Guid clientId)
        {
            var client = await _clientService.GetClientAsync(clientId);

            if (client == null)
                return NotFound("No se encontró el cliente.");

            return Ok(client);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> CreateClient([FromBody] CreateClientDto client)
        {
            if (client == null)
                return BadRequest("La información del cliente es requerida.");

            var result = await _clientService.CreateClientAsync(client);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => CreatedAtAction(nameof(Get), new { businessId = client.BusinessId }, result.Data)
            };
        }

        [HttpPut("{clientId:guid}")]
        [Authorize]
        public async Task<ActionResult> UpdateClient(Guid clientId, [FromBody] UpdateClientDto client)
        {
            if (client == null)
                return BadRequest("La información del cliente es requerida.");

            var result = await _clientService.UpdateClientAsync(clientId, client);

            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => Ok(new { message = "Cliente actualizado correctamente.", clientId })
            };
        }

        [HttpDelete("{clientId:guid}")]
        [Authorize]
        public async Task<ActionResult> DeleteClient(Guid clientId, [FromQuery] Guid businessId)
        {
            var result = await _clientService.DeleteClientAsync(clientId, businessId);

            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.NotFound => NotFound(result.Error),
                _ => Ok(new { message = "Cliente eliminado correctamente." })
            };
        }
    }
}
