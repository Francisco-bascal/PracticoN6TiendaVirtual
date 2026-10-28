using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.DTOs
{
    public class ActualizarUsuarioDTO
    {
        [Required]
        [Length(4, 50)]
        public string NombreUsuario { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        public string? Password { get; set; }

        [Required]
        public string Rol { get; set; } = string.Empty;

        // null = no cambia el estado actual del usuario.
        public bool? Activo { get; set; }
    }
}
