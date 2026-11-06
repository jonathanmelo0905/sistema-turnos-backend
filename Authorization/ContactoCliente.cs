using AgendaApp.Context;
using AgendaApp.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgendaApp.Authorization
{
    // Quién ve el teléfono y el correo reales del cliente de un turno (decisión #3 de la Fase 9):
    // - el dueño o quien tiene clients.contacts.view, siempre;
    // - quien atiende el turno, para el WhatsApp asistido, salvo que él mismo lo haya reservado para
    //   un cliente que ya existía (si no, bastaría con buscar al cliente y reservarle para ver su contacto).
    //   Si lo reservó él con un cliente nuevo, el contacto lo escribió él y sí lo ve.
    // A los demás les llega enmascarado ("300•••4567") y con ClientContactHidden = true.
    public static class ContactoCliente
    {
        public static bool Visible(Guid quienMira, bool tienePermiso, Guid atiende, Guid? creadoPor, bool clienteNuevoEnLaReserva) =>
            tienePermiso || (atiende == quienMira && (creadoPor != quienMira || clienteNuevoEnLaReserva));

        // "300 123 4567" → "300•••4567"; "maria@correo.com" → "ma•••@correo.com"
        public static string? Enmascarar(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;
            if (valor.Length < 6) return "•••";
            if (valor.Contains('@'))
            {
                var at = valor.IndexOf('@');
                return valor[..Math.Min(2, at)] + "•••" + valor[at..];
            }
            return valor[..3] + "•••" + valor[^4..];
        }

        public static void Aplicar(AppointmentDto a, Guid quienMira, bool tienePermiso)
        {
            if (Visible(quienMira, tienePermiso, a.UserId, a.CreatedByUserId, a.ClientIsNewFromBooking)) return;
            a.ClientPhone = Enmascarar(a.ClientPhone);
            a.ClientEmail = Enmascarar(a.ClientEmail);
            a.ClientContactHidden = true;
        }

        public static void Aplicar(TimeOffConflictDto c, Guid quienMira, bool tienePermiso)
        {
            if (Visible(quienMira, tienePermiso, c.UserId, c.CreatedByUserId, c.ClientIsNewFromBooking)) return;
            c.ClientPhone = Enmascarar(c.ClientPhone);
            c.ClientContactHidden = true;
        }
    }

    // Filtro de los controladores que devuelven turnos: enmascara el contacto en cada respuesta
    // (un turno, una lista o los conflictos de una ausencia) según quién consulta.
    public class ContactoClienteFilter : IAsyncResultFilter
    {
        private readonly ITenantGuard _tenant;

        public ContactoClienteFilter(ITenantGuard tenant) => _tenant = tenant;

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is ObjectResult { Value: not null } r && (r.StatusCode ?? 200) < 300)
            {
                var turnos = r.Value switch
                {
                    AppointmentDto a => new[] { a },
                    IEnumerable<AppointmentDto> lista => lista,
                    _ => null
                };
                var conflictos = r.Value as IEnumerable<TimeOffConflictDto>;

                if (turnos != null || conflictos != null)
                {
                    var permiso = await _tenant.HasPermissionAsync(PermissionCodes.ClientsContactsView);
                    foreach (var a in turnos ?? Enumerable.Empty<AppointmentDto>()) ContactoCliente.Aplicar(a, _tenant.UserId, permiso);
                    foreach (var c in conflictos ?? Enumerable.Empty<TimeOffConflictDto>()) ContactoCliente.Aplicar(c, _tenant.UserId, permiso);
                }
            }
            await next();
        }
    }
}
