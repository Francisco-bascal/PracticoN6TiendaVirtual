using System.Globalization;

namespace PracticoN6.Frontend.Shared.Services
{
    /// <summary>
    /// Formato de importes y fechas. No depende de la cultura del dispositivo ni de datos de globalización:
    /// el resultado es el mismo en el teléfono y en el navegador.
    /// </summary>
    public static class Formato
    {
        /// <summary>1234.5 → "$ 1.234,50".</summary>
        public static string Dinero(decimal monto)
        {
            string texto = monto.ToString("N2", CultureInfo.InvariantCulture)
                .Replace(",", "#")
                .Replace(".", ",")
                .Replace("#", ".");

            return "$ " + texto;
        }

        /// <summary>Fecha y hora en la hora local del dispositivo (la API entrega UTC).</summary>
        public static string FechaHora(DateTime fecha)
            => ALocal(fecha).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        public static string Fecha(DateTime fecha)
            => ALocal(fecha).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public static string ParaApi(DateTime fecha)
            => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static DateTime ALocal(DateTime fecha)
        {
            return fecha.Kind switch
            {
                DateTimeKind.Utc => fecha.ToLocalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(fecha, DateTimeKind.Utc).ToLocalTime(),
                _ => fecha
            };
        }

        public static string EtiquetaMotivo(string motivo) => motivo switch
        {
            "StockInicial" => "Stock inicial",
            "Compra" => "Compra",
            "Venta" => "Venta",
            "AnulacionCompra" => "Anulación de compra",
            "AnulacionVenta" => "Anulación de venta",
            "AjusteManual" => "Ajuste manual",
            _ => motivo
        };
    }
}
