using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.Models
{
    /// <summary>Error no controlado de la API o informado por la aplicación móvil.</summary>
    public class RegistroError
    {
        [Key]
        public int RegistroErrorId { get; set; }
        [Required]
        public DateTime Fecha { get; set; }
        // "API" para errores del servidor, "App" para los que informa el frontend.
        [Required]
        [MaxLength(10)]
        public string Origen { get; set; } = "API";
        [Required]
        [MaxLength(1000)]
        public string Mensaje { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? TipoExcepcion { get; set; }
        public string? StackTrace { get; set; }
        [MaxLength(300)]
        public string? Ruta { get; set; }
        [MaxLength(10)]
        public string? Metodo { get; set; }
        [MaxLength(50)]
        public string? Usuario { get; set; }
    }
}
