namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>
    /// Las fechas se guardan en UTC, pero los reportes se piden y se muestran en la hora local del negocio
    /// (configurable en Negocio:ZonaHoraria; por defecto Argentina).
    /// </summary>
    public class ZonaHorariaNegocio
    {
        private readonly TimeZoneInfo _zona;

        public ZonaHorariaNegocio(IConfiguration configuracion)
        {
            string id = configuracion["Negocio:ZonaHoraria"] ?? "America/Argentina/Buenos_Aires";

            try
            {
                _zona = TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Sin datos de zonas horarias en el sistema: se trabaja en UTC en lugar de fallar.
                _zona = TimeZoneInfo.Utc;
            }
        }

        public DateTime ALocal(DateTime fechaUtc)
        {
            var utc = DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, _zona);
        }

        /// <summary>
        /// Convierte un rango de días locales (ambos extremos inclusive) en el rango UTC equivalente:
        /// [desde 00:00, hasta + 1 día 00:00). Cualquiera de los extremos puede ser null (sin límite).
        /// </summary>
        public (DateTime? DesdeUtc, DateTime? HastaUtcExclusivo) RangoUtc(DateTime? desde, DateTime? hasta)
        {
            if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
            {
                throw new ArgumentException("La fecha 'desde' no puede ser posterior a la fecha 'hasta'.");
            }

            DateTime? desdeUtc = desde.HasValue ? ALocalAUtc(desde.Value.Date) : null;
            DateTime? hastaUtc = hasta.HasValue ? ALocalAUtc(hasta.Value.Date.AddDays(1)) : null;

            return (desdeUtc, hastaUtc);
        }

        private DateTime ALocalAUtc(DateTime fechaLocal)
        {
            var sinZona = DateTime.SpecifyKind(fechaLocal, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(sinZona, _zona);
        }
    }
}
