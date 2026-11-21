using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Management;
using VentaFacil.Core;

namespace VentaFacil.Printing
{
    /// <summary>Lo que sabemos de una impresora instalada en Windows.</summary>
    public class InfoImpresora
    {
        public string Nombre = "";
        public string Puerto = "";
        public string Controlador = "";
        public bool Predeterminada;
        public bool Desconectada;
        public bool EsTicket;        // parece impresora térmica de tickets
        public bool EsVirtual;       // PDF, XPS, Fax, OneNote…
        public bool SoloTexto;       // controlador "Generic / Text Only"
        public int AnchoMm;          // 58 u 80 si se pudo deducir, 0 si no
        public string Conexion = ""; // USB, Red, Serial, Paralelo, Virtual

        public string Tipo
        {
            get { return EsVirtual ? "Virtual (no imprime papel)" : EsTicket ? "Tickets" + (AnchoMm > 0 ? " " + AnchoMm + " mm" : "") : "Normal (hojas)"; }
        }

        public string Estado { get { return Desconectada ? "Desconectada / sin conexión" : "Lista"; } }
    }

    /// <summary>
    /// Detecta las impresoras instaladas en Windows y decide cuál conviene para tickets.
    /// No usa puertos: todo va por la cola de impresión de Windows, así que si Windows
    /// imprime la página de prueba, VentaFácil también puede imprimir.
    /// </summary>
    public static class Detector
    {
        static readonly string[] PistasTicket =
        {
            "POS", "TICKET", "RECEIPT", "RECIBO", "THERMAL", "TERMIC", "TÉRMIC", "TM-", "TM ", "TSP", "XP-", "XPRINTER",
            "3NSTAR", "RPT", "EC-", "EC LINE", "ECLINE", "BIXOLON", "SRP-", "STAR ", "CITIZEN", "CT-S", "SEWOO", "LK-T",
            "ZJ-", "58MM", "80MM", "58 MM", "80 MM", "POS58", "POS80", "POS-58", "POS-80", "RONGTA", "HPRT", "MUNBYN",
            "GPRINTER", "GP-", "EPSON TM", "STMICRO", "NEXUS", "QPOS", "PRP-", "SPRT", "HOIN", "ZKTECO", "TEXT ONLY"
        };

        static readonly string[] PistasVirtual = { "PDF", "XPS", "FAX", "ONENOTE", "SEND TO", "ENVIAR A", "SNAGIT", "CUTEWRITER", "DOCUMENT WRITER" };

        public static List<InfoImpresora> Listar()
        {
            var lista = new List<InfoImpresora>();
            string predeterminada = Impresora.Predeterminada();
            var wmi = DatosWmi();

            foreach (string nombre in PrinterSettings.InstalledPrinters)
            {
                var i = new InfoImpresora { Nombre = nombre, Predeterminada = string.Equals(nombre, predeterminada, StringComparison.OrdinalIgnoreCase) };
                InfoImpresora w;
                if (wmi.TryGetValue(nombre, out w))
                {
                    i.Puerto = w.Puerto; i.Controlador = w.Controlador; i.Desconectada = w.Desconectada;
                    if (w.Predeterminada) i.Predeterminada = true;
                }
                Clasificar(i);
                lista.Add(i);
            }
            // Primero las de tickets, luego la predeterminada, luego el resto; las virtuales al final
            return lista.OrderBy(x => x.EsVirtual).ThenByDescending(x => x.EsTicket).ThenByDescending(x => x.Predeterminada).ThenBy(x => x.Nombre).ToList();
        }

