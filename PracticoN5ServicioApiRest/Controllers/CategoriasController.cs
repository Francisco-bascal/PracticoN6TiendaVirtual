using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Todas las acciones requieren un token JWT válido (401 si falta o es inválido).
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriasController : ControllerBase
    {
        private readonly CategoriasService _servicio;

        public CategoriasController(CategoriasService servicio)
        {
            _servicio = servicio;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(CancellationToken cancellationToken = default)
        {
            var categorias = await _servicio.ObtenerTodosAsync(cancellationToken);
            return Ok(categorias);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(
            int id, 
            [FromQuery] bool incluirProductos = false, 
            CancellationToken cancellationToken = default)
        {
            var categoria = await _servicio.ObtenerPorIdAsync(id, incluirProductos, cancellationToken);
            if (categoria == null)
            {
                return NotFound($"No se encontró la categoría con ID {id}.");
            }

            return Ok(categoria);
        }

        [Authorize(Roles = NombresRol.Personal)]
        [HttpPost]
        public async Task<IActionResult> Crear(
            [FromBody] CategoriaProducto categoria, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var categoriaCreada = await _servicio.CrearAsync(categoria, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId), 
                    new { id = categoriaCreada.CategoriaId }, 
                    categoriaCreada);
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

        [Authorize(Roles = NombresRol.Personal)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Actualizar(
            int id, 
            [FromBody] CategoriaProducto categoria, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var categoriaActualizada = await _servicio.ActualizarAsync(id, categoria, cancellationToken);
                return Ok(categoriaActualizada);
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
