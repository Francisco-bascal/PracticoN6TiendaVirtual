using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Solo el personal del negocio (Administrador u Operador) accede a este recurso: 401 sin token válido, 403 con otro rol.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.Personal)]
    public class DetallesCompraController : ControllerBase
    {
        private readonly DetallesCompraService _servicio;

        public DetallesCompraController(DetallesCompraService servicio)
        {
            _servicio = servicio;
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id, CancellationToken cancellationToken = default)
        {
            var detalle = await _servicio.ObtenerPorIdAsync(id, cancellationToken);
            if (detalle == null)
            {
                return NotFound($"No se encontró el detalle de compra con ID {id}.");
            }

            return Ok(detalle);
        }

        [HttpGet("compra/{compraId:int}")]
        public async Task<IActionResult> ObtenerPorCompra(int compraId, CancellationToken cancellationToken = default)
        {
            var detalles = await _servicio.ObtenerPorCompraIdAsync(compraId, cancellationToken);
            return Ok(detalles);
        }

        [HttpGet("producto/{productoId:int}")]
        public async Task<IActionResult> ObtenerPorProducto(int productoId, CancellationToken cancellationToken = default)
        {
            var detalles = await _servicio.ObtenerPorProductoIdAsync(productoId, cancellationToken);
            return Ok(detalles);
        }
    }
}
