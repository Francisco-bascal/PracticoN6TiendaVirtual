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
    public class ProveedoresController : ControllerBase
    {
        private readonly ProveedoresService _servicio;

        public ProveedoresController(ProveedoresService servicio)
        {
            _servicio = servicio;
        }

        /// <summary>Devuelve los proveedores paginados (pagina y tamanoPagina por query string; defaults 1 y 10).</summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(
            [FromQuery] int pagina = 1, 
            [FromQuery] int tamanoPagina = 10, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var proveedores = await _servicio.ObtenerTodosAsync(pagina, tamanoPagina, cancellationToken);
                return Ok(proveedores);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(
            int id, 
            [FromQuery] bool incluirCompras = false, 
            CancellationToken cancellationToken = default)
        {
            var proveedor = await _servicio.ObtenerPorIdAsync(id, incluirCompras, cancellationToken);
            if (proveedor == null)
            {
                return NotFound($"No se encontró el proveedor con ID {id}.");
            }

            return Ok(proveedor);
        }

        [HttpPost]
        public async Task<IActionResult> Crear(
            [FromBody] Proveedor proveedor, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var proveedorCreado = await _servicio.CrearAsync(proveedor, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId), 
                    new { id = proveedorCreado.ProveedorId }, 
                    proveedorCreado);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Actualizar(
            int id, 
            [FromBody] Proveedor proveedor, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var proveedorActualizado = await _servicio.ActualizarAsync(id, proveedor, cancellationToken);
                return Ok(proveedorActualizado);
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
                return Conflict(ex.Message);
            }
        }

        [Authorize(Roles = "Administrador")]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Eliminar(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                await _servicio.EliminarAsync(id, cancellationToken);
                return NoContent();
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
