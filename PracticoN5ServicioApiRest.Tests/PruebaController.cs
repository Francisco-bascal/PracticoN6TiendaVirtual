using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PracticoN5ServicioApiRest.Tests
{
    /// <summary>Solo para pruebas: provoca una excepción no controlada para verificar el registro de errores.</summary>
    [ApiController]
    [AllowAnonymous]
    [Route("prueba")]
    public class PruebaController : ControllerBase
    {
        [HttpGet("error")]
        public IActionResult Fallar()
        {
            throw new InvalidOperationException("Falla provocada por la prueba");
        }
    }
}
