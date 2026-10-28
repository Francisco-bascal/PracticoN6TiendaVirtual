using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class DetallesVentaService
    {
        private readonly SistemaVentasDbContext _contexto;

        public DetallesVentaService(SistemaVentasDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<ICollection<DetalleVenta>> ObtenerPorVentaIdAsync(
            int ventaId, 
            CancellationToken cancellationToken = default)
        {
            return await _contexto.DetallesVenta
                .AsNoTracking()
                .Include(d => d.Producto)
                .Where(d => d.VentaId == ventaId)
                .ToListAsync(cancellationToken);
        }

        public async Task<ICollection<DetalleVenta>> ObtenerPorProductoIdAsync(
            int productoId, 
            CancellationToken cancellationToken = default)
        {
            return await _contexto.DetallesVenta
                .AsNoTracking()
                .Include(d => d.Venta)
                    .ThenInclude(v => v.Cliente)
                .Where(d => d.ProductoId == productoId)
                .ToListAsync(cancellationToken);
        }

        public async Task<DetalleVenta?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _contexto.DetallesVenta
                .AsNoTracking()
                .Include(d => d.Producto)
                .Include(d => d.Venta)
                .FirstOrDefaultAsync(d => d.DetalleVentaId == id, cancellationToken);
        }
    }
}
