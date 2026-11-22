using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Xml;

namespace VentaFacil.Core
{
    /// <summary>
    /// Lectura y escritura de archivos de Excel (.xlsx) sin librerías externas.
    /// Usa System.IO.Packaging (WindowsBase), que viene con .NET Framework 4.0.
    /// </summary>
    public static class Xlsx
    {
        const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        const string RelOfficeDoc = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
        const string RelWorksheet = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
        const string RelStyles = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
        const string RelShared = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings";
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public class Hoja { public string Nombre = ""; public List<List<object>> Filas = new List<List<object>>(); }

        // =====================================================================
        //  LECTURA
        // =====================================================================
        public static List<Hoja> Leer(string ruta)
        {
            Stream fs;
            string temporal = null;
            try { fs = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite); }
            catch (IOException)
            {
                temporal = Path.Combine(Path.GetTempPath(), "vf_" + Guid.NewGuid().ToString("N") + ".xlsx");
                File.Copy(ruta, temporal, true);
                fs = new FileStream(temporal, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            try
            {
                // Se copia a memoria: ZipPackage necesita un stream con búsqueda y así no se bloquea el archivo
                var ms = new MemoryStream();
                Copiar(fs, ms);
                fs.Dispose();
                fs = null;
                ms.Position = 0;
                using (var pkg = Package.Open(ms, FileMode.Open, FileAccess.Read))
                    return LeerPaquete(pkg);
            }
            finally
            {
                if (fs != null) fs.Dispose();
                if (temporal != null) try { File.Delete(temporal); } catch { }
            }
        }

        static void Copiar(Stream a, Stream b)
        {
            var buf = new byte[81920];
            int n;
            while ((n = a.Read(buf, 0, buf.Length)) > 0) b.Write(buf, 0, n);
        }

        static List<Hoja> LeerPaquete(Package pkg)
        {
            var relDoc = pkg.GetRelationshipsByType(RelOfficeDoc).FirstOrDefault();
            if (relDoc == null) throw new InvalidDataException("El archivo no es un libro de Excel válido.");
            Uri uriLibro = PackUriHelper.ResolvePartUri(new Uri("/", UriKind.Relative), relDoc.TargetUri);
            var libro = pkg.GetPart(uriLibro);

            // Relaciones del libro: id → parte
            var rels = new Dictionary<string, Uri>();
            List<string> compartidas = new List<string>();
            foreach (var r in libro.GetRelationships())
            {
                if (r.TargetMode != TargetMode.Internal) continue;
                Uri destino = PackUriHelper.ResolvePartUri(uriLibro, r.TargetUri);
                rels[r.Id] = destino;
                if (r.RelationshipType == RelShared && pkg.PartExists(destino))
                    compartidas = LeerCompartidas(pkg.GetPart(destino));
            }

            var hojas = new List<Hoja>();
            using (var s = libro.GetStream(FileMode.Open, FileAccess.Read))
            using (var xr = XmlReader.Create(s, new XmlReaderSettings { IgnoreWhitespace = false, DtdProcessing = DtdProcessing.Prohibit }))
            {
                while (xr.Read())
                {
                    if (xr.NodeType != XmlNodeType.Element || xr.LocalName != "sheet") continue;
                    string nombre = xr.GetAttribute("name") ?? "";
                    string id = xr.GetAttribute("id", NsRel);
                    Uri uri;
                    if (id == null || !rels.TryGetValue(id, out uri) || !pkg.PartExists(uri)) continue;
                    var h = new Hoja { Nombre = nombre };
                    LeerHoja(pkg.GetPart(uri), compartidas, h);
                    hojas.Add(h);
                }
            }
            return hojas;
        }

        static List<string> LeerCompartidas(PackagePart part)
        {
            var lista = new List<string>();
            using (var s = part.GetStream(FileMode.Open, FileAccess.Read))
            using (var xr = XmlReader.Create(s))
            {
                while (xr.Read())
                    if (xr.NodeType == XmlNodeType.Element && xr.LocalName == "si")
                        lista.Add(TextoRico(xr));
            }
            return lista;
        }

        /// <summary>Junta el texto de todos los &lt;t&gt; dentro del elemento actual (si o is), ignorando la fonética (rPh).</summary>
        static string TextoRico(XmlReader xr)
        {
            if (xr.IsEmptyElement) return "";
            var sb = new StringBuilder();
            using (var sub = xr.ReadSubtree())
            {
                sub.Read(); // el propio si / is
                sub.Read();
                while (!sub.EOF)
                {
                    if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "rPh") { sub.Skip(); continue; }
                    if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "t")
                    {
                        if (sub.IsEmptyElement) { sub.Read(); continue; }
                        sb.Append(sub.ReadElementContentAsString()); // ya avanza
                        continue;
                    }
                    sub.Read();
                }
            }
            return sb.ToString();
        }

