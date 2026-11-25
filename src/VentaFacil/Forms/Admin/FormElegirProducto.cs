using System;
using System.Collections.Generic;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Buscar y elegir un producto por descripción o código.</summary>
    public class FormElegirProducto : Dialogo
    {
        readonly TextBox _buscar;
        readonly DataGridView _grid;
        public Producto Elegido { get; private set; }

        public FormElegirProducto(string texto = "") : base("Buscar producto", 760, 480)
        {
            Controls.Add(Ui.Etiqueta("Escriba parte de la descripción o el código y presione Enter:", 12, 10, Tema.Negrita));
            _buscar = Ui.Caja(12, 36, 734, Tema.Mediana);
            _buscar.Text = texto;
            Controls.Add(_buscar);
            _grid = new DataGridView { Left = 12, Top = 76, Width = 734, Height = 392 };
            Ui.EstiloGrid(_grid);
            _grid.Columns.Add(Ui.Columna("codigo", "Código", 22));
            _grid.Columns.Add(Ui.Columna("descripcion", "Descripción", 50));
            _grid.Columns.Add(Ui.Columna("precio", "Precio", 14, true));
            _grid.Columns.Add(Ui.Columna("existencia", "Existencia", 14, true));
            if (Envios.EsOficina) _grid.Columns["existencia"].Visible = false;
            Controls.Add(_grid);

            _buscar.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Down)
                {
                    e.SuppressKeyPress = true;
                    Buscar();
                    if (_grid.Rows.Count == 1 && e.KeyCode == Keys.Enter) Elegir();
                    else if (_grid.Rows.Count > 0) _grid.Focus();
                }
            };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Elegir(); } };
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Elegir(); };
            Shown += (s, e) => { if (texto != "") Buscar(); _buscar.Focus(); _buscar.SelectAll(); };
        }

        void Buscar()
        {
            try
            {
                _grid.Rows.Clear();
                List<Producto> l = ProductoRepo.Buscar(_buscar.Text, false, 300);
                foreach (var p in l)
                {
                    int i = _grid.Rows.Add(p.Codigo, p.Descripcion, Fmt.Dinero(p.Precio), p.UsaInventario ? Fmt.Cant(p.Existencia, p.Granel) : "-");
                    _grid.Rows[i].Tag = p;
                }
            }
            catch (Exception ex) { Ui.Error(ex, "Buscar"); }
        }

        void Elegir()
        {
            if (_grid.CurrentRow == null) return;
            Elegido = _grid.CurrentRow.Tag as Producto;
            if (Elegido != null) DialogResult = DialogResult.OK;
        }

        /// <summary>Busca por código exacto; si no hay, abre el buscador.</summary>
        public static Producto Pedir(IWin32Window dueño, string codigoOTexto)
        {
            codigoOTexto = (codigoOTexto ?? "").Trim();
            if (codigoOTexto != "")
            {
                try
                {
                    var p = ProductoRepo.PorCodigo(codigoOTexto);
                    if (p != null) return p;
                }
                catch (Exception ex) { Ui.Error(ex, "Buscar producto"); return null; }
            }
            using (var f = new FormElegirProducto(codigoOTexto))
                return f.ShowDialog(dueño) == DialogResult.OK ? f.Elegido : null;
        }
    }
}
