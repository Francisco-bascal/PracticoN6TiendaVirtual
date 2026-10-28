namespace PracticoN5ServicioApiRest.Infraestructura
{
    /// <summary>Fuerza el registro de auditoría de una acción de lectura (por defecto solo se auditan las que modifican datos).</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class AuditarAttribute : Attribute { }

    /// <summary>Excluye una acción de la auditoría automática.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class NoAuditarAttribute : Attribute { }
}
