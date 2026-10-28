using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Infraestructura;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Reportes en PDF para consulta y envío por correo: solo para el personal.
    // Tipos disponibles: stock, entradas, salidas y movimientos.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.Personal)]
    public class ReportesController : ControllerBase
    {
        private readonly ReportesService _reportes;
        private readonly ReportesPdfService _pdf;
        private readonly EmailService _correo;
        private readonly UsuarioActual _usuarioActual;
        private readonly string _negocio;

        public ReportesController(
            ReportesService reportes,
            ReportesPdfService pdf,
            EmailService correo,
            UsuarioActual usuarioActual,
            IConfiguration configuracion)
        {
            _reportes = reportes;
            _pdf = pdf;
            _correo = correo;
            _usuarioActual = usuarioActual;
            _negocio = configuracion["Negocio:Nombre"] ?? "Tienda Móvil";
        }

        /// <summary>Genera el reporte en PDF. desde y hasta (días inclusive) aplican a entradas, salidas y movimientos.</summary>
        [Auditar]
        [HttpGet("{tipo}")]
        public async Task<IActionResult> Generar(
            string tipo,
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var reporte = await GenerarPdfAsync(tipo, desde, hasta, cancellationToken);
                return File(reporte.Contenido, "application/pdf", reporte.NombreArchivo);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Genera el reporte y lo envía por correo como adjunto PDF.</summary>
        [Auditar]
        [HttpPost("{tipo}/enviar")]
        public async Task<IActionResult> Enviar(
            string tipo,
            [FromBody] EnviarReporteDTO solicitud,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var reporte = await GenerarPdfAsync(tipo, solicitud.Desde, solicitud.Hasta, cancellationToken);

                var resultado = await _correo.EnviarAsync(
                    solicitud.Destinatario,
                    $"{_negocio}: {reporte.Titulo}",
                    $"Se adjunta el reporte \"{reporte.Titulo}\" generado por {_usuarioActual.Nombre}.",
                    reporte.NombreArchivo,
                    reporte.Contenido,
                    cancellationToken);

                return Ok(resultado);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private async Task<(string Titulo, string NombreArchivo, byte[] Contenido)> GenerarPdfAsync(
            string tipo,
            DateTime? desde,
            DateTime? hasta,
            CancellationToken cancellationToken)
        {
            string hoy = DateTime.UtcNow.ToString("yyyyMMdd");
            string? usuario = _usuarioActual.Nombre;

            switch (tipo.ToLowerInvariant())
            {
                case "stock":
                    var stock = await _reportes.ObtenerStockAsync(cancellationToken);
                    return ("Reporte de stock", $"reporte-stock-{hoy}.pdf", _pdf.GenerarStock(stock, usuario));

                case "entradas":
                    var entradas = await _reportes.ObtenerEntradasAsync(desde, hasta, cancellationToken);
                    return (entradas.Titulo, $"reporte-entradas-{hoy}.pdf", _pdf.GenerarDocumentos(entradas, usuario));

                case "salidas":
                    var salidas = await _reportes.ObtenerSalidasAsync(desde, hasta, cancellationToken);
                    return (salidas.Titulo, $"reporte-salidas-{hoy}.pdf", _pdf.GenerarDocumentos(salidas, usuario));

                case "movimientos":
                    var movimientos = await _reportes.ObtenerMovimientosAsync(desde, hasta, cancellationToken);
                    return ("Movimientos de stock", $"reporte-movimientos-{hoy}.pdf", _pdf.GenerarMovimientos(movimientos, usuario));

                default:
                    throw new KeyNotFoundException($"El reporte '{tipo}' no existe. Disponibles: stock, entradas, salidas y movimientos.");
            }
        }
    }
}
