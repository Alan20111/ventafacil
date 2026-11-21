using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VentaFacil.Core
{
    /// <summary>
    /// Lee la lista de costos estilo semillera (archivo "COSTOS SEMILLERA.xlsx"):
    ///   NO | STOCK | PRODUCTO | BTO | CANT | COSTO | MAYOREO | CANT | MED. MAY. | CANT | MENUDEO
    /// Los renglones con texto solo en la columna A son el proveedor o grupo de los productos que siguen.
    /// </summary>
    public static class ListaCostos
    {
        // ======================================================================
        //  Lectura (sin dependencias de pantalla, para poder probarla)
        // ======================================================================
        public class Renglon
        {
            public string Hoja = "", Grupo = "", Producto = "";
            public decimal? Stock, Bulto, Trae, Costo, Mayoreo, MayoreoDesde, Medio, MedioDesde, Menudeo;
            public bool PorPieza;
            public int Orden;
        }

        public static List<Renglon> Leer(string ruta, out List<string> avisos)
        {
            avisos = new List<string>();
            var salida = new List<Renglon>();
            int orden = 0;
            foreach (var hoja in Xlsx.Leer(ruta))
            {
                // Encabezado: el renglón cuya columna dice "PRODUCTO"
                int filaEnc = -1, colProd = -1;
                for (int r = 0; r < Math.Min(10, hoja.Filas.Count) && filaEnc < 0; r++)
                    for (int c = 0; c < hoja.Filas[r].Count; c++)
                        if (Texto(hoja.Filas[r][c]).ToUpperInvariant().StartsWith("PRODUCTO")) { filaEnc = r; colProd = c; break; }
                if (filaEnc < 0) { if (hoja.Filas.Count > 0) avisos.Add("Hoja \"" + hoja.Nombre + "\": no tiene columna PRODUCTO, se omitió."); continue; }

                string grupo = "";
                // Lo que esté escrito arriba del encabezado (p. ej. "JUANJO" o "CHILES SECOS") es el primer grupo
                for (int r = 0; r < filaEnc; r++) { var t = Texto(Celda(hoja, r, 0)).Trim(); if (t != "") grupo = t; }

                for (int r = filaEnc + 1; r < hoja.Filas.Count; r++)
                {
                    string a = Texto(Celda(hoja, r, 0)).Trim();
                    string prod = Texto(Celda(hoja, r, colProd)).Trim();
                    if (prod == "")
                    {
                        // ¿Renglón de grupo / proveedor? (texto en A, no número)
                        if (a != "" && Numero(Celda(hoja, r, 0)) == null) grupo = a;
                        continue;
                    }
                    if (prod.ToUpperInvariant().StartsWith("PRODUCTO")) continue;   // encabezado repetido

                    int b = colProd; // columnas relativas a PRODUCTO
                    var x = new Renglon
                    {
                        Hoja = hoja.Nombre.Trim().TrimEnd('.'),
                        Grupo = grupo,
                        Producto = Regex.Replace(prod, @"\s+", " ").ToUpperInvariant(),
                        Stock = Numero(Celda(hoja, r, b - 1)),
                        Bulto = Numero(Celda(hoja, r, b + 1)),
                        Trae = Numero(Celda(hoja, r, b + 2)),
                        Costo = Numero(Celda(hoja, r, b + 3)),
                        Mayoreo = Numero(Celda(hoja, r, b + 4)),
                        Medio = Numero(Celda(hoja, r, b + 6)),
                        Menudeo = Numero(Celda(hoja, r, b + 8)),
                        Orden = ++orden
                    };
                    bool pz1, pz2;
                    x.MayoreoDesde = Cantidad(Celda(hoja, r, b + 5), out pz1);
                    x.MedioDesde = Cantidad(Celda(hoja, r, b + 7), out pz2);
                    x.PorPieza = pz1 || pz2 || (!TextoKg(Celda(hoja, r, b + 5)) && !TextoKg(Celda(hoja, r, b + 7)) && PareceDePieza(x.Producto, grupo));
                    if (!x.Costo.HasValue && x.Bulto.HasValue && x.Trae.HasValue && x.Trae > 0) x.Costo = x.Bulto / x.Trae;
                    if (x.Costo.HasValue) x.Costo = Math.Round(x.Costo.Value, 4);
                    if (x.Bulto.HasValue) x.Bulto = Math.Round(x.Bulto.Value, 2);
                    if (x.Trae.HasValue) x.Trae = Math.Round(x.Trae.Value, 3);
                    if (x.Stock.HasValue) x.Stock = Math.Round(x.Stock.Value, 3);
                    if (x.Mayoreo.HasValue) x.Mayoreo = Fmt.R2(x.Mayoreo.Value);
                    if (x.Medio.HasValue) x.Medio = Fmt.R2(x.Medio.Value);
                    if (x.Menudeo.HasValue) x.Menudeo = Fmt.R2(x.Menudeo.Value);
                    salida.Add(x);
                }
            }
            return salida;
        }

        static readonly string[] GruposDePieza = { "DESECHABLE", "SOBRE", "MAYORISTA", "PASTAS PARA SOPA", "ZUKOS", "SALSA", "TOSTADA", "CHURRO RIQUISIMO" };
        static readonly string[] PalabrasDePieza = { "PAQ", "PZ", "PIEZA", "CUBO", "SOBRE", "VASO", "PLATO", "CUCHARA", "TENEDOR", "CHAROLA", "BOLSA", "CAJA", "FRASCO", "LATA", "BOTELLA", "SERVILLETA", "POPOTE", "CONTENEDOR", "TAPA", "DOMO" };

        static bool TextoKg(object v) { return Texto(v).ToUpperInvariant().Contains("KG"); }

        /// <summary>Productos empacados (tienen gramos/ml en el nombre, o son desechables, sobres…) se venden por pieza.</summary>
        static bool PareceDePieza(string producto, string grupo)
        {
            string g = (grupo ?? "").ToUpperInvariant();
            foreach (var x in GruposDePieza) if (g.Contains(x)) return true;
            string p = " " + producto.ToUpperInvariant() + " ";
            if (Regex.IsMatch(p, @"\d+\s*(G|GR|GRS|ML|LT|LTS|L|OZ)\b") || Regex.IsMatch(p, @"#\d+")) return true;
            foreach (var x in PalabrasDePieza) if (Regex.IsMatch(p, @"\b" + x)) return true;
            return false;
        }

        static object Celda(Xlsx.Hoja h, int r, int c)
        {
            if (r < 0 || c < 0 || r >= h.Filas.Count || c >= h.Filas[r].Count) return null;
            return h.Filas[r][c];
        }

        static string Texto(object v)
        {
            if (v == null) return "";
            if (v is double) return ((double)v).ToString(CultureInfo.InvariantCulture);
            return Convert.ToString(v);
        }

        static decimal? Numero(object v)
        {
            if (v is double)
            {
                double d = (double)v;
                if (double.IsNaN(d) || double.IsInfinity(d) || Math.Abs(d) > 1e12) return null;
                return (decimal)d;
            }
            decimal x;
            if (v is string && Fmt.TryDec(((string)v).Replace("$", ""), out x)) return x;
            return null;
        }

        /// <summary>"3KG" → 3, "6PZ" → 6 (pieza), 25 → 25, "1/2KG" → 0.5</summary>
        static decimal? Cantidad(object v, out bool pieza)
        {
            pieza = false;
            var n = Numero(v);
            if (n.HasValue) return n.Value > 0 ? n : null;
            string s = Texto(v).ToUpperInvariant().Replace(" ", "");
            if (s == "") return null;
            pieza = s.Contains("PZ") || s.Contains("PIEZA");
            var m = Regex.Match(s, @"(\d+)/(\d+)");
            if (m.Success && decimal.Parse(m.Groups[2].Value) > 0) return decimal.Parse(m.Groups[1].Value) / decimal.Parse(m.Groups[2].Value);
            m = Regex.Match(s, @"\d+(\.\d+)?");
            if (!m.Success) return null;
            decimal q = decimal.Parse(m.Value, CultureInfo.InvariantCulture);
            if (s.Contains("GR") || (s.EndsWith("G") && !s.EndsWith("KG"))) q = q / 1000m;
            return q > 0 ? q : (decimal?)null;
        }

    }
}
