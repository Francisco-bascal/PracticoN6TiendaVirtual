using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.DTOs
{
    public class RegistrarUsuarioDTO
    {
        [Required]
        [Length(4, 50)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        // Datos opcionales del perfil de cliente que se crea junto con la cuenta.
        [Length(2, 100)]
        public string? Nombre { get; set; }
        [MaxLength(100)]
        public string? Apellido { get; set; }
        [MaxLength(20)]
        public string? Telefono { get; set; }
        [MaxLength(200)]
        public string? Direccion { get; set; }
    }
}
