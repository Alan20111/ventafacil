using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Drawing.Text;
using VentaFacil.Core;
using VentaFacil.Data;

namespace VentaFacil.Printing
{
    /// <summary>
    /// Etiquetas con código de barras (Code 128) para los productos que no lo traen (granel, verdura…).
    /// Salen por la impresora de tickets: cada etiqueta es una tira con el nombre, el precio y las barras del
    /// CÓDIGO CORTO del producto, que el lector entrega y el cobro reconoce igual que cualquier otro código.
    /// Se dibuja como imagen: sirve igual por el controlador de Windows y, en modo ESC/POS, como trama
    /// (no depende de que la impresora tenga comando propio de códigos de barras).
    /// </summary>
    public static class Etiquetas
    {
        const int PuntosPorPulgada = 203; // impresoras térmicas comunes

        /// <summary>Ancho de la imagen en puntos: 384 (papel de 58 mm) o 576 (80 mm).</summary>
        public static int AnchoPx(Config c) { return c.AnchoTicket <= 58 ? 384 : 576; }

        public static Bitmap Dibujar(Producto p, Config c)
        {
            string codigo = p.Codigo;
            if (!Code128.Soporta(codigo))
                throw new InvalidOperationException("El código «" + codigo + "» no se puede convertir en código de barras (solo letras sin acentos, números y signos, hasta " + Code128.LongitudMaxima + " caracteres).");
            bool[] barras = Code128.Modulos(codigo);

            int W = AnchoPx(c);
            const int margen = 10, silencio = 10, altoBarras = 96;
            int px = Math.Max(1, Math.Min(4, (W - 2 * margen) / (barras.Length + 2 * silencio))); // puntos por módulo

            string precio = Fmt.Dinero(p.Precio) + (p.Granel ? " /kg" : " /pz");
            using (var fNombre = new Font("Segoe UI", W / 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var fPrecio = LetraQueCabe(precio, W / 7f, W - 2 * margen))
            using (var fCodigo = new Font("Consolas", W / 15f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var medida = new Bitmap(1, 1))
            using (var gm = Graphics.FromImage(medida))
            {
                int hNombre = (int)Math.Ceiling(fNombre.GetHeight(gm) * 2);   // hasta dos renglones
                int hPrecio = (int)Math.Ceiling(fPrecio.GetHeight(gm));
                int hCodigo = (int)Math.Ceiling(fCodigo.GetHeight(gm));
                int alto = margen + hNombre + 4 + hPrecio + 6 + altoBarras + 4 + hCodigo + margen + 14;

                var bmp = new Bitmap(W, alto, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.SmoothingMode = SmoothingMode.None;
                    g.PixelOffsetMode = PixelOffsetMode.None;
                    g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit; // texto nítido para papel térmico
                    var centro = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit };

                    int y = margen;
                    g.DrawString(p.Descripcion, fNombre, Brushes.Black, new RectangleF(margen, y, W - 2 * margen, hNombre), centro);
                    y += hNombre + 4;
                    g.DrawString(precio, fPrecio, Brushes.Black, new RectangleF(margen, y, W - 2 * margen, hPrecio), centro);
                    y += hPrecio + 6;

                    // Barras (tramos seguidos se dibujan de una sola vez)
                    int x0 = (W - barras.Length * px) / 2;
                    for (int i = 0; i < barras.Length; )
                    {
                        if (!barras[i]) { i++; continue; }
                        int j = i;
                        while (j < barras.Length && barras[j]) j++;
                        g.FillRectangle(Brushes.Black, x0 + i * px, y, (j - i) * px, altoBarras);
                        i = j;
                    }
                    y += altoBarras + 4;
                    g.DrawString(codigo, fCodigo, Brushes.Black, new RectangleF(margen, y, W - 2 * margen, hCodigo), centro);
                }
                return bmp;
            }
        }

        /// <summary>La letra más grande (hasta el tamaño dado) con la que el precio cabe en un renglón: nunca se corta.</summary>
        static Font LetraQueCabe(string texto, float tam, int ancho)
        {
            using (var b = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(b))
                for (; ; tam -= 2f)
                {
                    var f = new Font("Segoe UI", tam, FontStyle.Bold, GraphicsUnit.Pixel);
                    if (tam <= 16f || g.MeasureString(texto, f).Width <= ancho - 4) return f;
                    f.Dispose();
                }
        }

        /// <summary>Trama ESC/POS de las etiquetas, una tras otra, con corte entre ellas si la impresora corta.</summary>
        public static byte[] ComoEscPos(IList<Bitmap> etiquetas, Config c)
        {
            var b = new List<byte>();
            b.AddRange(new byte[] { 0x1B, 0x40 }); // inicializar
            foreach (var bmp in etiquetas)
            {
                var datos = RasterEscPos.Empaquetar((x, y) =>
                {
                    Color px = bmp.GetPixel(x, y);
                    return (px.R * 299 + px.G * 587 + px.B * 114) / 1000 < 128;
                }, bmp.Width, bmp.Height);
                b.AddRange(new byte[] { 0x1B, 0x61, 0x01 }); // centrado
                b.AddRange(RasterEscPos.Comandos(datos, bmp.Width, bmp.Height));
                for (int i = 0; i < Math.Max(2, c.LineasAvance); i++) b.Add(0x0A);
                if (c.Cortar) b.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x00 });
            }
            return b.ToArray();
        }

