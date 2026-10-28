using Microsoft.EntityFrameworkCore;
using PracticoN5ServicioApiRest.Data;
using PracticoN5ServicioApiRest.DTOs;
using PracticoN5ServicioApiRest.Models;

namespace PracticoN5ServicioApiRest.Services
{
    public class VentasService
    {
        private readonly SistemaVentasDbContext _contexto;
        private readonly MovimientosStockService _stock;
        private readonly UsuarioActual _usuarioActual;
        private readonly ClientesService _clientes;
        private readonly PasarelaPagoSimulada _pasarela;

        public VentasService(
            SistemaVentasDbContext contexto,
            MovimientosStockService stock,
            UsuarioActual usuarioActual,
            ClientesService clientes,
            PasarelaPagoSimulada pasarela)
        {
            _contexto = contexto;
            _stock = stock;
            _usuarioActual = usuarioActual;
            _clientes = clientes;
            _pasarela = pasarela;
        }

        /// <summary>Devuelve las ventas paginadas.</summary>
        /// <param name="pagina">Número de página (default 1).</param>
        /// <param name="tamanoPagina">Elementos por página (default 10, máx. 100).</param>
        public async Task<ResultadoPaginadoDto<Venta>> ObtenerTodosAsync(
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

            // AsNoTracking: consulta de solo lectura. Include: carga cliente y detalles con producto.
            var consulta = _contexto.Ventas
                .AsNoTracking()
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(v => v.Fecha)
                .ThenByDescending(v => v.VentaId);

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

            return new ResultadoPaginadoDto<Venta>
            {
                PaginaActual = pagina,
                TamanoPagina = tamanoPagina,
                TotalElementos = totalElementos,
                TotalPaginas = totalPaginas,
                Elementos = elementos
            };
        }

