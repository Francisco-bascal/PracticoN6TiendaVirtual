using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>
    /// Esta clase solo construye hosts derivados en entorno Production (nunca el host base de la fábrica),
    /// para que la base empiece vacía y se vea qué carga el inicializador.
    /// </summary>
    public class ProduccionTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public ProduccionTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        [Fact]
        public async Task EnProduccion_SeIgnoranLosDatosDeEjemplo_PeroSeCreaElAdministradorConfigurado()
        {
            // La configuración pide datos de ejemplo (Seed:DatosDemo=true), como la plantilla appsettings.example.json.
            var produccion = _fabrica.WithWebHostBuilder(host => host.UseEnvironment("Production"));
            var http = produccion.CreateClient();

            var operador = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "operador", password = TiendaFactory.PasswordDemo });
            var cliente = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "cliente", password = TiendaFactory.PasswordDemo });
            var admin = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "admin", password = TiendaFactory.PasswordAdmin });

            Assert.Equal(HttpStatusCode.Unauthorized, operador.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, cliente.StatusCode);
            Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        }
    }
}
