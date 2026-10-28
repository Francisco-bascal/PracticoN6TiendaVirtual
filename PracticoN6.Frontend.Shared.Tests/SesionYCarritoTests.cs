using System.Net;
using System.Security.Claims;
using System.Text.Json;
using PracticoN6.Frontend.Shared.Models;
using PracticoN6.Frontend.Shared.Services;

namespace PracticoN6.Frontend.Shared.Tests
{
    public class SesionServiceTests
    {
        [Fact]
        public async Task ElLoginGuardaLaSesion_ConfiguraElToken_YExponeElRol()
        {
            var entorno = new Entorno();

            await entorno.IniciarSesionComoAsync("ana", "Operador");

            Assert.True(entorno.Sesion.EstaAutenticado);
            Assert.True(entorno.Sesion.EsPersonal);
            Assert.False(entorno.Sesion.EsAdministrador);
            Assert.Equal("token-de-ana", entorno.Credenciales.Token);

            var estado = await entorno.Sesion.GetAuthenticationStateAsync();
            Assert.True(estado.User.Identity!.IsAuthenticated);
            Assert.Equal("ana", estado.User.Identity.Name);
            Assert.True(estado.User.IsInRole("Operador"));
            Assert.False(estado.User.IsInRole("Administrador"));
        }

        [Fact]
        public async Task SinSesion_ElEstadoEsAnonimo()
        {
            var entorno = new Entorno();

            var estado = await entorno.Sesion.GetAuthenticationStateAsync();

            Assert.False(estado.User.Identity!.IsAuthenticated);
            Assert.False(entorno.Sesion.EstaAutenticado);
        }

        [Fact]
        public async Task UnLoginConCredencialesInvalidas_PropagaElErrorYNoDejaSesion()
        {
            var entorno = new Entorno();
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Texto("Nombre de usuario o contraseña incorrectos.", HttpStatusCode.Unauthorized);

            var ex = await Assert.ThrowsAsync<ApiException>(() => entorno.Sesion.IniciarSesionAsync("ana", "mala"));

            Assert.Equal(401, ex.Codigo);
            Assert.False(entorno.Sesion.EstaAutenticado);
            Assert.Null(entorno.Credenciales.Token);
            Assert.Empty(entorno.Almacenamiento.Datos);
        }

        [Fact]
        public async Task LaSesionGuardadaSeRestauraAlReiniciarLaApp()
        {
            var primera = new Entorno();
            await primera.IniciarSesionComoAsync("ana", "Cliente");

            var reiniciada = new Entorno();
            foreach (var par in primera.Almacenamiento.Datos)
            {
                reiniciada.Almacenamiento.Datos[par.Key] = par.Value;
            }

            var estado = await reiniciada.Sesion.GetAuthenticationStateAsync();

            Assert.True(estado.User.Identity!.IsAuthenticated);
            Assert.Equal("ana", reiniciada.Sesion.NombreUsuario);
            Assert.Equal("token-de-ana", reiniciada.Credenciales.Token);
        }

        [Fact]
        public async Task UnaSesionGuardadaYaVencida_SeDescartaEnLugarDeRestaurarse()
        {
            var entorno = new Entorno();
            entorno.Almacenamiento.Datos["sesion"] = Entorno.RespuestaLogin("ana", "Cliente", DateTime.UtcNow.AddMinutes(-5));

            var estado = await entorno.Sesion.GetAuthenticationStateAsync();

            Assert.False(estado.User.Identity!.IsAuthenticated);
            Assert.Null(entorno.Credenciales.Token);
            Assert.DoesNotContain("sesion", entorno.Almacenamiento.Datos.Keys);
        }

        [Fact]
        public async Task UnaSesionGuardadaIlegible_EquivaleANoTenerSesion()
        {
            var entorno = new Entorno();
            entorno.Almacenamiento.Datos["sesion"] = "{esto no es un json";

            var estado = await entorno.Sesion.GetAuthenticationStateAsync();

            Assert.False(estado.User.Identity!.IsAuthenticated);
        }