        public async Task<Venta?> ObtenerPorIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _contexto.Ventas
                .AsNoTracking()
                .Include(v => v.Cliente)
                .Include(v => v.Pago!)
                    .ThenInclude(p => p.MedioPago)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.VentaId == id, cancellationToken);
        }

        public async Task<ICollection<Venta>> ObtenerPorClienteAsync(
            int clienteId, 
            CancellationToken cancellationToken = default)
        {
            return await _contexto.Ventas
                .AsNoTracking()
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(v => v.ClienteId == clienteId)
                .ToListAsync(cancellationToken);
        }

        /// <summary>Ventas de los clientes asociados a la cuenta de un usuario, más recientes primero.</summary>
        public async Task<ICollection<PedidoDTO>> ObtenerPedidosDeUsuarioAsync(
            int usuarioId,
            CancellationToken cancellationToken = default)
        {
            var ventas = await ConsultaPedidos()
                .Where(v => v.Cliente.UsuarioId == usuarioId)
                .OrderByDescending(v => v.Fecha)
                .ThenByDescending(v => v.VentaId)
                .ToListAsync(cancellationToken);

            return ventas.Select(MapearPedido).ToList();
        }

        public async Task<PedidoDTO?> ObtenerPedidoAsync(int ventaId, CancellationToken cancellationToken = default)
        {
            var venta = await ConsultaPedidos()
                .FirstOrDefaultAsync(v => v.VentaId == ventaId, cancellationToken);

            return venta == null ? null : MapearPedido(venta);
        }

        /// <summary>Indica si la venta pertenece a un cliente asociado a la cuenta del usuario.</summary>
        public async Task<bool> EsDeUsuarioAsync(int ventaId, int usuarioId, CancellationToken cancellationToken = default)
        {
            return await _contexto.Ventas
                .AnyAsync(v => v.VentaId == ventaId && v.Cliente.UsuarioId == usuarioId, cancellationToken);
        }

        /// <summary>Registra una venta, valida stock suficiente y lo decrementa, todo en una transacción.</summary>
        /// <param name="ventaDto">Datos de la venta: cliente, fecha opcional y líneas de detalle.</param>
        public async Task<Venta> RegistrarVentaAsync(CreateVentaDTO ventaDto, CancellationToken cancellationToken = default)
        {
            // Debe tener al menos una línea de detalle.
            if (ventaDto.Detalles == null || !ventaDto.Detalles.Any())
            {
                throw new InvalidOperationException("La venta debe contener al menos un producto en sus detalles.");
            }

            // El cliente debe existir.
            bool clienteExiste = await _contexto.Clientes
                .AnyAsync(c => c.ClienteId == ventaDto.ClienteId, cancellationToken);

            if (!clienteExiste)
            {
                throw new InvalidOperationException($"El cliente con ID {ventaDto.ClienteId} no existe.");
            }

            // Inicia una transacción: venta, detalles y stock se confirman o revierten juntos.
            await using var transaccion =
                await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var venta = new Venta
                {
                    // Fecha por defecto: la actual en UTC.
                    Fecha = ventaDto.Fecha == default
                        ? DateTime.UtcNow
                        : ventaDto.Fecha,

                    ClienteId = ventaDto.ClienteId,
                    UsuarioId = _usuarioActual.Id
                };

                foreach (var detalleDto in ventaDto.Detalles)
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

                    //Lógica de ajuste de cantidad en stock: vender resta stock (y falla si no hay suficiente).
                    await _stock.AjustarAsync(
                        detalleDto.ProductoId,
                        -detalleDto.Cantidad,
                        MotivosMovimiento.Venta,
                        venta: venta,
                        cancellationToken: cancellationToken);

                    var detalle = new DetalleVenta
                    {
                        ProductoId = detalleDto.ProductoId,
                        Cantidad = detalleDto.Cantidad,
                        PrecioUnitario = detalleDto.PrecioUnitario
                    };

                    venta.Detalles.Add(detalle);
                }

                // Guarda los cambios dentro de la transacción.
                await _contexto.Ventas.AddAsync(venta, cancellationToken);
                await _contexto.SaveChangesAsync(cancellationToken);

                // Confirma los cambios definitivamente.
                await transaccion.CommitAsync(cancellationToken);


                // Cargar datos relacionados para la respuesta
                await _contexto.Entry(venta)
                    .Reference(v => v.Cliente)
                    .LoadAsync(cancellationToken);

                await _contexto.Entry(venta)
                    .Collection(v => v.Detalles)
                    .Query()
                    .Include(d => d.Producto)
                    .LoadAsync(cancellationToken);

                return venta;
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
        /// Compra desde el carrito: toma los precios vigentes del catálogo (el cliente solo manda producto y cantidad),
        /// descuenta el stock, simula el pago y registra la venta. Todo ocurre en una sola transacción: si el pago es
        /// rechazado o falta stock, no queda nada registrado.
        /// </summary>
        public async Task<PedidoDTO> CheckoutAsync(CheckoutRequestDTO dto, CancellationToken cancellationToken = default)
        {
            int usuarioId = _usuarioActual.Id
                ?? throw new InvalidOperationException("No se pudo identificar al usuario que realiza la compra.");

            if (dto.Items == null || dto.Items.Count == 0)
            {
                throw new ArgumentException("El carrito está vacío.");
            }

            if (dto.Items.Any(i => i.Cantidad <= 0))
            {
                throw new ArgumentException("Todas las cantidades deben ser mayores a cero.");
            }

            // Une las líneas repetidas del mismo producto.
            var lineas = dto.Items
                .GroupBy(i => i.ProductoId)
                .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(i => i.Cantidad) })
                .ToList();

            var medioPago = await _contexto.MediosPago
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MedioPagoId == dto.MedioPagoId, cancellationToken);

            if (medioPago == null)
            {
                throw new KeyNotFoundException($"No se encontró el medio de pago con ID {dto.MedioPagoId}.");
            }

            // El personal puede vender a un cliente puntual (venta de mostrador); un cliente siempre compra para sí.
            Cliente cliente;
            if (dto.ClienteId.HasValue && _usuarioActual.EsPersonal)
            {
                cliente = await _contexto.Clientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.ClienteId == dto.ClienteId.Value, cancellationToken)
                    ?? throw new KeyNotFoundException($"No se encontró el cliente con ID {dto.ClienteId.Value}.");
            }
            else
            {
                cliente = await _clientes.ObtenerOCrearPorUsuarioAsync(usuarioId, cancellationToken);
            }

            // Precios vigentes del catálogo.
            var ids = lineas.Select(l => l.ProductoId).ToList();
            var productos = await _contexto.Productos
                .AsNoTracking()
                .Where(p => ids.Contains(p.ProductoId))
                .ToDictionaryAsync(p => p.ProductoId, cancellationToken);

            decimal total = 0;

            foreach (var linea in lineas)
            {
                if (!productos.TryGetValue(linea.ProductoId, out var producto))
                {
                    throw new KeyNotFoundException($"No se encontró el producto con ID {linea.ProductoId}.");
                }

                if (producto.Precio <= 0)
                {
                    throw new InvalidOperationException($"El producto '{producto.Nombre}' no tiene un precio configurado y no puede venderse.");
                }

                total += producto.Precio * linea.Cantidad;
            }

            await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var venta = new Venta
                {
                    Fecha = DateTime.UtcNow,
                    ClienteId = cliente.ClienteId,
                    UsuarioId = usuarioId
                };

                foreach (var linea in lineas)
                {
                    // Reserva el stock; falla con conflicto si otra compra se llevó las últimas unidades.
                    await _stock.AjustarAsync(
                        linea.ProductoId,
                        -linea.Cantidad,
                        MotivosMovimiento.Venta,
                        venta: venta,
                        cancellationToken: cancellationToken);

                    venta.Detalles.Add(new DetalleVenta
                    {
                        ProductoId = linea.ProductoId,
                        Cantidad = linea.Cantidad,
                        PrecioUnitario = productos[linea.ProductoId].Precio
                    });
                }

                // Si la pasarela rechaza el pago, la excepción deshace también la reserva de stock.
                string codigoAutorizacion = _pasarela.Autorizar(medioPago, total, dto.NumeroTarjeta);

                venta.Pago = new Pago
                {
                    Monto = total,
                    Fecha = DateTime.UtcNow,
                    CodigoAutorizacion = codigoAutorizacion,
                    MedioPagoId = medioPago.MedioPagoId
                };

                await _contexto.Ventas.AddAsync(venta, cancellationToken);
                await _contexto.SaveChangesAsync(cancellationToken);
                await transaccion.CommitAsync(cancellationToken);

                return (await ObtenerPedidoAsync(venta.VentaId, cancellationToken))!;
            }
            catch
            {
                await transaccion.RollbackAsync(cancellationToken);
                // Las entidades de la operación fallida no deben quedar pendientes de guardarse.
                _contexto.ChangeTracker.Clear();
                throw;
            }
        }

        /// <summary>
        /// Anula una venta y devuelve el stock que había descontado, todo en una transacción.
        /// La venta no se borra: queda marcada como anulada para conservar la trazabilidad.
        /// </summary>
        /// <param name="id">Identificador de la venta a anular.</param>
        public async Task<bool> AnularVentaAsync(int id, CancellationToken cancellationToken = default)
        {
            // Transacción: stock y marca de anulación se modifican juntos.
            await using var transaccion = await _contexto.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var venta = await _contexto.Ventas
                    .Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.VentaId == id, cancellationToken);

                if (venta == null)
                {
                    throw new KeyNotFoundException($"No se encontró la venta con ID {id}.");
                }

                if (venta.Anulada)
                {
                    throw new InvalidOperationException($"La venta con ID {id} ya fue anulada.");
                }

                // Reincorporar el stock de los productos vendidos (suma stock: no requiere validar disponibilidad).
                foreach (var detalle in venta.Detalles)
                {
                    await _stock.AjustarAsync(
                        detalle.ProductoId,
                        detalle.Cantidad,
                        MotivosMovimiento.AnulacionVenta,
                        venta: venta,
                        cancellationToken: cancellationToken);
                }

                venta.Anulada = true;
                venta.FechaAnulacion = DateTime.UtcNow;

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

        private IQueryable<Venta> ConsultaPedidos()
        {
            return _contexto.Ventas
                .AsNoTracking()
                .Include(v => v.Cliente)
                .Include(v => v.Pago!)
                    .ThenInclude(p => p.MedioPago)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto);
        }

        private static PedidoDTO MapearPedido(Venta venta)
        {
            return new PedidoDTO
            {
                VentaId = venta.VentaId,
                Fecha = venta.Fecha,
                ClienteId = venta.ClienteId,
                Cliente = $"{venta.Cliente.Nombre} {venta.Cliente.Apellido}".Trim(),
                Anulada = venta.Anulada,
                MedioPago = venta.Pago?.MedioPago?.Nombre,
                CodigoAutorizacion = venta.Pago?.CodigoAutorizacion,
                Items = venta.Detalles
                    .Select(d => new PedidoItemDTO
                    {
                        ProductoId = d.ProductoId,
                        Producto = d.Producto.Nombre,
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario
                    })
                    .ToList()
            };
        }
    }
}