        static void LeerHoja(PackagePart part, List<string> compartidas, Hoja h)
        {
            using (var s = part.GetStream(FileMode.Open, FileAccess.Read))
            using (var xr = XmlReader.Create(s))
            {
                int filaSiguiente = 0;   // 0-based
                while (xr.Read())
                {
                    if (xr.NodeType != XmlNodeType.Element || xr.LocalName != "row") continue;
                    int fila = filaSiguiente;
                    string rAttr = xr.GetAttribute("r");
                    int rn;
                    if (rAttr != null && int.TryParse(rAttr, NumberStyles.Integer, Inv, out rn) && rn > 0) fila = rn - 1;
                    filaSiguiente = fila + 1;
                    while (h.Filas.Count <= fila) h.Filas.Add(new List<object>());
                    var celdas = h.Filas[fila];
                    if (xr.IsEmptyElement) continue;

                    int profFila = xr.Depth;
                    int colSiguiente = 0;
                    while (xr.Read())
                    {
                        if (xr.NodeType == XmlNodeType.EndElement && xr.Depth == profFila) break;
                        if (xr.NodeType != XmlNodeType.Element || xr.LocalName != "c") continue;
                        int col = colSiguiente;
                        string refC = xr.GetAttribute("r");
                        if (refC != null) { int c = ColumnaDeRef(refC); if (c >= 0) col = c; }
                        colSiguiente = col + 1;
                        string tipo = xr.GetAttribute("t") ?? "n";
                        object valor = LeerCelda(xr, tipo, compartidas);
                        while (celdas.Count <= col) celdas.Add(null);
                        celdas[col] = valor;
                    }
                }
            }
            // quita filas vacías al final
            while (h.Filas.Count > 0 && h.Filas[h.Filas.Count - 1].All(v => v == null)) h.Filas.RemoveAt(h.Filas.Count - 1);
        }

