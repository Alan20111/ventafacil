using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Utilidades comunes de las secciones de administración.</summary>
    static class AdminUtil
    {
        /// <summary>Barra superior para botones de una sección.</summary>
        public static FlowLayoutPanel Barra()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 56, Padding = new Padding(12, 8, 8, 0), BackColor = Tema.AzulFondo,
                WrapContents = false
            };
        }

        public static Button BotonBarra(string texto, EventHandler click, int ancho = 90, bool principal = false)
        {
            // el ancho sale del texto: nunca se parte en dos renglones
            int justo = TextRenderer.MeasureText(texto, Tema.Negrita).Width + 28;
            var b = new Button { Text = texto, Width = Math.Max(ancho, justo), Height = 40, Font = Tema.Negrita, Margin = new Padding(0, 0, 8, 0) };
            Tema.Pastilla(b, principal);
            if (click != null) b.Click += click;
            return b;
        }

        /// <summary>Separación entre grupos de botones de una barra.</summary>
        public static Control Separador() { return new Panel { Width = 16, Height = 40, Margin = new Padding(0), BackColor = System.Drawing.Color.Transparent }; }

        /// <summary>Botón que abre un menú con varias opciones (para no llenar la barra).</summary>
        public static Button BotonMenu(string texto, params System.Collections.Generic.KeyValuePair<string, Action>[] opciones)
        {
            var menu = new ContextMenuStrip { Font = Tema.Normal, ShowImageMargin = false, BackColor = System.Drawing.Color.White };
            foreach (var o in opciones) { var x = o; menu.Items.Add(x.Key, null, (s, e) => x.Value()); }
            foreach (ToolStripItem it in menu.Items) it.Padding = new Padding(4, 6, 4, 6);
            var b = BotonBarra(texto + "  ▾", null);
            b.Click += (s, e) => menu.Show(b, 0, b.Height + 2);
            b.Disposed += (s, e) => menu.Dispose();
            return b;
        }
        public static System.Collections.Generic.KeyValuePair<string, Action> Opcion(string nombre, Action a) { return new System.Collections.Generic.KeyValuePair<string, Action>(nombre, a); }

        public static Label TituloSeccion(string texto)
        {
            return new Label { Text = texto, Dock = DockStyle.Top, Height = 36, Font = Tema.Mediana, ForeColor = Tema.Azul, Padding = new Padding(8, 8, 0, 0) };
        }

        public static DataGridView Grid()
        {
            var g = new DataGridView { Dock = DockStyle.Fill };
            Ui.EstiloGrid(g);
            return g;
        }

        /// <summary>Muestra un DataTable en la cuadrícula; columnas de dinero con formato.</summary>
        public static void Mostrar(DataGridView g, DataTable dt, params string[] columnasDinero)
        {
            g.DataSource = null;
            g.Columns.Clear();
            g.AutoGenerateColumns = true;
            g.DataSource = dt;
            foreach (DataGridViewColumn c in g.Columns)
            {
                c.SortMode = DataGridViewColumnSortMode.Automatic;
                c.HeaderText = Encabezado(c.Name);
                bool dinero = Array.IndexOf(columnasDinero, c.Name) >= 0;
                var tipo = dt.Columns[c.Name].DataType;
                if (dinero) { c.DefaultCellStyle.Format = "$#,##0.00"; c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight; }
                else if (tipo == typeof(double) || tipo == typeof(decimal) || tipo == typeof(long) || tipo == typeof(int))
                {
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                    if (tipo == typeof(double) || tipo == typeof(decimal)) c.DefaultCellStyle.Format = "#,##0.###";
                }
            }
        }

        static string Encabezado(string n)
        {
            switch (n)
            {
                case "Codigo": return "Código";
                case "Descripcion": return "Descripción";
                case "FormaPago": return "Forma de pago";
                case "Minimo": return "Mínimo";
                case "ValorCosto": return "Valor a costo";
                case "ValorVenta": return "Valor a precio de venta";
                case "AbrioPor": return "Abrió";
                case "Cerro": return "Cerró";
                case "PorEnviar": return "Por enviar";
                default: return n;
            }
        }

        public static void ExportarCsv(DataTable dt, string nombreSugerido)
        {
            if (dt == null || dt.Rows.Count == 0) { Ui.Aviso("No hay datos para exportar."); return; }
            using (var d = new SaveFileDialog { Filter = "Archivo CSV (Excel)|*.csv", FileName = nombreSugerido + "_" + DateTime.Now.ToString("yyyyMMdd") + ".csv" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                try
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < dt.Columns.Count; i++) sb.Append(i > 0 ? "," : "").Append(Csv(dt.Columns[i].ColumnName));
                    sb.AppendLine();
                    foreach (DataRow r in dt.Rows)
                    {
                        for (int i = 0; i < dt.Columns.Count; i++)
                        {
                            object v = r[i];
                            string s = v == DBNull.Value ? "" : (v is IFormattable ? ((IFormattable)v).ToString(null, System.Globalization.CultureInfo.InvariantCulture) : v.ToString());
                            sb.Append(i > 0 ? "," : "").Append(Csv(s));
                        }
                        sb.AppendLine();
                    }
                    File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
                    Ui.Info("Archivo guardado:\n" + d.FileName);
                }
                catch (Exception ex) { Ui.Error(ex, "Exportar CSV"); }
            }
        }

        public static string Csv(string s)
        {
            s = s ?? "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r', ';' }) >= 0) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        public static NumericUpDown Numero(int x, int y, int ancho, decimal min, decimal max, int decimales = 0)
        {
            return new NumericUpDown { Left = x, Top = y, Width = ancho, Minimum = min, Maximum = max, DecimalPlaces = decimales, Font = Tema.Normal, TextAlign = HorizontalAlignment.Right };
        }

        public static decimal ValorDec(object v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            try { return Data.Db.Dec(v); } catch { return 0m; }
        }
    }
}
