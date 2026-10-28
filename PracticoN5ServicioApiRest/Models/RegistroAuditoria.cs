using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.Models
{
    /// <summary>
    /// Traza de auditoría: quién hizo qué y cuándo. El usuario se guarda como texto (sin clave foránea)
    /// para que el registro sobreviva aunque el usuario se elimine después.
    /// </summary>
    public class RegistroAuditoria
    {
        [Key]
        public int RegistroAuditoriaId { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        [Required]
        [MaxLength(50)]
        public string Usuario { get; set; } = string.Empty;
        [Required]
        [MaxLength(60)]
        public string Accion { get; set; } = string.Empty;
        [Required]
        [MaxLength(60)]
        public string Entidad { get; set; } = string.Empty;
        [MaxLength(40)]
        public string? EntidadId { get; set; }
        [MaxLength(500)]
        public string? Detalle { get; set; }
        [MaxLength(45)]
        public string? DireccionIp { get; set; }
        public int CodigoRespuesta { get; set; }
    }
}