        static object LeerCelda(XmlReader xr, string tipo, List<string> compartidas)
        {
            if (xr.IsEmptyElement) return null;
            string v = null;
            string inline = null;
            using (var sub = xr.ReadSubtree())
            {
                sub.Read(); // <c>
                sub.Read();
                while (!sub.EOF)
                {
                    if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "v")
                    {
                        if (sub.IsEmptyElement) { v = ""; sub.Read(); }
                        else v = sub.ReadElementContentAsString();
                        continue;
                    }
                    if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "is") { inline = TextoRico(sub); sub.Read(); continue; }
                    if (sub.NodeType == XmlNodeType.Element && sub.LocalName == "f") { sub.Skip(); continue; } // se usa el valor calculado <v>
                    sub.Read();
                }
            }
            switch (tipo)
            {
                case "s":
                    int i;
                    return v != null && int.TryParse(v, NumberStyles.Integer, Inv, out i) && i >= 0 && i < compartidas.Count ? compartidas[i] : null;
                case "inlineStr": return inline ?? v;
                case "str": return v;
                case "b": return v == null ? null : (v == "1" ? "VERDADERO" : "FALSO");
                case "e": return null;
                default:
                    if (string.IsNullOrEmpty(v)) return null;
                    double d;
                    if (double.TryParse(v, NumberStyles.Float, Inv, out d)) return d;
                    return v;
            }
        }

        /// <summary>"C12" → 2 (0-based). -1 si no es válida.</summary>
        static int ColumnaDeRef(string r)
        {
            int col = 0, i = 0;
            while (i < r.Length && char.IsLetter(r[i])) { col = col * 26 + (char.ToUpperInvariant(r[i]) - 'A' + 1); i++; }
            return i == 0 ? -1 : col - 1;
        }

        // =====================================================================
        //  ESCRITURA
        // =====================================================================
        public enum Estilo { Normal, Titulo, Encabezado, Grupo, Dinero, Numero, Decimal3, Negrita, DineroNegrita }
        public class Celda { public object Valor; public string Formula; public Estilo Estilo; }
        public class HojaSalida
        {
            public string Nombre = "Hoja1";
            public List<List<Celda>> Filas = new List<List<Celda>>();
            public List<double> AnchosColumna = new List<double>();
            public int FilasCongeladas;
        }

        public static void Escribir(string ruta, List<HojaSalida> hojas)
        {
            if (hojas == null || hojas.Count == 0) hojas = new List<HojaSalida> { new HojaSalida() };
            if (File.Exists(ruta)) File.Delete(ruta);
            using (var pkg = Package.Open(ruta, FileMode.Create, FileAccess.ReadWrite))
            {
                var uriLibro = new Uri("/xl/workbook.xml", UriKind.Relative);
                var libro = pkg.CreatePart(uriLibro, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml", CompressionOption.Normal);
                pkg.CreateRelationship(uriLibro, TargetMode.Internal, RelOfficeDoc, "rId1");

                var nombres = NombresHojas(hojas);
                var sbLibro = new StringBuilder();
                sbLibro.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sbLibro.Append("<workbook xmlns=\"").Append(NsMain).Append("\" xmlns:r=\"").Append(NsRel).Append("\"><bookViews><workbookView/></bookViews><sheets>");
                for (int i = 0; i < hojas.Count; i++)
                {
                    var uriHoja = new Uri("/xl/worksheets/sheet" + (i + 1) + ".xml", UriKind.Relative);
                    var parte = pkg.CreatePart(uriHoja, "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml", CompressionOption.Normal);
                    libro.CreateRelationship(new Uri("worksheets/sheet" + (i + 1) + ".xml", UriKind.Relative), TargetMode.Internal, RelWorksheet, "rId" + (i + 1));
                    EscribirTexto(parte, XmlHoja(hojas[i]));
                    sbLibro.Append("<sheet name=\"").Append(Esc(nombres[i])).Append("\" sheetId=\"").Append(i + 1).Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
                }
                sbLibro.Append("</sheets></workbook>");
                EscribirTexto(libro, sbLibro.ToString());

                var uriEstilos = new Uri("/xl/styles.xml", UriKind.Relative);
                var estilos = pkg.CreatePart(uriEstilos, "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml", CompressionOption.Normal);
                libro.CreateRelationship(new Uri("styles.xml", UriKind.Relative), TargetMode.Internal, RelStyles, "rId" + (hojas.Count + 1));
                EscribirTexto(estilos, XmlEstilos());
            }
        }

        static void EscribirTexto(PackagePart parte, string xml)
        {
            using (var s = parte.GetStream(FileMode.Create, FileAccess.Write))
            {
                var b = new UTF8Encoding(false).GetBytes(xml);
                s.Write(b, 0, b.Length);
            }
        }

        static List<string> NombresHojas(List<HojaSalida> hojas)
        {
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lista = new List<string>();
            foreach (var h in hojas)
            {
                var sb = new StringBuilder();
                foreach (char ch in h.Nombre ?? "") if ("[]:*?/\\".IndexOf(ch) < 0 && !char.IsControl(ch)) sb.Append(ch);
                string n = sb.ToString().Trim().Trim('\'');
                if (n == "") n = "Hoja";
                if (n.Length > 31) n = n.Substring(0, 31).TrimEnd();
                string baseN = n;
                int k = 2;
                while (usados.Contains(n))
                {
                    string suf = " (" + k++ + ")";
                    n = (baseN.Length + suf.Length > 31 ? baseN.Substring(0, 31 - suf.Length).TrimEnd() : baseN) + suf;
                }
                usados.Add(n);
                lista.Add(n);
            }
            return lista;
        }

        static string NombreColumna(int c) // 0-based → "A"
        {
            string s = "";
            c++;
            while (c > 0) { int m = (c - 1) % 26; s = (char)('A' + m) + s; c = (c - 1) / 26; }
            return s;
        }

        static string XmlHoja(HojaSalida h)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"").Append(NsMain).Append("\" xmlns:r=\"").Append(NsRel).Append("\">");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\">");
            if (h.FilasCongeladas > 0)
                sb.Append("<pane ySplit=\"").Append(h.FilasCongeladas).Append("\" topLeftCell=\"A").Append(h.FilasCongeladas + 1)
                  .Append("\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/>");
            sb.Append("</sheetView></sheetViews>");
            sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
            if (h.AnchosColumna != null && h.AnchosColumna.Count > 0)
            {
                sb.Append("<cols>");
                for (int i = 0; i < h.AnchosColumna.Count; i++)
                {
                    double w = h.AnchosColumna[i];
                    if (w <= 0) continue;
                    sb.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1).Append("\" width=\"").Append(w.ToString("0.##", Inv)).Append("\" customWidth=\"1\"/>");
                }
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < h.Filas.Count; r++)
            {
                var fila = h.Filas[r];
                if (fila == null || fila.Count == 0) continue;
                sb.Append("<row r=\"").Append(r + 1).Append("\">");
                for (int c = 0; c < fila.Count; c++)
                {
                    var celda = fila[c];
                    if (celda == null || (celda.Valor == null && string.IsNullOrEmpty(celda.Formula) && celda.Estilo == Estilo.Normal)) continue;
                    string refC = NombreColumna(c) + (r + 1);
                    int s = (int)celda.Estilo;
                    string num = Numero(celda.Valor);
                    string formula = string.IsNullOrEmpty(celda.Formula) ? null : celda.Formula.TrimStart('=');
                    if (formula != null)
                    {
                        sb.Append("<c r=\"").Append(refC).Append("\" s=\"").Append(s).Append("\"");
                        if (num == null && celda.Valor != null) sb.Append(" t=\"str\"");
                        sb.Append("><f>").Append(Esc(formula)).Append("</f>");
                        if (num != null) sb.Append("<v>").Append(num).Append("</v>");
                        else if (celda.Valor != null) sb.Append("<v>").Append(Esc(Convert.ToString(celda.Valor, Inv))).Append("</v>");
                        sb.Append("</c>");
                    }
                    else if (celda.Valor == null)
                        sb.Append("<c r=\"").Append(refC).Append("\" s=\"").Append(s).Append("\"/>");
                    else if (num != null)
                        sb.Append("<c r=\"").Append(refC).Append("\" s=\"").Append(s).Append("\"><v>").Append(num).Append("</v></c>");
                    else
                        sb.Append("<c r=\"").Append(refC).Append("\" s=\"").Append(s).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                          .Append(Esc(Convert.ToString(celda.Valor, Inv))).Append("</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData>");
            sb.Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        /// <summary>Texto numérico invariante o null si el valor no es número.</summary>
        static string Numero(object v)
        {
            if (v == null) return null;
            if (v is decimal) return ((decimal)v).ToString(Inv);
            if (v is double) { double d = (double)v; return double.IsNaN(d) || double.IsInfinity(d) ? null : Math.Round(d, 6, MidpointRounding.AwayFromZero).ToString("R", Inv); }
            if (v is float) return ((float)v).ToString("R", Inv);
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong) return Convert.ToString(v, Inv);
            return null;
        }

        static string XmlEstilos()
        {
            // cellXfs en el mismo orden que el enum Estilo
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<styleSheet xmlns=\"" + NsMain + "\">" +
                "<numFmts count=\"3\">" +
                "<numFmt numFmtId=\"164\" formatCode=\"&quot;$&quot;#,##0.00\"/>" +
                "<numFmt numFmtId=\"165\" formatCode=\"#,##0.##\"/>" +
                "<numFmt numFmtId=\"166\" formatCode=\"0.000\"/>" +
                "</numFmts>" +
                "<fonts count=\"3\">" +
                "<font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                "<font><b/><sz val=\"14\"/><name val=\"Calibri\"/><family val=\"2\"/></font>" +
                "</fonts>" +
                "<fills count=\"4\">" +
                "<fill><patternFill patternType=\"none\"/></fill>" +
                "<fill><patternFill patternType=\"gray125\"/></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFDCE6F1\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                "</fills>" +
                "<borders count=\"2\">" +
                "<border><left/><right/><top/><bottom/><diagonal/></border>" +
                "<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right><top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border>" +
                "</borders>" +
                "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                "<cellXfs count=\"9\">" +
                "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +                                                        // Normal
                "<xf numFmtId=\"0\" fontId=\"2\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                       // Titulo
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf>" + // Encabezado
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +                       // Grupo
                "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +                              // Dinero
                "<xf numFmtId=\"165\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +                              // Numero
                "<xf numFmtId=\"166\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +                              // Decimal3
                "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +                                       // Negrita
                "<xf numFmtId=\"164\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyNumberFormat=\"1\"/>" +             // DineroNegrita
                "</cellXfs>" +
                "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                "</styleSheet>";
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length + 8);
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default:
                        // caracteres no válidos en XML 1.0 se omiten
                        if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') continue;
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
