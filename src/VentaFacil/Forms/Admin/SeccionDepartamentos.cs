using System;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Departamentos (familias) de productos.</summary>
    public class SeccionDepartamentos : UserControl
    {
        readonly DataGridView _grid;

        public SeccionDepartamentos()
        {
            BackColor = Tema.Crema;
            _grid = AdminUtil.Grid();
            _grid.Dock = DockStyle.Left;
            _grid.Width = 480;
            _grid.Columns.Add(Ui.Columna("nombre", "Departamento", 100));
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Renombrar(); };
            Controls.Add(_grid);

            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("Nuevo (Ctrl+N)", (s, e) => Nuevo(), 140));
            barra.Controls.Add(AdminUtil.BotonBarra("Renombrar (Ctrl+M)", (s, e) => Renombrar(), 175));
            barra.Controls.Add(AdminUtil.BotonBarra("Eliminar (Ctrl+E)", (s, e) => Eliminar(), 155));
            Controls.Add(barra);
            Controls.Add(AdminUtil.TituloSeccion("Departamentos"));
            Load += (s, e) => { Cargar(); _grid.Focus(); };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.N: Nuevo(); return true;
                case Keys.Control | Keys.M: Renombrar(); return true;
                case Keys.Control | Keys.E: Eliminar(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Cargar()
        {
            try
            {
                _grid.Rows.Clear();
                foreach (var d in DepartamentoRepo.Todos()) { int i = _grid.Rows.Add(d.Nombre); _grid.Rows[i].Tag = d; }
            }
            catch (Exception ex) { Ui.Error(ex, "Cargar departamentos"); }
        }

        Departamento Sel() { return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as Departamento; }

        void Nuevo()
        {
            var n = Ui.PedirTexto("Nuevo departamento", "Nombre:");
            if (string.IsNullOrWhiteSpace(n)) return;
            try { DepartamentoRepo.Guardar(new Departamento { Nombre = n }); Cargar(); }
            catch (Exception ex) { Ui.Error(ex, "No se pudo crear (¿ya existe?)"); }
        }

        void Renombrar()
        {
            var d = Sel();
            if (d == null) return;
            var n = Ui.PedirTexto("Renombrar departamento", "Nuevo nombre:", d.Nombre);
            if (string.IsNullOrWhiteSpace(n)) return;
            try { d.Nombre = n; DepartamentoRepo.Guardar(d); Cargar(); }
            catch (Exception ex) { Ui.Error(ex, "No se pudo renombrar (¿ya existe?)"); }
        }

        void Eliminar()
        {
            var d = Sel();
            if (d == null) return;
            if (!Ui.Confirmar("¿Eliminar el departamento \"" + d.Nombre + "\"?")) return;
            try
            {
                if (!DepartamentoRepo.Eliminar(d.Id)) Ui.Aviso("No se puede eliminar: tiene productos asignados.\nCámbielos de departamento primero.");
                Cargar();
            }
            catch (Exception ex) { Ui.Error(ex, "Eliminar departamento"); }
        }
    }
}
