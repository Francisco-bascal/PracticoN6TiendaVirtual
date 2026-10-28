using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Libro de movimientos de stock (entradas, salidas, anulaciones y ajustes): solo para el personal.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = NombresRol.Personal)]
    public class MovimientosStockController : ControllerBase
    {
        private readonly MovimientosStockService _servicio;

        public MovimientosStockController(MovimientosStockService servicio)
        {
            _servicio = servicio;
        }

        /// <summary>Movimientos más recientes primero. Filtros opcionales: desde, hasta (días locales inclusive), productoId y motivo.</summary>
        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(
            [FromQuery] DateTime? desde = null,
            [FromQuery] DateTime? hasta = null,
            [FromQuery] int? productoId = null,
            [FromQuery] string? motivo = null,
            [FromQuery] int pagina = 1,
            [FromQuery] int tamanoPagina = 20,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return Ok(await _servicio.ObtenerAsync(desde, hasta, productoId, motivo, pagina, tamanoPagina, cancellationToken));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
