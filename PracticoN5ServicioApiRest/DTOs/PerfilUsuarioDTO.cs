using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.DTOs
{
    /// <summary>Datos del usuario autenticado junto con su perfil de cliente (si lo tiene).</summary>
    public class PerfilUsuarioDTO
    {
        public int UsuarioId { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        public int? ClienteId { get; set; }
        public string? Nombre { get; set; }
        public string? Apellido { get; set; }
        public string? Telefono { get; set; }
        public string? Direccion { get; set; }
    }

    public class ActualizarPerfilDTO
    {
        [Required]
        [Length(2, 100)]
        public string Nombre { get; set; } = string.Empty;
        [MaxLength(100)]
        public string? Apellido { get; set; }
        [MaxLength(20)]
        public string? Telefono { get; set; }
        [MaxLength(200)]
        public string? Direccion { get; set; }
    }
}
