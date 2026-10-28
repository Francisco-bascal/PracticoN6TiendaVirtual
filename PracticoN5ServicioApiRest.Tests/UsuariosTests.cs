using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>Gestión de usuarios y roles por el administrador, y perfil propio.</summary>
    public class UsuariosTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public UsuariosTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        private static async Task<int> IdDeAsync(HttpClient admin, string nombreUsuario)
        {
            var usuarios = await (await admin.GetAsync("/api/Usuarios")).LeerAsync<List<JsonElement>>();
            return usuarios.First(u => u.GetProperty("nombreUsuario").GetString() == nombreUsuario).GetProperty("usuarioId").GetInt32();
        }

        [Fact]
        public async Task ElAdministradorCreaUsuariosConCualquierRol_YPuedeCambiarselo()
        {
            var admin = await _fabrica.AdminAsync();

            var creado = await (await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "nuevo.operador", email = "no@example.com", password = "secreta1", rol = "Operador" })).LeerAsync<JsonElement>();
            int id = creado.GetProperty("usuarioId").GetInt32();
            Assert.Equal("Operador", creado.GetProperty("rol").GetString());
            Assert.True(creado.GetProperty("activo").GetBoolean());

            var promovido = await (await admin.PutAsJsonAsync($"/api/Usuarios/{id}", new { nombreUsuario = "nuevo.operador", email = "no@example.com", rol = "Administrador" })).LeerAsync<JsonElement>();
            Assert.Equal("Administrador", promovido.GetProperty("rol").GetString());

            // El rol se refleja en el siguiente inicio de sesión.
            var sesion = await _fabrica.CreateClient().PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "nuevo.operador", password = "secreta1" });
            Assert.Equal("Administrador", (await sesion.LeerAsync<LoginDto>()).Rol);
        }

        [Fact]
        public async Task UnRolInexistente_Devuelve400_ConLosRolesValidos()
        {
            var admin = await _fabrica.AdminAsync();

            var respuesta = await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "rol.malo", email = "malo@example.com", password = "secreta1", rol = "SuperUsuario" });

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Contains("Administrador, Operador, Cliente", await respuesta.MensajeAsync());
        }

        [Fact]
        public async Task LaListaDeUsuarios_NuncaExponeLaContrasena()
        {
            var admin = await _fabrica.AdminAsync();

            string json = await (await admin.GetAsync("/api/Usuarios")).Content.ReadAsStringAsync();

            Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AQAAAA", json);
        }

        [Fact]
        public async Task SePuedeEliminarAUnUsuarioSinHistorial_PeroNoAUnoQueYaOpero()
        {
            var admin = await _fabrica.AdminAsync();
            var operador = await _fabrica.OperadorAsync();
            await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "sin.historial", email = "sh@example.com", password = "secreta1", rol = "Operador" });

            // "operador" ya creó movimientos de stock en otros tests de esta clase: acá se asegura el historial.
            await operador.CrearProductoAsync(stock: 3);

            int sinHistorial = await IdDeAsync(admin, "sin.historial");
            int conHistorial = await IdDeAsync(admin, "operador");

            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/Usuarios/{sinHistorial}")).StatusCode);
            var bloqueado = await admin.DeleteAsync($"/api/Usuarios/{conHistorial}");
            Assert.Equal(HttpStatusCode.Conflict, bloqueado.StatusCode);
            Assert.Contains("Desactívelo", await bloqueado.MensajeAsync());
        }

        [Fact]
        public async Task UnClienteConCompras_NoSePuedeEliminar_PeroSiSeDesactiva()
        {
            var admin = await _fabrica.AdminAsync();
            await _fabrica.CreateClient().PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "comprador.fiel", email = "fiel@example.com", password = "secreta1" });
            var comprador = await _fabrica.ClienteAsync("comprador.fiel", "secreta1");
            int producto = await admin.CrearProductoAsync(stock: 5);
            await comprador.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = producto, cantidad = 1 } }, medioPagoId = 3 });
            int id = await IdDeAsync(admin, "comprador.fiel");

            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/Usuarios/{id}")).StatusCode);

            var desactivado = await admin.PutAsJsonAsync($"/api/Usuarios/{id}", new { nombreUsuario = "comprador.fiel", email = "fiel@example.com", rol = "Cliente", activo = false });
            Assert.Equal(HttpStatusCode.OK, desactivado.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await _fabrica.CreateClient().PostAsJsonAsync("/api/Usuarios/login", new { nombreUsuario = "comprador.fiel", password = "secreta1" })).StatusCode);
        }

        [Fact]
        public async Task ElAdministradorNoPuedeDesactivarseASiMismoAunqueHayaOtroAdministrador()
        {
            var admin = await _fabrica.AdminAsync();
            await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "segundo.admin", email = "segundo@example.com", password = "secreta1", rol = "Administrador" });
            int adminId = await IdDeAsync(admin, "admin");

            var respuesta = await admin.PutAsJsonAsync($"/api/Usuarios/{adminId}", new { nombreUsuario = "admin", email = "admin@tienda.local", rol = "Administrador", activo = false });

            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
            Assert.Contains("propio usuario", await respuesta.MensajeAsync());
        }

        [Fact]
        public async Task ElPerfilPropioSeConsultaYSeActualiza()
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            var actualizado = await (await cliente.PutAsJsonAsync("/api/Usuarios/yo", new { nombre = "Clara", apellido = "Demo", telefono = "3815000000", direccion = "Mitre 456" })).LeerAsync<JsonElement>();
            var perfil = await (await cliente.GetAsync("/api/Usuarios/yo")).LeerAsync<JsonElement>();

            Assert.Equal("Clara", actualizado.GetProperty("nombre").GetString());
            Assert.Equal("Mitre 456", perfil.GetProperty("direccion").GetString());
            Assert.Equal("cliente@tienda.local", perfil.GetProperty("email").GetString());
            Assert.Equal("Cliente", perfil.GetProperty("rol").GetString());
        }

        [Fact]
        public async Task UnOperadorSinPerfilDeCliente_TambienPuedeConsultarYCompletarSuPerfil()
        {
            var operador = await _fabrica.OperadorAsync();

            var perfil = await (await operador.GetAsync("/api/Usuarios/yo")).LeerAsync<JsonElement>();
            Assert.Equal("Operador", perfil.GetProperty("rol").GetString());

            var completado = await operador.PutAsJsonAsync("/api/Usuarios/yo", new { nombre = "Olga" });
            Assert.Equal(HttpStatusCode.OK, completado.StatusCode);
        }

        [Fact]
        public async Task SeNiegaElAccesoAlAdministradorDeUsuariosAUnCliente()
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/Usuarios/1")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/Usuarios/usuario/admin")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PutAsJsonAsync("/api/Usuarios/1", new { nombreUsuario = "x", email = "x@example.com", rol = "Administrador" })).StatusCode);
        }
    }

    /// <summary>
    /// Escenario del "único administrador": necesita una base donde nadie más sea administrador,
    /// por eso vive en su propia clase (y su propia fábrica) y no comparte estado con las demás pruebas.
    /// </summary>
    public class UnicoAdministradorTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public UnicoAdministradorTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        [Fact]
        public async Task NoSePuedeDegradarNiDesactivarAlUnicoAdministrador_NiEliminarseASiMismo()
        {
            var admin = await _fabrica.AdminAsync();
            int adminId = await IdDeAdminAsync(admin);

            var degradar = await admin.PutAsJsonAsync($"/api/Usuarios/{adminId}", new { nombreUsuario = "admin", email = "admin@tienda.local", rol = "Operador" });
            var desactivar = await admin.PutAsJsonAsync($"/api/Usuarios/{adminId}", new { nombreUsuario = "admin", email = "admin@tienda.local", rol = "Administrador", activo = false });
            var eliminar = await admin.DeleteAsync($"/api/Usuarios/{adminId}");

            Assert.Equal(HttpStatusCode.Conflict, degradar.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, desactivar.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, eliminar.StatusCode);
        }

        private static async Task<int> IdDeAdminAsync(HttpClient admin)
        {
            var usuarios = await (await admin.GetAsync("/api/Usuarios")).LeerAsync<List<JsonElement>>();
            return usuarios.First(u => u.GetProperty("nombreUsuario").GetString() == "admin").GetProperty("usuarioId").GetInt32();
        }
    }
}
