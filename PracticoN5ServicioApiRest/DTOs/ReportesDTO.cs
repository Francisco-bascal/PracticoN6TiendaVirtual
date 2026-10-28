using System.ComponentModel.DataAnnotations;

namespace PracticoN5ServicioApiRest.DTOs
{
    public class EnviarReporteDTO
    {
        [Required]
        [EmailAddress]
        [MaxLength(150)]
        public string Destinatario { get; set; } = string.Empty;
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
    }

    public class EnviarComprobanteDTO
    {
        [EmailAddress]
        [MaxLength(150)]
        public string? Destinatario { get; set; }
    }

    public class ResultadoEnvioDTO
    {
        public bool Enviado { get; set; }
        // "SMTP" si salió por el servidor de correo configurado; "Archivo" si no hay SMTP y se dejó un .eml en disco (modo desarrollo).
        public string Modo { get; set; } = string.Empty;
        public string Mensaje { get; set; } = string.Empty;
    }

    public class FilaStockDTO
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public int Stock { get; set; }
        public decimal Precio { get; set; }
        public decimal Valor => Stock * Precio;
        public bool StockBajo { get; set; }
    }

    public class ReporteStockDTO
    {
        public DateTime Generado { get; set; }
        public int StockMinimo { get; set; }
        public ICollection<FilaStockDTO> Filas { get; set; } = new List<FilaStockDTO>();
        public int TotalUnidades => Filas.Sum(f => f.Stock);
        public decimal ValorTotal => Filas.Sum(f => f.Valor);
        public int ProductosConStockBajo => Filas.Count(f => f.StockBajo);
    }

    public class FilaDocumentoDTO
    {
        public DateTime Fecha { get; set; }
        public int Numero { get; set; }
        // Proveedor en las entradas, cliente en las salidas.
        public string Tercero { get; set; } = string.Empty;
        public string Producto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal => Cantidad * PrecioUnitario;
    }

    /// <summary>Reporte de entradas (compras) o salidas (ventas) de mercadería en un período.</summary>
    public class ReporteDocumentosDTO
    {
        public string Titulo { get; set; } = string.Empty;
        public string TituloTercero { get; set; } = string.Empty;
        public DateTime Generado { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public ICollection<FilaDocumentoDTO> Filas { get; set; } = new List<FilaDocumentoDTO>();
        public int TotalUnidades => Filas.Sum(f => f.Cantidad);
        public decimal TotalImporte => Filas.Sum(f => f.Subtotal);
    }

    public class ReporteMovimientosDTO
    {
        public DateTime Generado { get; set; }
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public ICollection<MovimientoStockDTO> Filas { get; set; } = new List<MovimientoStockDTO>();
        public int TotalEntradas => Filas.Where(f => f.Cantidad > 0).Sum(f => f.Cantidad);
        public int TotalSalidas => -Filas.Where(f => f.Cantidad < 0).Sum(f => f.Cantidad);
    }
}
