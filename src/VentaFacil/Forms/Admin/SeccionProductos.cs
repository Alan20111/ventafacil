using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Catálogo de productos: buscar, alta (F3), modificar (F4), eliminar (F5), importar/exportar CSV.</summary>
    public class SeccionProductos : UserControl
    {
        readonly TextBox _buscar;
        readonly CheckBox _inactivos;
        readonly DataGridView _grid;
        readonly Label _conteo;
        readonly Timer _timer = new Timer { Interval = 300 };
        List<Producto> _lista = new List<Producto>();

        public SeccionProductos()
        {
            BackColor = Tema.Crema;

            _grid = AdminUtil.Grid();
            _grid.Columns.Add(Ui.Columna("codigo", "Código", 10));
            _grid.Columns.Add(Ui.Columna("barras", "Cód. barras", 13));
            _grid.Columns.Add(Ui.Columna("descripcion", "Producto", 34));
            _grid.Columns.Add(Ui.Columna("proveedor", "Proveedor", 13));
            _grid.Columns.Add(Ui.Columna("costo", "Costo", 10, true));
            _grid.Columns.Add(Ui.Columna("mayoreo", "Mayoreo", 13, true));
            _grid.Columns.Add(Ui.Columna("medio", "Med. may.", 13, true));
            _grid.Columns.Add(Ui.Columna("precio", "Menudeo", 10, true));
            _grid.Columns.Add(Ui.Columna("existencia", "Stock", 8, true));
            _grid.Columns.Add(Ui.Columna("minimo", "Mín.", 6, true));
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Modificar(); };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Modificar(); } };
            Controls.Add(_grid);

            var filtro = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Tema.Crema };
            filtro.Controls.Add(Ui.Etiqueta("Buscar:", 10, 14, Tema.Negrita));
            _buscar = Ui.Caja(80, 10, 380, Tema.Mediana);
            filtro.Controls.Add(_buscar);
            _inactivos = new CheckBox { Text = "Ver desactivados", Left = 476, Top = 14, AutoSize = true };
            filtro.Controls.Add(_inactivos);
            _conteo = Ui.Etiqueta("", 620, 14, null, Tema.Humo);
            filtro.Controls.Add(_conteo);
            Controls.Add(filtro);

            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("F3 Nuevo", (s, e) => Nuevo()));
            barra.Controls.Add(AdminUtil.BotonBarra("F4 Modificar", (s, e) => Modificar()));
            barra.Controls.Add(AdminUtil.BotonBarra("F5 Eliminar", (s, e) => Eliminar()));
            var impXls = AdminUtil.BotonBarra("Importar Excel semillera", (s, e) => { if (ExcelSemillera.Importar(FindForm())) Cargar(); }, 200);
            impXls.ForeColor = Tema.Verde;
            barra.Controls.Add(impXls);
            var barras = AdminUtil.BotonBarra("Códigos de barras…", (s, e) => CodigosDeBarras(), 190);
            barras.ForeColor = Tema.Azul;
            barra.Controls.Add(barras);
            barra.Controls.Add(AdminUtil.BotonBarra("Etiqueta…", (s, e) => { var p = Seleccionado(); if (p == null) Ui.Aviso("Elija un producto de la lista."); else FormVistaEtiqueta.Mostrar(p); }, 120));
            barra.Controls.Add(AdminUtil.BotonBarra("Lista de costos (Excel)", (s, e) => ExcelSemillera.ExportarLista(FindForm()), 190));
            barra.Controls.Add(AdminUtil.BotonBarra("Importar CSV", (s, e) => Importar()));
            barra.Controls.Add(AdminUtil.BotonBarra("Exportar CSV", (s, e) => Exportar()));
            Controls.Add(barra);
            Controls.Add(AdminUtil.TituloSeccion("Productos"));

            _timer.Tick += (s, e) => { _timer.Stop(); Cargar(); };
            _buscar.TextChanged += (s, e) => { _timer.Stop(); _timer.Start(); };
            _buscar.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _timer.Stop(); Cargar(); _grid.Focus(); }
            };
            _inactivos.CheckedChanged += (s, e) => Cargar();
            Load += (s, e) => { Cargar(); _buscar.Focus(); };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F3: Nuevo(); return true;
                case Keys.F4: Modificar(); return true;
                case Keys.F5: Eliminar(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing) { if (disposing) _timer.Dispose(); base.Dispose(disposing); }

        void Cargar()
        {
            try
            {
                _lista = ProductoRepo.Buscar(_buscar.Text, _inactivos.Checked, 1000);
                _grid.Rows.Clear();
                foreach (var p in _lista)
                {
                    string u = p.Granel ? "kg" : "pz";
                    int i = _grid.Rows.Add(p.Codigo, p.Codigo2, p.Descripcion + (p.Granel ? "" : "  (pz)"), p.Proveedor, Fmt.Dinero(p.Costo),
                        p.PrecioMayoreo > 0 ? Fmt.Dinero(p.PrecioMayoreo) + " ≥" + Fmt.Dec(p.MayoreoDesde) + u : "",
                        p.PrecioMedio > 0 ? Fmt.Dinero(p.PrecioMedio) + " ≥" + Fmt.Dec(p.MedioDesde) + u : "",
                        Fmt.Dinero(p.Precio),
                        p.UsaInventario ? Fmt.Dec(p.Existencia) : "-", p.UsaInventario ? Fmt.Dec(p.Minimo) : "");
                    _grid.Rows[i].Tag = p;
                    if (!p.Activo) _grid.Rows[i].DefaultCellStyle.ForeColor = Tema.Humo;
                    else if (p.UsaInventario && p.Existencia <= p.Minimo) _grid.Rows[i].Cells["existencia"].Style.ForeColor = Tema.Rojo;
                }
                _conteo.Text = _lista.Count >= 1000 ? "Mostrando los primeros 1000; escriba para filtrar." : _lista.Count + " productos";
            }
            catch (Exception ex) { Ui.Error(ex, "Cargar productos"); }
        }

        /// <summary>Abre la asignación rápida de códigos de barras y, al cerrar, recarga la lista.</summary>
        public void CodigosDeBarras()
        {
            using (var f = new FormAsignarCodigos()) f.ShowDialog(FindForm());
            Cargar();
        }

        Producto Seleccionado()
        {
            if (_grid.CurrentRow == null) return null;
            return _grid.CurrentRow.Tag as Producto;
        }

        void Nuevo()
        {
            using (var f = new FormProducto(null))
                if (f.ShowDialog(this) == DialogResult.OK) { Cargar(); Seleccionar(f.Producto.Id); }
        }

        void Modificar()
        {
            var p = Seleccionado();
            if (p == null) return;
            try { p = ProductoRepo.Obtener(p.Id); } catch (Exception ex) { Ui.Error(ex, "Leer producto"); return; }
            using (var f = new FormProducto(p))
                if (f.ShowDialog(this) == DialogResult.OK) { Cargar(); Seleccionar(p.Id); }
        }

        void Seleccionar(long id)
        {
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var p = r.Tag as Producto;
                if (p != null && p.Id == id) { _grid.CurrentCell = r.Cells[0]; break; }
            }
            _grid.Focus();
        }

        void Eliminar()
        {
            var p = Seleccionado();
            if (p == null) return;
            if (!Ui.Confirmar("¿Eliminar el producto?\n\n" + p.Codigo + "  " + p.Descripcion + "\n\nSi ya tiene ventas, solo se desactivará.")) return;
            try { ProductoRepo.Eliminar(p.Id); Cargar(); }
            catch (Exception ex) { Ui.Error(ex, "Eliminar producto"); }
        }

        void Importar()
        {
            using (var d = new OpenFileDialog { Filter = "Archivo CSV|*.csv;*.txt|Todos|*.*", Title = "Importar productos" })
            {
                if (d.ShowDialog() != DialogResult.OK) return;
                if (!Ui.Confirmar("Se importarán los productos del archivo.\n\nColumnas: codigo, descripcion, departamento, precio, costo, existencia, minimo, granel, codigo2\n\n" +
                    "Los productos con código existente se actualizarán (incluida su existencia, si viene en el archivo).\n\n¿Continuar?")) return;
                try
                {
                    Cursor = Cursors.WaitCursor;
                    var r = ImportarCsv.Importar(d.FileName, Sesion.Usuario != null ? Sesion.Usuario.Id : 0);
                    Cursor = Cursors.Default;
                    string msg = "Importación terminada.\n\nNuevos: " + r.Nuevos + "\nActualizados: " + r.Actualizados + "\nCon error: " + r.Errores.Count;
                    if (r.Errores.Count > 0)
                    {
                        msg += "\n\nPrimeros errores:\n";
                        for (int i = 0; i < Math.Min(10, r.Errores.Count); i++) msg += r.Errores[i] + "\n";
                    }
                    Ui.Info(msg);
                    Cargar();
                }
                catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex, "Importar productos (no se guardó nada)"); }
            }
        }

        void Exportar()
        {
            try
            {
                var dt = new DataTable();
                foreach (var c in new[] { "codigo", "descripcion", "departamento", "precio", "costo", "existencia", "minimo", "granel", "codigo2" }) dt.Columns.Add(c);
                foreach (var p in AdminConsultas.TodosLosProductos())
                {
                    if (!p.Activo) continue;
                    dt.Rows.Add(p.Codigo, p.Descripcion, p.Departamento, Fmt.Dec(p.Precio), Fmt.Dec(p.Costo), Fmt.Dec(p.Existencia), Fmt.Dec(p.Minimo), p.Granel ? "1" : "0", p.Codigo2);
                }
                AdminUtil.ExportarCsv(dt, "productos");
            }
            catch (Exception ex) { Ui.Error(ex, "Exportar productos"); }
        }
    }
}
