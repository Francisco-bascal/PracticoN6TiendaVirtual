using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticoN5ServicioApiRest.Models
{
    public class DetalleCompra
    {
        [Key]
        public int DetalleCompraId { get; set; }
        [Required]
        [Range(1, int.MaxValue)]
        public int Cantidad { get; set; }
        [Required]
        [Precision(18,2)]
        [Range(0.01, double.MaxValue)]
        public decimal PrecioUnitario { get; set; }


        [ForeignKey(nameof(Compra))]
        public int CompraId { get; set; }
        [Required]
        public Compra Compra { get; set; } = null!;


        [ForeignKey(nameof(Producto))]
        public int ProductoId { get; set; }
        [Required]
        public Producto Producto { get; set; } = null!;
    }
}