        [Fact]
        public async Task CerrarSesion_LimpiaTodoYAvisa()
        {
            var entorno = new Entorno();
            await entorno.IniciarSesionComoAsync("ana");
            int cambios = 0;
            entorno.Sesion.SesionCambio += () => cambios++;
            AuthenticationStateCambio? ultimo = null;
            entorno.Sesion.AuthenticationStateChanged += tarea => ultimo = new AuthenticationStateCambio(tarea);

            await entorno.Sesion.CerrarSesionAsync();

            Assert.False(entorno.Sesion.EstaAutenticado);
            Assert.Null(entorno.Credenciales.Token);
            Assert.DoesNotContain("sesion", entorno.Almacenamiento.Datos.Keys);
            Assert.Equal(1, cambios);
            Assert.False((await ultimo!.Tarea).User.Identity!.IsAuthenticated);
        }

        [Fact]
        public async Task UnaSesionVencidaEnElServidor_CierraLaSesionYDejaUnAviso()
        {
            var entorno = new Entorno();
            await entorno.IniciarSesionComoAsync("ana");
            entorno.Servidor.Respuesta = _ => ManejadorFalso.Json("""{"mensaje":"Debe iniciar sesión"}""", HttpStatusCode.Unauthorized);

            await Assert.ThrowsAsync<ApiException>(() => entorno.Tienda.ObtenerMisPedidosAsync());

            Assert.False(entorno.Sesion.EstaAutenticado);
            Assert.Contains("venció", entorno.Sesion.AvisoPendiente);
        }

        private record AuthenticationStateCambio(Task<Microsoft.AspNetCore.Components.Authorization.AuthenticationState> Tarea);
    }

    public class CarritoServiceTests
    {
        private static Producto Producto(int id, string nombre, decimal precio, int stock) =>
            new() { ProductoId = id, Nombre = nombre, Precio = precio, Stock = stock, Disponible = stock > 0 };

        private static async Task<Entorno> ConSesionAsync(string usuario = "ana")
        {
            var entorno = new Entorno();
            await entorno.IniciarSesionComoAsync(usuario);
            await entorno.Carrito.CargarAsync();
            return entorno;
        }

        [Fact]
        public async Task AgregaProductosYCalculaUnidadesYTotal()
        {
            var entorno = await ConSesionAsync();

            await entorno.Carrito.AgregarAsync(Producto(1, "Auriculares", 28500m, 25), 2);
            await entorno.Carrito.AgregarAsync(Producto(2, "Soga", 5200m, 50));

            Assert.Equal(3, entorno.Carrito.Unidades);
            Assert.Equal(2 * 28500m + 5200m, entorno.Carrito.Total);
            Assert.Equal(2, entorno.Carrito.Items.Count);
        }

        [Fact]
        public async Task AgregarUnProductoYaPresenteSumaCantidad_SinSuperarElStock()
        {
            var entorno = await ConSesionAsync();
            var producto = Producto(1, "Parlante", 34900m, 3);

            int primera = await entorno.Carrito.AgregarAsync(producto, 2);
            int segunda = await entorno.Carrito.AgregarAsync(producto, 5);
            int tercera = await entorno.Carrito.AgregarAsync(producto, 1);

            Assert.Equal(2, primera);
            Assert.Equal(1, segunda);   // solo cabía una más
            Assert.Equal(0, tercera);   // ya está el máximo
            Assert.Equal(3, entorno.Carrito.CantidadDe(1));
        }

        [Fact]
        public async Task UnProductoSinStockNoEntraAlCarrito()
        {
            var entorno = await ConSesionAsync();

            int agregadas = await entorno.Carrito.AgregarAsync(Producto(1, "Teclado", 19800m, 0));

            Assert.Equal(0, agregadas);
            Assert.True(entorno.Carrito.EstaVacio);
        }

