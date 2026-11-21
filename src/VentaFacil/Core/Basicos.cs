using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VentaFacil.Data;

namespace VentaFacil.Core
{
    public static class Seguridad
    {
        const string Sal = "VentaFacil#2026";
        public static string Hash(string clave)
        {
            using (var sha = SHA256.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(Sal + (clave ?? "")));
                var sb = new StringBuilder();
                foreach (var x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }
    }

    /// <summary>Formatos de dinero y cantidades. Siempre punto decimal, estilo México.</summary>
    public static class Fmt
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public static string Dinero(decimal v) { return (v < 0 ? "-$" : "$") + Math.Abs(v).ToString("#,##0.00", Inv); }
        public static string Dec(decimal v) { return v.ToString("0.###", Inv); }
        public static string Cant(decimal v, bool granel) { return granel ? v.ToString("0.000", Inv) : v.ToString("0.###", Inv); }

        public static decimal ParseDec(string s)
        {
            decimal d;
            return TryDec(s, out d) ? d : 0m;
        }

        public static bool TryDec(string s, out decimal d)
        {
            s = (s ?? "").Trim().Replace("$", "").Replace(" ", "");
            // acepta coma decimal si no hay punto
            if (s.IndexOf(',') >= 0 && s.IndexOf('.') < 0 && s.Split(',').Length == 2 && s.Split(',')[1].Length != 3) s = s.Replace(',', '.');
            s = s.Replace(",", "");
            return decimal.TryParse(s, NumberStyles.Number, Inv, out d);
        }

        public static decimal R2(decimal v) { return Math.Round(v, 2, MidpointRounding.AwayFromZero); }
    }

    /// <summary>
    /// Configuración del negocio y de la caja. Se guarda en la tabla config.
    /// </summary>
    public class Config
    {
        public static Config Actual { get; private set; }

        public string NegocioNombre = "MI NEGOCIO";
        public string NegocioDireccion = "";
        public string NegocioTelefono = "";
        public string NegocioRfc = "";
        public string TicketEncabezado = "";
        public string TicketPie = "¡GRACIAS POR SU COMPRA!";
        public int Caja = 1;

        // Impresión
        public string Impresora = "";              // nombre de la impresora de Windows
        public string ModoImpresion = "WINDOWS";   // WINDOWS (controlador) | ESCPOS (directo RAW)
        public int AnchoTicket = 80;               // 58 u 80 mm
        public int Columnas = 42;                  // caracteres por renglón
        public string Fuente = "Consolas";
        public float TamanoFuente = 8f;
        public bool ImprimirAutomatico = true;     // imprimir ticket al cobrar
        public bool PreguntarImprimir = false;     // preguntar antes de imprimir
        public bool Cortar = true;                 // cortar papel (ESC/POS)
        public int LineasAvance = 4;
        public int Copias = 1;
        public bool ImporteConLetra = true;

        // Cajón
        public bool AbrirCajon = true;             // al cobrar en efectivo
        public string CajonModo = "IMPRESORA";     // IMPRESORA (pulso ESC/POS) | SERIAL | NINGUNO
        public string CajonPuerto = "COM1";
        public int CajonPin = 0;                   // 0 = pin 2, 1 = pin 5

        // Báscula
        public bool BasculaActiva = false;
        public string BasculaPuerto = "COM1";
        public int BasculaBaudios = 9600;
        public string BasculaComando = "P";

        // Operación
        public bool PedirFondoAlAbrir = true;
        public bool DeclararEfectivoEnCorte = true;
        public bool PermitirVentaSinExistencia = true;
        public bool CajeroPuedeCancelar = false;   // cancelar tickets / descuentos sin admin
        public bool AvisarNoEncontrado = true;
        public string Banner = "";

        public static void Cargar()
        {
            var c = new Config();
            var d = ConfigRepo.Todo();
            foreach (var f in typeof(Config).GetFields())
            {
                if (f.IsStatic) continue;
                string v;
                if (!d.TryGetValue(f.Name, out v)) continue;
                try
                {
                    if (f.FieldType == typeof(string)) f.SetValue(c, v);
                    else if (f.FieldType == typeof(int)) f.SetValue(c, int.Parse(v, CultureInfo.InvariantCulture));
                    else if (f.FieldType == typeof(float)) f.SetValue(c, float.Parse(v, CultureInfo.InvariantCulture));
                    else if (f.FieldType == typeof(bool)) f.SetValue(c, v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
                }
                catch { /* valor inválido: se queda el predeterminado */ }
            }
            Actual = c;
        }

        public void Guardar()
        {
            var d = new Dictionary<string, string>();
            foreach (var f in typeof(Config).GetFields())
            {
                if (f.IsStatic) continue;
                object v = f.GetValue(this);
                string s;
                if (v is bool) s = (bool)v ? "1" : "0";
                else if (v is float) s = ((float)v).ToString(CultureInfo.InvariantCulture);
                else if (v is int) s = ((int)v).ToString(CultureInfo.InvariantCulture);
                else s = Convert.ToString(v);
                d[f.Name] = s;
            }
            ConfigRepo.Guardar(d);
            Actual = this;
        }

        public Config Copia() { return (Config)MemberwiseClone(); }
    }

    /// <summary>Quién está trabajando y en qué turno.</summary>
    public static class Sesion
    {
        public static Usuario Usuario;
        public static Turno Turno;
        public static int Caja { get { return Config.Actual.Caja; } }
    }

    public static class NumeroALetras
    {
        static readonly string[] Unidades = { "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE", "DIEZ",
            "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISEIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE", "VEINTE",
            "VEINTIUN", "VEINTIDOS", "VEINTITRES", "VEINTICUATRO", "VEINTICINCO", "VEINTISEIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE" };
        static readonly string[] Decenas = { "", "", "", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA" };
        static readonly string[] Centenas = { "", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS" };

        /// <summary>84.38 → "OCHENTA Y CUATRO PESOS 38/100 M.N."</summary>
        public static string Pesos(decimal importe)
        {
            importe = Fmt.R2(Math.Abs(importe));
            long entero = (long)Math.Floor(importe);
            int centavos = (int)Math.Round((importe - entero) * 100);
            string letras = entero == 0 ? "CERO" : Convertir(entero);
            return letras + (entero == 1 ? " PESO " : " PESOS ") + centavos.ToString("00") + "/100 M.N.";
        }

        static string Convertir(long n)
        {
            if (n == 0) return "";
            if (n < 30) return Unidades[n];
            if (n < 100) return Decenas[n / 10] + (n % 10 > 0 ? " Y " + Unidades[n % 10] : "");
            if (n == 100) return "CIEN";
            if (n < 1000) return Centenas[n / 100] + (n % 100 > 0 ? " " + Convertir(n % 100) : "");
            if (n < 1000000)
            {
                long miles = n / 1000, resto = n % 1000;
                return (miles == 1 ? "MIL" : Convertir(miles) + " MIL") + (resto > 0 ? " " + Convertir(resto) : "");
            }
            if (n < 1000000000000)
            {
                long mill = n / 1000000, resto = n % 1000000;
                return (mill == 1 ? "UN MILLON" : Convertir(mill) + " MILLONES") + (resto > 0 ? " " + Convertir(resto) : "");
            }
            return n.ToString();
        }
    }
}
