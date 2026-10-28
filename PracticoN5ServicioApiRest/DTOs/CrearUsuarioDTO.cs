using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.DTOs
{
    public class CrearUsuarioDTO
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

        [Required]
        public string Rol { get; set; } = string.Empty;
    }
}