using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.Infraestructura;
using PracticoN5ServicioApiRest.Services;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

// Configuración personal de cada integrante (por ejemplo, la conexión a su SQL Server). No se versiona: ver appsettings.example.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Add services to the container.

// IgnoreCycles evita errores de serialización JSON por referencias circulares entre navegaciones.
builder.Services.AddControllers(options =>
{
    // Auditoría automática de las acciones que modifican datos (ver AuditoriaFilter).
    options.Filters.Add<AuditoriaFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Swagger con esquema Bearer: habilita un botón "Authorize" para enviar el JWT en cada petición.
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingrese el JWT obtenido mediante el login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:DefaultConnection. En desarrollo viene en appsettings.Development.json; " +
        "en otros entornos (o para usar SQL Server) configúrela en appsettings.Local.json, variables de entorno o secretos de usuario.");

string? claveJwt = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(claveJwt) || claveJwt.Length < 32)
{
    throw new InvalidOperationException("Falta Jwt:Key o es demasiado corta (se necesitan al menos 32 caracteres).");
}
// SQL Server es el proveedor real. "Sqlite" (Database:Proveedor) es solo para probar la app sin instalar SQL Server:
// usa un archivo local y crea las tablas directamente (Database:CrearEsquema), sin pasar por las migraciones.
bool usarSqlite = string.Equals(builder.Configuration["Database:Proveedor"], "Sqlite", StringComparison.OrdinalIgnoreCase);
builder.Services.AddDbContext<SistemaVentasDbContext>(options =>
{
    if (usarSqlite)
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// Permite a los servicios conocer al usuario autenticado de la petición actual (UsuarioActual).
builder.Services.AddHttpContextAccessor();

// Registrar servicios de la capa de negocio
builder.Services.AddScoped<UsuarioActual>();
builder.Services.AddSingleton<ZonaHorariaNegocio>();
builder.Services.AddScoped<CategoriasService>();
builder.Services.AddScoped<ProductosService>();
builder.Services.AddScoped<ClientesService>();
builder.Services.AddScoped<ProveedoresService>();
builder.Services.AddScoped<UsuariosService>();
builder.Services.AddScoped<ComprasService>();
builder.Services.AddScoped<DetallesCompraService>();
builder.Services.AddScoped<VentasService>();
builder.Services.AddScoped<DetallesVentaService>();
builder.Services.AddScoped<ImagenesService>();
builder.Services.AddScoped<MovimientosStockService>();
builder.Services.AddScoped<MediosPagoService>();
builder.Services.AddSingleton<PasarelaPagoSimulada>();
builder.Services.AddScoped<AuditoriaService>();
builder.Services.AddScoped<ErrorLogService>();
builder.Services.AddScoped<ReportesService>();
builder.Services.AddSingleton<ReportesPdfService>();
builder.Services.AddScoped<EmailService>();

// CORS permisivo: acepta cualquier origen, header y verbo HTTP.
builder.Services.AddCors(options =>
{
    options.AddPolicy("Permissive", policy =>
    {
        // Cualquier origen (scheme + dominio + puerto).
        policy
            .AllowAnyOrigin()
            // Cualquier header, incluido Authorization (necesario para enviar el JWT).
            .AllowAnyHeader()
            // Todos los verbos HTTP (GET, POST, PUT, PATCH, DELETE, etc.).
            .AllowAnyMethod();
    });
});

// Límites de frecuencia. El tope se lee de la configuración al atender cada petición (no al arrancar).
// "login": intentos de inicio de sesión por IP (frena la fuerza bruta sobre las contraseñas).
// "errores": informes de error de la app por usuario (evita que alguien llene el registro de errores).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", contexto =>
    {
        int limite = contexto.RequestServices.GetRequiredService<IConfiguration>().GetValue<int?>("RateLimit:LoginPorMinuto") ?? 20;

        return RateLimitPartition.GetFixedWindowLimiter(
            contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1) });
    });

    options.AddPolicy("errores", contexto =>
    {
        int limite = contexto.RequestServices.GetRequiredService<IConfiguration>().GetValue<int?>("RateLimit:ErroresPorMinuto") ?? 30;
        string clave = contexto.User.Identity?.Name ?? contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

        return RateLimitPartition.GetFixedWindowLimiter(
            clave,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = limite, Window = TimeSpan.FromMinutes(1) });
    });

    options.OnRejected = async (contexto, cancellationToken) =>
    {
        await contexto.HttpContext.Response.WriteAsJsonAsync(
            new { mensaje = "Demasiados intentos. Espere un minuto e intente nuevamente." },
            cancellationToken);
    };
});

