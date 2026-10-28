using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;
using PracticoN5ServicioApiRest.Services;

namespace PracticoN5ServicioApiRest.Controllers
{
    // Todas las acciones requieren un token JWT válido (401 si falta o es inválido).
    // Cualquier usuario autenticado consulta el catálogo; solo el personal lo modifica.
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductosController : ControllerBase
    {
        private readonly ProductosService _servicio;
        private readonly ImagenesService _imagenesService;

        public ProductosController(ProductosService servicio, ImagenesService imagenesService)
        {
            _servicio = servicio;
            _imagenesService = imagenesService;
        }

        /// <summary>
        /// Devuelve el catálogo paginado (pagina y tamanoPagina por query string; defaults 1 y 10).
        /// Filtros opcionales: busqueda (texto), categoriaId, soloDisponibles y orden (nombre, precio_asc, precio_desc, recientes).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] string? busqueda = null,
            [FromQuery] int? categoriaId = null,
            [FromQuery] bool soloDisponibles = false,
            [FromQuery] string? orden = null,
            [FromQuery] int pagina = 1, 
            [FromQuery] int tamanoPagina = 10, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                var productos = await _servicio.ObtenerTodosAsync(
                    busqueda, categoriaId, soloDisponibles, orden, pagina, tamanoPagina, cancellationToken);
                return Ok(productos);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerPorId(int id, CancellationToken cancellationToken = default)
        {
            var producto = await _servicio.ObtenerPorIdAsync(id, cancellationToken);
            if (producto == null)
            {
                return NotFound($"No se encontró el producto con ID {id}.");
            }

            return Ok(producto);
        }

        [HttpGet("categoria/{categoriaId:int}")]
        public async Task<IActionResult> ObtenerPorCategoria(int categoriaId, CancellationToken cancellationToken = default)
        {
            var productos = await _servicio.ObtenerPorCategoriaAsync(categoriaId, cancellationToken);
            return Ok(productos);
        }

        [Authorize(Roles = NombresRol.Personal)]
        [HttpPost]
        public async Task<IActionResult> Crear([FromBody] CreateProductoDTO producto, CancellationToken cancellationToken = default)
        {
            try
            {
                var productoCreado = await _servicio.CrearAsync(producto, cancellationToken);
                return CreatedAtAction(
                    nameof(ObtenerPorId), 
                    new { id = productoCreado.ProductoId }, 
                    productoCreado);
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
        public async Task<IActionResult> Actualizar(int id, [FromBody] CreateProductoDTO producto, CancellationToken cancellationToken = default)
        {
            try
            {
                var productoActualizado = await _servicio.ActualizarAsync(id, producto, cancellationToken);
                return Ok(productoActualizado);
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

        /// <summary>Ajusta el stock sumando (positivo) o restando (negativo) unidades; queda registrado como ajuste manual.</summary>
        [Authorize(Roles = NombresRol.Personal)]
        [HttpPatch("{id:int}/stock")]
        public async Task<IActionResult> ActualizarStock(
            int id, 
            [FromBody] int cantidadCambio, 
            [FromQuery] string? observacion = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _servicio.ActualizarStockAsync(id, cantidadCambio, observacion, cancellationToken);
                return Ok("Stock actualizado correctamente.");
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

        [Authorize(Roles = NombresRol.Personal)]
        [HttpPost("{id:int}/imagen")]
        public async Task<IActionResult> CargarImagen(int id, IFormFile archivo, CancellationToken cancellationToken = default)
        {
            try
            {
                string ruta = await _imagenesService.GuardarAsync(archivo, cancellationToken);

                try
                {
                    var resultado = await _servicio.ActualizarImagenAsync(id, ruta, cancellationToken);
                    return Ok(resultado);
                }
                catch
                {
                    _imagenesService.Eliminar(ruta);
                    throw;
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        // Las imágenes del catálogo son públicas: una etiqueta <img> no puede enviar el token JWT en un header.
        [AllowAnonymous]
        [HttpGet("{id:int}/imagen")]
        public async Task<IActionResult> ObtenerImagen(int id, CancellationToken cancellationToken = default)
        {
            var producto = await _servicio.ObtenerPorIdAsync(id, cancellationToken);
            if (producto == null)
            {
                return NotFound($"No se encontró el producto con ID {id}.");
            }

            if (string.IsNullOrWhiteSpace(producto.ImagenRuta))
            {
                return NotFound("El producto no tiene una imagen cargada.");
            }

            string rutaFisica = _imagenesService.ObtenerRutaFisica(producto.ImagenRuta);

            if (!System.IO.File.Exists(rutaFisica))
            {
                return NotFound("No se encontró el archivo de imagen del producto.");
            }

            return PhysicalFile(rutaFisica, _imagenesService.ObtenerContentType(producto.ImagenRuta));
        }

        [Authorize(Roles = NombresRol.Administrador)]
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
