using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Medios de pago disponibles para el checkout; los consulta cualquier usuario autenticado.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MediosPagoController : ControllerBase
    {
        private readonly MediosPagoService _servicio;

        public MediosPagoController(MediosPagoService servicio)
        {
            _servicio = servicio;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerTodos(CancellationToken cancellationToken = default)
        {
            return Ok(await _servicio.ObtenerTodosAsync(cancellationToken));
        }
    }
}
