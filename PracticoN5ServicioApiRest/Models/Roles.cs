namespace PracticoN5ServicioApiRest.Models
{
    /// <summary>Nombres de los roles del sistema (coinciden con los registros sembrados en la tabla Roles).</summary>
    public static class NombresRol
    {
        public const string Administrador = "Administrador";
        public const string Operador = "Operador";
        public const string Cliente = "Cliente";

        /// <summary>Personal del negocio: para usar en [Authorize(Roles = NombresRol.Personal)].</summary>
        public const string Personal = Administrador + "," + Operador;
    }
}
