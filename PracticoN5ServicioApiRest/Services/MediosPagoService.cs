using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;

namespace PracticoN5ServicioApiRest.Services
{
    public class MediosPagoService
    {
        private readonly SistemaVentasDbContext _contexto;

        public MediosPagoService(SistemaVentasDbContext contexto)
        {
            _contexto = contexto;
        }

        public async Task<ICollection<MedioPagoDTO>> ObtenerTodosAsync(CancellationToken cancellationToken = default)
        {
            return await _contexto.MediosPago
                .AsNoTracking()
                .OrderBy(m => m.MedioPagoId)
                .Select(m => new MedioPagoDTO
                {
                    MedioPagoId = m.MedioPagoId,
                    Nombre = m.Nombre,
                    RequiereTarjeta = m.RequiereTarjeta
                })
                .ToListAsync(cancellationToken);
        }
    }
}
