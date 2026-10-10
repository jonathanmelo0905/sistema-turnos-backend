using AgendaApp.Authorization;
using AgendaApp.Context;
using AgendaApp.Dtos;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgendaApp.Controllers
{
    // Clientes de la empresa del token.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ClientsController : ControllerBase
    {
        private readonly IClientService _clientService;
        private readonly ITenantGuard _tenant;
        private readonly AgendaProDbContext _context;

        public ClientsController(IClientService clientService, ITenantGuard tenant, AgendaProDbContext context)
        {
            _clientService = clientService;
            _tenant = tenant;
            _context = context;
        }

        [HttpGet]
        [Authorize(Policy = "clients.contacts.view")]
        public async Task<ActionResult<IEnumerable<ClientsDto>>> Get()
        {
            return Ok(await _clientService.GetClientsAsync(_tenant.BusinessId));
        }

        // GET /api/clients/search?q= — para reservar en mostrador (todo el personal).
        // Sin el permiso de contactos, el teléfono y el correo se muestran enmascarados.
        [HttpGet("search")]
        public async Task<ActionResult<List<ClientSearchDto>>> Search([FromQuery] string? q)
        {
            var texto = (q ?? "").Trim();
            if (texto.Length < 2) return Ok(new List<ClientSearchDto>());

            var veContactos = await _tenant.HasPermissionAsync(PermissionCodes.ClientsContactsView);
            var digitos = new string(texto.Where(char.IsDigit).ToArray());

            var lista = await _context.Clients.AsNoTracking()
                .Where(c => c.BusinessId == _tenant.BusinessId &&
                    ((c.Name != null && c.Name.Contains(texto)) ||
                     (digitos.Length >= 3 && c.Phone != null && c.Phone.Contains(digitos)) ||
                     (c.Email != null && c.Email.Contains(texto))))
                .OrderBy(c => c.Name)
                .Take(8)
                .Select(c => new ClientSearchDto
                {
                    ClientId = c.ClientId,
                    Name = c.Name,
                    Phone = c.Phone,
                    Email = c.Email,
                    Visits = c.Appointments.Count(a => a.Status == AppointmentStatus.Completed),
                    LastVisit = c.Appointments.Where(a => a.Status == AppointmentStatus.Completed).Max(a => (DateTime?)a.DateTimeStart)
                })
                .ToListAsync();

            if (!veContactos)
                foreach (var c in lista)
                {
                    c.Phone = Enmascarar(c.Phone);
                    c.Email = Enmascarar(c.Email);
                }

            return Ok(lista);
        }

        [HttpGet("{clientId:guid}")]
        [Authorize(Policy = "clients.contacts.view")]
        public async Task<ActionResult<ClientsDto>> GetClient(Guid clientId)
        {
            var client = await _clientService.GetClientAsync(clientId);
            if (client == null || !await _context.Clients.AnyAsync(c => c.ClientId == clientId && c.BusinessId == _tenant.BusinessId))
                return NotFound("No se encontró el cliente.");
            return Ok(client);
        }

        [HttpPost]
        public async Task<ActionResult> CreateClient([FromBody] CreateClientDto client)
        {
            if (client == null) return BadRequest("La información del cliente es requerida.");
            client.BusinessId = _tenant.BusinessId;

            var result = await _clientService.CreateClientAsync(client);
            return result.ErrorType switch
            {
                ServiceErrorType.BadRequest => BadRequest(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => Ok(result.Data)
            };
        }

        [HttpPut("{clientId:guid}")]
        [Authorize(Policy = "clients.contacts.view")]
        public async Task<ActionResult> UpdateClient(Guid clientId, [FromBody] UpdateClientDto client)
        {
            if (client == null) return BadRequest("La información del cliente es requerida.");
            client.BusinessId = _tenant.BusinessId;

            var result = await _clientService.UpdateClientAsync(clientId, client);
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound => NotFound(result.Error),
                ServiceErrorType.Conflict => Conflict(result.Error),
                _ => Ok(new { message = "Cliente actualizado correctamente.", clientId })
            };
        }

        // "300 123 4567" → "300•••4567"
        private static string? Enmascarar(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor) || valor.Length < 6) return valor == null ? null : "•••";
            if (valor.Contains('@'))
            {
                var at = valor.IndexOf('@');
                return valor[..Math.Min(2, at)] + "•••" + valor[at..];
            }
            return valor[..3] + "•••" + valor[^4..];
        }
    }
}
