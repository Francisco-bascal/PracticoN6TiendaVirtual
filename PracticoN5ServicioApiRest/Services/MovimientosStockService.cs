using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>El stock disponible no alcanza para la operación pedida (se informa como conflicto de negocio).</summary>
    public class StockInsuficienteException : InvalidOperationException
    {
        public string Producto { get; }
        public int StockActual { get; }
        public int Solicitado { get; }

        public StockInsuficienteException(string producto, int stockActual, int solicitado)
            : base($"Stock insuficiente para el producto '{producto}'. Stock disponible: {stockActual}, Solicitado: {solicitado}.")
        {
            Producto = producto;
            StockActual = stockActual;
            Solicitado = solicitado;
        }
    }

    /// <summary>Único punto por el que cambia el stock de un producto: actualiza la cantidad y deja el movimiento registrado.</summary>
    public class MovimientosStockService
    {
        private const int LongitudMaximaObservacion = 200;

        private readonly SistemaVentasDbContext _contexto;
        private readonly UsuarioActual _usuarioActual;
        private readonly ZonaHorariaNegocio _zonaHoraria;

        public MovimientosStockService(
            SistemaVentasDbContext contexto,
            UsuarioActual usuarioActual,
            ZonaHorariaNegocio zonaHoraria)
        {
            _contexto = contexto;
            _usuarioActual = usuarioActual;
            _zonaHoraria = zonaHoraria;
        }

        /// <summary>
        /// Suma (<paramref name="delta"/> positivo) o resta (negativo) unidades al stock del producto y agrega el movimiento
        /// al contexto. El stock se modifica con un UPDATE condicional en la base: si dos operaciones compiten por las
        /// últimas unidades, solo una lo logra y la otra recibe <see cref="StockInsuficienteException"/>.
        /// No guarda el movimiento: lo persiste el SaveChanges de quien llama (normalmente dentro de su transacción).
        /// </summary>
        public async Task AjustarAsync(
            int productoId,
            int delta,
            string motivo,
            Compra? compra = null,
            Venta? venta = null,
            string? observacion = null,
            CancellationToken cancellationToken = default)
        {
            if (delta == 0)
            {
                return;
            }

            // Se valida antes de modificar el stock: si fallara al guardar el movimiento, el stock ya habría cambiado.
            if (observacion != null && observacion.Length > LongitudMaximaObservacion)
            {
                throw new ArgumentException($"La observación no puede superar los {LongitudMaximaObservacion} caracteres.", nameof(observacion));
            }

            int filas = await _contexto.Productos
                .Where(p => p.ProductoId == productoId && p.Stock + delta >= 0)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + delta), cancellationToken);

            if (filas == 0)
            {
                var producto = await _contexto.Productos
                    .AsNoTracking()
                    .Where(p => p.ProductoId == productoId)
                    .Select(p => new { p.Nombre, p.Stock })
                    .FirstOrDefaultAsync(cancellationToken);

                if (producto == null)
                {
                    throw new KeyNotFoundException($"No se encontró el producto con ID {productoId}.");
                }

                throw new StockInsuficienteException(producto.Nombre, producto.Stock, -delta);
            }

            _contexto.MovimientosStock.Add(CrearMovimiento(productoId, delta, motivo, compra, venta, observacion));
        }

        /// <summary>Registra el movimiento de un producto nuevo que ya se insertó con su stock inicial.</summary>
        public void RegistrarStockInicial(Producto producto)
        {
            if (producto.Stock <= 0)
            {
                return;
            }

            var movimiento = CrearMovimiento(0, producto.Stock, MotivosMovimiento.StockInicial, null, null, null);
            movimiento.Producto = producto;
            _contexto.MovimientosStock.Add(movimiento);
        }

        private MovimientoStock CrearMovimiento(
            int productoId,
            int cantidad,
            string motivo,
            Compra? compra,
            Venta? venta,
            string? observacion)
        {
            return new MovimientoStock
            {
                Fecha = DateTime.UtcNow,
                ProductoId = productoId,
                Cantidad = cantidad,
                Motivo = motivo,
                Observacion = observacion,
                Compra = compra,
                Venta = venta,
                UsuarioId = _usuarioActual.Id
            };
        }

        /// <summary>Movimientos más recientes primero; todos los filtros son opcionales.</summary>
        public async Task<ResultadoPaginadoDto<MovimientoStockDTO>> ObtenerAsync(
            DateTime? desde,
            DateTime? hasta,
            int? productoId,
            string? motivo,
            int pagina = 1,
            int tamanoPagina = 20,
            CancellationToken cancellationToken = default)
        {
            Paginacion.Validar(pagina, tamanoPagina);

            var consulta = ConsultaFiltrada(desde, hasta, productoId, motivo);
            return await Paginacion.PaginarAsync(Proyectar(consulta), pagina, tamanoPagina, cancellationToken);
        }

        /// <summary>Todos los movimientos del período (para el reporte PDF).</summary>
        public async Task<ICollection<MovimientoStockDTO>> ObtenerTodosAsync(
            DateTime? desde,
            DateTime? hasta,
            int? productoId = null,
            CancellationToken cancellationToken = default)
        {
            return await Proyectar(ConsultaFiltrada(desde, hasta, productoId, null))
                .ToListAsync(cancellationToken);
        }

        private IQueryable<MovimientoStock> ConsultaFiltrada(
            DateTime? desde,
            DateTime? hasta,
            int? productoId,
            string? motivo)
        {
            var (desdeUtc, hastaUtc) = _zonaHoraria.RangoUtc(desde, hasta);

            var consulta = _contexto.MovimientosStock.AsNoTracking().AsQueryable();

            if (desdeUtc.HasValue)
            {
                consulta = consulta.Where(m => m.Fecha >= desdeUtc.Value);
            }

            if (hastaUtc.HasValue)
            {
                consulta = consulta.Where(m => m.Fecha < hastaUtc.Value);
            }

            if (productoId.HasValue)
            {
                consulta = consulta.Where(m => m.ProductoId == productoId.Value);
            }

            if (!string.IsNullOrWhiteSpace(motivo))
            {
                consulta = consulta.Where(m => m.Motivo == motivo);
            }

            return consulta;
        }

        private static IQueryable<MovimientoStockDTO> Proyectar(IQueryable<MovimientoStock> consulta)
        {
            return consulta
                .OrderByDescending(m => m.Fecha)
                .ThenByDescending(m => m.MovimientoStockId)
                .Select(m => new MovimientoStockDTO
                {
                    MovimientoStockId = m.MovimientoStockId,
                    Fecha = m.Fecha,
                    ProductoId = m.ProductoId,
                    Producto = m.Producto.Nombre,
                    Cantidad = m.Cantidad,
                    Motivo = m.Motivo,
                    Observacion = m.Observacion,
                    Usuario = m.Usuario != null ? m.Usuario.NombreUsuario : null,
                    CompraId = m.CompraId,
                    VentaId = m.VentaId
                });
        }
    }
}
