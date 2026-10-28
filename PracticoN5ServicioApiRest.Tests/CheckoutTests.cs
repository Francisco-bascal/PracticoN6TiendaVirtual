using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>Compra desde el carrito con pago simulado.</summary>
    public class CheckoutTests : IClassFixture<TiendaFactory>
    {
        private const int Credito = 1;
        private const int Transferencia = 3;

        private readonly TiendaFactory _fabrica;

        public CheckoutTests(TiendaFactory fabrica)
        {
            _fabrica = fabrica;
        }

        [Fact]
        public async Task UsaLosPreciosDelServidor_DescuentaStock_YRegistraElPago()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(precio: 1500m, stock: 10);

            // El cliente intenta pagar un precio de $1 por unidad: el servidor lo ignora.
            var respuesta = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new
            {
                items = new[] { new { productoId = id, cantidad = 2, precioUnitario = 1m } },
                medioPagoId = Transferencia
            });
            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
            var pedido = await respuesta.LeerAsync<PedidoDto>();

            Assert.Equal(3000m, pedido.Total);
            Assert.Equal(1500m, pedido.Items.Single().PrecioUnitario);
            Assert.Equal("Transferencia bancaria", pedido.MedioPago);
            Assert.StartsWith("SIM-", pedido.CodigoAutorizacion);
            Assert.False(pedido.Anulada);
            Assert.Equal(8, (await admin.ObtenerProductoAsync(id)).Stock);

            var salida = (await admin.MovimientosAsync(id)).First(m => m.Motivo == "Venta");
            Assert.Equal(-2, salida.Cantidad);
            Assert.Equal("cliente", salida.Usuario);
            Assert.Equal(pedido.VentaId, salida.VentaId);
        }

        [Fact]
        public async Task LaCompraQuedaEnMisPedidos_ConSusItems()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(precio: 200m, stock: 10);
            var pedido = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 3 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();

            var pedidos = await (await cliente.GetAsync("/api/Ventas/mis-pedidos")).LeerAsync<List<PedidoDto>>();

            var guardado = pedidos.Single(p => p.VentaId == pedido.VentaId);
            Assert.Equal(600m, guardado.Total);
            Assert.Equal("Cliente Demo", guardado.Cliente);
            Assert.Equal(3, guardado.Items.Single().Cantidad);
        }

        [Fact]
        public async Task LosPedidosVienenOrdenadosDelMasRecienteAlMasAntiguo()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var primero = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();
            var segundo = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();

            var pedidos = await (await cliente.GetAsync("/api/Ventas/mis-pedidos")).LeerAsync<List<PedidoDto>>();

            Assert.True(pedidos.FindIndex(p => p.VentaId == segundo.VentaId) < pedidos.FindIndex(p => p.VentaId == primero.VentaId));
        }

        [Fact]
        public async Task ConTarjeta_ExigeUnNumeroValido_YConUnoCorrectoAprueba()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);

            var sinNumero = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Credito });
            var numeroCorto = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Credito, numeroTarjeta = "1234" });
            var aprobada = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Credito, numeroTarjeta = "4242 4242 4242 4242" });

            Assert.Equal(HttpStatusCode.BadRequest, sinNumero.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, numeroCorto.StatusCode);
            Assert.Equal(HttpStatusCode.Created, aprobada.StatusCode);
            Assert.Equal("Tarjeta de crédito", (await aprobada.LeerAsync<PedidoDto>()).MedioPago);
            Assert.Equal(9, (await admin.ObtenerProductoAsync(id)).Stock);
        }

        [Fact]
        public async Task UnPagoRechazado_Devuelve402_YNoDejaStockNiVentaNiMovimientos()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            int pedidosAntes = (await (await cliente.GetAsync("/api/Ventas/mis-pedidos")).LeerAsync<List<PedidoDto>>()).Count;

            var respuesta = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new
            {
                items = new[] { new { productoId = id, cantidad = 4 } },
                medioPagoId = Credito,
                numeroTarjeta = "4000000000000000"
            });

            Assert.Equal(HttpStatusCode.PaymentRequired, respuesta.StatusCode);
            Assert.Contains("rechazado", await respuesta.MensajeAsync());
            Assert.Equal(10, (await admin.ObtenerProductoAsync(id)).Stock);
            Assert.DoesNotContain(await admin.MovimientosAsync(id), m => m.Motivo == "Venta");
            Assert.Equal(pedidosAntes, (await (await cliente.GetAsync("/api/Ventas/mis-pedidos")).LeerAsync<List<PedidoDto>>()).Count);
        }

        [Fact]
        public async Task SinStockSuficiente_Devuelve409_YNingunaLineaDelCarritoQuedaDescontada()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int abundante = await admin.CrearProductoAsync(stock: 10);
            int escaso = await admin.CrearProductoAsync(stock: 1);

            var respuesta = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new
            {
                items = new[] { new { productoId = abundante, cantidad = 3 }, new { productoId = escaso, cantidad = 2 } },
                medioPagoId = Transferencia
            });

            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
            Assert.Contains("Stock insuficiente", await respuesta.MensajeAsync());
            Assert.Equal(10, (await admin.ObtenerProductoAsync(abundante)).Stock);
            Assert.Equal(1, (await admin.ObtenerProductoAsync(escaso)).Stock);
        }

        [Fact]
        public async Task LaUltimaUnidadSeVendeUnaSolaVez()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 1);
            var compra = new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia };

            var primera = await cliente.PostAsJsonAsync("/api/Ventas/checkout", compra);
            var segunda = await cliente.PostAsJsonAsync("/api/Ventas/checkout", compra);

            Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, segunda.StatusCode);
            Assert.Equal(0, (await admin.ObtenerProductoAsync(id)).Stock);
        }

        [Fact]
        public async Task LasLineasRepetidasDelMismoProductoSeUnen()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(precio: 100m, stock: 10);

            var pedido = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new
            {
                items = new[] { new { productoId = id, cantidad = 1 }, new { productoId = id, cantidad = 2 } },
                medioPagoId = Transferencia
            })).LeerAsync<PedidoDto>();

            var linea = Assert.Single(pedido.Items);
            Assert.Equal(3, linea.Cantidad);
            Assert.Equal(300m, pedido.Total);
        }

        [Fact]
        public async Task UnProductoSinPrecioNoPuedeVenderse()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(precio: 0m, stock: 10);

            var respuesta = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia });

            Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
            Assert.Equal(10, (await admin.ObtenerProductoAsync(id)).Stock);
        }

        [Theory]
        [InlineData("vacio")]
        [InlineData("cantidad-cero")]
        [InlineData("cantidad-negativa")]
        public async Task UnCarritoInvalido_Devuelve400(string caso)
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);

            object[] items = caso switch
            {
                "vacio" => Array.Empty<object>(),
                "cantidad-cero" => new object[] { new { productoId = id, cantidad = 0 } },
                _ => new object[] { new { productoId = id, cantidad = -2 } }
            };

            var respuesta = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items, medioPagoId = Transferencia });

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Equal(10, (await admin.ObtenerProductoAsync(id)).Stock);
        }

        [Fact]
        public async Task UnProductoOUnMedioDePagoInexistente_Devuelve404()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);

            var productoInexistente = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = 987654, cantidad = 1 } }, medioPagoId = Transferencia });
            var medioInexistente = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = 99 });

            Assert.Equal(HttpStatusCode.NotFound, productoInexistente.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, medioInexistente.StatusCode);
        }

        [Fact]
        public async Task UnClienteNoPuedeComprarEnNombreDeOtro_PeroElPersonalSi()
        {
            var admin = await _fabrica.AdminAsync();
            var operador = await _fabrica.OperadorAsync();
            int id = await admin.CrearProductoAsync(stock: 10);

            // Un cliente nuevo manda el id del cliente demo: se ignora y la venta queda a su nombre.
            await _fabrica.CreateClient().PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "comprador", email = "comprador@example.com", password = "secreta1", nombre = "Carla", apellido = "Ruiz" });
            var comprador = await _fabrica.ClienteAsync("comprador", "secreta1");
            int clienteDemo = await admin.ClienteIdDemoAsync();

            var propia = await (await comprador.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia, clienteId = clienteDemo })).LeerAsync<PedidoDto>();
            var deMostrador = await (await operador.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia, clienteId = clienteDemo })).LeerAsync<PedidoDto>();

            Assert.Equal("Carla Ruiz", propia.Cliente);
            Assert.NotEqual(clienteDemo, propia.ClienteId);
            Assert.Equal(clienteDemo, deMostrador.ClienteId);
        }

        [Fact]
        public async Task UnUsuarioConRolClienteCreadoPorElAdministrador_PuedeComprar()
        {
            var admin = await _fabrica.AdminAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            await admin.PostAsJsonAsync("/api/Usuarios", new { nombreUsuario = "cliente.admin", email = "ca@example.com", password = "secreta1", rol = "Cliente" });

            var cliente = await _fabrica.ClienteAsync("cliente.admin", "secreta1");
            var respuesta = await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia });

            Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        }

        [Fact]
        public async Task UnClienteSoloVeSusPropiasVentas()
        {
            var admin = await _fabrica.AdminAsync();
            var duenio = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var pedido = await (await duenio.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();

            await _fabrica.CreateClient().PostAsJsonAsync("/api/Usuarios/registro", new { nombreUsuario = "curioso", email = "curioso@example.com", password = "secreta1" });
            var ajeno = await _fabrica.ClienteAsync("curioso", "secreta1");

            Assert.Equal(HttpStatusCode.OK, (await duenio.GetAsync($"/api/Ventas/{pedido.VentaId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await ajeno.GetAsync($"/api/Ventas/{pedido.VentaId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await ajeno.GetAsync($"/api/Ventas/{pedido.VentaId}/comprobante")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await ajeno.PostAsJsonAsync($"/api/Ventas/{pedido.VentaId}/comprobante/enviar", new { })).StatusCode);
            Assert.DoesNotContain(await (await ajeno.GetAsync("/api/Ventas/mis-pedidos")).LeerAsync<List<PedidoDto>>(), p => p.VentaId == pedido.VentaId);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/Ventas/{pedido.VentaId}")).StatusCode);
        }

        [Fact]
        public async Task ElComprobante_EsUnPdfValido()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var pedido = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 2 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();

            var respuesta = await cliente.GetAsync($"/api/Ventas/{pedido.VentaId}/comprobante");

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
            var bytes = await respuesta.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 1000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        }

        [Fact]
        public async Task ElComprobanteSeEnviaSiempreAlCorreoDeLaCuenta_AunqueElClientePidaOtro()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var pedido = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();
            string carpeta = Path.Combine(_fabrica.CarpetaContenido, "App_Data", "Correos");
            var antes = Directory.Exists(carpeta) ? Directory.GetFiles(carpeta).ToHashSet() : new HashSet<string>();

            var respuesta = await cliente.PostAsJsonAsync($"/api/Ventas/{pedido.VentaId}/comprobante/enviar", new { destinatario = "otra.persona@example.com" });
            var resultado = await respuesta.LeerAsync<JsonElement>();

            // Sin SMTP configurado el correo no sale: queda como archivo .eml (modo desarrollo) y se informa con claridad.
            Assert.False(resultado.GetProperty("enviado").GetBoolean());
            Assert.Equal("Archivo", resultado.GetProperty("modo").GetString());

            string eml = Directory.GetFiles(carpeta).Except(antes).Single();
            string contenido = await File.ReadAllTextAsync(eml);
            Assert.Contains("cliente@tienda.local", contenido);
            Assert.DoesNotContain("otra.persona@example.com", contenido);
            Assert.Contains($"comprobante-{pedido.VentaId}.pdf", contenido);
            Assert.Contains("application/pdf", contenido);
        }

        [Fact]
        public async Task ElPersonalPuedeEnviarElComprobanteAOtroDestinatario()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            var operador = await _fabrica.OperadorAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var pedido = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 1 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();
            string carpeta = Path.Combine(_fabrica.CarpetaContenido, "App_Data", "Correos");
            var antes = Directory.Exists(carpeta) ? Directory.GetFiles(carpeta).ToHashSet() : new HashSet<string>();

            var respuesta = await operador.PostAsJsonAsync($"/api/Ventas/{pedido.VentaId}/comprobante/enviar", new { destinatario = "contabilidad@example.com" });
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

            string eml = Directory.GetFiles(carpeta).Except(antes).Single();
            Assert.Contains("contabilidad@example.com", await File.ReadAllTextAsync(eml));
        }

        [Fact]
        public async Task ElComprobanteDeUnaVentaAnuladaLoIndica_YElPedidoFiguraComoAnulado()
        {
            var admin = await _fabrica.AdminAsync();
            var cliente = await _fabrica.ClienteDemoAsync();
            int id = await admin.CrearProductoAsync(stock: 10);
            var pedido = await (await cliente.PostAsJsonAsync("/api/Ventas/checkout", new { items = new[] { new { productoId = id, cantidad = 2 } }, medioPagoId = Transferencia })).LeerAsync<PedidoDto>();

            await admin.PostAsync($"/api/Ventas/{pedido.VentaId}/anular", null);

            var pedidos = await (await cliente.GetAsync("/api/Ventas/mis-pedidos")).LeerAsync<List<PedidoDto>>();
            Assert.True(pedidos.Single(p => p.VentaId == pedido.VentaId).Anulada);
            Assert.Equal(10, (await admin.ObtenerProductoAsync(id)).Stock);
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync($"/api/Ventas/{pedido.VentaId}/comprobante")).StatusCode);
        }

        [Fact]
        public async Task LosMediosDePagoDisponibles()
        {
            var cliente = await _fabrica.ClienteDemoAsync();

            var medios = await (await cliente.GetAsync("/api/MediosPago")).LeerAsync<List<JsonElement>>();

            Assert.Equal(3, medios.Count);
            Assert.Contains(medios, m => m.GetProperty("nombre").GetString() == "Tarjeta de crédito" && m.GetProperty("requiereTarjeta").GetBoolean());
            Assert.Contains(medios, m => m.GetProperty("nombre").GetString() == "Transferencia bancaria" && !m.GetProperty("requiereTarjeta").GetBoolean());
        }
    }
}
