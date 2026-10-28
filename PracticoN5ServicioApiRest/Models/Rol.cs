using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PracticoN5ServicioApiRest.Models
{
    public class Rol
    {
        [Key]
        public int RolId { get; set; }
        [Required]
        [Length(3, 30)]
        public string Nombre { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? Descripcion { get; set; }


        [JsonIgnore]
        [InverseProperty(nameof(Usuario.Rol))]
        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}
