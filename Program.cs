using AgendaApp.Authorization;
using AgendaApp.Context;
using AgendaApp.Middleware;
using AgendaApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using UserServiceImpl = AgendaApp.Services.UserService;

var builder = WebApplication.CreateBuilder(args);

// Agrega la política de CORS con whitelist explícita de orígenes
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AppCorsPolicy", policy =>
    {
        // En Development también se aceptan los fronts abiertos desde la red local (celular, tablet)
        if (builder.Environment.IsDevelopment())
            policy.SetIsOriginAllowed(origen => allowedOrigins.Contains(origen) || EsOrigenRedLocal(origen));
        else
            policy.WithOrigins(allowedOrigins);

        policy
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Límite de peticiones para la API pública (por IP, ventana de 1 minuto). Valores en "RateLimit:*".
// Los usuarios con sesión (panel del negocio) no se limitan.
var limiteLectura = builder.Configuration.GetValue("RateLimit:LecturaPorMinuto", 120);
var limiteEscritura = builder.Configuration.GetValue("RateLimit:EscrituraPorMinuto", 20);
builder.Services.AddRateLimiter(options =>
{
    RateLimitPartition<string> PorIp(HttpContext ctx, string politica, int limite) =>
        ctx.User.Identity?.IsAuthenticated == true
            ? RateLimitPartition.GetNoLimiter($"{politica}:sesion")
            : RateLimitPartition.GetFixedWindowLimiter($"{politica}:{ctx.Connection.RemoteIpAddress}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });

    options.AddPolicy(RateLimitPolicies.PublicoLectura, ctx => PorIp(ctx, RateLimitPolicies.PublicoLectura, limiteLectura));
    options.AddPolicy(RateLimitPolicies.PublicoEscritura, ctx => PorIp(ctx, RateLimitPolicies.PublicoEscritura, limiteEscritura));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            context.HttpContext.Response.Headers.RetryAfter = ((int)espera.TotalSeconds).ToString();
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "Hiciste demasiadas solicitudes seguidas. Espera un momento e intenta de nuevo." }, token);
    };
});

// Registrar DbContext
builder.Services.AddDbContext<AgendaProDbContext>(options =>
{
    options.UseMySql(builder.Configuration.GetConnectionString("TurnosConnection"),
    new MySqlServerVersion(new Version(8, 0, 2)));
});

builder.Services.AddScoped<AvailabilityService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserServiceImpl>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IServiceService, ServiceService>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IBusinessScheduleService, BusinessScheduleService>();
builder.Services.AddScoped<IPlanService, PlanService>();
builder.Services.AddScoped<ISchedulesService, SchedulesService>();
builder.Services.AddScoped<IUserServicesService, UserServicesService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITimeOffService, TimeOffService>();
builder.Services.AddScoped<IPublicService, PublicService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<IValleyService, ValleyService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ICorreoCitasService, CorreoCitasService>();
builder.Services.AddScoped<IReminderService, ReminderService>();
builder.Services.AddScoped<IUserPhotoService, UserPhotoService>();
builder.Services.AddScoped<IBusinessImageService, BusinessImageService>();
builder.Services.AddScoped<IPlatformService, PlatformService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
// Archivos subidos: disco local por ahora. Al desplegar, registrar aquí la implementación
// de almacenamiento de objetos (S3/R2/Azure Blob) según "Almacen:Tipo".
builder.Services.AddSingleton<AgendaApp.Services.Almacen.IAlmacenArchivos, AgendaApp.Services.Almacen.AlmacenLocal>();
builder.Services.AddHostedService<ReminderJob>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantGuard, TenantGuard>();

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IPermissionCache, PermissionCache>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// Configuraci�n de JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization(options =>
{
    foreach (var code in PermissionCodes.Catalog.Keys)
    {
        options.AddPolicy(code, policy => policy.Requirements.Add(new PermissionRequirement(code)));
    }
    // Panel de plataforma: solo operadores
    options.AddPolicy("platform", policy => policy.RequireRole(Roles.PlatformAdmin));
});

// Add services to the container.

builder.Services.AddControllers(o => o.Filters.Add<MensajeErrorFilter>());
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Empresas sin dirección pública (creadas antes de la Fase 4) reciben una
using (var scope = app.Services.CreateScope())
{
    await SlugHelper.BackfillAsync(scope.ServiceProvider.GetRequiredService<AgendaProDbContext>());
}

// Solo desarrollo: "dotnet run -- seed-historial" genera historial demo y termina
if (args.Contains("seed-historial") && app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await DemoHistorySeeder.RunAsync(scope.ServiceProvider.GetRequiredService<AgendaProDbContext>(),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed"));
    return;
}

// "dotnet run -- crear-operador <correo> <contraseña> [nombre]" crea un operador de plataforma y termina
if (args.Contains("crear-operador"))
{
    using var scope = app.Services.CreateScope();
    Environment.ExitCode = await OperadorSeeder.RunAsync(scope.ServiceProvider.GetRequiredService<AgendaProDbContext>(),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Operador"), args);
    return;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAppExceptionHandling();

// En Development no se redirige: el celular entra por http (puerto 5232), sin el certificado de localhost
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("AppCorsPolicy");
// Archivos de AlmacenLocal (fotos del personal) en /archivos. Se crea la carpeta para
// que se sirva aunque aún no se haya subido nada.
var carpetaArchivos = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "archivos");
Directory.CreateDirectory(carpetaArchivos);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(carpetaArchivos),
    RequestPath = "/archivos"
});
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization(); 

app.MapControllers();

app.Run();


// Origen http(s) de una IP privada (192.168.x.x, 10.x.x.x, 172.16-31.x.x)
static bool EsOrigenRedLocal(string origen)
{
    if (!Uri.TryCreate(origen, UriKind.Absolute, out var uri) ||
        !System.Net.IPAddress.TryParse(uri.Host, out var ip)) return false;
    var b = ip.GetAddressBytes();
    return b.Length == 4 && (b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31));
}