// Autenticación/autorización con JWT Bearer.
// Autenticación: valida QUIÉN es el cliente. Autorización: QUÉ puede hacer.
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Reglas de validación que aplica el middleware a cada token recibido.
        options.TokenValidationParameters = new TokenValidationParameters
        {
            // El token debe ser emitido por nuestro emisor (Jwt:Issuer).
            ValidateIssuer = true,
            // Deshabilitada: la API no tiene una audiencia fija (la consumen la app móvil y Swagger por igual).
            ValidateAudience = false,
            // El token no debe estar expirado.
            ValidateLifetime = true,
            // La firma debe poder verificarse con la clave conocida (Jwt:Key).
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            // Misma clave con la que se firma en UsuariosService.LoginAsync.
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!))
        };

        // Respuestas JSON en español en lugar de los mensajes genéricos de ASP.NET Core.
        options.Events = new JwtBearerEvents
        {
            // El token solo prueba quién era el usuario al iniciar sesión. Se verifica contra la base que siga existiendo,
            // esté activo y conserve el mismo rol: así desactivar a alguien o cambiarle el rol rige de inmediato
            // (y no recién cuando el token venza).
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;

                if (!int.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out int usuarioId))
                {
                    context.Fail("El token no identifica a ningún usuario.");
                    return;
                }

                var baseDatos = context.HttpContext.RequestServices.GetRequiredService<SistemaVentasDbContext>();

                var actual = await baseDatos.Usuarios
                    .AsNoTracking()
                    .Where(u => u.UsuarioId == usuarioId)
                    .Select(u => new { u.Activo, Rol = u.Rol.Nombre })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (actual == null || !actual.Activo || actual.Rol != principal!.FindFirstValue(ClaimTypes.Role))
                {
                    context.Fail("La sesión ya no es válida.");
                }
            },

            // Autenticación fallida: token faltante, inválido o expirado en un [Authorize].
            OnChallenge = async context =>
            {
                context.HandleResponse();

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                await context.Response.WriteAsJsonAsync(new
                {
                    mensaje = "Debe iniciar sesión para acceder a este recurso."
                });
            },

            // Autenticado pero sin permisos para la acción solicitada.
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(new
                {
                    mensaje = "No tiene permisos para acceder a este recurso."
                });
            }
        };
    });

// Habilita la interpretación de [Authorize] y las políticas de los claims del token.
builder.Services.AddAuthorization();

var app = builder.Build();

//Usa swagger independientemente de si es entorno de desarrollo o de producción
app.UseSwagger();
app.UseSwaggerUI();

// Archivos de la tienda web (Blazor WebAssembly): el runtime (_framework) y los recursos estáticos (index.html, estilos, scripts).
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// Fuerza el redireccionamiento a HTTPS. En desarrollo se omite: el emulador/dispositivo Android llega por HTTP
// (http://10.0.2.2:5276) y una redirección a https://localhost lo dejaría sin conexión.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Impide que el navegador "adivine" el tipo de un archivo servido (por ejemplo, tratar como HTML una imagen subida).
app.Use(async (contexto, siguiente) =>
{
    contexto.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await siguiente();
});

// Aplica la política de CORS a todo el pipeline. Va antes de la autenticación para que las respuestas
// de CORS lleguen aunque la petición luego sea rechazada por auth.
app.UseCors("Permissive");

// Captura las excepciones no controladas, las guarda en el registro de errores y responde un 500 en JSON.
// Va después de CORS para que esa respuesta de error también lleve los headers de CORS.
app.UseMiddleware<ManejoErroresMiddleware>();

// Orden obligatorio: UseAuthentication valida el token y construye la identidad del usuario;
// UseAuthorization evalúa los [Authorize] en base a esa identidad. Deben ir en ese orden.
app.UseAuthentication();
app.UseAuthorization();

// Después de autenticar: el límite de errores se aplica por usuario.
app.UseRateLimiter();

app.MapControllers();

// La API también sirve la tienda web (PracticoN6.Frontend.Web): abrir http://localhost:5276/ muestra la aplicación, de modo que
// para verla alcanza con iniciar un solo proyecto. Swagger sigue en /swagger. Una ruta /api/... inexistente responde 404
// (no la página de la tienda) para no confundir a los clientes de la API.
app.MapFallback("/api/{**ruta}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

// Migraciones pendientes, administrador inicial y datos de ejemplo (según la configuración).
await DbInitializer.InicializarAsync(app.Services);

app.Run();

// Expone la clase Program a los tests de integración (WebApplicationFactory<Program>).
public partial class Program { }