        static void ImprimirWindows(IList<Bitmap> etiquetas, Config c)
        {
            int indice = 0;
            using (var doc = new PrintDocument())
            {
                if (!string.IsNullOrEmpty(c.Impresora)) doc.PrinterSettings.PrinterName = c.Impresora;
                if (!doc.PrinterSettings.IsValid) throw new InvalidOperationException("La impresora \"" + c.Impresora + "\" no está instalada.");
                doc.DocumentName = "Etiquetas VentaFacil";
                doc.PrintController = new StandardPrintController(); // sin ventana de "Imprimiendo…"
                doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                int anchoCentesimas = (int)(c.AnchoTicket / 25.4 * 100);
                int altoMax = 0;
                foreach (var e in etiquetas) altoMax = Math.Max(altoMax, e.Height);
                try { doc.DefaultPageSettings.PaperSize = new PaperSize("Etiqueta", anchoCentesimas, (int)(altoMax * 100.0 / PuntosPorPulgada) + 20); }
                catch { }

                doc.PrintPage += (s, e) =>
                {
                    var bmp = etiquetas[indice];
                    float ancho = e.PageSettings.PrintableArea.Width > 0 ? Math.Min(e.PageSettings.PrintableArea.Width, anchoCentesimas) : anchoCentesimas;
                    float escala = ancho / bmp.Width; // centésimas de pulgada por punto
                    e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor; // barras nítidas, sin suavizado
                    e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                    e.Graphics.DrawImage(bmp, new RectangleF(0, 0, bmp.Width * escala, bmp.Height * escala));
                    indice++;
                    e.HasMorePages = indice < etiquetas.Count;
                };
                doc.Print();
            }
        }

        /// <summary>Imprime las etiquetas (una por copia y por producto) por la impresora de tickets configurada.</summary>
        public static void Imprimir(IList<Producto> productos, int copias, Config c = null)
        {
            if (productos == null || productos.Count == 0) throw new ArgumentException("No hay productos para etiquetar.");
            c = Impresora.Resuelta(c ?? Config.Actual);
            if (Detector.EstaDesconectada(c.Impresora))
                throw new InvalidOperationException("Windows marca la impresora \"" + c.Impresora + "\" como SIN CONEXIÓN.\nRevise el cable y que esté encendida.");
            var imagenes = new List<Bitmap>();
            try
            {
                foreach (var p in productos)
                    for (int k = 0; k < Math.Max(1, copias); k++) imagenes.Add(Dibujar(p, c));
                if (c.ModoImpresion == "ESCPOS") RawPrinter.Enviar(c.Impresora, ComoEscPos(imagenes, c), "Etiquetas VentaFacil");
                else ImprimirWindows(imagenes, c);
            }
            finally { foreach (var b in imagenes) b.Dispose(); }
        }
    }
}
