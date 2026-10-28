using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;

namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>Arma los datos de los reportes de stock, entradas y salidas. Las fechas de salida están en hora local del negocio.</summary>
    public class ReportesService
    {
        private const int StockMinimoPorDefecto = 5;

        private readonly SistemaVentasDbContext _contexto;
        private readonly MovimientosStockService _movimientos;
        private readonly ZonaHorariaNegocio _zonaHoraria;
        private readonly IConfiguration _configuracion;

        public ReportesService(
            SistemaVentasDbContext contexto,
            MovimientosStockService movimientos,
            ZonaHorariaNegocio zonaHoraria,
            IConfiguration configuracion)
        {
            _contexto = contexto;
            _movimientos = movimientos;
            _zonaHoraria = zonaHoraria;
            _configuracion = configuracion;
        }

        /// <summary>Inventario actual: stock, precio y valor de cada producto, marcando los que están por debajo del mínimo.</summary>
        public async Task<ReporteStockDTO> ObtenerStockAsync(CancellationToken cancellationToken = default)
        {
            int stockMinimo = _configuracion.GetValue<int?>("Inventario:StockMinimo") ?? StockMinimoPorDefecto;

            var productos = await _contexto.Productos
                .AsNoTracking()
                .OrderBy(p => p.Categoria.Nombre)
                .ThenBy(p => p.Nombre)
                .Select(p => new { p.ProductoId, p.Nombre, Categoria = p.Categoria.Nombre, p.Stock, p.Precio })
                .ToListAsync(cancellationToken);

            return new ReporteStockDTO
            {
                Generado = _zonaHoraria.ALocal(DateTime.UtcNow),
                StockMinimo = stockMinimo,
                Filas = productos
                    .Select(p => new FilaStockDTO
                    {
                        ProductoId = p.ProductoId,
                        Producto = p.Nombre,
                        Categoria = p.Categoria,
                        Stock = p.Stock,
                        Precio = p.Precio,
                        StockBajo = p.Stock <= stockMinimo
                    })
                    .ToList()
            };
        }

        /// <summary>Entradas de mercadería: líneas de las compras no anuladas del período.</summary>
        public async Task<ReporteDocumentosDTO> ObtenerEntradasAsync(
            DateTime? desde,
            DateTime? hasta,
            CancellationToken cancellationToken = default)
        {
            var (desdeUtc, hastaUtc) = _zonaHoraria.RangoUtc(desde, hasta);

            var consulta = _contexto.DetallesCompra
                .AsNoTracking()
                .Where(d => !d.Compra.Anulada);

            if (desdeUtc.HasValue)
            {
                consulta = consulta.Where(d => d.Compra.Fecha >= desdeUtc.Value);
            }

            if (hastaUtc.HasValue)
            {
                consulta = consulta.Where(d => d.Compra.Fecha < hastaUtc.Value);
            }

            var lineas = await consulta
                .OrderBy(d => d.Compra.Fecha)
                .ThenBy(d => d.CompraId)
                .ThenBy(d => d.DetalleCompraId)
                .Select(d => new
                {
                    d.Compra.Fecha,
                    d.CompraId,
                    Proveedor = d.Compra.Proveedor.Nombre,
                    Producto = d.Producto.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario
                })
                .ToListAsync(cancellationToken);

            return new ReporteDocumentosDTO
            {
                Titulo = "Entradas de mercadería",
                TituloTercero = "Proveedor",
                Generado = _zonaHoraria.ALocal(DateTime.UtcNow),
                Desde = desde,
                Hasta = hasta,
                Filas = lineas
                    .Select(l => new FilaDocumentoDTO
                    {
                        Fecha = _zonaHoraria.ALocal(l.Fecha),
                        Numero = l.CompraId,
                        Tercero = l.Proveedor,
                        Producto = l.Producto,
                        Cantidad = l.Cantidad,
                        PrecioUnitario = l.PrecioUnitario
                    })
                    .ToList()
            };
        }

        /// <summary>Salidas de mercadería: líneas de las ventas no anuladas del período.</summary>
        public async Task<ReporteDocumentosDTO> ObtenerSalidasAsync(
            DateTime? desde,
            DateTime? hasta,
            CancellationToken cancellationToken = default)
        {
            var (desdeUtc, hastaUtc) = _zonaHoraria.RangoUtc(desde, hasta);

            var consulta = _contexto.DetallesVenta
                .AsNoTracking()
                .Where(d => !d.Venta.Anulada);

            if (desdeUtc.HasValue)
            {
                consulta = consulta.Where(d => d.Venta.Fecha >= desdeUtc.Value);
            }

            if (hastaUtc.HasValue)
            {
                consulta = consulta.Where(d => d.Venta.Fecha < hastaUtc.Value);
            }

            var lineas = await consulta
                .OrderBy(d => d.Venta.Fecha)
                .ThenBy(d => d.VentaId)
                .ThenBy(d => d.DetalleVentaId)
                .Select(d => new
                {
                    d.Venta.Fecha,
                    d.VentaId,
                    ClienteNombre = d.Venta.Cliente.Nombre,
                    ClienteApellido = d.Venta.Cliente.Apellido,
                    Producto = d.Producto.Nombre,
                    d.Cantidad,
                    d.PrecioUnitario
                })
                .ToListAsync(cancellationToken);

            return new ReporteDocumentosDTO
            {
                Titulo = "Salidas de mercadería (ventas)",
                TituloTercero = "Cliente",
                Generado = _zonaHoraria.ALocal(DateTime.UtcNow),
                Desde = desde,
                Hasta = hasta,
                Filas = lineas
                    .Select(l => new FilaDocumentoDTO
                    {
                        Fecha = _zonaHoraria.ALocal(l.Fecha),
                        Numero = l.VentaId,
                        Tercero = $"{l.ClienteNombre} {l.ClienteApellido}".Trim(),
                        Producto = l.Producto,
                        Cantidad = l.Cantidad,
                        PrecioUnitario = l.PrecioUnitario
                    })
                    .ToList()
            };
        }

        /// <summary>Libro de movimientos de stock del período (entradas, salidas, anulaciones y ajustes).</summary>
        public async Task<ReporteMovimientosDTO> ObtenerMovimientosAsync(
            DateTime? desde,
            DateTime? hasta,
            CancellationToken cancellationToken = default)
        {
            var movimientos = await _movimientos.ObtenerTodosAsync(desde, hasta, null, cancellationToken);

            foreach (var movimiento in movimientos)
            {
                movimiento.Fecha = _zonaHoraria.ALocal(movimiento.Fecha);
            }

            return new ReporteMovimientosDTO
            {
                Generado = _zonaHoraria.ALocal(DateTime.UtcNow),
                Desde = desde,
                Hasta = hasta,
                Filas = movimientos
            };
        }
    }
}