        [Fact]
        public async Task CambiarLaCantidad_RespetaElStockYQuitaElItemEnCero()
        {
            var entorno = await ConSesionAsync();
            await entorno.Carrito.AgregarAsync(Producto(1, "Cargador", 12900m, 4));

            await entorno.Carrito.CambiarCantidadAsync(1, 10);
            Assert.Equal(4, entorno.Carrito.CantidadDe(1));

            await entorno.Carrito.CambiarCantidadAsync(1, 2);
            Assert.Equal(2, entorno.Carrito.CantidadDe(1));

            await entorno.Carrito.CambiarCantidadAsync(1, 0);
            Assert.True(entorno.Carrito.EstaVacio);
        }

        [Fact]
        public async Task QuitarYVaciar()
        {
            var entorno = await ConSesionAsync();
            await entorno.Carrito.AgregarAsync(Producto(1, "A", 10m, 5));
            await entorno.Carrito.AgregarAsync(Producto(2, "B", 20m, 5));

            await entorno.Carrito.QuitarAsync(1);
            Assert.Equal(new[] { 2 }, entorno.Carrito.Items.Select(i => i.ProductoId));

            await entorno.Carrito.VaciarAsync();
            Assert.True(entorno.Carrito.EstaVacio);
        }

        [Fact]
        public async Task AlAgregarDeNuevoSeActualizanElPrecioYElStockVigentes()
        {
            var entorno = await ConSesionAsync();
            await entorno.Carrito.AgregarAsync(Producto(1, "Pava", 24500m, 10));

            await entorno.Carrito.AgregarAsync(Producto(1, "Pava", 26000m, 10));

            Assert.Equal(26000m, entorno.Carrito.Items.Single().PrecioUnitario);
        }

        [Fact]
        public async Task CadaUsuarioTieneSuPropioCarrito_QueSeConservaEntreSesiones()
        {
            var entorno = await ConSesionAsync("ana");
            await entorno.Carrito.AgregarAsync(Producto(1, "A", 10m, 5), 2);

            await entorno.Sesion.CerrarSesionAsync();
            await Task.Delay(50);   // el cambio de sesión recarga el carrito sin esperar a nadie
            Assert.True(entorno.Carrito.EstaVacio);

            await entorno.IniciarSesionComoAsync("beto");
            await entorno.Carrito.CargarAsync();
            Assert.True(entorno.Carrito.EstaVacio);
            await entorno.Carrito.AgregarAsync(Producto(2, "B", 20m, 5));

            await entorno.Sesion.CerrarSesionAsync();
            await entorno.IniciarSesionComoAsync("ana");
            await entorno.Carrito.CargarAsync();

            Assert.Equal(new[] { 1 }, entorno.Carrito.Items.Select(i => i.ProductoId));
            Assert.Equal(2, entorno.Carrito.CantidadDe(1));
        }

        [Fact]
        public async Task CargarElCarritoVariasVecesAlMismoTiempo_NoDuplicaLosItems()
        {
            // Al iniciar sesión el carrito se carga desde dos lugares a la vez; con un almacenamiento realmente asíncrono
            // (como el del navegador) las lecturas se superponen.
            var entorno = new Entorno(almacenamiento: new AlmacenamientoLento());
            await entorno.IniciarSesionComoAsync("ana");
            entorno.Almacenamiento.Datos["carrito.ana"] =
                """[{"productoId":1,"nombre":"A","precioUnitario":10,"cantidad":2,"stock":5}]""";

            await Task.WhenAll(entorno.Carrito.CargarAsync(), entorno.Carrito.CargarAsync(), entorno.Carrito.CargarAsync());

            var item = Assert.Single(entorno.Carrito.Items);
            Assert.Equal(2, item.Cantidad);
            Assert.Equal(20m, entorno.Carrito.Total);
        }

