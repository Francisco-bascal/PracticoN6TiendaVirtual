using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Solo el personal del negocio (Administrador u Operador) accede a este recurso: 401 sin token válido, 403 con otro rol.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.Personal)]
    public class ComprasController : ControllerBase
    {
        private readonly ComprasService _servicio;

        public ComprasController(ComprasService servicio)
        {
            _servicio = servicio;
        }

        /// <summary>Devuelve las compras paginadas (pagina y tamanoPagina por query string; defaults 1 y 10).</summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(
            [FromQuery] int pagina = 1, 
            [FromQuery] int tamanoPagina = 10, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var compras = await _servicio.ObtenerTodosAsync(pagina, tamanoPagina, cancellationToken);
                return Ok(compras);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id, CancellationToken cancellationToken = default)
        {
            var compra = await _servicio.ObtenerPorIdAsync(id, cancellationToken);
            if (compra == null)
            {
                return NotFound($"No se encontró la compra con ID {id}.");
            }

            return Ok(compra);
        }

        [HttpGet("proveedor/{proveedorId:int}")]
        public async Task<IActionResult> ObtenerPorProveedor(
            int proveedorId, 
            CancellationToken cancellationToken = default)
        {
            var compras = await _servicio.ObtenerPorProveedorAsync(proveedorId, cancellationToken);
            return Ok(compras);
        }

        [HttpPost]
        public async Task<IActionResult> Registrar([FromBody] CreateCompraDTO compra, CancellationToken cancellationToken = default)
        {
            try
            {
                var compraRegistrada = await _servicio.RegistrarCompraAsync(compra, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId), 
                    new { id = compraRegistrada.CompraId }, 
                    compraRegistrada);
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

        [Authorize(Roles = "Administrador")]
        [HttpPost("{id:int}/anular")]
        public async Task<IActionResult> Anular(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                await _servicio.AnularCompraAsync(id, cancellationToken);
                return Ok("Compra anulada exitosamente y stock revertido.");
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
    }
}