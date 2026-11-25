using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Printing;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Reportes por rango de fechas.</summary>
    public class SeccionReportes : UserControl
    {
        class Rep
        {
            public string Nombre; public Func<DateTime, DateTime, DataTable> Consulta; public string[] Dinero; public string Archivo;
            public override string ToString() { return Nombre; }
        }

        readonly DateTimePicker _desde, _hasta;
        readonly DataGridView _grid;
        readonly Label _resumen, _ayuda;
        DataTable _dt;
        readonly Rep _rep;
        bool _cambiando;

        static readonly Rep[] Todos =
        {
            new Rep { Nombre = "Ventas por día", Consulta = AdminConsultas.VentasPorDia, Dinero = new[] { "Total", "Costo", "Ganancia" }, Archivo = "ventas_por_dia" },
            new Rep { Nombre = "Tickets", Consulta = AdminConsultas.Tickets, Dinero = new[] { "Total" }, Archivo = "tickets" },
            new Rep { Nombre = "Más vendidos", Consulta = AdminConsultas.ProductosMasVendidos, Dinero = new[] { "Importe", "Ganancia" }, Archivo = "productos_vendidos" },
            new Rep { Nombre = "Por cajero", Consulta = AdminConsultas.VentasPorCajero, Dinero = new[] { "Total" }, Archivo = "ventas_por_cajero" },
            new Rep { Nombre = "Forma de pago", Consulta = AdminConsultas.VentasPorFormaPago, Dinero = new[] { "Total" }, Archivo = "ventas_por_forma_pago" },
            new Rep { Nombre = "Por grupo", Consulta = AdminConsultas.VentasPorDepartamento, Dinero = new[] { "Importe", "Ganancia" }, Archivo = "ventas_por_grupo" },
            new Rep { Nombre = "Cortes de caja", Consulta = AdminConsultas.Turnos, Dinero = new[] { "Fondo", "Declarado" }, Archivo = "cortes" },
            new Rep { Nombre = "Movimientos", Consulta = AdminConsultas.MovimientosCaja, Dinero = new[] { "Monto" }, Archivo = "movimientos_caja" }
        };

        /// <summary>Cada reporte es una vista del módulo Resultados.</summary>
        public static void AgregarVistas(ModuloVista m) { foreach (var r in Todos) { var x = r; m.AgregarVista(x.Nombre, () => new SeccionReportes(x)); } }

        SeccionReportes(Rep rep)
        {
            _rep = rep;
            BackColor = Tema.Crema;
            _grid = AdminUtil.Grid();
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Detalle(); };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Detalle(); } };
            Controls.Add(_grid);

            var abajo = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Tema.AzulFondo };
            _resumen = Ui.Etiqueta("", 12, 10, Tema.Negrita, Tema.Azul);
            abajo.Controls.Add(_resumen);
            _ayuda = new Label { Dock = DockStyle.Right, Width = 300, TextAlign = ContentAlignment.MiddleRight, Font = Tema.Normal, ForeColor = Tema.Humo, Padding = new Padding(0, 0, 12, 0) };
            abajo.Controls.Add(_ayuda);
            Controls.Add(abajo);

            // Se elige el período y se consulta solo: no hay botón de consultar
            var filtro = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Tema.Crema };
            filtro.Controls.Add(Ui.Boton("Hoy", 12, 10, 64, 40, (s, e) => Rango(DateTime.Today, DateTime.Today)));
            filtro.Controls.Add(Ui.Boton("Ayer", 84, 10, 68, 40, (s, e) => Rango(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(-1))));
            filtro.Controls.Add(Ui.Boton("Semana", 160, 10, 88, 40, (s, e) => Rango(DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7)), DateTime.Today)));
            filtro.Controls.Add(Ui.Boton("Mes", 256, 10, 64, 40, (s, e) => Rango(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), DateTime.Today)));
            filtro.Controls.Add(Ui.Etiqueta("Del:", 336, 20, Tema.Negrita));
            _desde = new DateTimePicker { Left = 376, Top = 17, Width = 130, Format = DateTimePickerFormat.Short, Font = Tema.Normal };
            filtro.Controls.Add(_desde);
            filtro.Controls.Add(Ui.Etiqueta("al:", 518, 20, Tema.Negrita));
            _hasta = new DateTimePicker { Left = 548, Top = 17, Width = 130, Format = DateTimePickerFormat.Short, Font = Tema.Normal };
            filtro.Controls.Add(_hasta);
            filtro.Controls.Add(Ui.Boton("Exportar a Excel", 702, 10, 160, 40, (s, e) => ExcelSemillera.ExportarTabla(_dt, _rep.Archivo)));
            filtro.Controls.Add(Ui.Boton("Imprimir", 870, 10, 104, 40, (s, e) => ImprimirTicket()));
            Controls.Add(filtro);

            EventHandler cambio = (s, e) => { if (!_cambiando) Consultar(); };
            _desde.ValueChanged += cambio; _hasta.ValueChanged += cambio;
            Load += (s, e) => Rango(DateTime.Today, DateTime.Today);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.R)) { Consultar(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Rango(DateTime d, DateTime h)
        {
            _cambiando = true;
            try { _desde.Value = d; _hasta.Value = h; } finally { _cambiando = false; }
            Consultar();
        }

        void Consultar()
        {
            if (_hasta.Value.Date < _desde.Value.Date) { _resumen.Text = "La segunda fecha es anterior a la primera."; return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                _dt = _rep.Consulta(_desde.Value.Date, _hasta.Value.Date);
                if (_rep.Archivo == "ventas_por_dia")
                {
                    _dt.Columns.Add("Ganancia", typeof(double));
                    foreach (DataRow r in _dt.Rows) r["Ganancia"] = (double)(AdminUtil.ValorDec(r["Total"]) - AdminUtil.ValorDec(r["Costo"]));
                }
                AdminUtil.Mostrar(_grid, _dt, _rep.Dinero);
                if (_rep.Archivo == "tickets")
                    foreach (DataGridViewRow r in _grid.Rows)
                        if (Convert.ToString(r.Cells["Estado"].Value) == "CANCELADA") r.DefaultCellStyle.ForeColor = Tema.Rojo;
                string res = _dt.Rows.Count + " renglones";
                foreach (var col in _rep.Dinero)
                {
                    if (_rep.Archivo == "cortes") break;
                    decimal suma = 0;
                    foreach (DataRow r in _dt.Rows)
                        if (!_dt.Columns.Contains("Estado") || Convert.ToString(r["Estado"]) != "CANCELADA") suma += AdminUtil.ValorDec(r[col]);
                    res += "   ·   " + col + ": " + Fmt.Dinero(suma);
                }
                _resumen.Text = res;
                _ayuda.Text = _rep.Archivo == "tickets" ? "Doble clic: ver ticket" : _rep.Archivo == "cortes" ? "Doble clic: ver corte" : "";
            }
            catch (Exception ex) { Ui.Error(ex, "Consultar reporte"); }
            finally { Cursor = Cursors.Default; }
        }

        void Detalle()
        {
            if (_rep == null || _grid.CurrentRow == null || _dt == null) return;
            var fila = (_grid.CurrentRow.DataBoundItem as DataRowView);
            if (fila == null) return;
            try
            {
                if (_rep.Archivo == "tickets")
                {
                    using (var f = new FormDetalleTicket(Convert.ToInt64(fila["Folio"])))
                        if (f.ShowDialog(this) == DialogResult.OK) Consultar();
                }
                else if (_rep.Archivo == "cortes")
                {
                    using (var f = new FormDetalleCorte(Convert.ToInt64(fila["Turno"]))) f.ShowDialog(this);
                }
            }
            catch (Exception ex) { Ui.Error(ex, "Detalle"); }
        }

        /// <summary>Imprime el reporte en la impresora de tickets (formato resumido).</summary>
        void ImprimirTicket()
        {
            if (_rep == null || _dt == null || _dt.Rows.Count == 0) { Ui.Aviso("Primero consulte un reporte con datos."); return; }
            try
            {
                var c = Config.Actual.Copia();
                c.Copias = 1;
                var t = new Ticket(c.Columnas);
                t.Centro(c.NegocioNombre, Ticket.Estilo.Negrita);
                t.Centro(_rep.Nombre.ToUpper(), Ticket.Estilo.Negrita);
                t.Centro(_desde.Value.ToString("dd/MM/yyyy") + " al " + _hasta.Value.ToString("dd/MM/yyyy"));
                t.Separador();
                string colValor = _rep.Dinero.Length > 0 ? _rep.Dinero[0] : _dt.Columns[_dt.Columns.Count - 1].ColumnName;
                foreach (DataRow r in _dt.Rows)
                {
                    string izq = "";
                    for (int i = 0; i < _dt.Columns.Count && izq.Length < c.Columnas; i++)
                    {
                        var col = _dt.Columns[i].ColumnName;
                        if (col == colValor || Array.IndexOf(_rep.Dinero, col) >= 0) continue;
                        string v = Texto(r[i]);
                        if (v == "") continue;
                        izq += (izq == "" ? "" : " ") + v;
                        if (i >= 1) break;
                    }
                    string der = Fmt.Dinero(AdminUtil.ValorDec(r[colValor]));
                    int max = c.Columnas - der.Length - 1;
                    if (izq.Length > max) izq = izq.Substring(0, Math.Max(0, max));
                    t.Par(izq, der);
                }
                t.Separador();
                foreach (var col in _rep.Dinero)
                {
                    if (_rep.Archivo == "cortes") break;
                    decimal suma = 0;
                    foreach (DataRow r in _dt.Rows)
                        if (!_dt.Columns.Contains("Estado") || Convert.ToString(r["Estado"]) != "CANCELADA") suma += AdminUtil.ValorDec(r[col]);
                    t.Par("Total " + col + ":", Fmt.Dinero(suma), Ticket.Estilo.Negrita);
                }
                t.Linea("Impreso: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                Impresora.Imprimir(t, c);
            }
            catch (Exception ex) { Ui.Error(ex, "Imprimir reporte"); }
        }

        static string Texto(object v)
        {
            if (v == null || v == DBNull.Value) return "";
            if (v is double || v is decimal) return Data.Db.Dec(v).ToString("0.###", CultureInfo.InvariantCulture);
            return Convert.ToString(v);
        }
    }
}
