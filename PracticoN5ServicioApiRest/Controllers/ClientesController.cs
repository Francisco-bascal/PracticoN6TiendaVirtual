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
    public class ClientesController : ControllerBase
    {
        private readonly ClientesService _servicio;

        public ClientesController(ClientesService servicio)
        {
            _servicio = servicio;
        }

        /// <summary>Devuelve los clientes paginados (pagina y tamanoPagina por query string; defaults 1 y 10).</summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(
            [FromQuery] int pagina = 1, 
            [FromQuery] int tamanoPagina = 10, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var clientes = await _servicio.ObtenerTodosAsync(pagina, tamanoPagina, cancellationToken);
                return Ok(clientes);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(
            int id, 
            [FromQuery] bool incluirVentas = false, 
            CancellationToken cancellationToken = default)
        {
            var cliente = await _servicio.ObtenerPorIdAsync(id, incluirVentas, cancellationToken);
            if (cliente == null)
            {
                return NotFound($"No se encontró el cliente con ID {id}.");
            }

            return Ok(cliente);
        }

        [HttpPost]
        public async Task<IActionResult> Crear(
            [FromBody] Cliente cliente, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var clienteCreado = await _servicio.CrearAsync(cliente, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId), 
                    new { id = clienteCreado.ClienteId }, 
                    clienteCreado);
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
            [FromBody] Cliente cliente, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var clienteActualizado = await _servicio.ActualizarAsync(id, cliente, cancellationToken);
                return Ok(clienteActualizado);
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
