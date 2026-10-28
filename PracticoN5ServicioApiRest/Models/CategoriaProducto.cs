using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticoN5ServicioApiRest.Models
{
    public class CategoriaProducto
    {
        [Key]
        public int CategoriaId { get; set; }
        [Required]
        [Length(3, 50)]
        public string Nombre { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? Descripcion { get; set; }


        [InverseProperty(nameof(Producto.Categoria))]
        public ICollection<Producto> Productos { get; set; } = new List<Producto>();
    }
}