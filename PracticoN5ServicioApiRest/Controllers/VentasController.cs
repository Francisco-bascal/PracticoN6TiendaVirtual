using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Infraestructura;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Todas las acciones requieren un token JWT válido (401 si falta o es inválido).
    // El personal gestiona todas las ventas; un cliente solo compra (checkout) y consulta las suyas.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly VentasService _servicio;
        private readonly ReportesPdfService _pdf;
        private readonly EmailService _correo;
        private readonly UsuarioActual _usuarioActual;

        public VentasController(
            VentasService servicio,
            ReportesPdfService pdf,
            EmailService correo,
            UsuarioActual usuarioActual)
        {
            _servicio = servicio;
            _pdf = pdf;
            _correo = correo;
            _usuarioActual = usuarioActual;
        }

        /// <summary>Devuelve las ventas paginadas (pagina y tamanoPagina por query string; defaults 1 y 10).</summary>
        [Authorize(Roles = NombresRol.Personal)]
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(
            [FromQuery] int pagina = 1, 
            [FromQuery] int tamanoPagina = 10, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var ventas = await _servicio.ObtenerTodosAsync(pagina, tamanoPagina, cancellationToken);
                return Ok(ventas);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Compras del usuario autenticado, más recientes primero.</summary>
        [HttpGet("mis-pedidos")]
        public async Task<IActionResult> ObtenerMisPedidos(CancellationToken cancellationToken = default)
        {
            int usuarioId = _usuarioActual.Id!.Value;
            return Ok(await _servicio.ObtenerPedidosDeUsuarioAsync(usuarioId, cancellationToken));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id, CancellationToken cancellationToken = default)
        {
            if (!await PuedeVerAsync(id, cancellationToken))
            {
                return NotFound($"No se encontró la venta con ID {id}.");
            }

            var venta = await _servicio.ObtenerPorIdAsync(id, cancellationToken);
            if (venta == null)
            {
                return NotFound($"No se encontró la venta con ID {id}.");
            }

            return Ok(venta);
        }

        [Authorize(Roles = NombresRol.Personal)]
        [HttpGet("cliente/{clienteId:int}")]
        public async Task<IActionResult> ObtenerPorCliente(
            int clienteId, 
            CancellationToken cancellationToken = default)
        {
            var ventas = await _servicio.ObtenerPorClienteAsync(clienteId, cancellationToken);
            return Ok(ventas);
        }

        [Authorize(Roles = NombresRol.Personal)]
        [HttpPost]
        public async Task<IActionResult> Registrar(
            [FromBody] CreateVentaDTO venta, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var ventaRegistrada = await _servicio.RegistrarVentaAsync(venta, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId), 
                    new { id = ventaRegistrada.VentaId }, 
                    ventaRegistrada);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Compra desde el carrito con pago simulado. Responde 201 con el pedido, 402 si el pago es rechazado
        /// y 409 si no hay stock suficiente.
        /// </summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout(
            [FromBody] CheckoutRequestDTO solicitud,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var pedido = await _servicio.CheckoutAsync(solicitud, cancellationToken);
                return CreatedAtAction(nameof(ObtenerPorId), new { id = pedido.VentaId }, pedido);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (PagoRechazadoException ex)
            {
                return StatusCode(StatusCodes.Status402PaymentRequired, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [Authorize(Roles = NombresRol.Administrador)]
        [HttpPost("{id:int}/anular")]
        public async Task<IActionResult> Anular(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                await _servicio.AnularVentaAsync(id, cancellationToken);
                return Ok("Venta anulada exitosamente y stock restituido.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        /// <summary>Comprobante de la venta en PDF (el personal ve cualquiera; un cliente, solo los suyos).</summary>
        [Auditar]
        [HttpGet("{id:int}/comprobante")]
        public async Task<IActionResult> ObtenerComprobante(int id, CancellationToken cancellationToken = default)
        {
            var pedido = await ObtenerPedidoVisibleAsync(id, cancellationToken);
            if (pedido == null)
            {
                return NotFound($"No se encontró la venta con ID {id}.");
            }

            return File(_pdf.GenerarComprobante(pedido), "application/pdf", $"comprobante-{id}.pdf");
        }

        /// <summary>
        /// Envía el comprobante por correo. Un cliente lo recibe siempre en el correo de su cuenta;
        /// el personal puede indicar otro destinatario.
        /// </summary>
        [Auditar]
        [HttpPost("{id:int}/comprobante/enviar")]
        public async Task<IActionResult> EnviarComprobante(
            int id,
            [FromBody] EnviarComprobanteDTO? solicitud,
            [FromServices] UsuariosService usuarios,
            CancellationToken cancellationToken = default)
        {
            var pedido = await ObtenerPedidoVisibleAsync(id, cancellationToken);
            if (pedido == null)
            {
                return NotFound($"No se encontró la venta con ID {id}.");
            }

            string? destinatario = solicitud?.Destinatario;

            if (!_usuarioActual.EsPersonal || string.IsNullOrWhiteSpace(destinatario))
            {
                // Un cliente (o una petición sin destinatario) usa el correo de la cuenta autenticada.
                var perfil = await usuarios.ObtenerPerfilAsync(_usuarioActual.Id!.Value, cancellationToken);
                destinatario = perfil.Email;
            }

            try
            {
                var resultado = await _correo.EnviarAsync(
                    destinatario,
                    $"Comprobante de compra N° {id}",
                    $"Hola. Adjuntamos el comprobante de la compra N° {id} por un total de {ReportesPdfService.Dinero(pedido.Total)}.",
                    $"comprobante-{id}.pdf",
                    _pdf.GenerarComprobante(pedido),
                    cancellationToken);

                return Ok(resultado);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // El personal accede a cualquier venta; un cliente, solo a las que pertenecen a su cuenta.
        private async Task<bool> PuedeVerAsync(int ventaId, CancellationToken cancellationToken)
        {
            if (_usuarioActual.EsPersonal)
            {
                return true;
            }

            return await _servicio.EsDeUsuarioAsync(ventaId, _usuarioActual.Id!.Value, cancellationToken);
        }

        private async Task<PedidoDTO?> ObtenerPedidoVisibleAsync(int ventaId, CancellationToken cancellationToken)
        {
            if (!await PuedeVerAsync(ventaId, cancellationToken))
            {
                return null;
            }

            return await _servicio.ObtenerPedidoAsync(ventaId, cancellationToken);
        }
    }
}
