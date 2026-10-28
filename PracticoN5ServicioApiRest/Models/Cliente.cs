using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace PracticoN5ServicioApiRest.Models
{
    public class Cliente
    {
        [Key]
        public int ClienteId { get; set; }
        [Required]
        [Length(2, 100)]
        public string Nombre { get; set; } = string.Empty;
        [Length(2, 100)]
        public string? Apellido { get; set; }
        [Length(7,20)]
        public string? Telefono { get; set; }
        [EmailAddress]
        [MaxLength(150)]
        public string? Email { get; set; }
        [Length(3,200)]
        public string? Direccion { get; set; }


        // Cuenta de acceso del cliente (opcional: los clientes cargados por el personal pueden no tener usuario).
        // Se gestiona solo desde el servidor, nunca desde el body de la petición.
        [JsonIgnore]
        [ForeignKey(nameof(Usuario))]
        public int? UsuarioId { get; set; }
        [JsonIgnore]
        public Usuario? Usuario { get; set; }


        [InverseProperty(nameof(Venta.Cliente))]
        public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
    }
}
