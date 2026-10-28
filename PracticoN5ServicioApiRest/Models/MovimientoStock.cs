using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticoN5ServicioApiRest.Models
{
    /// <summary>Motivos válidos de un movimiento de stock.</summary>
    public static class MotivosMovimiento
    {
        public const string StockInicial = "StockInicial";
        public const string Compra = "Compra";
        public const string Venta = "Venta";
        public const string AnulacionCompra = "AnulacionCompra";
        public const string AnulacionVenta = "AnulacionVenta";
        public const string AjusteManual = "AjusteManual";
    }

    /// <summary>
    /// Libro de movimientos de stock: cada entrada (cantidad positiva) o salida (cantidad negativa) de un producto.
    /// El stock de Producto es la suma de sus movimientos; este registro permite reconstruir cómo se llegó a él.
    /// </summary>
    public class MovimientoStock
    {
        [Key]
        public int MovimientoStockId { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        // Con signo: positiva = entrada, negativa = salida.
        [Required]
        public int Cantidad { get; set; }
        [Required]
        [MaxLength(30)]
        public string Motivo { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? Observacion { get; set; }


        [ForeignKey(nameof(Producto))]
        public int ProductoId { get; set; }
        public Producto Producto { get; set; } = null!;

        // Documento que originó el movimiento (solo uno de los dos, o ninguno en ajustes manuales).
        [ForeignKey(nameof(Compra))]
        public int? CompraId { get; set; }
        public Compra? Compra { get; set; }

        [ForeignKey(nameof(Venta))]
        public int? VentaId { get; set; }
        public Venta? Venta { get; set; }

        [ForeignKey(nameof(Usuario))]
        public int? UsuarioId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Usuario? Usuario { get; set; }
    }
}
