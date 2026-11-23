using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using VentaFacil.Core;

namespace VentaFacil.Printing
{
    /// <summary>
    /// Envío de bytes "crudos" (RAW) a una impresora instalada en Windows.
    /// Sirve para impresoras térmicas ESC/POS (Epson, Star en modo ESC/POS, 3nStar, EC Line, Bixolon, genéricas chinas…),
    /// conectadas por USB, serial, paralelo o red, siempre que estén instaladas en Windows (incluso con el controlador "Generic / Text Only").
    /// </summary>
    public static class RawPrinter
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        class DOCINFO
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPWStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPWStr)] public string pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool OpenPrinter(string src, out IntPtr hPrinter, IntPtr pd);
        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool ClosePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFO di);
        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool EndDocPrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool StartPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool EndPagePrinter(IntPtr hPrinter);
        [DllImport("winspool.drv", SetLastError = true)]
        static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static void Enviar(string impresora, byte[] datos, string documento = "VentaFacil")
        {
            if (string.IsNullOrEmpty(impresora)) impresora = new PrinterSettings().PrinterName;
            IntPtr h;
            if (!OpenPrinter(impresora.Normalize(), out h, IntPtr.Zero))
                throw new InvalidOperationException("No se pudo abrir la impresora \"" + impresora + "\". Revise que esté instalada y encendida.");
            IntPtr buf = IntPtr.Zero;
            try
            {
                var di = new DOCINFO { pDocName = documento, pDataType = "RAW" };
                if (!StartDocPrinter(h, 1, di)) throw new InvalidOperationException("La impresora rechazó el documento (StartDoc).");
                try
                {
                    StartPagePrinter(h);
                    buf = Marshal.AllocCoTaskMem(datos.Length);
                    Marshal.Copy(datos, 0, buf, datos.Length);
                    int escritos;
                    if (!WritePrinter(h, buf, datos.Length, out escritos)) throw new InvalidOperationException("No se pudo enviar a la impresora.");
                    EndPagePrinter(h);
                }
                finally { EndDocPrinter(h); }
            }
            finally
            {
                if (buf != IntPtr.Zero) Marshal.FreeCoTaskMem(buf);
                ClosePrinter(h);
            }
        }
    }

    /// <summary>Documento de texto para ticket: renglones con formato simple.</summary>
    public class Ticket
    {
        public enum Estilo { Normal, Negrita, Grande }
        public class Renglon { public string Texto; public Estilo Estilo; public bool Centrado; }

        public readonly List<Renglon> Renglones = new List<Renglon>();
        public readonly int Ancho;
        public bool AbrirCajon;

        public Ticket(int columnas) { Ancho = Math.Max(24, columnas); }

        public Ticket Linea(string t = "", Estilo e = Estilo.Normal) { foreach (var p in Partir(t ?? "", Ancho)) Renglones.Add(new Renglon { Texto = p, Estilo = e }); return this; }
        public Ticket Centro(string t, Estilo e = Estilo.Normal)
        {
            int ancho = e == Estilo.Grande ? Ancho / 2 : Ancho;
            foreach (var p in Partir(t ?? "", ancho)) Renglones.Add(new Renglon { Texto = p, Estilo = e, Centrado = true });
            return this;
        }
        public Ticket Separador(char c = '-') { Renglones.Add(new Renglon { Texto = new string(c, Ancho) }); return this; }
        /// <summary>Texto a la izquierda y valor alineado a la derecha.</summary>
        public Ticket Par(string izq, string der, Estilo e = Estilo.Normal)
        {
            izq = izq ?? ""; der = der ?? "";
            int espacio = Ancho - der.Length - 1;
            if (izq.Length > espacio) { Linea(izq, e); izq = ""; }
            Renglones.Add(new Renglon { Texto = izq.PadRight(Ancho - der.Length) + der, Estilo = e });
            return this;
        }

        static IEnumerable<string> Partir(string texto, int ancho)
        {
            if (texto.Length == 0) { yield return ""; yield break; }
            foreach (var parrafo in texto.Replace("\r", "").Split('\n'))
            {
                var resto = parrafo;
                while (resto.Length > ancho)
                {
                    int corte = resto.LastIndexOf(' ', ancho);
                    if (corte <= 0) corte = ancho;
                    yield return resto.Substring(0, corte).TrimEnd();
                    resto = resto.Substring(corte).TrimStart();
                }
                yield return resto;
            }
        }

        public string ComoTexto()
        {
            var sb = new StringBuilder();
            foreach (var r in Renglones) sb.AppendLine(r.Centrado ? Centrar(r.Texto, r.Estilo == Estilo.Grande ? Ancho / 2 : Ancho) : r.Texto);
            return sb.ToString();
        }

        static string Centrar(string t, int ancho)
        {
            if (t.Length >= ancho) return t;
            return new string(' ', (ancho - t.Length) / 2) + t;
        }

        // ------------------------------------------------------------------
        //  ESC/POS
        // ------------------------------------------------------------------
        public byte[] ComoEscPos(Config c)
        {
            var b = new List<byte>();
            Action<byte[]> add = x => b.AddRange(x);
            var enc = Encoding.GetEncoding(850); // página de códigos PC850 (acentos y ñ)
            add(new byte[] { 0x1B, 0x40 });            // inicializar
            add(new byte[] { 0x1B, 0x74, 0x02 });      // tabla de caracteres PC850
            foreach (var r in Renglones)
            {
                add(new byte[] { 0x1B, 0x61, (byte)(r.Centrado ? 1 : 0) });                 // alineación
                byte modo = 0;
                if (r.Estilo == Estilo.Negrita) modo = 0x08;
                if (r.Estilo == Estilo.Grande) modo = 0x08 | 0x10 | 0x20;                 // negrita + doble alto + doble ancho
                add(new byte[] { 0x1B, 0x21, modo });
                add(enc.GetBytes(r.Texto));
                add(new byte[] { 0x0A });
            }
            add(new byte[] { 0x1B, 0x21, 0x00, 0x1B, 0x61, 0x00 });
            for (int i = 0; i < c.LineasAvance; i++) b.Add(0x0A);
            if (c.Cortar) add(new byte[] { 0x1D, 0x56, 0x42, 0x00 });  // corte parcial con avance
            if (AbrirCajon) add(Cajon.Pulso(c));
            return b.ToArray();
        }

        // ------------------------------------------------------------------
        //  Impresión por controlador de Windows (cualquier impresora)
        // ------------------------------------------------------------------
        public void ImprimirWindows(Config c)
        {
            int indice = 0;
            using (var normal = new Font(c.Fuente, c.TamanoFuente, FontStyle.Regular))
            using (var negrita = new Font(c.Fuente, c.TamanoFuente, FontStyle.Bold))
            using (var grande = new Font(c.Fuente, c.TamanoFuente * 1.8f, FontStyle.Bold))
            using (var doc = new PrintDocument())
            {
                if (!string.IsNullOrEmpty(c.Impresora)) doc.PrinterSettings.PrinterName = c.Impresora;
                if (!doc.PrinterSettings.IsValid) throw new InvalidOperationException("La impresora \"" + c.Impresora + "\" no está instalada.");
                doc.DocumentName = "Ticket VentaFacil";
                doc.PrintController = new StandardPrintController(); // sin ventana de "Imprimiendo…"
                doc.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
                // Papel continuo: alto grande para que el controlador térmico no corte a mitad
                int anchoCentesimas = (int)(c.AnchoTicket / 25.4 * 100);
                try
                {
                    int alto = Math.Max(300, (int)(Renglones.Count * c.TamanoFuente * 2.2) + 120);
                    doc.DefaultPageSettings.PaperSize = new PaperSize("Ticket", anchoCentesimas, alto);
                }
                catch { }

                doc.PrintPage += (s, e) =>
                {
                    var g = e.Graphics;
                    float ancho = e.PageSettings.PrintableArea.Width > 0 ? Math.Min(e.PageSettings.PrintableArea.Width, anchoCentesimas) : anchoCentesimas;
                    float x0 = Math.Max(0, e.PageSettings.PrintableArea.Left > 0 ? 0 : 4);
                    float y = 2;
                    var fmtCentro = new StringFormat { Alignment = StringAlignment.Center };
                    // Ajusta el tamaño de letra para que quepan las columnas configuradas
                    float escala = 1f;
                    float anchoTexto = g.MeasureString(new string('M', Ancho), normal, int.MaxValue, StringFormat.GenericTypographic).Width;
                    if (anchoTexto > ancho - 4) escala = (ancho - 4) / anchoTexto;
                    using (var fN = new Font(normal.FontFamily, normal.Size * escala, FontStyle.Regular))
                    using (var fB = new Font(normal.FontFamily, normal.Size * escala, FontStyle.Bold))
                    using (var fG = new Font(normal.FontFamily, grande.Size * escala, FontStyle.Bold))
                    {
                        while (indice < Renglones.Count)
                        {
                            var r = Renglones[indice];
                            var f = r.Estilo == Estilo.Grande ? fG : r.Estilo == Estilo.Negrita ? fB : fN;
                            float h = f.GetHeight(g);
                            if (e.PageBounds.Height > 0 && y > 2 && y + h > e.PageBounds.Height - 4)
                            {
                                e.HasMorePages = true;
                                return;
                            }
                            if (r.Centrado) g.DrawString(r.Texto, f, Brushes.Black, new RectangleF(x0, y, ancho - x0, h), fmtCentro);
                            else g.DrawString(r.Texto, f, Brushes.Black, x0, y, StringFormat.GenericTypographic);
                            y += h;
                            indice++;
                        }
                    }
                    e.HasMorePages = false;
                };
                doc.Print();
            }
        }
    }

    public static class Cajon
    {
        /// <summary>ESC p m t1 t2 — pulso al cajón conectado a la impresora (RJ11).</summary>
        public static byte[] Pulso(Config c)
        {
            return new byte[] { 0x1B, 0x70, (byte)(c.CajonPin == 1 ? 1 : 0), 0x19, 0xFA };
        }

        public static void Abrir(Config c)
        {
            if (c.CajonModo == "NINGUNO") return;
            if (c.CajonModo != "SERIAL") c = Impresora.Resuelta(c);
            if (c.CajonModo == "SERIAL")
            {
                using (var sp = new SerialPort(c.CajonPuerto, 9600))
                {
                    sp.Open();
                    sp.Write(Pulso(c), 0, 5);
                    sp.Close();
                }
                return;
            }
            RawPrinter.Enviar(c.Impresora, Pulso(c), "Abrir cajon");
        }
    }

    /// <summary>Impresión de un ticket según la configuración (Windows o ESC/POS).</summary>
    public static class Impresora
    {
        /// <summary>Copia de la configuración con el nombre real de impresora a usar.</summary>
        public static Config Resuelta(Config c)
        {
            var r = c.Copia();
            string aviso;
            r.Impresora = Detector.Resolver(c.Impresora, out aviso);
            if (aviso != null) Log.Error(null, aviso);
            if (string.IsNullOrEmpty(r.Impresora))
                throw new InvalidOperationException("No hay ninguna impresora instalada en Windows.\nInstale la impresora (que imprima la página de prueba de Windows) y elíjala en F6 Ajustes > Impresora.");
            return r;
        }

        public static void Imprimir(Ticket t, Config c = null)
        {
            c = Resuelta(c ?? Config.Actual);
            if (Detector.EstaDesconectada(c.Impresora))
                throw new InvalidOperationException("Windows marca la impresora \"" + c.Impresora + "\" como SIN CONEXIÓN.\nRevise el cable y que esté encendida. En Windows: Dispositivos e impresoras > clic derecho > Ver lo que se imprime > menú Impresora > quite \"Usar impresora sin conexión\".");
            for (int i = 0; i < Math.Max(1, c.Copias); i++)
            {
                bool cajonEsteVez = t.AbrirCajon && i == 0;
                if (c.ModoImpresion == "ESCPOS")
                {
                    bool original = t.AbrirCajon;
                    t.AbrirCajon = cajonEsteVez && c.CajonModo == "IMPRESORA";
                    try { RawPrinter.Enviar(c.Impresora, t.ComoEscPos(c), "Ticket VentaFacil"); }
                    finally { t.AbrirCajon = original; }
                    if (cajonEsteVez && c.CajonModo == "SERIAL") Cajon.Abrir(c);
                }
                else
                {
                    t.ImprimirWindows(c);
                    if (cajonEsteVez) { try { Cajon.Abrir(c); } catch { /* el ticket ya salió; el cajón se puede abrir a mano */ } }
                }
            }
        }

        public static List<string> Instaladas()
        {
            var l = new List<string>();
            foreach (string p in PrinterSettings.InstalledPrinters) l.Add(p);
            return l;
        }

        public static string Predeterminada()
        {
            try { return new PrinterSettings().PrinterName; } catch { return ""; }
        }
    }
}
