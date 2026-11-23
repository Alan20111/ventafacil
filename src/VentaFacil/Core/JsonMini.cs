using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>
    /// Lector y escritor de JSON sin dependencias (.NET Framework 4.0 no trae uno cómodo). Pensado para archivos que
    /// una persona edita a mano (los perfiles de importación), así que PERDONA lo que JSON estricto no: comentarios
    /// `// …` y `/* … */`, y una coma de más antes de `}` o `]`.
    /// Valores: objeto → Dictionary&lt;string, object&gt;; lista → List&lt;object&gt;; texto → string; número → double;
    /// verdadero/falso → bool; null → null. Los errores dicen en qué línea y columna están.
    /// </summary>
    public static class JsonMini
    {
        public static object Leer(string texto)
        {
            var l = new Lector(texto ?? "");
            l.Espacios();
            object v = l.Valor();
            l.Espacios();
            if (!l.Fin) throw l.Error("Hay texto de más después del valor");
            return v;
        }

        public static string Escribir(object valor)
        {
            var sb = new StringBuilder();
            Escribir(sb, valor, 0);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ escribir
        static void Escribir(StringBuilder sb, object v, int nivel)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { Cadena(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is double || v is float || v is decimal || v is int || v is long) { sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture)); return; }
            var dic = v as IDictionary<string, object>;
            if (dic != null)
            {
                if (dic.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                int i = 0;
                foreach (var kv in dic)
                {
                    sb.Append(' ', (nivel + 1) * 2);
                    Cadena(sb, kv.Key);
                    sb.Append(": ");
                    Escribir(sb, kv.Value, nivel + 1);
                    sb.Append(++i < dic.Count ? ",\n" : "\n");
                }
                sb.Append(' ', nivel * 2).Append('}');
                return;
            }
            var lista = v as IEnumerable;
            if (lista != null)
            {
                var items = new List<object>();
                foreach (object o in lista) items.Add(o);
                if (items.Count == 0) { sb.Append("[]"); return; }
                bool simples = true;
                foreach (object o in items) if (o is IDictionary<string, object> || (o is IEnumerable && !(o is string))) simples = false;
                if (simples)
                {   // las listas de textos cortos van en una sola línea: se lee mejor
                    sb.Append('[');
                    for (int i = 0; i < items.Count; i++) { if (i > 0) sb.Append(", "); Escribir(sb, items[i], nivel); }
                    sb.Append(']');
                    return;
                }
                sb.Append("[\n");
                for (int i = 0; i < items.Count; i++)
                {
                    sb.Append(' ', (nivel + 1) * 2);
                    Escribir(sb, items[i], nivel + 1);
                    sb.Append(i + 1 < items.Count ? ",\n" : "\n");
                }
                sb.Append(' ', nivel * 2).Append(']');
                return;
            }
            throw new ArgumentException("No se puede escribir en JSON un valor de tipo " + v.GetType().Name);
        }

        static void Cadena(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ------------------------------------------------------------------ leer
        sealed class Lector
        {
            readonly string _t;
            int _i;
            public Lector(string t) { _t = t; }
            public bool Fin { get { return _i >= _t.Length; } }

            public FormatException Error(string mensaje)
            {
                int linea = 1, col = 1;
                for (int k = 0; k < _i && k < _t.Length; k++) { if (_t[k] == '\n') { linea++; col = 1; } else col++; }
                return new FormatException(mensaje + " (línea " + linea + ", columna " + col + ").");
            }

            public void Espacios()
            {
                while (!Fin)
                {
                    char c = _t[_i];
                    if (char.IsWhiteSpace(c) || c == '﻿') _i++;
                    else if (c == '/' && _i + 1 < _t.Length && _t[_i + 1] == '/') { while (!Fin && _t[_i] != '\n') _i++; }
                    else if (c == '/' && _i + 1 < _t.Length && _t[_i + 1] == '*')
                    {
                        int fin = _t.IndexOf("*/", _i + 2, StringComparison.Ordinal);
                        if (fin < 0) throw Error("Falta cerrar un comentario /* … */");
                        _i = fin + 2;
                    }
                    else break;
                }
            }

            public object Valor()
            {
                if (Fin) throw Error("Se esperaba un valor y se acabó el texto");
                char c = _t[_i];
                if (c == '{') return Objeto();
                if (c == '[') return Lista();
                if (c == '"') return Texto();
                if (Palabra("true")) return true;
                if (Palabra("false")) return false;
                if (Palabra("null")) return null;
                if (c == '-' || char.IsDigit(c)) return Numero();
                throw Error("No entiendo «" + c + "»");
            }

            bool Palabra(string p)
            {
                if (string.CompareOrdinal(_t, _i, p, 0, p.Length) != 0) return false;
                _i += p.Length;
                return true;
            }

            object Numero()
            {
                int ini = _i;
                if (_t[_i] == '-') _i++;
                while (!Fin && (char.IsDigit(_t[_i]) || _t[_i] == '.' || _t[_i] == 'e' || _t[_i] == 'E' || _t[_i] == '+' || _t[_i] == '-')) _i++;
                double d;
                if (!double.TryParse(_t.Substring(ini, _i - ini), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) { _i = ini; throw Error("Número inválido"); }
                return d;
            }

            string Texto()
            {
                _i++; // comilla de apertura
                var sb = new StringBuilder();
                while (true)
                {
                    if (Fin) throw Error("Falta cerrar las comillas de un texto");
                    char c = _t[_i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (Fin) throw Error("Texto cortado después de una barra \\");
                    char e = _t[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            int cod;
                            if (_i + 4 > _t.Length || !int.TryParse(_t.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cod)) throw Error("Secuencia \\u inválida");
                            sb.Append((char)cod); _i += 4; break;
                        default: throw Error("Secuencia de escape desconocida: \\" + e);
                    }
                }
            }

            List<object> Lista()
            {
                _i++;
                var l = new List<object>();
                while (true)
                {
                    Espacios();
                    if (Fin) throw Error("Falta cerrar una lista con ]");
                    if (_t[_i] == ']') { _i++; return l; }
                    l.Add(Valor());
                    Espacios();
                    if (!Fin && _t[_i] == ',') { _i++; continue; }
                    if (!Fin && _t[_i] == ']') { _i++; return l; }
                    throw Error("Se esperaba una coma o ]");
                }
            }

            Dictionary<string, object> Objeto()
            {
                _i++;
                var d = new Dictionary<string, object>();
                while (true)
                {
                    Espacios();
                    if (Fin) throw Error("Falta cerrar un objeto con }");
                    if (_t[_i] == '}') { _i++; return d; }
                    if (_t[_i] != '"') throw Error("Se esperaba el nombre de un campo entre comillas");
                    string clave = Texto();
                    Espacios();
                    if (Fin || _t[_i] != ':') throw Error("Falta los dos puntos después de «" + clave + "»");
                    _i++;
                    Espacios();
                    d[clave] = Valor();
                    Espacios();
                    if (!Fin && _t[_i] == ',') { _i++; continue; }
                    if (!Fin && _t[_i] == '}') { _i++; return d; }
                    throw Error("Se esperaba una coma o }");
                }
            }
        }
    }
}
