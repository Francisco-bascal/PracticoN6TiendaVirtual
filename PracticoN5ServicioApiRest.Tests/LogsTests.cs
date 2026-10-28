using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>Auditoría (quién hizo qué) y registro de errores.</summary>
    public class LogsTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public LogsTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        private static async Task<List<JsonElement>> AuditoriaAsync(HttpClient admin, string consulta)
        {
            var pagina = await (await admin.GetAsync("/api/Logs/auditoria?" + consulta)).LeerAsync<PaginaDto<JsonElement>>();
            return pagina.Elementos;
        }

        [Fact]
        public async Task LasAccionesQueModificanDatos_QuedanAuditadasConUsuarioRecursoYResultado()
        {
            var admin = await _fabrica.AdminAsync();
            var operador = await _fabrica.OperadorAsync();

            int id = await operador.CrearProductoAsync();
            await operador.PatchAsJsonAsync($"/api/Productos/{id}/stock", 5);

            var creacion = Assert.Single(await AuditoriaAsync(admin, $"usuario=operador&entidad=Productos&accion=Crear"), r => r.GetProperty("entidadId").GetString() == id.ToString());
            Assert.Equal(201, creacion.GetProperty("codigoRespuesta").GetInt32());
            Assert.Equal("POST /api/Productos", creacion.GetProperty("detalle").GetString());
            Assert.False(string.IsNullOrEmpty(creacion.GetProperty("fecha").GetString()));

            var ajuste = Assert.Single(await AuditoriaAsync(admin, "usuario=operador&entidad=Productos&accion=ActualizarStock"), r => r.GetProperty("entidadId").GetString() == id.ToString());
            Assert.Equal(200, ajuste.GetProperty("codigoRespuesta").GetInt32());
        }

        [Fact]
        public async Task LaAuditoriaRegistraTambienLasOperacionesRechazadas()
        {
            var admin = await _fabrica.AdminAsync();
            var operador = await _fabrica.OperadorAsync();
            int id = await operador.CrearProductoAsync(stock: 1);

            await operador.PostAsJsonAsync("/api/Ventas", new { clienteId = await operador.ClienteIdDemoAsync(), detalles = new[] { new { productoId = id, cantidad = 50, precioUnitario = 10m } } });

            var rechazadas = await AuditoriaAsync(admin, "usuario=operador&entidad=Ventas&accion=Registrar");
            Assert.Contains(rechazadas, r => r.GetProperty("codigoRespuesta").GetInt32() == 400);
        }

        [Fact]
        public async Task ElLoginFallidoQuedaAuditado_SinGuardarLaContrasena()
        {
            var admin = await _fabrica.AdminAsync();
            var http = _fabrica.CreateClient();
            const string passwordSecreta = "ContraseñaQueNoDebeVerse987";

            await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "operador", password = passwordSecreta });
            await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "operador", password = TiendaFactory.PasswordDemo });

            var intentos = await AuditoriaAsync(admin, "usuario=operador&entidad=Usuarios&accion=Login");

            Assert.Contains(intentos, r => r.GetProperty("codigoRespuesta").GetInt32() == 401 && r.GetProperty("detalle").GetString() == "Inicio de sesión fallido");
            Assert.Contains(intentos, r => r.GetProperty("codigoRespuesta").GetInt32() == 200 && r.GetProperty("detalle").GetString() == "Inicio de sesión exitoso");

            var todo = await (await admin.GetAsync("/api/Logs/auditoria?tamanoPagina=100")).Content.ReadAsStringAsync();
            Assert.DoesNotContain(passwordSecreta, todo);
            Assert.DoesNotContain(TiendaFactory.PasswordDemo, todo);
        }

        [Fact]
        public async Task LasLecturasNoSeAudita()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();

            await cliente.GetAsync("/api/Productos?busqueda=lecturaSinAuditar");

            var registros = await AuditoriaAsync(admin, "usuario=cliente&entidad=Productos&accion=Index");
            Assert.Empty(registros);
        }

        [Fact]
        public async Task LaAuditoriaSePuedeFiltrarPorFechaYPaginar()
        {
            var admin = await _fabrica.AdminAsync();
            var operador = await _fabrica.OperadorAsync();
            await operador.CrearProductoAsync();
            await operador.CrearProductoAsync();
            string ayer = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            string manana = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

            var enRango = await (await admin.GetAsync($"/api/Logs/auditoria?desde={ayer}&hasta={manana}&tamanoPagina=1")).LeerAsync<PaginaDto<JsonElement>>();
            var antiguos = await (await admin.GetAsync("/api/Logs/auditoria?desde=2000-01-01&hasta=2000-01-31")).LeerAsync<PaginaDto<JsonElement>>();

            Assert.Single(enRango.Elementos);
            Assert.True(enRango.TotalPaginas >= 2);
            Assert.Equal(0, antiguos.TotalElementos);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/Logs/auditoria?tamanoPagina=1000")).StatusCode);
        }

        [Fact]
        public async Task UnaExcepcionNoControlada_Responde500ConCodigo_YQuedaEnElRegistroDeErrores()
        {
            var admin = await _fabrica.AdminAsync();

            var respuesta = await _fabrica.CreateClient().GetAsync("/prueba/error");
            var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
            int codigo = cuerpo.GetProperty("codigoError").GetInt32();
            Assert.Contains("error inesperado", cuerpo.GetProperty("mensaje").GetString());
            // El detalle técnico no se le muestra al usuario.
            Assert.DoesNotContain("Falla provocada", cuerpo.GetRawText());

            var errores = await (await admin.GetAsync("/api/Logs/errores?origen=API&tamanoPagina=100")).LeerAsync<PaginaDto<JsonElement>>();
            var guardado = Assert.Single(errores.Elementos, e => e.GetProperty("registroErrorId").GetInt32() == codigo);
            Assert.Equal("Falla provocada por la prueba", guardado.GetProperty("mensaje").GetString());
            Assert.Equal("System.InvalidOperationException", guardado.GetProperty("tipoExcepcion").GetString());
            Assert.Equal("/prueba/error", guardado.GetProperty("ruta").GetString());
            Assert.Equal("GET", guardado.GetProperty("metodo").GetString());
            Assert.Contains("PruebaController", guardado.GetProperty("stackTrace").GetString());
        }

        [Fact]
        public async Task LaRespuestaDeErrorLlevaLosHeadersDeCors()
        {
            var peticion = new HttpRequestMessage(HttpMethod.Get, "/prueba/error");
            peticion.Headers.Add("Origin", "https://app.ejemplo.test");

            var respuesta = await _fabrica.CreateClient().SendAsync(peticion);

            Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
            Assert.True(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
        }

        [Fact]
        public async Task LaAppPuedeInformarUnError_QueSeGuardaConSuUsuarioYNoSeAudita()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();

            var respuesta = await cliente.PostAsJsonAsync("/api/Logs/errores", new
            {
                mensaje = "Object reference not set to an instance of an object",
                tipoExcepcion = "System.NullReferenceException",
                stackTrace = "   at Tienda.Pages.Carrito.Pagar()",
                ruta = "/carrito"
            });
            int codigo = (await respuesta.LeerAsync<JsonElement>()).GetProperty("codigoError").GetInt32();

            var errores = await (await admin.GetAsync("/api/Logs/errores?origen=App")).LeerAsync<PaginaDto<JsonElement>>();
            var guardado = Assert.Single(errores.Elementos, e => e.GetProperty("registroErrorId").GetInt32() == codigo);
            Assert.Equal("cliente", guardado.GetProperty("usuario").GetString());
            Assert.Equal("/carrito", guardado.GetProperty("ruta").GetString());

            Assert.Empty(await AuditoriaAsync(admin, "usuario=cliente&entidad=Logs"));
        }

        [Fact]
        public async Task ElErrorInformadoPorLaApp_TieneUnTamanoMaximo()
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            var respuesta = await cliente.PostAsJsonAsync("/api/Logs/errores", new { mensaje = "x", stackTrace = new string('a', 8001) });

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task TodasLasRespuestasIndicanQueNoSeAdivineElTipoDeContenido()
        {
            var respuesta = await _fabrica.CreateClient().GetAsync("/api/Usuarios/test");

            Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
        }

        [Fact]
        public async Task LosInformesDeErrorDeLaApp_TienenLimiteDeFrecuenciaPorUsuario()
        {
            var limitada = _fabrica.WithWebHostBuilder(host => host.ConfigureAppConfiguration((_, configuracion) =>
                configuracion.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimit:ErroresPorMinuto"] = "3" })));
            var cliente = limitada.CreateClient();
            await cliente.IniciarSesionAsync("cliente", TiendaFactory.PasswordDemo);
            var otro = limitada.CreateClient();
            await otro.IniciarSesionAsync("operador", TiendaFactory.PasswordDemo);

            var respuestas = new List<HttpStatusCode>();
            for (int i = 0; i < 5; i++)
            {
                respuestas.Add((await cliente.PostAsJsonAsync("/api/Logs/errores", new { mensaje = $"error {i}" })).StatusCode);
            }

            Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests }, respuestas);
            // El límite es por usuario: otro usuario no se ve afectado.
            Assert.Equal(HttpStatusCode.OK, (await otro.PostAsJsonAsync("/api/Logs/errores", new { mensaje = "otro" })).StatusCode);
        }

        [Fact]
        public async Task InformarUnErrorSinMensaje_Devuelve400_YSinTokenDevuelve401()
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            var sinMensaje = await cliente.PostAsJsonAsync("/api/Logs/errores", new { mensaje = "" });
            var anonimo = await _fabrica.CreateClient().PostAsJsonAsync("/api/Logs/errores", new { mensaje = "x" });

            Assert.Equal(HttpStatusCode.BadRequest, sinMensaje.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, anonimo.StatusCode);
        }
    }
}
