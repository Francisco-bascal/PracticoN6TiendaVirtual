namespace PracticoN5ServicioApiRest.DTOs
{
    public class LoginResponseDTO
    {
        public string Token { get; set; } = string.Empty;
        public string NombreUsuario { get; set; } = string.Empty;
        public string Rol { get; set; } = string.Empty;
        // Vencimiento del token (UTC), para que la app sepa cuándo debe pedir un nuevo inicio de sesión.
        public DateTime ExpiraUtc { get; set; }
    }
}