        [Fact]
        public async Task ElCarritoSobreviveAlReinicioDeLaApp()
        {
            var primera = await ConSesionAsync("ana");
            await primera.Carrito.AgregarAsync(Producto(1, "A", 10m, 5), 3);

            var reiniciada = new Entorno();
            foreach (var par in primera.Almacenamiento.Datos)
            {
                reiniciada.Almacenamiento.Datos[par.Key] = par.Value;
            }
            await reiniciada.Sesion.AsegurarRestauradaAsync();
            await reiniciada.Carrito.CargarAsync();

            Assert.Equal(3, reiniciada.Carrito.CantidadDe(1));
        }

        [Fact]
        public async Task UnCarritoGuardadoIlegible_SeDescartaYEmpiezaVacio()
        {
            var entorno = new Entorno();
            await entorno.IniciarSesionComoAsync("ana");
            entorno.Almacenamiento.Datos["carrito.ana"] = "[[[no es json";

            await entorno.Carrito.CargarAsync();

            Assert.True(entorno.Carrito.EstaVacio);
        }

        [Fact]
        public async Task CadaCambioNotificaALaInterfaz()
        {
            var entorno = await ConSesionAsync();
            int avisos = 0;
            entorno.Carrito.Cambio += () => avisos++;

            await entorno.Carrito.AgregarAsync(Producto(1, "A", 10m, 5));
            await entorno.Carrito.CambiarCantidadAsync(1, 3);
            await entorno.Carrito.VaciarAsync();

            Assert.Equal(3, avisos);
        }

        [Fact]
        public async Task ElCarritoSeGuardaEnPreferenciasNoEnAlmacenamientoSeguroSinNombreDeUsuarioEnClaro()
        {
            var entorno = await ConSesionAsync("Ana");
            await entorno.Carrito.AgregarAsync(Producto(1, "A", 10m, 5));

            // La clave usa el usuario en minúsculas; el contenido es JSON con los ítems.
            Assert.True(entorno.Almacenamiento.Datos.TryGetValue("carrito.ana", out var json));
            using var documento = JsonDocument.Parse(json!);
            Assert.Equal(1, documento.RootElement.GetArrayLength());
        }
    }

    public class ModelosTests
    {
        [Fact]
        public void LaVentaDeLaApiSeConvierteEnUnPedido()
        {
            const string json = """
                {"ventaId":9,"fecha":"2026-10-27T10:00:00Z","anulada":false,"clienteId":2,
                 "cliente":{"nombre":"Cliente","apellido":"Demo"},
                 "pago":{"codigoAutorizacion":"SIM-1234ABCD","medioPago":{"nombre":"Transferencia bancaria"}},
                 "detalles":[{"productoId":1,"cantidad":2,"precioUnitario":100.5,"producto":{"nombre":"Auriculares"}},
                             {"productoId":2,"cantidad":1,"precioUnitario":50,"producto":{"nombre":"Soga"}}]}
                """;

            var venta = JsonSerializer.Deserialize<Venta>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var pedido = venta.APedido();

            Assert.Equal(9, pedido.VentaId);
            Assert.Equal("Cliente Demo", pedido.Cliente);
            Assert.Equal("Transferencia bancaria", pedido.MedioPago);
            Assert.Equal("SIM-1234ABCD", pedido.CodigoAutorizacion);
            Assert.Equal(251m, pedido.Total);
            Assert.Equal(new[] { "Auriculares", "Soga" }, pedido.Items.Select(i => i.Producto));
        }

        [Fact]
        public void UnaVentaSinPagoNiClienteNoRompeLaConversion()
        {
            var pedido = new Venta { VentaId = 1 }.APedido();

            Assert.Null(pedido.MedioPago);
            Assert.Equal(string.Empty, pedido.Cliente);
            Assert.Equal(0m, pedido.Total);
        }
    }
}
