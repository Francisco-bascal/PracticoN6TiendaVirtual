using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>Compras, ventas y ajustes: el stock cambia y cada cambio queda en el libro de movimientos.</summary>
    public class StockTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public StockTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        private static object Compra(int proveedorId, int productoId, int cantidad, decimal costo = 500m) =>
            new { proveedorId, detalles = new[] { new { productoId, cantidad, precioUnitario = costo } } };

        private static object Venta(int clienteId, int productoId, int cantidad, decimal precio = 1500m) =>
            new { clienteId, detalles = new[] { new { productoId, cantidad, precioUnitario = precio } } };

        [Fact]
        public async Task UnProductoNuevoConStock_RegistraElStockInicialComoMovimiento()
        {
            var admin = await _fabrica.AdminAsync();

            int id = await admin.CrearProductoAsync(stock: 12);

            var movimientos = await admin.MovimientosAsync(id);
            var inicial = Assert.Single(movimientos);
            Assert.Equal("StockInicial", inicial.Motivo);
            Assert.Equal(12, inicial.Cantidad);
        }

        [Fact]
        public async Task UnProductoNuevoSinStock_NoGeneraMovimientos()
        {
            var admin = await _fabrica.AdminAsync();

            int id = await admin.CrearProductoAsync(stock: 0);

            Assert.Empty(await admin.MovimientosAsync(id));
        }

        [Fact]
        public async Task EditarElStockDelProducto_RegistraUnAjusteConLaDiferencia()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var producto = await admin.ObtenerProductoAsync(id);

            var respuesta = await admin.PutAsJsonAsync($"/api/Productos/{id}", new
            {
                nombre = producto.Nombre,
                descripcion = producto.Descripcion,
                precio = producto.Precio,
                stock = 7,
                categoriaId = producto.CategoriaId
            });
            var actualizado = await respuesta.LeerAsync<ProductoDto>();

            Assert.Equal(7, actualizado.Stock);
            var ajuste = (await admin.MovimientosAsync(id)).First(m => m.Motivo == "AjusteManual");
            Assert.Equal(-3, ajuste.Cantidad);
        }

        [Fact]
        public async Task AjusteManualDeStock_QuedaRegistradoConObservacion_YNoPermiteStockNegativo()
        {
            var operador = await _fabrica.OperadorAsync();
            int id = await operador.CrearProductoAsync(stock: 10);

            var baja = await operador.PatchAsJsonAsync($"/api/Productos/{id}/stock?observacion=Rotura%20en%20dep%C3%B3sito", -3);
            Assert.Equal(HttpStatusCode.OK, baja.StatusCode);
            Assert.Equal(7, (await operador.ObtenerProductoAsync(id)).Stock);

            var ajuste = (await operador.MovimientosAsync(id)).First(m => m.Motivo == "AjusteManual");
            Assert.Equal(-3, ajuste.Cantidad);
            Assert.Equal("Rotura en depósito", ajuste.Observacion);
            Assert.Equal("operador", ajuste.Usuario);

            var demasiado = await operador.PatchAsJsonAsync($"/api/Productos/{id}/stock", -100);
            Assert.Equal(HttpStatusCode.BadRequest, demasiado.StatusCode);
            Assert.Equal(7, (await operador.ObtenerProductoAsync(id)).Stock);
        }

        [Fact]
        public async Task UnaObservacionDemasiadoLarga_SeRechazaSinTocarElStock()
        {
            var operador = await _fabrica.OperadorAsync();
            int id = await operador.CrearProductoAsync(stock: 10);

            var respuesta = await operador.PatchAsJsonAsync($"/api/Productos/{id}/stock?observacion={new string('x', 201)}", -3);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Equal(10, (await operador.ObtenerProductoAsync(id)).Stock);
            Assert.Single(await operador.MovimientosAsync(id));   // solo el stock inicial
        }

        [Fact]
        public async Task UnaCompra_SumaStockYRegistraLaEntradaConSuUsuario()
        {
            var operador = await _fabrica.OperadorAsync();
            int id = await operador.CrearProductoAsync(stock: 0);

            var respuesta = await operador.PostAsJsonAsync("/api/Compras", Compra(await operador.ProveedorIdAsync(), id, 20));
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            var compra = await respuesta.LeerAsync<JsonElement>();

            Assert.Equal(20, (await operador.ObtenerProductoAsync(id)).Stock);
            var entrada = Assert.Single(await operador.MovimientosAsync(id));
            Assert.Equal("Compra", entrada.Motivo);
            Assert.Equal(20, entrada.Cantidad);
            Assert.Equal(compra.GetProperty("compraId").GetInt32(), entrada.CompraId);
            Assert.Equal("operador", entrada.Usuario);
        }

        [Fact]
        public async Task UnaCompraConUnProductoInexistente_Devuelve404_YNoDejaStockNiMovimientosParcialesDelOtroProducto()
        {
            var operador = await _fabrica.OperadorAsync();
            int id = await operador.CrearProductoAsync(stock: 5);
            int comprasAntes = (await (await operador.GetAsync("/api/Compras")).LeerAsync<PaginaDto<JsonElement>>()).TotalElementos;

            var respuesta = await operador.PostAsJsonAsync("/api/Compras", new
            {
                proveedorId = await operador.ProveedorIdAsync(),
                detalles = new[]
                {
                    new { productoId = id, cantidad = 10, precioUnitario = 100m },
                    new { productoId = 999999, cantidad = 1, precioUnitario = 100m }
                }
            });

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            Assert.Equal(5, (await operador.ObtenerProductoAsync(id)).Stock);
            Assert.DoesNotContain(await operador.MovimientosAsync(id), m => m.Motivo == "Compra");
            // Tampoco queda una compra "fantasma" sin efecto sobre el stock.
            Assert.Equal(comprasAntes, (await (await operador.GetAsync("/api/Compras")).LeerAsync<PaginaDto<JsonElement>>()).TotalElementos);
        }

        [Fact]
        public async Task AnularUnaCompra_RevierteElStock_ConservaElRegistro_YNoSePuedeAnularDosVeces()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 0);
            var compra = await (await admin.PostAsJsonAsync("/api/Compras", Compra(await admin.ProveedorIdAsync(), id, 8))).LeerAsync<JsonElement>();
            int compraId = compra.GetProperty("compraId").GetInt32();

            var anulacion = await admin.PostAsync($"/api/Compras/{compraId}/anular", null);
            Assert.Equal(HttpStatusCode.OK, anulacion.StatusCode);

            Assert.Equal(0, (await admin.ObtenerProductoAsync(id)).Stock);
            var guardada = await (await admin.GetAsync($"/api/Compras/{compraId}")).LeerAsync<JsonElement>();
            Assert.True(guardada.GetProperty("anulada").GetBoolean());
            Assert.NotEqual(JsonValueKind.Null, guardada.GetProperty("fechaAnulacion").ValueKind);
            Assert.Contains(await admin.MovimientosAsync(id), m => m.Motivo == "AnulacionCompra" && m.Cantidad == -8);

            var otraVez = await admin.PostAsync($"/api/Compras/{compraId}/anular", null);
            Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
        }

        [Fact]
        public async Task NoSePuedeAnularUnaCompraCuyoStockYaSeVendio()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 0);
            var compra = await (await admin.PostAsJsonAsync("/api/Compras", Compra(await admin.ProveedorIdAsync(), id, 5))).LeerAsync<JsonElement>();
            await admin.PostAsJsonAsync("/api/Ventas", Venta(await admin.ClienteIdDemoAsync(), id, 5));

            var anulacion = await admin.PostAsync($"/api/Compras/{compra.GetProperty("compraId").GetInt32()}/anular", null);

            Assert.Equal(HttpStatusCode.Conflict, anulacion.StatusCode);
            Assert.Contains("stock actual", await anulacion.MensajeAsync());
            var sinCambios = await (await admin.GetAsync($"/api/Compras/{compra.GetProperty("compraId").GetInt32()}")).LeerAsync<JsonElement>();
            Assert.False(sinCambios.GetProperty("anulada").GetBoolean());
        }

        [Fact]
        public async Task UnaVentaDelPersonal_RestaStockYRegistraLaSalida()
        {
            var operador = await _fabrica.OperadorAsync();
            int id = await operador.CrearProductoAsync(stock: 10);

            var respuesta = await operador.PostAsJsonAsync("/api/Ventas", Venta(await operador.ClienteIdDemoAsync(), id, 4));
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            var venta = await respuesta.LeerAsync<JsonElement>();

            Assert.Equal(6, (await operador.ObtenerProductoAsync(id)).Stock);
            var salida = (await operador.MovimientosAsync(id)).First(m => m.Motivo == "Venta");
            Assert.Equal(-4, salida.Cantidad);
            Assert.Equal(venta.GetProperty("ventaId").GetInt32(), salida.VentaId);
        }

        [Fact]
        public async Task UnaVentaSinStockSuficiente_SeRechazaYNoCambiaNada()
        {
            var operador = await _fabrica.OperadorAsync();
            int id = await operador.CrearProductoAsync(stock: 2);

            var respuesta = await operador.PostAsJsonAsync("/api/Ventas", Venta(await operador.ClienteIdDemoAsync(), id, 3));

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Contains("Stock insuficiente", await respuesta.MensajeAsync());
            Assert.Equal(2, (await operador.ObtenerProductoAsync(id)).Stock);
            Assert.DoesNotContain(await operador.MovimientosAsync(id), m => m.Motivo == "Venta");
        }

        [Fact]
        public async Task AnularUnaVenta_RestituyeElStock_NoBorraLaVenta_ySoloLoHaceElAdministrador()
        {
            var admin = await _fabrica.AdminAsync();
            var operador = await _fabrica.OperadorAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var venta = await (await operador.PostAsJsonAsync("/api/Ventas", Venta(await operador.ClienteIdDemoAsync(), id, 4))).LeerAsync<JsonElement>();
            int ventaId = venta.GetProperty("ventaId").GetInt32();

            var denegada = await operador.PostAsync($"/api/Ventas/{ventaId}/anular", null);
            Assert.Equal(HttpStatusCode.Forbidden, denegada.StatusCode);

            var anulada = await admin.PostAsync($"/api/Ventas/{ventaId}/anular", null);
            Assert.Equal(HttpStatusCode.OK, anulada.StatusCode);

            Assert.Equal(10, (await admin.ObtenerProductoAsync(id)).Stock);
            var guardada = await (await admin.GetAsync($"/api/Ventas/{ventaId}")).LeerAsync<JsonElement>();
            Assert.True(guardada.GetProperty("anulada").GetBoolean());
            Assert.Contains(await admin.MovimientosAsync(id), m => m.Motivo == "AnulacionVenta" && m.Cantidad == 4);

            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/Ventas/{ventaId}/anular", null)).StatusCode);
        }

        [Fact]
        public async Task ElStockDeUnProductoEsLaSumaDeSusMovimientos()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            int proveedor = await admin.ProveedorIdAsync();
            int cliente = await admin.ClienteIdDemoAsync();

            await admin.PostAsJsonAsync("/api/Compras", Compra(proveedor, id, 7));
            await admin.PostAsJsonAsync("/api/Ventas", Venta(cliente, id, 5));
            await admin.PatchAsJsonAsync($"/api/Productos/{id}/stock", -2);
            var venta = await (await admin.PostAsJsonAsync("/api/Ventas", Venta(cliente, id, 3))).LeerAsync<JsonElement>();
            await admin.PostAsync($"/api/Ventas/{venta.GetProperty("ventaId").GetInt32()}/anular", null);

            var producto = await admin.ObtenerProductoAsync(id);
            var movimientos = await admin.MovimientosAsync(id);

            Assert.Equal(producto.Stock, movimientos.Sum(m => m.Cantidad));
            Assert.Equal(10 + 7 - 5 - 2, producto.Stock);
        }

        [Fact]
        public async Task UnProductoConVentas_NoSePuedeEliminar_PeroUnoSoloConAjustesSi()
        {
            var admin = await _fabrica.AdminAsync();
            int vendido = await admin.CrearProductoAsync(stock: 5);
            await admin.PostAsJsonAsync("/api/Ventas", Venta(await admin.ClienteIdDemoAsync(), vendido, 1));
            int sinHistorial = await admin.CrearProductoAsync(stock: 5);

            Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/Productos/{vendido}")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/Productos/{sinHistorial}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/Productos/{sinHistorial}")).StatusCode);
        }

        [Fact]
        public async Task ElListadoDeMovimientos_FiltraPorMotivoYPorFecha()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            await admin.PostAsJsonAsync("/api/Compras", Compra(await admin.ProveedorIdAsync(), id, 3));

            string ayer = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            string manana = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

            var compras = await (await admin.GetAsync($"/api/MovimientosStock?productoId={id}&motivo=Compra")).LeerAsync<PaginaDto<MovimientoDto>>();
            var enRango = await (await admin.GetAsync($"/api/MovimientosStock?productoId={id}&desde={ayer}&hasta={manana}")).LeerAsync<PaginaDto<MovimientoDto>>();
            var viejos = await (await admin.GetAsync($"/api/MovimientosStock?productoId={id}&desde=2000-01-01&hasta=2000-12-31")).LeerAsync<PaginaDto<MovimientoDto>>();

            Assert.Single(compras.Elementos);
            Assert.Equal(2, enRango.TotalElementos);
            Assert.Equal(0, viejos.TotalElementos);
        }

        [Fact]
        public async Task LasFechasInvertidasEnUnFiltro_Devuelven400()
        {
            var admin = await _fabrica.AdminAsync();

            var respuesta = await admin.GetAsync("/api/MovimientosStock?desde=2026-02-01&hasta=2026-01-01");

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
    }

    internal static class HttpClientExtensions
    {
        public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient http, string ruta, T valor)
        {
            return http.PatchAsync(ruta, JsonContent.Create(valor));
        }
    }
}
