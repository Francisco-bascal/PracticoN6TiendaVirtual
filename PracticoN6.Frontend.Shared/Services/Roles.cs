namespace PracticoN6.Frontend.Shared.Services
{
    /// <summary>Nombres de rol (coinciden con los de la API) para usar en [Authorize(Roles = ...)] y en AuthorizeView.</summary>
    public static class Roles
    {
        public const string Administrador = "Administrador";
        public const string Operador = "Operador";
        public const string Cliente = "Cliente";
        public const string Personal = Administrador + "," + Operador;
    }
}
