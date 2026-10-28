using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.DTOs
{
    /// <summary>Error capturado en la aplicación móvil que se informa al servidor para dejar constancia.</summary>
    public class ErrorClienteDTO
    {
        [Required]
        [MaxLength(1000)]
        public string Mensaje { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? TipoExcepcion { get; set; }
        [MaxLength(8000)]
        public string? StackTrace { get; set; }
        [MaxLength(300)]
        public string? Ruta { get; set; }
    }
}
