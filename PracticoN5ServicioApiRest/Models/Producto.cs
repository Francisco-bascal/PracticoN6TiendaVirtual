using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticoN5ServicioApiRest.Models
{
    public class Producto
    {
        [Key]
        public int ProductoId { get; set; }
        [Required]
        [Length(2, 100)]
        public string Nombre { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? Descripcion { get; set; }
        [Required]
        [Precision(18,2)]
        [Range(0, double.MaxValue)]
        public decimal Precio { get; set; }
        [Required]
        [Range(0, int.MaxValue)]
        public int Stock { get; set; }
        [MaxLength(300)]
        public string? ImagenRuta { get; set; }


        [ForeignKey(nameof(Categoria))]
        public int CategoriaId { get; set; }
        public virtual CategoriaProducto Categoria { get; set; } = null!;
    }
}