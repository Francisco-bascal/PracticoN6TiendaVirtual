using System.Net;
using System.Net.Http.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>Matriz de acceso por rol: qué puede y qué no puede hacer cada uno.</summary>
    public class AutorizacionTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public AutorizacionTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        [Theory]
        [InlineData("GET", "/api/Clientes")]
        [InlineData("GET", "/api/Proveedores")]
        [InlineData("GET", "/api/Compras")]
        [InlineData("GET", "/api/Ventas")]
        [InlineData("GET", "/api/DetallesVenta/1")]
        [InlineData("GET", "/api/MovimientosStock")]
        [InlineData("GET", "/api/Reportes/stock")]
        [InlineData("GET", "/api/Logs/auditoria")]
        [InlineData("GET", "/api/Logs/errores")]
        [InlineData("GET", "/api/Usuarios")]
        [InlineData("GET", "/api/Roles")]
        [InlineData("POST", "/api/Productos")]
        [InlineData("POST", "/api/Categorias")]
        [InlineData("POST", "/api/Compras")]
        [InlineData("POST", "/api/Ventas")]
        [InlineData("PATCH", "/api/Productos/1/stock")]
        public async Task UnCliente_NoPuedeAccederARecursosDelPersonal(string metodo, string ruta)
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            var respuesta = await Enviar(cliente, metodo, ruta);

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            Assert.Contains("permisos", await respuesta.MensajeAsync());
        }

        [Theory]
        [InlineData("GET", "/api/Logs/auditoria")]
        [InlineData("GET", "/api/Logs/errores")]
        [InlineData("GET", "/api/Usuarios")]
        [InlineData("GET", "/api/Roles")]
        [InlineData("POST", "/api/Usuarios")]
        [InlineData("DELETE", "/api/Productos/1")]
        [InlineData("POST", "/api/Ventas/1/anular")]
        [InlineData("POST", "/api/Compras/1/anular")]
        [InlineData("DELETE", "/api/Usuarios/1")]
        public async Task UnOperador_NoPuedeAccederARecursosDelAdministrador(string metodo, string ruta)
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await Enviar(operador, metodo, ruta);

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }

        [Theory]
        [InlineData("/api/Productos")]
        [InlineData("/api/Categorias")]
        [InlineData("/api/MediosPago")]
        [InlineData("/api/Ventas/mis-pedidos")]
        [InlineData("/api/Usuarios/yo")]
        public async Task UnCliente_PuedeUsarElCatalogoYSuCuenta(string ruta)
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            var respuesta = await cliente.GetAsync(ruta);

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Theory]
        [InlineData("/api/Clientes")]
        [InlineData("/api/Proveedores")]
        [InlineData("/api/Compras")]
        [InlineData("/api/Ventas")]
        [InlineData("/api/MovimientosStock")]
        [InlineData("/api/Reportes/stock")]
        public async Task UnOperador_PuedeGestionarElNegocio(string ruta)
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await operador.GetAsync(ruta);

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Theory]
        [InlineData("/api/Usuarios")]
        [InlineData("/api/Roles")]
        [InlineData("/api/Logs/auditoria")]
        [InlineData("/api/Logs/errores")]
        public async Task UnAdministrador_PuedeAdministrarUsuariosYRegistros(string ruta)
        {
            var admin = await _fabrica.AdminAsync();

            var respuesta = await admin.GetAsync(ruta);

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Fact]
        public async Task LosRolesDisponiblesSonAdministradorOperadorYCliente()
        {
            var admin = await _fabrica.AdminAsync();

            var roles = await (await admin.GetAsync("/api/Roles")).LeerAsync<List<System.Text.Json.JsonElement>>();

            Assert.Equal(new[] { "Administrador", "Operador", "Cliente" }, roles.Select(r => r.GetProperty("nombre").GetString()));
        }

        [Fact]
        public async Task UnOperador_PuedeCrearProductos_PeroNoEliminarlos()
        {
            var operador = await _fabrica.OperadorAsync();
            var id = await operador.CrearProductoAsync();

            var eliminar = await operador.DeleteAsync($"/api/Productos/{id}");

            Assert.Equal(HttpStatusCode.Forbidden, eliminar.StatusCode);
        }

        private static Task<HttpResponseMessage> Enviar(HttpClient http, string metodo, string ruta)
        {
            var peticion = new HttpRequestMessage(new HttpMethod(metodo), ruta);

            if (metodo is "POST" or "PATCH" or "PUT")
            {
                peticion.Content = JsonContent.Create(new { });
            }

            return http.SendAsync(peticion);
        }
    }
}
