using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>Un renglón de la hoja de conteo.</summary>
    public class FilaConteo
    {
        public string Codigo = "";
        public string Producto = "";
        public string Grupo = "";
        public decimal EnSistema;
    }

    /// <summary>Lo que se leyó de una hoja de conteo ya llena.</summary>
    public class ConteoLeido
    {
        public bool HalloColumnas;
        public List<KeyValuePair<string, decimal>> Contados = new List<KeyValuePair<string, decimal>>();
        public List<string> Invalidos = new List<string>();
        public int SinContar;
        public int Repetidos;
    }

    /// <summary>
    /// Formato de la hoja de conteo y lectura de lo contado. No usa pantalla ni base de datos:
    /// se prueba aparte (herramientas/pruebas-conteo) con el mismo código que usa el programa.
    /// </summary>
    public static class ConteoExcel
    {
        public static Xlsx.HojaSalida ArmarHoja(string titulo, IEnumerable<FilaConteo> filas, Func<FilaConteo, decimal?> contado = null)
        {
            var h = new Xlsx.HojaSalida { Nombre = "CONTEO", FilasCongeladas = 4 };
            h.AnchosColumna.AddRange(new double[] { 18, 42, 22, 13, 13 });
            h.Filas.Add(new List<Xlsx.Celda> { C(titulo, Xlsx.Estilo.Titulo) });
            h.Filas.Add(new List<Xlsx.Celda> { C("Escriba lo que CUENTE en la columna CONTADO. Deje en blanco lo que no cuente. No cambie los códigos.") });
            h.Filas.Add(new List<Xlsx.Celda>());
            h.Filas.Add(new List<Xlsx.Celda> { C("CÓDIGO", Xlsx.Estilo.Encabezado), C("PRODUCTO", Xlsx.Estilo.Encabezado), C("GRUPO", Xlsx.Estilo.Encabezado), C("EN SISTEMA", Xlsx.Estilo.Encabezado), C("CONTADO", Xlsx.Estilo.Encabezado) });
            foreach (var f in filas)
            {
                decimal? c = contado == null ? null : contado(f);
                h.Filas.Add(new List<Xlsx.Celda> { C(f.Codigo), C(f.Producto), C(f.Grupo), C(f.EnSistema, Xlsx.Estilo.Decimal3), C(c.HasValue ? (object)c.Value : null, Xlsx.Estilo.Decimal3) });
            }
            return h;
        }

        static Xlsx.Celda C(object v, Xlsx.Estilo e = Xlsx.Estilo.Normal) { return new Xlsx.Celda { Valor = v, Estilo = e }; }

        /// <summary>Busca en cualquier hoja el encabezado (CÓDIGO y CONTADO, sin importar mayúsculas ni acentos) y lee lo contado.</summary>
        public static ConteoLeido Leer(List<Xlsx.Hoja> hojas)
        {
            var r = new ConteoLeido();
            var vistos = new HashSet<string>();
            foreach (var hoja in hojas)
            {
                int fEnc = -1, iCod = -1, iCont = -1;
                for (int f = 0; f < hoja.Filas.Count && f < 30 && fEnc < 0; f++)
                {
                    var fila = hoja.Filas[f];
                    int a = -1, b = -1;
                    for (int c = 0; c < fila.Count; c++)
                    {
                        string n = Norm(fila[c]);
                        if (n == "CODIGO") a = c; else if (n == "CONTADO") b = c;
                    }
                    if (a >= 0 && b >= 0) { fEnc = f; iCod = a; iCont = b; }
                }
                if (fEnc < 0) continue;
                r.HalloColumnas = true;
                for (int f = fEnc + 1; f < hoja.Filas.Count; f++)
                {
                    var fila = hoja.Filas[f];
                    string codigo = iCod < fila.Count ? CodigoDe(fila[iCod]) : "";
                    if (codigo == "") continue;
                    object celda = iCont < fila.Count ? fila[iCont] : null;
                    if (celda == null || Convert.ToString(celda, CultureInfo.InvariantCulture).Trim() == "") { r.SinContar++; continue; }
                    decimal valor;
                    if (!LeerCantidad(celda, out valor) || valor < 0) { r.Invalidos.Add(codigo); continue; }
                    if (!vistos.Add(codigo)) { r.Repetidos++; continue; }
                    r.Contados.Add(new KeyValuePair<string, decimal>(codigo, valor));
                }
            }
            return r;
        }

        static string Norm(object o)
        {
            string s = Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";
            var sb = new StringBuilder();
            foreach (char c in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(c)) sb.Append(char.ToUpperInvariant(c));
            return sb.ToString();
        }

        /// <summary>El código puede venir como texto o como número (Excel convierte "7501234567890" en número).</summary>
        static string CodigoDe(object o)
        {
            if (o == null) return "";
            if (o is double) return Math.Round((double)o).ToString("0", CultureInfo.InvariantCulture);
            return Convert.ToString(o, CultureInfo.InvariantCulture).Trim();
        }

        static bool LeerCantidad(object o, out decimal valor)
        {
            valor = 0m;
            if (o is double) { valor = Dinero.Leer(o); return true; }
            return Fmt.TryDec(Convert.ToString(o, CultureInfo.InvariantCulture), out valor);
        }
    }
}
