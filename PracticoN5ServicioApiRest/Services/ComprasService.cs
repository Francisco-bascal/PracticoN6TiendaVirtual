using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class ComprasService
    {
        private readonly SistemaVentasDbContext _contexto;
        private readonly MovimientosStockService _stock;
        private readonly UsuarioActual _usuarioActual;

        public ComprasService(
            SistemaVentasDbContext contexto,
            MovimientosStockService stock,
            UsuarioActual usuarioActual)
        {
            _contexto = contexto;
            _stock = stock;
            _usuarioActual = usuarioActual;
        }

        /// <summary>Devuelve las compras paginadas.</summary>
        /// <param name="pagina">Número de página (default 1).</param>
        /// <param name="tamanoPagina">Elementos por página (default 10, máx. 100).</param>
        public async Task<ResultadoPaginadoDto<Compra>> ObtenerTodosAsync(
            int pagina = 1, 
            int tamanoPagina = 10, 
            CancellationToken cancellationToken = default)
        {
            // Validación de los parámetros de paginación.
            if (pagina <= 0)
            {
                throw new ArgumentException("El número de página debe ser mayor o igual a 1.", nameof(pagina));
            }

            if (tamanoPagina <= 0)
            {
                throw new ArgumentException("El tamaño de página debe ser mayor a 0.", nameof(tamanoPagina));
            }

            if (tamanoPagina > 100)
            {
                throw new ArgumentException("El tamaño de página no puede superar el límite máximo de 100 elementos.", nameof(tamanoPagina));
            }

            // AsNoTracking: consulta de solo lectura. Include: carga proveedor y detalles con producto.
            var consulta = _contexto.Compras
                .AsNoTracking()
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(c => c.Fecha)
                .ThenByDescending(c => c.CompraId);

            // Total de elementos (para calcular el total de páginas).
            int totalElementos = await consulta.CountAsync(cancellationToken);

            // Total de páginas, redondeando hacia arriba.
            int totalPaginas = (int)Math.Ceiling(totalElementos / (double)tamanoPagina);

            // Skip: salta las páginas anteriores. Take: toma la página actual.
            // ToListAsync: materializa la consulta.
            var elementos = await consulta
                .Skip((pagina - 1) * tamanoPagina)
                .Take(tamanoPagina)
                .ToListAsync(cancellationToken);

            return new ResultadoPaginadoDto<Compra>
            {
                PaginaActual = pagina,
                TamanoPagina = tamanoPagina,
                TotalElementos = totalElementos,
                TotalPaginas = totalPaginas,
                Elementos = elementos
            };
        }

        public async Task<Compra?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _contexto.Compras
                .AsNoTracking()
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(c => c.CompraId == id, cancellationToken);
        }

        public async Task<ICollection<Compra>> ObtenerPorProveedorAsync(
            int proveedorId, 
            CancellationToken cancellationToken = default)
        {
            return await _contexto.Compras
                .AsNoTracking()
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(c => c.ProveedorId == proveedorId)
                .ToListAsync(cancellationToken);
        }

        /// <summary>Registra una compra (entrada de mercadería) e incrementa el stock de cada producto, todo en una transacción.</summary>
        /// <param name="compraDto">Datos de la compra: proveedor, fecha opcional y líneas de detalle.</param>
        public async Task<Compra> RegistrarCompraAsync(CreateCompraDTO compraDto, CancellationToken cancellationToken = default)
        {
            // Debe tener al menos una línea de detalle.
            if (compraDto.Detalles == null || !compraDto.Detalles.Any())
            {
                throw new InvalidOperationException(
                    "La compra debe incluir al menos una línea de detalle con un producto.");
            }

            // El proveedor debe existir.
            bool proveedorExiste = await _contexto.Proveedores
                .AnyAsync(
                    p => p.ProveedorId == compraDto.ProveedorId,
                    cancellationToken);

            if (!proveedorExiste)
            {
                throw new InvalidOperationException(
                    $"El proveedor con ID {compraDto.ProveedorId} no existe.");
            }

            // Inicia una transacción: compra, detalles y stock se confirman o revierten juntos.
            await using var transaccion =
                await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var compra = new Compra
                {
                    // Fecha por defecto: la actual en UTC.
                    Fecha = compraDto.Fecha == default
                        ? DateTime.UtcNow
                        : compraDto.Fecha,

                    ProveedorId = compraDto.ProveedorId,
                    UsuarioId = _usuarioActual.Id
                };

                foreach (var detalleDto in compraDto.Detalles)
                {
                    // Validación de cada línea: cantidad y precio deben ser positivos.
                    if (detalleDto.Cantidad <= 0)
                    {
                        throw new ArgumentException(
                            $"La cantidad para el producto ID {detalleDto.ProductoId} debe ser mayor a cero.");
                    }

                    if (detalleDto.PrecioUnitario <= 0)
                    {
                        throw new ArgumentException(
                            $"El precio unitario para el producto ID {detalleDto.ProductoId} debe ser mayor a cero.");
                    }

                    bool productoExiste = await _contexto.Productos
                        .AnyAsync(p => p.ProductoId == detalleDto.ProductoId, cancellationToken);

                    if (!productoExiste)
                    {
                        throw new KeyNotFoundException(
                            $"No se encontró el producto con ID {detalleDto.ProductoId} para asociar al detalle de compra.");
                    }

                    //Lógica de adición de stock: comprar suma stock y deja un movimiento de entrada.
                    await _stock.AjustarAsync(
                        detalleDto.ProductoId,
                        detalleDto.Cantidad,
                        MotivosMovimiento.Compra,
                        compra: compra,
                        cancellationToken: cancellationToken);

                    var detalle = new DetalleCompra
                    {
                        ProductoId = detalleDto.ProductoId,
                        Cantidad = detalleDto.Cantidad,
                        PrecioUnitario = detalleDto.PrecioUnitario
                    };

                    compra.Detalles.Add(detalle);
                }

                // Guarda los cambios dentro de la transacción.
                await _contexto.Compras.AddAsync(compra, cancellationToken);
                await _contexto.SaveChangesAsync(cancellationToken);

                // Confirma los cambios definitivamente.
                await transaccion.CommitAsync(cancellationToken);

                // Cargar datos relacionados para la respuesta
                await _contexto.Entry(compra)
                    .Reference(c => c.Proveedor)
                    .LoadAsync(cancellationToken);

                await _contexto.Entry(compra)
                    .Collection(c => c.Detalles)
                    .Query()
                    .Include(d => d.Producto)
                    .LoadAsync(cancellationToken);

                return compra;
            }
            catch
            {
                // Ante cualquier error deshace todo y relanza la excepción.
                await transaccion.RollbackAsync(cancellationToken);
                // Las entidades de la operación fallida no deben quedar pendientes de guardarse.
                _contexto.ChangeTracker.Clear();
                throw;
            }
        }

        /// <summary>
        /// Anula una compra y revierte el stock que había sumado, todo en una transacción.
        /// La compra no se borra: queda marcada como anulada para conservar la trazabilidad.
        /// </summary>
        /// <param name="id">Identificador de la compra a anular.</param>
        public async Task<bool> AnularCompraAsync(int id, CancellationToken cancellationToken = default)
        {
            // Transacción: stock y marca de anulación se modifican juntos.
            await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var compra = await _contexto.Compras
                    .Include(c => c.Detalles)
                    .FirstOrDefaultAsync(c => c.CompraId == id, cancellationToken);

                if (compra == null)
                {
                    throw new KeyNotFoundException($"No se encontró la compra con ID {id}.");
                }

                if (compra.Anulada)
                {
                    throw new InvalidOperationException($"La compra con ID {id} ya fue anulada.");
                }

                // Revertir el stock agregado en la compra
                foreach (var detalle in compra.Detalles)
                {
                    try
                    {
                        // Descuenta del stock lo que la compra había sumado (falla si el stock ya se consumió).
                        await _stock.AjustarAsync(
                            detalle.ProductoId,
                            -detalle.Cantidad,
                            MotivosMovimiento.AnulacionCompra,
                            compra: compra,
                            cancellationToken: cancellationToken);
                    }
                    catch (StockInsuficienteException ex)
                    {
                        // Impide anular si el stock ya se consumió (quedaría negativo).
                        throw new InvalidOperationException(
                            $"No es posible anular la compra: el stock actual de '{ex.Producto}' ({ex.StockActual}) es inferior a la cantidad comprada ({detalle.Cantidad}) a revertir.");
                    }
                }

                compra.Anulada = true;
                compra.FechaAnulacion = DateTime.UtcNow;

                // Guarda y confirma los cambios.
                await _contexto.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);

                return true;
            }
            catch
            {
                // Deshace todo (stock y marca de anulación) y relanza la excepción.
                await transaccion.RollbackAsync(cancellationToken);
                // Las entidades de la operación fallida no deben quedar pendientes de guardarse.
                _contexto.ChangeTracker.Clear();
                throw;
            }
        }
    }
}
