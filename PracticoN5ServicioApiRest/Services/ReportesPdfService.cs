using PracticoN5ServicioApiRest.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace PracticoN5ServicioApiRest.Services
{
    /// <summary>Genera los PDF de los reportes y del comprobante de venta.</summary>
    public class ReportesPdfService
    {
        private static readonly string ColorPrimario = "#1F3A5F";
        private static readonly string ColorFilaAlterna = "#F2F5F9";
        private static readonly string ColorAlerta = "#B3261E";

        private static readonly CultureInfo Cultura = ObtenerCultura();

        private readonly IConfiguration _configuracion;
        private readonly ZonaHorariaNegocio _zonaHoraria;

        static ReportesPdfService()
        {
            // Licencia Community de QuestPDF: gratuita para uso educativo y proyectos pequeños.
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public ReportesPdfService(IConfiguration configuracion, ZonaHorariaNegocio zonaHoraria)
        {
            _configuracion = configuracion;
            _zonaHoraria = zonaHoraria;
        }

        private string Negocio => _configuracion["Negocio:Nombre"] ?? "Tienda Móvil";

        public byte[] GenerarStock(ReporteStockDTO reporte, string? generadoPor)
        {
            return Documento(
                "Reporte de stock",
                $"Inventario al {Fecha(reporte.Generado)} · Stock mínimo: {reporte.StockMinimo} unidades",
                generadoPor,
                contenido => contenido.Column(columna =>
                {
                    columna.Spacing(10);

                    columna.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(4);
                            c.RelativeColumn(3);
                            c.RelativeColumn(1.5f);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2.5f);
                        });

                        Encabezado(tabla, new[] { 2, 3, 4 }, "Producto", "Categoría", "Stock", "Precio", "Valor");

                        int i = 0;
                        foreach (var fila in reporte.Filas)
                        {
                            bool alterna = i++ % 2 == 1;
                            string? color = fila.StockBajo ? ColorAlerta : null;

                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, fila.Producto, color));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, fila.Categoria, color));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, fila.Stock.ToString(Cultura), color, fila.StockBajo));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, Dinero(fila.Precio), color));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, Dinero(fila.Valor), color));
                        }
                    });

                    columna.Item().PaddingTop(5).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10).SemiBold());
                        t.Span($"Productos: {reporte.Filas.Count}   ·   Unidades en stock: {reporte.TotalUnidades.ToString("N0", Cultura)}   ·   Valor del inventario: {Dinero(reporte.ValorTotal)}");
                    });

                    if (reporte.ProductosConStockBajo > 0)
                    {
                        columna.Item().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(9).FontColor(ColorAlerta));
                            t.Span($"En rojo: {reporte.ProductosConStockBajo} producto(s) con stock igual o inferior al mínimo.");
                        });
                    }
                }));
        }

        public byte[] GenerarDocumentos(ReporteDocumentosDTO reporte, string? generadoPor)
        {
            return Documento(
                reporte.Titulo,
                Periodo(reporte.Desde, reporte.Hasta),
                generadoPor,
                contenido => contenido.Column(columna =>
                {
                    columna.Spacing(10);

                    columna.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.9f);
                            c.RelativeColumn(0.9f);
                            c.RelativeColumn(3.2f);
                            c.RelativeColumn(3.3f);
                            c.RelativeColumn(1.2f);
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(2.4f);
                        });

                        Encabezado(tabla, new[] { 4, 5, 6 }, "Fecha", "N°", reporte.TituloTercero, "Producto", "Cant.", "P. unit.", "Subtotal");

                        int i = 0;
                        foreach (var fila in reporte.Filas)
                        {
                            bool alterna = i++ % 2 == 1;

                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, Fecha(fila.Fecha)));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, fila.Numero.ToString(Cultura)));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, fila.Tercero));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, fila.Producto));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, fila.Cantidad.ToString(Cultura)));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, Dinero(fila.PrecioUnitario)));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, Dinero(fila.Subtotal)));
                        }
                    });

                    if (reporte.Filas.Count == 0)
                    {
                        columna.Item().AlignCenter().Text("No hay movimientos en el período seleccionado.").Italic();
                    }

                    columna.Item().PaddingTop(5).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10).SemiBold());
                        t.Span($"Líneas: {reporte.Filas.Count}   ·   Unidades: {reporte.TotalUnidades.ToString("N0", Cultura)}   ·   Importe total: {Dinero(reporte.TotalImporte)}");
                    });
                }));
        }

        public byte[] GenerarMovimientos(ReporteMovimientosDTO reporte, string? generadoPor)
        {
            return Documento(
                "Movimientos de stock",
                Periodo(reporte.Desde, reporte.Hasta),
                generadoPor,
                contenido => contenido.Column(columna =>
                {
                    columna.Spacing(10);

                    columna.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.4f);
                            c.RelativeColumn(4);
                            c.RelativeColumn(3);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2.4f);
                            c.RelativeColumn(1.8f);
                        });

                        Encabezado(tabla, new[] { 3 }, "Fecha", "Producto", "Motivo", "Cant.", "Usuario", "Doc.");

                        int i = 0;
                        foreach (var fila in reporte.Filas)
                        {
                            bool alterna = i++ % 2 == 1;
                            string cantidad = (fila.Cantidad > 0 ? "+" : string.Empty) + fila.Cantidad.ToString(Cultura);
                            string documento = fila.CompraId.HasValue ? $"Compra {fila.CompraId}"
                                : fila.VentaId.HasValue ? $"Venta {fila.VentaId}"
                                : "—";

                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, Fecha(fila.Fecha)));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, fila.Producto));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, EtiquetaMotivo(fila.Motivo)));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, cantidad, fila.Cantidad < 0 ? ColorAlerta : null, true));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, fila.Usuario ?? "—"));
                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, documento));
                        }
                    });

                    if (reporte.Filas.Count == 0)
                    {
                        columna.Item().AlignCenter().Text("No hay movimientos en el período seleccionado.").Italic();
                    }

                    columna.Item().PaddingTop(5).Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(10).SemiBold());
                        t.Span($"Movimientos: {reporte.Filas.Count}   ·   Entradas: {reporte.TotalEntradas.ToString("N0", Cultura)} u.   ·   Salidas: {reporte.TotalSalidas.ToString("N0", Cultura)} u.");
                    });
                }));
        }

        public byte[] GenerarComprobante(PedidoDTO pedido)
        {
            return Documento(
                $"Comprobante de venta N° {pedido.VentaId}",
                pedido.Anulada ? "VENTA ANULADA" : $"Fecha: {Fecha(_zonaHoraria.ALocal(pedido.Fecha))}",
                null,
                contenido => contenido.Column(columna =>
                {
                    columna.Spacing(12);

                    columna.Item().Text(t =>
                    {
                        t.Line($"Cliente: {pedido.Cliente}");
                        if (pedido.MedioPago != null)
                        {
                            t.Line($"Medio de pago: {pedido.MedioPago}");
                        }
                        if (pedido.CodigoAutorizacion != null)
                        {
                            t.Line($"Código de autorización: {pedido.CodigoAutorizacion}");
                        }
                    });

                    columna.Item().Table(tabla =>
                    {
                        tabla.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(5);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2.2f);
                            c.RelativeColumn(2.4f);
                        });

                        Encabezado(tabla, new[] { 1, 2, 3 }, "Producto", "Cant.", "P. unit.", "Subtotal");

                        int i = 0;
                        foreach (var item in pedido.Items)
                        {
                            bool alterna = i++ % 2 == 1;

                            tabla.Cell().Element(c => Celda(c, alterna)).Text(t => Texto(t, item.Producto));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, item.Cantidad.ToString(Cultura)));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, Dinero(item.PrecioUnitario)));
                            tabla.Cell().Element(c => Celda(c, alterna)).AlignRight().Text(t => Texto(t, Dinero(item.Subtotal)));
                        }
                    });

                    columna.Item().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(13).Bold().FontColor(ColorPrimario));
                        t.Span($"Total: {Dinero(pedido.Total)}");
                    });

                    columna.Item().PaddingTop(10).Text("Pago simulado con fines académicos: este comprobante no tiene validez fiscal.")
                        .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                }));
        }

        private byte[] Documento(string titulo, string subtitulo, string? generadoPor, Action<IContainer> contenido)
        {
            return Document.Create(documento =>
            {
                documento.Page(pagina =>
                {
                    pagina.Size(PageSizes.A4);
                    pagina.Margin(30);
                    pagina.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Lato));

                    pagina.Header().Column(columna =>
                    {
                        columna.Item().Text(Negocio).FontSize(10).FontColor(Colors.Grey.Darken1);
                        columna.Item().Text(titulo).FontSize(18).Bold().FontColor(ColorPrimario);
                        columna.Item().Text(subtitulo).FontSize(10);
                        columna.Item().PaddingTop(6).LineHorizontal(1).LineColor(ColorPrimario);
                    });

                    pagina.Content().PaddingVertical(10).Element(contenido);

                    pagina.Footer().Row(fila =>
                    {
                        fila.RelativeItem().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                            string por = string.IsNullOrWhiteSpace(generadoPor) ? string.Empty : $" por {generadoPor}";
                            t.Span($"Generado el {Fecha(_zonaHoraria.ALocal(DateTime.UtcNow))}{por}");
                        });

                        fila.AutoItem().Text(t =>
                        {
                            t.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                            t.Span("Página ");
                            t.CurrentPageNumber();
                            t.Span(" de ");
                            t.TotalPages();
                        });
                    });
                });
            }).GeneratePdf();
        }

        // "derecha" son los índices de las columnas numéricas: su título se alinea igual que sus valores.
        private static void Encabezado(TableDescriptor tabla, int[] derecha, params string[] titulos)
        {
            tabla.Header(encabezado =>
            {
                for (int i = 0; i < titulos.Length; i++)
                {
                    var celda = encabezado.Cell()
                        .Background(ColorPrimario)
                        .PaddingVertical(4)
                        .PaddingHorizontal(4);

                    if (derecha.Contains(i))
                    {
                        celda = celda.AlignRight();
                    }

                    celda.Text(titulos[i])
                        .FontColor(Colors.White)
                        .SemiBold();
                }
            });
        }

        private static IContainer Celda(IContainer contenedor, bool alterna)
        {
            return contenedor
                .Background(alterna ? ColorFilaAlterna : Colors.White)
                .PaddingVertical(3)
                .PaddingHorizontal(4);
        }

        private static void Texto(TextDescriptor texto, string valor, string? color = null, bool negrita = false)
        {
            var span = texto.Span(valor);

            if (color != null)
            {
                span.FontColor(color);
            }

            if (negrita)
            {
                span.Bold();
            }
        }

        private static string EtiquetaMotivo(string motivo) => motivo switch
        {
            "StockInicial" => "Stock inicial",
            "Compra" => "Compra",
            "Venta" => "Venta",
            "AnulacionCompra" => "Anulación de compra",
            "AnulacionVenta" => "Anulación de venta",
            "AjusteManual" => "Ajuste manual",
            _ => motivo
        };

        private static string Periodo(DateTime? desde, DateTime? hasta)
        {
            if (desde.HasValue && hasta.HasValue)
            {
                return $"Período: del {desde.Value:dd/MM/yyyy} al {hasta.Value:dd/MM/yyyy}";
            }

            if (desde.HasValue)
            {
                return $"Período: desde el {desde.Value:dd/MM/yyyy}";
            }

            if (hasta.HasValue)
            {
                return $"Período: hasta el {hasta.Value:dd/MM/yyyy}";
            }

            return "Período: todo el historial";
        }

        private static string Fecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        /// <summary>Importe en el formato local ($ 1.234,50), igual al de la app y al de los PDF.</summary>
        public static string Dinero(decimal monto) => monto.ToString("C2", Cultura);

        private static CultureInfo ObtenerCultura()
        {
            try
            {
                return new CultureInfo("es-AR");
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }
}
