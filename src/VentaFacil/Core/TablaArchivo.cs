using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>
    /// Lee un archivo de Excel (.xlsx) o de texto (.csv) como tabla con encabezados, para usarlo como origen
    /// de la importación (por ejemplo, un reporte exportado de Zafirah). Sin pantallas: se prueba aparte.
    /// </summary>
    public static class TablaArchivo
    {
        public static DataTable Leer(string ruta)
        {
            string ext = (Path.GetExtension(ruta) ?? "").ToLowerInvariant();
            if (ext == ".xlsx")
            {
                foreach (var hoja in Xlsx.Leer(ruta))
                {
                    var t = DeHoja(hoja);
                    if (t != null && t.Rows.Count > 0) return t;
                }
                throw new InvalidOperationException("No encontré una tabla con encabezados y datos en el Excel.");
            }
            if (ext == ".csv" || ext == ".txt") return DeCsv(File.ReadAllText(ruta, DetectarCodificacion(ruta)));
            throw new InvalidOperationException("Use un archivo de Excel (.xlsx) o de texto (.csv).");
        }

        static Encoding DetectarCodificacion(string ruta)
        {
            // UTF-8 si es válido; si no, Windows-1252 (lo normal en programas viejos)
            byte[] b = File.ReadAllBytes(ruta);
            try { new UTF8Encoding(false, true).GetString(b); return new UTF8Encoding(false); }
            catch (ArgumentException) { return Encoding.GetEncoding(1252); }
        }

        static bool EsTexto(object o) { return o is string && !string.IsNullOrWhiteSpace((string)o); }

        /// <summary>El encabezado es el primer renglón (entre los 15 primeros) con 2 o más textos y casi sin números.</summary>
        public static DataTable DeHoja(Xlsx.Hoja hoja)
        {
            int enc = -1;
            for (int f = 0; f < hoja.Filas.Count && f < 15; f++)
            {
                var fila = hoja.Filas[f];
                int textos = fila.Count(EsTexto), llenas = fila.Count(c => c != null && !(c is string && string.IsNullOrWhiteSpace((string)c)));
                if (textos >= 2 && textos >= llenas * 0.8) { enc = f; break; }
            }
            if (enc < 0) return null;

            var cabecera = hoja.Filas[enc];
            int ancho = cabecera.Count;
            var t = new DataTable();
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < ancho; c++)
            {
                string nombre = cabecera[c] is string ? ((string)cabecera[c]).Trim() : Convert.ToString(cabecera[c], CultureInfo.InvariantCulture);
                if (string.IsNullOrWhiteSpace(nombre)) nombre = "Columna " + (c + 1);
                string unico = nombre; int k = 2;
                while (!usados.Add(unico)) unico = nombre + " (" + (k++) + ")";
                t.Columns.Add(unico, typeof(object));
            }
            for (int f = enc + 1; f < hoja.Filas.Count; f++)
            {
                var fila = hoja.Filas[f];
                if (fila.All(x => x == null || (x is string && string.IsNullOrWhiteSpace((string)x)))) continue;
                var valores = new object[ancho];
                for (int c = 0; c < ancho; c++) valores[c] = c < fila.Count ? fila[c] : null;
                t.Rows.Add(valores);
            }
            return t;
        }

        public static DataTable DeCsv(string texto)
        {
            if (texto.Length > 0 && texto[0] == '﻿') texto = texto.Substring(1);
            var filas = new List<List<string>>();
            string primera = texto.Split('\n')[0];
            char sep = new[] { ';', ',', '\t' }.OrderByDescending(s => primera.Count(ch => ch == s)).First();
            var fila = new List<string>(); var campo = new StringBuilder(); bool comillas = false;
            for (int i = 0; i < texto.Length; i++)
            {
                char ch = texto[i];
                if (comillas)
                {
                    if (ch == '"') { if (i + 1 < texto.Length && texto[i + 1] == '"') { campo.Append('"'); i++; } else comillas = false; }
                    else campo.Append(ch);
                }
                else if (ch == '"') comillas = true;
                else if (ch == sep) { fila.Add(campo.ToString()); campo.Clear(); }
                else if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                    fila.Add(campo.ToString()); campo.Clear();
                    if (fila.Any(x => x.Trim() != "")) filas.Add(fila);
                    fila = new List<string>();
                }
                else campo.Append(ch);
            }
            if (campo.Length > 0 || fila.Count > 0) { fila.Add(campo.ToString()); if (fila.Any(x => x.Trim() != "")) filas.Add(fila); }
            if (filas.Count < 2) throw new InvalidOperationException("El archivo no tiene encabezados y datos.");

            var hoja = new Xlsx.Hoja { Nombre = "csv" };
            foreach (var f in filas) hoja.Filas.Add(f.Select(x => (object)x).ToList());
            // Un CSV trae todo como texto: así se conservan ceros a la izquierda en los códigos
            var t = DeHoja(hoja);
            if (t == null) throw new InvalidOperationException("No encontré los encabezados en el archivo.");
            return t;
        }
    }
}