        /// <summary>Puerto, controlador y estado desde WMI (si falla, seguimos sin esos datos).</summary>
        static Dictionary<string, InfoImpresora> DatosWmi()
        {
            var d = new Dictionary<string, InfoImpresora>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var buscador = new ManagementObjectSearcher("SELECT Name, PortName, DriverName, WorkOffline, Default, PrinterStatus FROM Win32_Printer"))
                foreach (ManagementObject o in buscador.Get())
                {
                    string nombre = Convert.ToString(o["Name"]);
                    if (string.IsNullOrEmpty(nombre)) continue;
                    d[nombre] = new InfoImpresora
                    {
                        Nombre = nombre,
                        Puerto = Convert.ToString(o["PortName"]),
                        Controlador = Convert.ToString(o["DriverName"]),
                        Desconectada = o["WorkOffline"] is bool && (bool)o["WorkOffline"],
                        Predeterminada = o["Default"] is bool && (bool)o["Default"]
                    };
                }
            }
            catch (Exception ex) { Log.Error(ex, "WMI impresoras"); }
            return d;
        }

        static void Clasificar(InfoImpresora i)
        {
            string texto = (i.Nombre + " " + i.Controlador).ToUpperInvariant();
            string puerto = (i.Puerto ?? "").ToUpperInvariant();

            i.SoloTexto = texto.Contains("TEXT ONLY") || texto.Contains("SOLO TEXTO");
            i.EsVirtual = PistasVirtual.Any(p => texto.Contains(p)) || puerto.StartsWith("PORTPROMPT") || puerto.StartsWith("XPS") || puerto.StartsWith("NUL") || puerto.StartsWith("SHRFAX");

            if (i.EsVirtual) i.Conexion = "Virtual";
            else if (puerto.StartsWith("USB")) i.Conexion = "USB";
            else if (puerto.StartsWith("COM")) i.Conexion = "Serial";
            else if (puerto.StartsWith("LPT")) i.Conexion = "Paralelo";
            else if (puerto.StartsWith("FILE")) i.Conexion = "Archivo";
            else if (i.Nombre.StartsWith("\\\\") || puerto.StartsWith("IP_") || puerto.StartsWith("WSD") || puerto.StartsWith("TCP") || puerto.Contains(".")) i.Conexion = "Red";
            else i.Conexion = puerto == "" ? "" : "Otro";

            if (i.EsVirtual) return;
            i.EsTicket = PistasTicket.Any(p => texto.Contains(p));
            if (texto.Contains("58")) i.AnchoMm = 58;
            else if (texto.Contains("80") || texto.Contains("TM-T") || texto.Contains("TSP")) i.AnchoMm = 80;

            // Los tamaños de papel del controlador delatan a una térmica (todos angostos)
            if (i.Conexion != "Red")
            {
                try
                {
                    var ps = new PrinterSettings { PrinterName = i.Nombre };
                    var anchos = new List<int>();
                    foreach (PaperSize p in ps.PaperSizes) if (p.Width > 0) anchos.Add(p.Width);
                    if (anchos.Count > 0 && anchos.Max() <= 330)
                    {
                        i.EsTicket = true;
                        if (i.AnchoMm == 0) i.AnchoMm = anchos.Max() >= 260 ? 80 : 58;
                    }
                }
                catch { /* algunos controladores no responden: se ignora */ }
            }
            if (i.EsTicket && i.AnchoMm == 0) i.AnchoMm = 80;
        }

        /// <summary>La mejor opción para tickets: térmica conectada → predeterminada → cualquiera real.</summary>
        public static InfoImpresora Recomendar(List<InfoImpresora> lista)
        {
            return lista.FirstOrDefault(i => i.EsTicket && !i.Desconectada && !i.EsVirtual)
                ?? lista.FirstOrDefault(i => i.EsTicket && !i.EsVirtual)
                ?? lista.FirstOrDefault(i => i.Predeterminada && !i.EsVirtual)
                ?? lista.FirstOrDefault(i => !i.EsVirtual);
        }

        /// <summary>Ajusta modo, ancho y letras según el tipo de impresora.</summary>
        public static void Aplicar(InfoImpresora i, Config c, bool fijarNombre)
        {
            if (fijarNombre) c.Impresora = i.Nombre;
            // "Generic / Text Only" no sabe dibujar: hay que mandarle texto directo
            c.ModoImpresion = i.SoloTexto ? "ESCPOS" : "WINDOWS";
            int ancho = i.AnchoMm > 0 ? i.AnchoMm : (i.EsTicket ? 80 : 80);
            c.AnchoTicket = ancho <= 58 ? 58 : 80;
            c.Columnas = c.AnchoTicket == 58 ? 32 : 42;
            c.TamanoFuente = c.AnchoTicket == 58 ? 7f : 8f;
            c.Cortar = i.EsTicket;
            if (!i.EsTicket) c.CajonModo = c.CajonModo == "IMPRESORA" ? "IMPRESORA" : c.CajonModo;
        }

        /// <summary>
        /// Nombre real a usar al imprimir. "" = automático (predeterminada de Windows).
        /// Si la impresora elegida ya no existe (p. ej. Windows la reinstaló como "… (Copia 1)"),
        /// busca una parecida y si no, usa la predeterminada. Nunca deja de imprimir por el nombre.
        /// </summary>
        public static string Resolver(string configurada, out string aviso)
        {
            aviso = null;
            var instaladas = Impresora.Instaladas();
            if (string.IsNullOrEmpty(configurada)) return Impresora.Predeterminada();
            var exacta = instaladas.FirstOrDefault(n => string.Equals(n, configurada, StringComparison.OrdinalIgnoreCase));
            if (exacta != null) return exacta;

            string baseNombre = QuitarCopia(configurada);
            var parecida = instaladas.FirstOrDefault(n => string.Equals(QuitarCopia(n), baseNombre, StringComparison.OrdinalIgnoreCase))
                        ?? instaladas.FirstOrDefault(n => n.IndexOf(baseNombre, StringComparison.OrdinalIgnoreCase) >= 0);
            string pred = Impresora.Predeterminada();
            string usada = parecida ?? pred;
            aviso = "La impresora \"" + configurada + "\" ya no está instalada; se usó \"" + usada + "\".";
            return usada;
        }

        static string QuitarCopia(string n)
        {
            int i = n.IndexOf(" (Cop", StringComparison.OrdinalIgnoreCase);
            return (i > 0 ? n.Substring(0, i) : n).Trim();
        }

        /// <summary>¿Windows la marca sin conexión? (para dar un aviso claro en vez de "no pasa nada").</summary>
        public static bool EstaDesconectada(string nombre)
        {
            try
            {
                using (var b = new ManagementObjectSearcher("SELECT Name, WorkOffline FROM Win32_Printer"))
                foreach (ManagementObject o in b.Get())
                    if (string.Equals(Convert.ToString(o["Name"]), nombre, StringComparison.OrdinalIgnoreCase))
                        return o["WorkOffline"] is bool && (bool)o["WorkOffline"];
            }
            catch { }
            return false;
        }
    }
}
