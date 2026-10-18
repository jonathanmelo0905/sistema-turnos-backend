# CLAUDE.md — backend

API .NET 8 (proyecto `AgendaApp`), EF Core 8 + Pomelo MySQL, JWT con refresh tokens. Base `agendaprodb_dev`; la cadena de conexión y `Jwt:Key` están en user-secrets. Contexto general en `../CLAUDE.md`.

## Comandos

```
dotnet build
dotnet run --launch-profile https          # https://localhost:7122, Swagger en Development
dotnet ef migrations add <Nombre>
dotnet ef database update
dotnet run -- seed-historial               # solo Development: ~60 días de turnos y reseñas en "Barbería Demo"
node scripts/seed-demo.mjs                 # empresas, usuarios y servicios demo
```

- Las credenciales de los usuarios demo están en `scripts/seed-demo.mjs`.
- `seed-historial` (`Services/DemoHistorySeeder.cs`) marca sus turnos con `Notes = "[historial-demo]"` y no se duplica si ya existe.
- No hay proyecto de pruebas: se valida con `dotnet build` y llamadas reales a la API.

## Arquitectura

- Capas: `Controllers` → `Services` (interfaz `IXxxService`, resultados con `ServiceResult`) → `Context` (`AgendaProDbContext`). `Middleware/ExceptionHandlingMiddleware` traduce excepciones.
- **Multiempresa en una sola base**: toda entidad de negocio lleva `BusinessId`. `Authorization/TenantGuard.cs` (`ITenantGuard`) toma la empresa del JWT y valida que cada recurso pedido le pertenezca; nunca confiar en un `businessId` del cuerpo.
- **Permisos**: roles Owner / Encargado / Colaborador + permisos granulares como policies (`PermissionAuthorizationHandler`, `PermissionCache`): `business.edit`, `collaborators.manage`, `services.manage`, `accounting.view`, `stats.view`, `clients.contacts.view`, `flexibility.approve`, `valley.manage`. El Owner los tiene todos implícitamente. Si se agrega uno, replicarlo en `frontend-admin/src/app/core/constants/permisos.constants.ts`.
- **Turnos** (`AppointmentService`): Pending → Confirmed → InProgress → Completed, más Cancelled (con motivo), NoShow y PendingOutOfHours/RejectedOutOfHours. Cada cambio queda en `AppointmentHistory`; el precio se congela al reservar; el fin sale de la duración del servicio; reprogramar/ceder revalida disponibilidad; no se borra, se cancela; InProgress solo el mismo día.
- **Disponibilidad** (`AvailabilityService`): horario de empresa ∩ jornada del colaborador − pausa − ausencias aprobadas − turnos existentes; inicios cada 15 min. Ausencias (`TimeOffService`): Pendiente/Aprobada/Rechazada/Cancelada, las aprueba Owner o Encargado (`flexibility.approve`).
- **API pública** (`PublicController` / `PublicService`, `/api/public`): sin login; empresa por `Businesses.Slug`; reserva como invitado ("cualquiera" asigna a quien tenga libre y menos carga); el cliente se reconoce por `Clients.AccessToken` (cabecera `X-Client-Token`) o por código de 6 caracteres + celular/correo, o con un código de 6 números enviado al correo (`access/request` siempre 200, hash en `Clients.AccessCodeHash`, 10 min, 5 intentos; `access/verify` devuelve mis turnos con el token), o con contraseña opcional (`Clients.PasswordHash`, BCrypt; se crea con `POST me/password` y la sesión, se usa en `access/password` con correo o celular); al reservar sin sesión con un correo o WhatsApp que ya tiene cuenta, `book` responde `requires: "Acceso"` (no reserva ni entrega token); con sesión y datos distintos, `requires: "ConfirmarDatos"` hasta que llegue `updateMyData`; cancelación en línea hasta `CancelLimitHours`.
- **Límite de peticiones** (`AddRateLimiter` en `Program.cs`, nombres en `Middleware/RateLimitPolicies.cs`): por IP y ventana de 1 min; `publico-lectura` en todo `/api/public` y la disponibilidad anónima, `publico-escritura` en reservar, buscar, cancelar y reseñar. Valores en `RateLimit:LecturaPorMinuto/EscrituraPorMinuto` (120/20; en Development 300/60). Con sesión no se limita. Responde 429 con `Retry-After` y `{ message }`.
- **WhatsApp asistido** (no hay chat interno): cuando el personal reprograma/cede/cancela o una ausencia choca con turnos, el turno queda `NeedsContact` con `ContactReason` hasta `POST appointments/{id}/contacted`. `NotificationService` alimenta la campanita (`StaffNotifications`).
- **Recordatorios** (`ReminderService` + `ReminderJob`, cada 30 min): avisa en la campanita (`Reminder`) al colaborador por cada turno Pending/Confirmed de mañana, una sola vez. `POST appointments/{id}/reminder { channel }` registra `ReminderSentAt/ReminderChannel`; con `Email` lo envía `CorreoCitasService` (SMTP de `Smtp:*`; sin `Smtp:Host`, `.eml` en `correos-dev/`). `POST appointments/reminders/run` solo en Development.
- **Correos al cliente** (`CorreoCitasService`, plantilla única): confirmación (en `AppointmentService.BookAsync`, salvo la web, que la manda `PublicService` tras completar el correo), reprogramación, cancelación y recordatorio. Remitente con el nombre de la empresa y `Reply-To` a `Businesses.Email`; nunca lanza (falla → log). Enlaces con `App:ClienteUrl` y logo con `App:ApiUrl`.
- **Reseñas** (`Reviews`, `ReviewsController`): una por turno Completed, la deja el cliente desde la API pública; la responde quien atendió o alguien con `stats.view`; ocultar solo con `stats.view` (sigue contando en estadísticas). En la página pública solo el primer nombre del cliente.
- **Estadísticas** (`StatsService`, `GET api/stats`, `stats.view`): agrega en memoria los turnos del rango: KPIs vs. periodo anterior de igual duración, serie por día, por servicio, por colaborador con ocupación (minutos reservados / jornada disponible), mapa de calor día×hora con horas abiertas y las 5 horas más flojas.
- **Horas Valle** (`ValleyService`, `api/valley`, `valley.manage`): franjas día×hora con pocas citas en 30 días (por % o cantidad) → descuento mín–máx según intensidad; se cotiza por solape del servicio (mínimo configurable, proporcional opcional). Caché 15 min por empresa. La disponibilidad devuelve `valley` por inicio y `BookAsync` recalcula y congela `Price`, `ListPrice` y `ValleyDiscountPct`. `GET api/stats` trae `valley` (efectividad en el periodo).

## Migraciones y trampas de MySQL

- Crear tablas está permitido en este proyecto, siempre vía migraciones EF.
- Las tablas heredadas usan Guid `char(36)` con `utf8mb4_0900_ai_ci`: toda columna Guid nueva (sobre todo FKs) necesita `.HasCharSet("utf8mb4").UseCollation("utf8mb4_0900_ai_ci")` en el modelo, o la migración falla por collation incompatible.
- Revisar cada migración generada antes de aplicarla: ha intentado borrar índices inexistentes (`IX_Appointments_BusinessId`) y poner defaults equivocados (`Users.Active` debe quedar en `true`).
- `EF.Functions.DateDiffMinute` es ambiguo porque también está referenciado el paquete de SqlServer: llamar `MySqlDbFunctionsExtensions.DateDiffMinute(EF.Functions, ...)`.
- Filtrar en la consulta antes de materializar; no usar `AsEnumerable()` antes del `Where`.
