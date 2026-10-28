using Microsoft.Extensions.DependencyInjection;
using PracticoN5ServicioApiRest.Services;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    public class ReportesTests : IClassFixture<TiendaFactory>
    {
        private readonly TiendaFactory _fabrica;

        public ReportesTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        [Theory]
        [InlineData("stock")]
        [InlineData("entradas")]
        [InlineData("salidas")]
        [InlineData("movimientos")]
        public async Task CadaReporteSeDescargaComoPdfValido(string tipo)
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await operador.GetAsync($"/api/Reportes/{tipo}");

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
            Assert.Contains($"reporte-{tipo}-", respuesta.Content.Headers.ContentDisposition?.FileName);
            var bytes = await respuesta.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 1000);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public async Task LosReportesConPeriodoSeGeneranConFechasEnFormatoIso()
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await operador.GetAsync("/api/Reportes/entradas?desde=2026-01-01&hasta=2026-12-31");

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Fact]
        public async Task UnReporteInexistente_Devuelve404()
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await operador.GetAsync("/api/Reportes/inventado");

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
            Assert.Contains("stock, entradas, salidas y movimientos", await respuesta.MensajeAsync());
        }

        [Fact]
        public async Task UnPeriodoConFechasInvertidas_Devuelve400()
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await operador.GetAsync("/api/Reportes/salidas?desde=2026-05-10&hasta=2026-05-01");

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task SinToken_LosReportesDevuelven401()
        {
            var respuesta = await _fabrica.CreateClient().GetAsync("/api/Reportes/stock");

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }

        [Fact]
        public async Task ElReporteSeEnviaPorCorreoComoAdjuntoPdf()
        {
            var operador = await _fabrica.OperadorAsync();
            string carpeta = Path.Combine(_fabrica.CarpetaContenido, "App_Data", "Correos");
            var antes = Directory.Exists(carpeta) ? Directory.GetFiles(carpeta).ToHashSet() : new HashSet<string>();

            var respuesta = await operador.PostAsJsonAsync("/api/Reportes/stock/enviar", new { destinatario = "gerencia@example.com" });
            var resultado = await respuesta.LeerAsync<JsonElement>();

            Assert.Equal("Archivo", resultado.GetProperty("modo").GetString());
            Assert.False(resultado.GetProperty("enviado").GetBoolean());
            string eml = Directory.GetFiles(carpeta).Except(antes).Single();
            string contenido = await File.ReadAllTextAsync(eml);
            Assert.Contains("gerencia@example.com", contenido);
            Assert.Contains("Reporte de stock", contenido);
            Assert.Contains("reporte-stock-", contenido);
            Assert.Contains("application/pdf", contenido);
        }

        [Theory]
        [InlineData("no-es-un-correo")]
        [InlineData("")]
        public async Task EnviarAUnCorreoInvalido_Devuelve400(string destinatario)
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await operador.PostAsJsonAsync("/api/Reportes/stock/enviar", new { destinatario });

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task EnviarUnReporteInexistente_Devuelve404()
        {
            var operador = await _fabrica.OperadorAsync();

            var respuesta = await operador.PostAsJsonAsync("/api/Reportes/inventado/enviar", new { destinatario = "a@example.com" });

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }

        // ---- Contenido de los reportes (se verifica el armado de datos; el PDF solo los dibuja) ----

        [Fact]
        public async Task ElReporteDeStock_MarcaLosProductosPorDebajoDelMinimo_YSumaElValor()
        {
            var admin = await _fabrica.AdminAsync();
            int escaso = await admin.CrearProductoAsync(precio: 100m, stock: 3);
            int abundante = await admin.CrearProductoAsync(precio: 50m, stock: 40);

            using var scope = _fabrica.Services.CreateScope();
            var reporte = await scope.ServiceProvider.GetRequiredService<ReportesService>().ObtenerStockAsync();

            Assert.Equal(5, reporte.StockMinimo);
            Assert.True(reporte.Filas.Single(f => f.ProductoId == escaso).StockBajo);
            Assert.False(reporte.Filas.Single(f => f.ProductoId == abundante).StockBajo);
            Assert.Equal(reporte.Filas.Sum(f => f.Stock * f.Precio), reporte.ValorTotal);
            Assert.Equal(reporte.Filas.Sum(f => f.Stock), reporte.TotalUnidades);
        }

        [Fact]
        public async Task EntradasYSalidas_ExcluyenLosDocumentosAnulados()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 0, nombre: "Reporte " + Guid.NewGuid().ToString("N")[..6]);
            int proveedor = await admin.ProveedorIdAsync();
            int cliente = await admin.ClienteIdDemoAsync();

            var compraValida = await (await admin.PostAsJsonAsync("/api/Compras", new { proveedorId = proveedor, detalles = new[] { new { productoId = id, cantidad = 10, precioUnitario = 20m } } })).LeerAsync<JsonElement>();
            var compraAnulada = await (await admin.PostAsJsonAsync("/api/Compras", new { proveedorId = proveedor, detalles = new[] { new { productoId = id, cantidad = 4, precioUnitario = 20m } } })).LeerAsync<JsonElement>();
            await admin.PostAsync($"/api/Compras/{compraAnulada.GetProperty("compraId").GetInt32()}/anular", null);

            var ventaValida = await (await admin.PostAsJsonAsync("/api/Ventas", new { clienteId = cliente, detalles = new[] { new { productoId = id, cantidad = 3, precioUnitario = 50m } } })).LeerAsync<JsonElement>();
            var ventaAnulada = await (await admin.PostAsJsonAsync("/api/Ventas", new { clienteId = cliente, detalles = new[] { new { productoId = id, cantidad = 2, precioUnitario = 50m } } })).LeerAsync<JsonElement>();
            await admin.PostAsync($"/api/Ventas/{ventaAnulada.GetProperty("ventaId").GetInt32()}/anular", null);

            string nombre = (await admin.ObtenerProductoAsync(id)).Nombre;
            using var scope = _fabrica.Services.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<ReportesService>();
            var entradas = await servicio.ObtenerEntradasAsync(null, null);
            var salidas = await servicio.ObtenerSalidasAsync(null, null);

            var entrada = Assert.Single(entradas.Filas, f => f.Producto == nombre);
            Assert.Equal(compraValida.GetProperty("compraId").GetInt32(), entrada.Numero);
            Assert.Equal(200m, entrada.Subtotal);
            Assert.Equal("Distribuidora Central S.A.", entrada.Tercero);

            var salida = Assert.Single(salidas.Filas, f => f.Producto == nombre);
            Assert.Equal(ventaValida.GetProperty("ventaId").GetInt32(), salida.Numero);
            Assert.Equal(150m, salida.Subtotal);
            Assert.Equal("Cliente Demo", salida.Tercero);
        }

        [Fact]
        public async Task ElReporteDeMovimientos_IncluyeTodosLosMovimientosDelPeriodo()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            await admin.PatchAsJsonAsync($"/api/Productos/{id}/stock", -4);

            using var scope = _fabrica.Services.CreateScope();
            var hoy = DateTime.UtcNow.Date;
            var reporte = await scope.ServiceProvider.GetRequiredService<ReportesService>().ObtenerMovimientosAsync(hoy.AddDays(-1), hoy.AddDays(1));

            string nombre = (await admin.ObtenerProductoAsync(id)).Nombre;
            Assert.Equal(new[] { 10, -4 }, reporte.Filas.Where(f => f.Producto == nombre).OrderBy(f => f.MovimientoStockId).Select(f => f.Cantidad));
            Assert.True(reporte.TotalEntradas >= 10);
            Assert.True(reporte.TotalSalidas >= 4);
        }

        [Fact]
        public async Task GenerarUnReporte_QuedaRegistradoEnLaAuditoria()
        {
            var operador = await _fabrica.OperadorAsync();
            var admin = await _fabrica.AdminAsync();
            await operador.GetAsync("/api/Reportes/stock");

            var registros = await (await admin.GetAsync("/api/Logs/auditoria?usuario=operador&entidad=Reportes&accion=Generar")).LeerAsync<PaginaDto<JsonElement>>();

            Assert.NotEmpty(registros.Elementos);
            Assert.Contains("/api/Reportes/stock", registros.Elementos[0].GetProperty("detalle").GetString());
        }
    }
}
