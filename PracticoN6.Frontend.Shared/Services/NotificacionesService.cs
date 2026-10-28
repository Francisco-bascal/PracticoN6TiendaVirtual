namespace PracticoN6.Frontend.Shared.Services
{
    public record Notificacion(string Texto, string Tipo);

    /// <summary>Avisos breves ("toast") que se muestran unos segundos sobre la pantalla actual.</summary>
    public class NotificacionesService
    {
        public event Action<Notificacion>? Nueva;

        public void Mostrar(string texto, string tipo = "info") => Nueva?.Invoke(new Notificacion(texto, tipo));
        public void Exito(string texto) => Mostrar(texto, "exito");
        public void Error(string texto) => Mostrar(texto, "error");
    }
}
