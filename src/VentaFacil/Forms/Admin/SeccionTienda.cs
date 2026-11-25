using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Resultados, en la OFICINA: lo que reportó la tienda en su último acuse.</summary>
    static class TiendaUtil
    {
        public static Label Titulo(string texto)
        {
            string f = ReporteTienda.Fecha;
            return AdminUtil.TituloSeccion(texto + (f == "" ? "  ·  todavía no llega ningún reporte de la tienda" : "  ·  datos de la tienda al " + f));
        }
    }

    /// <summary>Ventas por día, productos vendidos o cortes de la tienda, por rango de fechas.</summary>
    public class SeccionTiendaRango : UserControl
    {
        readonly Func<DateTime, DateTime, DataTable> _consulta;
        readonly string[] _dinero;
        readonly string _suma;
        readonly DateTimePicker _desde, _hasta;
        readonly DataGridView _grid;
        readonly Label _resumen;
        DataTable _dt;

        public SeccionTiendaRango(string titulo, string archivo, Func<DateTime, DateTime, DataTable> consulta, string suma, params string[] dinero)
        {
            BackColor = Tema.Crema;
            _consulta = consulta; _dinero = dinero; _suma = suma;
            _grid = AdminUtil.Grid();
            Controls.Add(_grid);
            var abajo = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Tema.AzulFondo };
            _resumen = Ui.Etiqueta("", 10, 8, Tema.Negrita, Tema.Azul);
            abajo.Controls.Add(_resumen);
            Controls.Add(abajo);
            var f = new Panel { Dock = DockStyle.Top, Height = 54, BackColor = Tema.Crema };
            f.Controls.Add(Ui.Etiqueta("Desde:", 10, 16, Tema.Negrita));
            _desde = new DateTimePicker { Left = 70, Top = 12, Width = 130, Format = DateTimePickerFormat.Short, Font = Tema.Normal }; f.Controls.Add(_desde);
            f.Controls.Add(Ui.Etiqueta("Hasta:", 210, 16, Tema.Negrita));
            _hasta = new DateTimePicker { Left = 266, Top = 12, Width = 130, Format = DateTimePickerFormat.Short, Font = Tema.Normal }; f.Controls.Add(_hasta);
            f.Controls.Add(Ui.Boton("Semana", 408, 9, 85, 36, (s, e) => Rango(DateTime.Today.AddDays(-6))));
            f.Controls.Add(Ui.Boton("Mes", 498, 9, 70, 36, (s, e) => Rango(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))));
            f.Controls.Add(Ui.Boton("Consultar", 573, 9, 110, 36, (s, e) => Cargar(), true));
            f.Controls.Add(Ui.Boton("Exportar Excel", 688, 9, 130, 36, (s, e) => ExcelSemillera.ExportarTabla(_dt, archivo)));
            Controls.Add(f);
            Controls.Add(TiendaUtil.Titulo(titulo));
            Load += (s, e) => Rango(DateTime.Today.AddDays(-6));
        }

        void Rango(DateTime desde) { _desde.Value = desde; _hasta.Value = DateTime.Today; Cargar(); }

        void Cargar()
        {
            try
            {
                _dt = _consulta(_desde.Value.Date, _hasta.Value.Date);
                AdminUtil.Mostrar(_grid, _dt, _dinero);
                _resumen.Text = _dt.Rows.Count + " renglones" + (_dt.Rows.Count == 0 ? "" : "   ·   " + _suma + ": " + Fmt.Dinero(_dt.Rows.Cast<DataRow>().Sum(r => AdminUtil.ValorDec(r[_suma]))));
            }
            catch (Exception ex) { Ui.Error(ex, "Consulta"); }
        }
    }

    /// <summary>Existencias reales de la tienda y lo que va en camino.</summary>
    public class SeccionTiendaExistencias : UserControl
    {
        public SeccionTiendaExistencias()
        {
            BackColor = Tema.Crema;
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = Tema.Negrita, Padding = new Point(14, 6) };
            foreach (var faltan in new[] { false, true })
            {
                bool x = faltan;
                var t = new TabPage(x ? "Bajos de existencia" : "Todas") { BackColor = Tema.Crema };
                t.Controls.Add(new PanelReporte(() => ReporteTienda.Existencias(x), x ? "tienda_faltantes" : "tienda_existencias", "ValorCosto", "Precio", "Costo") { Dock = DockStyle.Fill, Font = Tema.Normal, SoloSumar = "ValorCosto" });
                tabs.TabPages.Add(t);
            }
            Controls.Add(tabs);
            Controls.Add(TiendaUtil.Titulo("Existencias en la tienda"));
        }
    }

    /// <summary>Lo que no coincide entre el catálogo de la oficina y el de la tienda, y qué hacer con cada cosa.</summary>
    public class SeccionTiendaDiferencias : UserControl
    {
        readonly DataGridView _grid;
        readonly Label _resumen;

        public SeccionTiendaDiferencias()
        {
            BackColor = Tema.Crema;
            _grid = AdminUtil.Grid();
            _grid.MultiSelect = true;
            _grid.Columns.Add(Ui.Columna("t", "Qué pasa", 18));
            _grid.Columns.Add(Ui.Columna("c", "Código", 12));
            _grid.Columns.Add(Ui.Columna("p", "Producto", 36));
            _grid.Columns.Add(Ui.Columna("d", "Dato", 18));
            _grid.Columns.Add(Ui.Columna("o", "En la oficina", 14, true));
            _grid.Columns.Add(Ui.Columna("s", "En la tienda", 14, true));
            Controls.Add(_grid);
            var abajo = new Panel { Dock = DockStyle.Bottom, Height = 34, BackColor = Tema.AzulFondo };
            _resumen = Ui.Etiqueta("", 10, 8, Tema.Negrita, Tema.Azul);
            abajo.Controls.Add(_resumen);
            Controls.Add(abajo);
            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("Usar lo de la tienda", (s, e) => Hacer(ReporteTienda.TomarDeTienda, "La oficina se quedará con lo de la tienda (los productos que solo existen allá se agregan aquí)"), 190));
            barra.Controls.Add(AdminUtil.BotonBarra("Mandar lo de la oficina", (s, e) => Hacer(ReporteTienda.MandarDeOficina, "El próximo envío llevará lo de la oficina"), 205));
            barra.Controls.Add(AdminUtil.BotonBarra("Marcar todo", (s, e) => _grid.SelectAll(), 120));
            barra.Controls.Add(AdminUtil.BotonBarra("Actualizar", (s, e) => Cargar(), 110));
            Controls.Add(barra);
            Controls.Add(TiendaUtil.Titulo("Diferencias de catálogo"));
            Load += (s, e) => Cargar();
        }

        void Cargar()
        {
            try
            {
                var l = ReporteTienda.Diferencias();
                _grid.Rows.Clear();
                foreach (var d in l)
                {
                    int i = _grid.Rows.Add(d.Tipo, d.Codigo, d.Producto, Envios.TituloCampo(d.Campo), Envios.Bonito(d.Campo, d.Oficina), Envios.Bonito(d.Campo, d.Tienda));
                    _grid.Rows[i].Tag = d;
                }
                _resumen.Text = ReporteTienda.Fecha == "" ? "Cuando la tienda mande su reporte, aquí saldrá lo que no coincida." : l.Count == 0 ? "Todo coincide (sin contar lo que está por enviarse)." : l.Count + " diferencias. Elija renglones (Ctrl o Mayús + clic) y qué hacer con ellos.";
            }
            catch (Exception ex) { Ui.Error(ex, "Diferencias"); }
        }

        void Hacer(Func<DiferenciaTienda, string> accion, string que)
        {
            var l = _grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).Select(r => r.Tag as DiferenciaTienda).Where(d => d != null).ToList();
            if (l.Count == 0) { Ui.Aviso("Elija al menos un renglón."); return; }
            if (!Ui.Confirmar(que + ".\n\nRenglones elegidos: " + l.Count + ".\n¿Continuar?")) return;
            var no = new List<string>();
            try
            {
                Cursor = Cursors.WaitCursor;
                foreach (var d in l) { string porque = accion(d); if (porque != null) no.Add(porque); }
            }
            catch (Exception ex) { Ui.Error(ex, "Diferencias"); }
            finally { Cursor = Cursors.Default; }
            if (no.Count > 0) Ui.Aviso("No se pudo con " + no.Count + ":\n\n" + string.Join("\n", no.Take(10).ToArray()) + (no.Count > 10 ? "\n…" : ""));
            Cargar();
        }
    }
}
