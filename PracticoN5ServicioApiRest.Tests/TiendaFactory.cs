using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PracticoN5ServicioApiRest.Data;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>
    /// Levanta la API completa en memoria con una base SQLite en memoria y los datos de ejemplo
    /// (usuarios admin / operador / cliente y el catálogo demo). Cada clase de tests usa su propia instancia.
    /// </summary>
    public class TiendaFactory : WebApplicationFactory<Program>
    {
        public const string PasswordAdmin = "Admin1234";
        public const string PasswordDemo = "Demo1234";

        private readonly SqliteConnection _conexion = new("DataSource=:memory:");

        public string CarpetaContenido { get; } = Path.Combine(Path.GetTempPath(), "tienda-tests-" + Guid.NewGuid().ToString("N"));

        public TiendaFactory()
        {
            // Variables de entorno: Program.cs lee la configuración antes de que WebApplicationFactory pueda inyectar la suya.
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=no-se-usa;Database=no-se-usa");
            Environment.SetEnvironmentVariable("Jwt__Key", "clave-de-pruebas-con-mas-de-treinta-y-dos-caracteres");
            Environment.SetEnvironmentVariable("Jwt__Issuer", "MiWebApi");
            Environment.SetEnvironmentVariable("Database__CrearEsquema", "true");
            Environment.SetEnvironmentVariable("Seed__AdminPassword", PasswordAdmin);
            Environment.SetEnvironmentVariable("Seed__DatosDemo", "true");
            Environment.SetEnvironmentVariable("Seed__PasswordDemo", PasswordDemo);
            Environment.SetEnvironmentVariable("RateLimit__LoginPorMinuto", "100000");
            Environment.SetEnvironmentVariable("Negocio__ZonaHoraria", "America/Argentina/Buenos_Aires");
            Environment.SetEnvironmentVariable("Logging__LogLevel__Default", "Warning");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(Path.Combine(CarpetaContenido, "wwwroot"));
            // Los hosts derivados (WithWebHostBuilder) vuelven a pasar por acá y comparten la misma base en memoria.
            if (_conexion.State != System.Data.ConnectionState.Open)
            {
                _conexion.Open();
            }

            builder.UseContentRoot(CarpetaContenido);

            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<DbContextOptions<SistemaVentasDbContext>>();
                servicios.AddDbContext<SistemaVentasDbContext>(opciones => opciones.UseSqlite(_conexion));

                // Controlador solo de pruebas, para provocar una excepción no controlada.
                servicios.AddControllers().AddApplicationPart(typeof(TiendaFactory).Assembly);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _conexion.Dispose();

                try
                {
                    Directory.Delete(CarpetaContenido, recursive: true);
                }
                catch (IOException)
                {
                    // Limpieza de la carpeta temporal: si falla no afecta al resultado de las pruebas.
                }
            }
        }

        /// <summary>Cliente HTTP con el token de un usuario ya autenticado.</summary>
        public async Task<HttpClient> ClienteAsync(string usuario, string password)
        {
            var http = CreateClient();
            await http.IniciarSesionAsync(usuario, password);
            return http;
        }

        public Task<HttpClient> AdminAsync() => ClienteAsync("admin", PasswordAdmin);
        public Task<HttpClient> OperadorAsync() => ClienteAsync("operador", PasswordDemo);
        public Task<HttpClient> ClienteDemoAsync() => ClienteAsync("cliente", PasswordDemo);
    }
}
