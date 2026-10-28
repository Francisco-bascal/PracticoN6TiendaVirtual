using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class ProductosService
    {
        private readonly SistemaVentasDbContext _contexto;
        private readonly ImagenesService _imagenesService;
        private readonly MovimientosStockService _stock;

        public ProductosService(
            SistemaVentasDbContext contexto,
            ImagenesService imagenesService,
            MovimientosStockService stock)
        {
            _contexto = contexto;
            _imagenesService = imagenesService;
            _stock = stock;
        }

        /// <summary>Devuelve el catálogo paginado, con búsqueda y filtros opcionales.</summary>
        /// <param name="busqueda">Texto a buscar en el nombre y la descripción.</param>
        /// <param name="categoriaId">Limita el resultado a una categoría.</param>
        /// <param name="soloDisponibles">true = oculta los productos sin stock.</param>
        /// <param name="orden">nombre (default), precio_asc, precio_desc o recientes.</param>
        /// <param name="pagina">Número de página (default 1).</param>
        /// <param name="tamanoPagina">Elementos por página (default 10, máx. 100).</param>
        public async Task<ResultadoPaginadoDto<ResponseProductoDTO>> ObtenerTodosAsync(
            string? busqueda = null,
            int? categoriaId = null,
            bool soloDisponibles = false,
            string? orden = null,
            int pagina = 1,
            int tamanoPagina = 10,
            CancellationToken cancellationToken = default)
        {
            // AsNoTracking: consulta de solo lectura.
            var consulta = _contexto.Productos.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                // LIKE (y no Contains): se comporta igual con SQL Server y SQLite y no distingue mayúsculas de minúsculas.
                string patron = PatronLike(busqueda.Trim());
                consulta = consulta.Where(p => EF.Functions.Like(p.Nombre, patron, "\\")
                    || (p.Descripcion != null && EF.Functions.Like(p.Descripcion, patron, "\\")));
            }

            if (categoriaId.HasValue)
            {
                consulta = consulta.Where(p => p.CategoriaId == categoriaId.Value);
            }

            if (soloDisponibles)
            {
                consulta = consulta.Where(p => p.Stock > 0);
            }

            // El desempate por id mantiene estable el orden entre páginas.
            var ordenada = (orden?.Trim().ToLowerInvariant()) switch
            {
                "precio_asc" => consulta.OrderBy(p => p.Precio).ThenBy(p => p.ProductoId),
                "precio_desc" => consulta.OrderByDescending(p => p.Precio).ThenBy(p => p.ProductoId),
                "recientes" => consulta.OrderByDescending(p => p.ProductoId),
                _ => consulta.OrderBy(p => p.Nombre).ThenBy(p => p.ProductoId)
            };

            return await Paginacion.PaginarAsync(Proyectar(ordenada), pagina, tamanoPagina, cancellationToken);
        }

        // Método de compatibilidad para consultas completas
        public async Task<ICollection<Producto>> GetProductosAsync(CancellationToken cancellationToken = default)
        {
            return await _contexto.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .ToListAsync(cancellationToken);
        }

        public async Task<ResponseProductoDTO?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await Proyectar(_contexto.Productos
                    .AsNoTracking()
                    .Where(p => p.ProductoId == id))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<ICollection<ResponseProductoDTO>> ObtenerPorCategoriaAsync(int categoriaId, CancellationToken cancellationToken = default)
        {
            return await Proyectar(_contexto.Productos
                    .AsNoTracking()
                    .Where(p => p.CategoriaId == categoriaId)
                    .OrderBy(p => p.Nombre))
                .ToListAsync(cancellationToken);
        }

        public async Task<ResponseProductoDTO> CrearAsync(CreateProductoDTO productoDto, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(productoDto.Nombre))
            {
                throw new ArgumentException("El nombre del producto es obligatorio.", nameof(productoDto));
            }

            if (productoDto.Precio < 0)
            {
                throw new ArgumentException("El precio del producto no puede ser negativo.", nameof(productoDto));
            }

            if (productoDto.Stock < 0)
            {
                throw new ArgumentException("El stock inicial no puede ser negativo.", nameof(productoDto));
            }

            bool categoriaExiste = await _contexto.Categorias
                .AnyAsync(c => c.CategoriaId == productoDto.CategoriaId, cancellationToken);

            if (!categoriaExiste)
            {
                throw new InvalidOperationException($"La categoría con ID {productoDto.CategoriaId} no existe.");
            }

            //Mapeo de DTO a Modelo
            var producto = new Producto
            {
                Nombre = productoDto.Nombre,
                Descripcion = productoDto.Descripcion,
                Precio = productoDto.Precio,
                Stock = productoDto.Stock,
                ImagenRuta = productoDto.ImagenRuta,
                CategoriaId = productoDto.CategoriaId
            };

            await _contexto.Productos.AddAsync(producto, cancellationToken);

            // El stock con el que nace el producto queda registrado como su primer movimiento.
            _stock.RegistrarStockInicial(producto);

            await _contexto.SaveChangesAsync(cancellationToken);

            // Cargar datos de la categoría para el retorno completo
            await _contexto.Entry(producto)
                .Reference(p => p.Categoria)
                .LoadAsync(cancellationToken);

            return Mapear(producto);
        }

        public async Task<ResponseProductoDTO> ActualizarAsync(int id, CreateProductoDTO productoActualizado, CancellationToken cancellationToken = default)
        {
            var productoExistente = await _contexto.Productos
                .FirstOrDefaultAsync(p => p.ProductoId == id, cancellationToken);

            if (productoExistente == null)
            {
                throw new KeyNotFoundException($"No se encontró el producto con ID {id}.");
            }

            if (string.IsNullOrWhiteSpace(productoActualizado.Nombre))
            {
                throw new ArgumentException("El nombre del producto no puede estar vacío.", nameof(productoActualizado));
            }

            if (productoActualizado.Precio < 0)
            {
                throw new ArgumentException("El precio del producto no puede ser negativo.", nameof(productoActualizado));
            }

            if (productoActualizado.Stock < 0)
            {
                throw new ArgumentException("El stock del producto no puede ser negativo.", nameof(productoActualizado));
            }

            if (productoExistente.CategoriaId != productoActualizado.CategoriaId)
            {
                bool categoriaExiste = await _contexto.Categorias
                    .AnyAsync(c => c.CategoriaId == productoActualizado.CategoriaId, cancellationToken);

                if (!categoriaExiste)
                {
                    throw new InvalidOperationException($"La categoría con ID {productoActualizado.CategoriaId} no existe.");
                }
            }

            // Cambiar el stock desde la edición también deja un movimiento (ajuste manual) con la diferencia.
            int diferenciaStock = productoActualizado.Stock - productoExistente.Stock;

            productoExistente.Nombre = productoActualizado.Nombre;
            productoExistente.Descripcion = productoActualizado.Descripcion;
            productoExistente.Precio = productoActualizado.Precio;
            productoExistente.ImagenRuta = productoActualizado.ImagenRuta;
            productoExistente.CategoriaId = productoActualizado.CategoriaId;

            await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await _contexto.SaveChangesAsync(cancellationToken);

                if (diferenciaStock != 0)
                {
                    await _stock.AjustarAsync(
                        id,
                        diferenciaStock,
                        MotivosMovimiento.AjusteManual,
                        observacion: "Edición del producto",
                        cancellationToken: cancellationToken);

                    await _contexto.SaveChangesAsync(cancellationToken);

                    // El stock se cambió directamente en la base: se relee para no devolver el valor viejo.
                    await _contexto.Entry(productoExistente).ReloadAsync(cancellationToken);
                }

                await transaccion.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaccion.RollbackAsync(cancellationToken);
                // Las entidades de la operación fallida no deben quedar pendientes de guardarse.
                _contexto.ChangeTracker.Clear();
                throw;
            }

            await _contexto.Entry(productoExistente)
                .Reference(p => p.Categoria)
                .LoadAsync(cancellationToken);

            return Mapear(productoExistente);
        }

        /// <summary>Ajusta el stock sumando (positivo) o restando (negativo) unidades, dejando el movimiento registrado.</summary>
        public async Task<bool> ActualizarStockAsync(
            int productoId,
            int cantidadCambio,
            string? observacion = null,
            CancellationToken cancellationToken = default)
        {
            // El stock y su movimiento se confirman juntos: nunca queda uno sin el otro.
            await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await _stock.AjustarAsync(
                    productoId,
                    cantidadCambio,
                    MotivosMovimiento.AjusteManual,
                    observacion: observacion,
                    cancellationToken: cancellationToken);

                await _contexto.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaccion.RollbackAsync(cancellationToken);
                _contexto.ChangeTracker.Clear();
                throw;
            }

            return true;
        }

        public async Task<ResponseProductoDTO> ActualizarImagenAsync(int id, string rutaImagen, CancellationToken cancellationToken = default)
        {
            var producto = await _contexto.Productos
                .FirstOrDefaultAsync(p => p.ProductoId == id, cancellationToken);

            if (producto == null)
            {
                throw new KeyNotFoundException($"No se encontró el producto con ID {id}.");
            }

            string? rutaAnterior = producto.ImagenRuta;

            producto.ImagenRuta = rutaImagen;
            await _contexto.SaveChangesAsync(cancellationToken);

            // Elimina la imagen anterior cuando se reemplaza por una distinta.
            if (!string.IsNullOrWhiteSpace(rutaAnterior) && rutaAnterior != rutaImagen)
            {
                _imagenesService.Eliminar(rutaAnterior);
            }

            await _contexto.Entry(producto)
                .Reference(p => p.Categoria)
                .LoadAsync(cancellationToken);

            return Mapear(producto);
        }

        public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
        {
            var producto = await _contexto.Productos
                .FirstOrDefaultAsync(p => p.ProductoId == id, cancellationToken);

            if (producto == null)
            {
                throw new KeyNotFoundException($"No se encontró el producto con ID {id}.");
            }

            bool tieneCompras = await _contexto.DetallesCompra
                .AnyAsync(d => d.ProductoId == id, cancellationToken);

            if (tieneCompras)
            {
                throw new InvalidOperationException("No se puede eliminar el producto porque tiene compras registradas asociadas.");
            }

            bool tieneVentas = await _contexto.DetallesVenta
                .AnyAsync(d => d.ProductoId == id, cancellationToken);

            if (tieneVentas)
            {
                throw new InvalidOperationException("No se puede eliminar el producto porque tiene ventas registradas asociadas.");
            }

            string? rutaImagen = producto.ImagenRuta;

            // Sin compras ni ventas, los únicos movimientos posibles son el stock inicial y los ajustes manuales.
            var movimientos = await _contexto.MovimientosStock
                .Where(m => m.ProductoId == id)
                .ToListAsync(cancellationToken);

            _contexto.MovimientosStock.RemoveRange(movimientos);
            _contexto.Productos.Remove(producto);
            await _contexto.SaveChangesAsync(cancellationToken);

            // El fallo del borrado del archivo no revierte la eliminación del registro.
            try
            {
                _imagenesService.Eliminar(rutaImagen);
            }
            catch (IOException)
            {
                // Un archivo huérfano no debe impedir la eliminación del producto.
            }

            return true;
        }

        // Escapa los comodines de LIKE para que el texto buscado se tome literalmente ("50%" no debe coincidir con todo).
        private static string PatronLike(string texto)
        {
            string escapado = texto
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .Replace("[", "\\[");

            return $"%{escapado}%";
        }

        private static IQueryable<ResponseProductoDTO> Proyectar(IQueryable<Producto> consulta)
        {
            return consulta.Select(p => new ResponseProductoDTO
            {
                ProductoId = p.ProductoId,
                Nombre = p.Nombre,
                Descripcion = p.Descripcion,
                Precio = p.Precio,
                Stock = p.Stock,
                ImagenRuta = p.ImagenRuta,
                CategoriaId = p.CategoriaId,
                Categoria = p.Categoria.Nombre
            });
        }

        private static ResponseProductoDTO Mapear(Producto producto)
        {
            return new ResponseProductoDTO
            {
                ProductoId = producto.ProductoId,
                Nombre = producto.Nombre,
                Descripcion = producto.Descripcion,
                Precio = producto.Precio,
                Stock = producto.Stock,
                ImagenRuta = producto.ImagenRuta,
                CategoriaId = producto.CategoriaId,
                Categoria = producto.Categoria?.Nombre ?? string.Empty
            };
        }
    }
}
