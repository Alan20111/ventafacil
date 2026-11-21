using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VentaFacil.Core;
using VentaFacil.Data;

namespace VentaFacil.Forms.Admin
{
    /// <summary>
    /// Importa productos desde CSV (Excel "Guardar como CSV").
    /// Columnas: codigo, descripcion, departamento, precio, costo, existencia, minimo, granel, codigo2.
    /// Si el archivo no trae encabezado se asume ese orden.
    /// </summary>
    public static class ImportarCsv
    {
        public class Resultado { public int Nuevos; public int Actualizados; public List<string> Errores = new List<string>(); }

        public static Resultado Importar(string archivo, long usuarioId)
        {
            var texto = LeerTexto(archivo);
            var renglones = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Where(r => r.Trim() != "").ToList();
            var res = new Resultado();
            if (renglones.Count == 0) return res;

            char sep = Separador(renglones[0]);
            var columnas = new Dictionary<string, int>();
            string[] nombres = { "codigo", "descripcion", "departamento", "precio", "costo", "existencia", "minimo", "granel", "codigo2" };
            var primera = Partir(renglones[0], sep).Select(Normalizar).ToList();
            int inicio = 0;
            if (primera.Contains("codigo") || primera.Contains("descripcion"))
            {
                for (int i = 0; i < primera.Count; i++)
                {
                    string n = primera[i];
                    if (n == "clave" || n == "codigodebarras" || n == "codbarras") n = "codigo";
                    if (n == "nombre" || n == "producto") n = "descripcion";
                    if (n == "preciodeventa" || n == "precioventa" || n == "venta") n = "precio";
                    if (n == "preciocosto" || n == "preciodecosto") n = "costo";
                    if (n == "stock" || n == "inventario" || n == "cantidad") n = "existencia";
                    if (n == "depto" || n == "categoria") n = "departamento";
                    if (n == "codigoalterno") n = "codigo2";
                    if (Array.IndexOf(nombres, n) >= 0 && !columnas.ContainsKey(n)) columnas[n] = i;
                }
                inicio = 1;
            }
            else
                for (int i = 0; i < nombres.Length; i++) columnas[nombres[i]] = i;

            if (!columnas.ContainsKey("codigo") || !columnas.ContainsKey("descripcion"))
                throw new InvalidOperationException("El archivo debe tener al menos las columnas \"codigo\" y \"descripcion\".");

            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                var deptos = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                for (int r = inicio; r < renglones.Count; r++)
                {
                    var c = Partir(renglones[r], sep);
                    Func<string, string> val = n => { int i; return columnas.TryGetValue(n, out i) && i < c.Count ? c[i].Trim() : ""; };
                    try
                    {
                        string codigo = val("codigo");
                        string desc = val("descripcion");
                        if (codigo == "" && desc == "") continue;
                        if (codigo == "") throw new FormatException("sin código");
                        if (desc == "") throw new FormatException("sin descripción");

                        var existente = Db.Lista(cn, tx, "SELECT p.*, '' AS departamento FROM productos p WHERE p.codigo=@p0", Producto.De, codigo).FirstOrDefault();
                        var p = existente ?? new Producto { Codigo = codigo };
                        p.Descripcion = desc;
                        p.Activo = true;
                        string dep = val("departamento");
                        if (dep != "" || existente == null)
                        {
                            long idDep;
                            if (!deptos.TryGetValue(dep, out idDep)) { idDep = DepartamentoRepo.ObtenerOCrear(cn, tx, dep); deptos[dep] = idDep; }
                            p.DepartamentoId = idDep;
                        }
                        if (val("precio") != "") p.Precio = Num(val("precio"), "precio");
                        if (val("costo") != "") p.Costo = Num(val("costo"), "costo");
                        if (val("minimo") != "") p.Minimo = Num(val("minimo"), "mínimo");
                        if (val("granel") != "") { var g = Normalizar(val("granel")); p.Granel = g == "1" || g == "si" || g == "s" || g == "kg" || g == "true" || g == "granel"; }
                        if (columnas.ContainsKey("codigo2")) p.Codigo2 = val("codigo2");
                        bool traeExistencia = val("existencia") != "";
                        decimal existencia = traeExistencia ? Num(val("existencia"), "existencia") : 0;

                        if (existente == null)
                        {
                            p.Existencia = existencia;
                            ProductoRepo.Guardar(cn, tx, p, usuarioId);
                            res.Nuevos++;
                        }
                        else
                        {
                            ProductoRepo.Guardar(cn, tx, p, usuarioId);
                            if (traeExistencia && existencia != p.Existencia)
                                ProductoRepo.MoverExistencia(cn, tx, p.Id, existencia - p.Existencia, "AJUSTE", "Importación CSV", usuarioId);
                            res.Actualizados++;
                        }
                    }
                    catch (Exception ex)
                    {
                        res.Errores.Add("Renglón " + (r + 1) + ": " + ex.Message);
                    }
                }
                tx.Commit();
            }
            return res;
        }

        static decimal Num(string s, string campo)
        {
            decimal d;
            if (!Fmt.TryDec(s, out d)) throw new FormatException(campo + " inválido (\"" + s + "\")");
            return d;
        }

        static string LeerTexto(string archivo)
        {
            var bytes = File.ReadAllBytes(archivo);
            try
            {
                return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿');
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(1252).GetString(bytes);
            }
        }

        static char Separador(string linea)
        {
            int comas = linea.Count(ch => ch == ','), pyc = linea.Count(ch => ch == ';'), tabs = linea.Count(ch => ch == '\t');
            if (tabs > comas && tabs > pyc) return '\t';
            return pyc > comas ? ';' : ',';
        }

        static string Normalizar(string s)
        {
            s = (s ?? "").Trim().ToLowerInvariant();
            var sb = new StringBuilder();
            foreach (var ch in s.Normalize(NormalizationForm.FormD))
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        /// <summary>Parte un renglón CSV respetando comillas.</summary>
        static List<string> Partir(string linea, char sep)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            bool comillas = false;
            for (int i = 0; i < linea.Length; i++)
            {
                char ch = linea[i];
                if (comillas)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < linea.Length && linea[i + 1] == '"') { sb.Append('"'); i++; }
                        else comillas = false;
                    }
                    else sb.Append(ch);
                }
                else if (ch == '"') comillas = true;
                else if (ch == sep) { res.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(ch);
            }
            res.Add(sb.ToString());
            return res;
        }
    }
}
