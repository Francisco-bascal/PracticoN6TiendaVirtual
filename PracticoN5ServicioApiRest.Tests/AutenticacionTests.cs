using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    public class AutenticacionTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public AutenticacionTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        [Fact]
        public async Task Login_Correcto_DevuelveTokenRolYVencimiento()
        {
            var http = _fabrica.CreateClient();

            var respuesta = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "admin", password = TiendaFactory.PasswordAdmin });
            var login = await respuesta.LeerAsync<LoginDto>();

            Assert.False(string.IsNullOrWhiteSpace(login.Token));
            Assert.Equal("admin", login.NombreUsuario);
            Assert.Equal("Administrador", login.Rol);
            Assert.True(login.ExpiraUtc > DateTime.UtcNow);
        }

        [Fact]
        public async Task Login_ElNombreDeUsuarioNoDistingueMayusculas()
        {
            var http = _fabrica.CreateClient();

            var respuesta = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "ADMIN", password = TiendaFactory.PasswordAdmin });

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Theory]
        [InlineData("admin", "incorrecta")]
        [InlineData("noexiste", "Admin1234")]
        public async Task Login_ConCredencialesInvalidas_Devuelve401(string usuario, string password)
        {
            var http = _fabrica.CreateClient();

            var respuesta = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = usuario, password });

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }

        [Fact]
        public async Task UnaRutaDeLaApiInexistente_Devuelve404_NoLaPaginaDeLaTienda()
        {
            // La API también sirve la tienda web como "fallback"; una ruta /api/... que no existe no debe devolver ese HTML.
            var respuesta = await _fabrica.CreateClient().GetAsync("/api/ruta/que/no/existe");

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            Assert.NotEqual("text/html", respuesta.Content.Headers.ContentType?.MediaType);
        }

        [Fact]
        public async Task RutaProtegida_SinToken_Devuelve401ConMensaje()
        {
            var http = _fabrica.CreateClient();

            var respuesta = await http.GetAsync("/api/Productos");

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
            Assert.Contains("iniciar sesión", await respuesta.MensajeAsync());
        }

        [Fact]
        public async Task RutaProtegida_ConTokenInvalido_Devuelve401()
        {
            var http = _fabrica.CreateClient();
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "esto.no.es-un-token");

            var respuesta = await http.GetAsync("/api/Productos");

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }

        [Fact]
        public async Task Registro_CreaUnClienteConSuPerfilYPuedeIniciarSesion()
        {
            var http = _fabrica.CreateClient();

            var registro = await http.PostAsJsonAsync("/api/Usuarios/registro", new
            {
                nombreUsuario = "maria.perez",
                email = "maria@example.com",
                password = "secreta1",
                nombre = "María",
                apellido = "Pérez",
                telefono = "3815551234",
                direccion = "San Martín 100"
            });
            Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

            var cliente = await _fabrica.ClienteAsync("maria.perez", "secreta1");
            var perfil = await (await cliente.GetAsync("/api/Usuarios/yo")).LeerAsync<JsonElement>();

            Assert.Equal("Cliente", perfil.GetProperty("rol").GetString());
            Assert.Equal("María", perfil.GetProperty("nombre").GetString());
            Assert.Equal("Pérez", perfil.GetProperty("apellido").GetString());
            Assert.NotEqual(JsonValueKind.Null, perfil.GetProperty("clienteId").ValueKind);
        }

        [Fact]
        public async Task Registro_NuncaPermiteElegirElRol()
        {
            var http = _fabrica.CreateClient();

            // Aunque el cuerpo traiga "rol", el registro público siempre crea un Cliente.
            var registro = await http.PostAsJsonAsync("/api/Usuarios/registro", new
            {
                nombreUsuario = "intruso",
                email = "intruso@example.com",
                password = "secreta1",
                rol = "Administrador"
            });
            Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

            var usuario = await _fabrica.ClienteAsync("intruso", "secreta1");
            var perfil = await (await usuario.GetAsync("/api/Usuarios/yo")).LeerAsync<JsonElement>();
            Assert.Equal("Cliente", perfil.GetProperty("rol").GetString());
        }

        [Fact]
        public async Task Registro_ConNombreDeUsuarioOEmailRepetido_Devuelve409()
        {
            var http = _fabrica.CreateClient();
            await (await http.PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "duplicado", email = "dup@example.com", password = "secreta1" })).LeerAsync<JsonElement>();

            var mismoNombre = await http.PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "Duplicado", email = "otro@example.com", password = "secreta1" });
            var mismoEmail = await http.PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "otro.usuario", email = "DUP@example.com", password = "secreta1" });

            Assert.Equal(HttpStatusCode.Conflict, mismoNombre.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, mismoEmail.StatusCode);
        }

        [Fact]
        public async Task Registro_ConContrasenaCorta_Devuelve400()
        {
            var http = _fabrica.CreateClient();

            var respuesta = await http.PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "pass.corta", email = "corta@example.com", password = "123" });

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task Registro_ConDatosInvalidos_Devuelve400()
        {
            var http = _fabrica.CreateClient();

            var respuesta = await http.PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "ab", email = "no-es-un-correo", password = "secreta1" });

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task Registro_NuncaEnlazaLaCuentaConUnClienteExistenteAunqueCoincidaElCorreo()
        {
            // El personal cargó a "Juan" con su historial. Quien se registre con ese correo (sin verificarlo) no debe heredarlo.
            var operador = await _fabrica.OperadorAsync();
            var existente = await (await operador.PostAsJsonAsync("/api/Clientes", new { nombre = "Juan", apellido = "Gómez", email = "juan.gomez@example.com" })).LeerAsync<ClienteDto>();

            var http = _fabrica.CreateClient();
            await http.PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "impostor", email = "juan.gomez@example.com", password = "secreta1" });

            var cuenta = await _fabrica.ClienteAsync("impostor", "secreta1");
            var perfil = await (await cuenta.GetAsync("/api/Usuarios/yo")).LeerAsync<JsonElement>();

            Assert.NotEqual(existente.ClienteId, perfil.GetProperty("clienteId").GetInt32());
            Assert.NotEqual("Juan", perfil.GetProperty("nombre").GetString());
        }

        [Fact]
        public async Task ElPersonalNoPuedeEnlazarUnClienteAUnaCuentaDesdeElCuerpoDeLaPeticion()
        {
            var operador = await _fabrica.OperadorAsync();
            var admin = await _fabrica.AdminAsync();
            var usuarios = await (await admin.GetAsync("/api/Usuarios")).LeerAsync<List<JsonElement>>();
            int idAdmin = usuarios.First(u => u.GetProperty("nombreUsuario").GetString() == "admin").GetProperty("usuarioId").GetInt32();

            // usuarioId no forma parte del contrato de entrada: se ignora.
            await operador.PostAsJsonAsync("/api/Clientes", new { nombre = "Intento", email = "intento@example.com", usuarioId = idAdmin });

            var perfilAdmin = await (await admin.GetAsync("/api/Usuarios/yo")).LeerAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, perfilAdmin.GetProperty("clienteId").ValueKind);
        }

        [Fact]
        public async Task UsuarioDesactivado_NoPuedeIniciarSesion_YPuedeReactivarse()
        {
            var admin = await _fabrica.AdminAsync();
            var creado = await (await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "temporal", email = "temporal@example.com", password = "secreta1", rol = "Operador" })).LeerAsync<JsonElement>();
            int id = creado.GetProperty("usuarioId").GetInt32();
            var http = _fabrica.CreateClient();

            var desactivar = await admin.PutAsJsonAsync($"/api/Usuarios/{id}", new { nombreUsuario = "temporal", email = "temporal@example.com", rol = "Operador", activo = false });
            Assert.Equal(HttpStatusCode.OK, desactivar.StatusCode);

            var bloqueado = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "temporal", password = "secreta1" });
            Assert.Equal(HttpStatusCode.Unauthorized, bloqueado.StatusCode);

            await admin.PutAsJsonAsync($"/api/Usuarios/{id}", new { nombreUsuario = "temporal", email = "temporal@example.com", rol = "Operador", activo = true });
            var permitido = await http.PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "temporal", password = "secreta1" });
            Assert.Equal(HttpStatusCode.OK, permitido.StatusCode);
        }

        [Fact]
        public async Task UnTokenYaEmitido_DejaDeValerCuandoSeDesactivaAlUsuario()
        {
            var admin = await _fabrica.AdminAsync();
            var creado = await (await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "cesante", email = "cesante@example.com", password = "secreta1", rol = "Operador" })).LeerAsync<JsonElement>();
            int id = creado.GetProperty("usuarioId").GetInt32();
            var sesion = await _fabrica.ClienteAsync("cesante", "secreta1");
            Assert.Equal(HttpStatusCode.OK, (await sesion.GetAsync("/api/Clientes")).StatusCode);

            await admin.PutAsJsonAsync($"/api/Usuarios/{id}", new { nombreUsuario = "cesante", email = "cesante@example.com", rol = "Operador", activo = false });

            // El token sigue vigente por fecha, pero el usuario ya no puede usarlo.
            Assert.Equal(HttpStatusCode.Unauthorized, (await sesion.GetAsync("/api/Clientes")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await sesion.GetAsync("/api/Productos")).StatusCode);
        }

        [Fact]
        public async Task UnTokenYaEmitido_DejaDeValerCuandoCambiaElRolDelUsuario()
        {
            var admin = await _fabrica.AdminAsync();
            var creado = await (await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "degradado", email = "degradado@example.com", password = "secreta1", rol = "Operador" })).LeerAsync<JsonElement>();
            var sesionVieja = await _fabrica.ClienteAsync("degradado", "secreta1");
            Assert.Equal(HttpStatusCode.OK, (await sesionVieja.GetAsync("/api/Clientes")).StatusCode);

            await admin.PutAsJsonAsync($"/api/Usuarios/{creado.GetProperty("usuarioId").GetInt32()}", new { nombreUsuario = "degradado", email = "degradado@example.com", rol = "Cliente" });

            Assert.Equal(HttpStatusCode.Unauthorized, (await sesionVieja.GetAsync("/api/Clientes")).StatusCode);

            // Con una sesión nueva rige el rol nuevo: ya no accede al personal.
            var sesionNueva = await _fabrica.ClienteAsync("degradado", "secreta1");
            Assert.Equal(HttpStatusCode.Forbidden, (await sesionNueva.GetAsync("/api/Clientes")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await sesionNueva.GetAsync("/api/Productos")).StatusCode);
        }

        [Fact]
        public async Task UnTokenYaEmitido_DejaDeValerCuandoSeEliminaAlUsuario()
        {
            var admin = await _fabrica.AdminAsync();
            var creado = await (await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "eliminado", email = "eliminado@example.com", password = "secreta1", rol = "Operador" })).LeerAsync<JsonElement>();
            var sesion = await _fabrica.ClienteAsync("eliminado", "secreta1");

            await admin.DeleteAsync($"/api/Usuarios/{creado.GetProperty("usuarioId").GetInt32()}");

            Assert.Equal(HttpStatusCode.Unauthorized, (await sesion.GetAsync("/api/Productos")).StatusCode);
        }
    }
}
