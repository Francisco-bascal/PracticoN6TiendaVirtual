using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.DTOs
{
    public class CheckoutItemDTO
    {
        public int ProductoId { get; set; }
        public int Cantidad { get; set; }
    }

    /// <summary>Compra desde el carrito: solo viajan productos y cantidades; los precios los define el servidor.</summary>
    public class CheckoutRequestDTO
    {
        public ICollection<CheckoutItemDTO> Items { get; set; } = new List<CheckoutItemDTO>();
        public int MedioPagoId { get; set; }

        // Solo se usa para simular la autorización del pago. No se guarda ni se registra.
        [MaxLength(25)]
        public string? NumeroTarjeta { get; set; }

        // Solo lo respeta el personal (venta de mostrador); un cliente siempre compra para sí mismo.
        public int? ClienteId { get; set; }
    }

    public class PedidoItemDTO
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Cantidad * PrecioUnitario;
    }

    /// <summary>Resumen de una venta con sus ítems y su pago; se usa como respuesta del checkout y en "Mis pedidos".</summary>
    public class PedidoDTO
    {
        public int VentaId { get; set; }
        public DateTime Fecha { get; set; }
        public int ClienteId { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public bool Anulada { get; set; }
        public string? MedioPago { get; set; }
        public string? CodigoAutorizacion { get; set; }
        public decimal Total => Items.Sum(i => i.Subtotal);
        public ICollection<PedidoItemDTO> Items { get; set; } = new List<PedidoItemDTO>();
    }

    public class MedioPagoDTO
    {
        public int MedioPagoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public bool RequiereTarjeta { get; set; }
    }
}
