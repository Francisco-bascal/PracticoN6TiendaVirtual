using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PracticoN5ServicioApiRest.Models
{
    public class Usuario
    {
        [Key]
        public int UsuarioId { get; set; }
        [Required]
        [Length(4, 50)]
        public string NombreUsuario { get; set; } = string.Empty;
        [EmailAddress, Required, MaxLength(150)]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        // Un usuario desactivado conserva su historial pero ya no puede iniciar sesión.
        public bool Activo { get; set; } = true;


        [ForeignKey(nameof(Rol))]
        public int RolId { get; set; }
        public Rol Rol { get; set; } = null!;
    }
}
