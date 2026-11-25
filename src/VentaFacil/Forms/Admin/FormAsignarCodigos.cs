using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Printing;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>
    /// Asignación rápida de códigos de barras. Se elige (o ya está seleccionado) un producto, se ESCANEA su
    /// empaque, el código se guarda en su código alterno y se pasa solo al siguiente. Así el dueño no teclea
    /// nada: solo toma el producto y pasa el lector. El código corto del producto no se toca.
    /// Lo que no lleva código de barras (granel, verdura) se marca con Ctrl+K o se le imprime una etiqueta con Ctrl+T.
    /// </summary>
    public class SeccionCodigos : UserControl
    {
        readonly DataGridView _grid;
        readonly TextBox _buscar, _escaner;
        readonly ComboBox _grupo;
        readonly CheckBox _conCodigo, _marcados;
        readonly Label _producto, _resumen, _mensaje;
        List<Producto> _todos = new List<Producto>();
        bool _llenando;

        public SeccionCodigos()
        {
            BackColor = Tema.Crema;
            _grid = AdminUtil.Grid();
            _grid.Columns.Add(Ui.Columna("producto", "Producto", 40));
            _grid.Columns.Add(Ui.Columna("grupo", "Grupo", 22));
            _grid.Columns.Add(Ui.Columna("corto", "Cód. corto", 12));
            _grid.Columns.Add(Ui.Columna("barras", "Código de barras", 22));
            _grid.Columns.Add(Ui.Columna("estado", "Estado", 14));
            Controls.Add(_grid);

            // ---- escaneo, abajo ----
            var abajo = new Panel { Dock = DockStyle.Bottom, Height = 128, BackColor = Tema.AzulFondo };
            abajo.Controls.Add(Ui.Etiqueta("Escanee:", 12, 14, Tema.Mediana));
            _producto = new Label { Left = 112, Top = 12, Width = 900, Height = 30, AutoEllipsis = true, Font = Tema.Mediana, ForeColor = Tema.Azul, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
            abajo.Controls.Add(_producto);
            _escaner = new TextBox { Left = 12, Top = 52, Width = 480, Font = Tema.Captura, MaxLength = 40, BorderStyle = BorderStyle.FixedSingle };
            abajo.Controls.Add(_escaner);
            _mensaje = new Label { Left = 508, Top = 52, Width = 480, Height = 43, AutoEllipsis = true, Font = Tema.Negrita, ForeColor = Tema.Verde, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleLeft };
            abajo.Controls.Add(_mensaje);
            abajo.Controls.Add(Ui.Etiqueta("Pase el lector: se guarda solo y sigue con el siguiente (↑ ↓ para moverse).", 12, 102, null, Tema.Humo));
            _resumen = new Label { Left = 600, Top = 100, Width = 390, Height = 24, TextAlign = ContentAlignment.MiddleLeft, Font = Tema.Normal, ForeColor = Tema.Azul, BackColor = Color.Transparent };
            abajo.Controls.Add(_resumen);
            Controls.Add(abajo);

            // ---- filtros ----
            var filtro = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Tema.Crema };
            filtro.Controls.Add(Ui.Etiqueta("Buscar:", 12, 15, Tema.Negrita));
            _buscar = Ui.Caja(84, 11, 240); filtro.Controls.Add(_buscar);
            filtro.Controls.Add(Ui.Etiqueta("Grupo:", 340, 15, Tema.Negrita));
            _grupo = new ComboBox { Left = 406, Top = 11, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            filtro.Controls.Add(_grupo);
            _conCodigo = new CheckBox { Text = "Incluir con código", Left = 644, Top = 13, AutoSize = true, Font = Tema.Normal };
            _marcados = new CheckBox { Text = "Incluir «sin código»", Left = 826, Top = 13, AutoSize = true, Font = Tema.Normal };
            filtro.Controls.Add(_conCodigo); filtro.Controls.Add(_marcados);
            Controls.Add(filtro);

            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("Saltar  (Ctrl+S)", (s, e) => Siguiente()));
            barra.Controls.Add(AdminUtil.BotonBarra("Sin código de barras  (Ctrl+K)", (s, e) => SinBarras()));
            barra.Controls.Add(AdminUtil.BotonBarra("Quitar código  (Ctrl+Q)", (s, e) => Quitar()));
            barra.Controls.Add(AdminUtil.BotonBarra("Imprimir etiqueta  (Ctrl+T)", (s, e) => Etiqueta()));
            Controls.Add(barra);

            _buscar.TextChanged += (s, e) => Filtrar(null);
            _grupo.SelectedIndexChanged += (s, e) => Filtrar(null);
            _conCodigo.CheckedChanged += (s, e) => Filtrar(null);
            _marcados.CheckedChanged += (s, e) => Filtrar(null);
            _grid.SelectionChanged += (s, e) => { if (!_llenando && IsHandleCreated) { MostrarActual(); BeginInvoke((Action)(() => { if (!_buscar.Focused) _escaner.Focus(); })); } };
            _grid.CellClick += (s, e) => _escaner.Focus();
            _escaner.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Asignar(); }
                else if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; Mover(1); }
                else if (e.KeyCode == Keys.Up) { e.SuppressKeyPress = true; Mover(-1); }
            };
            Load += (s, e) => { Cargar(); BeginInvoke((Action)(() => _escaner.Focus())); };
        }

        /// <summary>Ctrl+letra: F1–F6 son de los módulos.</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.S: Siguiente(); return true;
                case Keys.Control | Keys.K: SinBarras(); return true;
                case Keys.Control | Keys.Q: Quitar(); return true;
                case Keys.Control | Keys.T: Etiqueta(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---------------- datos ----------------
        void Cargar()
        {
            try
            {
                _todos = ProductoRepo.Buscar("", false, 5000)
                    .OrderBy(p => ConteoFisico.Grupo(p)).ThenBy(p => p.Orden).ThenBy(p => p.Descripcion).ToList();
                _llenando = true;
                _grupo.Items.Clear();
                _grupo.Items.Add("(Todos)");
                foreach (var g in _todos.Select(ConteoFisico.Grupo).Distinct().OrderBy(x => x)) _grupo.Items.Add(g);
                _grupo.SelectedIndex = 0;
                _llenando = false;
                Filtrar(null);
            }
            catch (Exception ex) { _llenando = false; Ui.Error(ex, "Cargar productos"); }
        }

        static bool Pendiente(Producto p) { return p.Codigo2 == "" && !p.SinBarras; }

        void Filtrar(long? seleccionar)
        {
            if (_llenando) return;
            _llenando = true;
            string texto = (_buscar.Text ?? "").Trim().ToLowerInvariant();
            string grupo = _grupo.SelectedIndex > 0 ? _grupo.SelectedItem.ToString() : null;
            _grid.Rows.Clear();
            foreach (var p in _todos)
            {
                bool tiene = p.Codigo2 != "";
                bool marcado = p.Codigo2 == "" && p.SinBarras;
                if (tiene && !_conCodigo.Checked) continue;
                if (marcado && !_marcados.Checked) continue;
                if (grupo != null && ConteoFisico.Grupo(p) != grupo) continue;
                if (texto != "" && p.Descripcion.ToLowerInvariant().IndexOf(texto, StringComparison.Ordinal) < 0 && p.Codigo.ToLowerInvariant().IndexOf(texto, StringComparison.Ordinal) < 0) continue;
                PintarFila(_grid.Rows[_grid.Rows.Add()], p);
            }
            _llenando = false;
            if (_grid.Rows.Count > 0)
            {
                int idx = 0;
                if (seleccionar.HasValue) for (int i = 0; i < _grid.Rows.Count; i++) if (((Producto)_grid.Rows[i].Tag).Id == seleccionar.Value) { idx = i; break; }
                _grid.CurrentCell = _grid.Rows[idx].Cells[0];
            }
            MostrarActual();
            Resumen();
        }

        void PintarFila(DataGridViewRow fila, Producto p)
        {
            fila.Tag = p;
            fila.Cells[0].Value = p.Descripcion;
            fila.Cells[1].Value = ConteoFisico.Grupo(p);
            fila.Cells[2].Value = p.Codigo;
            fila.Cells[3].Value = p.Codigo2;
            bool tiene = p.Codigo2 != "";
            fila.Cells[4].Value = tiene ? "Listo" : p.SinBarras ? "Sin código" : "Pendiente";
            fila.DefaultCellStyle.ForeColor = tiene ? Tema.Verde : p.SinBarras ? Tema.Humo : Tema.Texto;
        }

        void Resumen()
        {
            try
            {
                int con, pend, marc;
                ProductoRepo.ResumenBarras(out con, out pend, out marc);
                _resumen.Text = "Con código: " + con + "   ·   Pendientes: " + pend + "   ·   Sin código: " + marc;
            }
            catch (Exception) { }
        }

        Producto Actual()
        {
            return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as Producto;
        }

        void MostrarActual()
        {
            var p = Actual();
            _producto.Text = p == null ? "(no hay productos con estos filtros)" : p.Descripcion + (p.Codigo2 != "" ? "      ya tiene: " + p.Codigo2 : "");
        }

        void Mover(int paso)
        {
            if (_grid.Rows.Count == 0 || _grid.CurrentRow == null) return;
            int i = Math.Max(0, Math.Min(_grid.Rows.Count - 1, _grid.CurrentRow.Index + paso));
            _grid.CurrentCell = _grid.Rows[i].Cells[0];
            _mensaje.Text = "";
        }

        /// <summary>Pasa al siguiente producto PENDIENTE (después del actual; si no hay, desde el principio).</summary>
        void Siguiente()
        {
            if (_grid.Rows.Count == 0) return;
            int desde = _grid.CurrentRow == null ? -1 : _grid.CurrentRow.Index;
            for (int k = 1; k <= _grid.Rows.Count; k++)
            {
                int i = (desde + k) % _grid.Rows.Count;
                if (Pendiente((Producto)_grid.Rows[i].Tag)) { _grid.CurrentCell = _grid.Rows[i].Cells[0]; return; }
            }
            Aviso("No quedan productos pendientes en esta lista.", false);
        }

        void Aviso(string texto, bool error)
        {
            _mensaje.ForeColor = error ? Tema.Rojo : Tema.Verde;
            _mensaje.Text = texto;
        }

        /// <summary>Vuelve a leer el producto de la base y actualiza su fila y la lista en memoria.</summary>
        void Refrescar(long id)
        {
            var nuevo = ProductoRepo.Obtener(id);
            if (nuevo == null) return;
            for (int i = 0; i < _todos.Count; i++) if (_todos[i].Id == id) _todos[i] = nuevo;
            foreach (DataGridViewRow fila in _grid.Rows)
                if (((Producto)fila.Tag).Id == id) { _llenando = true; PintarFila(fila, nuevo); _llenando = false; }
            MostrarActual();
            Resumen();
        }

        // ---------------- acciones ----------------
        void Asignar()
        {
            string codigo = (_escaner.Text ?? "").Trim();
            _escaner.Clear();
            if (codigo == "") return;
            var p = Actual();
            if (p == null) { Aviso("Elija primero un producto de la lista.", true); return; }
            try
            {
                if (codigo.Length == 13 && codigo[0] == '2' && codigo.All(char.IsDigit) &&
                    !Ui.Confirmar("Ese código empieza con 2: el cobro lo tomaría como etiqueta de BÁSCULA y no encontraría el producto.\n\n¿Guardarlo de todos modos?")) return;

                var otro = ProductoRepo.AsignarCodigoBarras(p.Id, codigo);
                if (otro != null)
                {
                    if (otro.Codigo2 == codigo)
                    {
                        if (!Ui.Confirmar("El código " + codigo + " ya es de:\n\n   «" + otro.Descripcion + "»\n\n¿Quitárselo y pasarlo a «" + p.Descripcion + "»?")) { Aviso("No se cambió nada.", true); return; }
                        ProductoRepo.QuitarCodigoBarras(otro.Id);
                        ProductoRepo.AsignarCodigoBarras(p.Id, codigo);
                        Refrescar(otro.Id);
                    }
                    else
                    {
                        Ui.Aviso("Ese código ya es el CÓDIGO CORTO de «" + otro.Descripcion + "».\nUn código no puede ser de dos productos.");
                        Aviso("No se guardó: código repetido.", true);
                        return;
                    }
                }
                Refrescar(p.Id);
                Aviso("✔ Guardado: " + codigo, false);
                Siguiente();
            }
            catch (Exception ex) { Ui.Error(ex, "Asignar código de barras"); }
        }

        void SinBarras()
        {
            var p = Actual();
            if (p == null) return;
            try
            {
                ProductoRepo.MarcarSinBarras(p.Id, true);
                Refrescar(p.Id);
                Aviso("Marcado sin código de barras: " + p.Descripcion, false);
                Siguiente();
            }
            catch (Exception ex) { Ui.Error(ex, "Marcar producto"); }
        }

        void Quitar()
        {
            var p = Actual();
            if (p == null) return;
            if (p.Codigo2 == "" && !p.SinBarras) { Aviso("Este producto no tiene código de barras.", true); return; }
            if (!Ui.Confirmar("¿Quitar el código de barras de «" + p.Descripcion + "»?" + (p.Codigo2 != "" ? "\n\n(" + p.Codigo2 + ")" : ""))) return;
            try
            {
                ProductoRepo.QuitarCodigoBarras(p.Id);
                ProductoRepo.MarcarSinBarras(p.Id, false);
                Refrescar(p.Id);
                Aviso("Quedó pendiente otra vez.", false);
            }
            catch (Exception ex) { Ui.Error(ex, "Quitar código"); }
        }

        void Etiqueta()
        {
            var p = Actual();
            if (p == null) return;
            FormVistaEtiqueta.Mostrar(p);
            _escaner.Focus();
        }
    }

    /// <summary>Vista previa de la etiqueta de un producto, con cuántas copias imprimir.</summary>
    public class FormVistaEtiqueta : Dialogo
    {
        readonly Producto _p;
        readonly NumericUpDown _copias;

        FormVistaEtiqueta(Producto p, Bitmap vista) : base("Etiqueta de " + p.Descripcion, 520, 640)
        {
            _p = p;
            Controls.Add(Ui.Etiqueta("Así sale la etiqueta (código corto " + p.Codigo + "):", 16, 12, Tema.Negrita));
            var panel = new Panel { Left = 16, Top = 44, Width = 488, Height = 460, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(8) };
            panel.Controls.Add(new PictureBox { Image = vista, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill });
            Controls.Add(panel);
            Controls.Add(Ui.Etiqueta("Copias:", 16, 524, Tema.Negrita));
            _copias = AdminUtil.Numero(100, 520, 80, 1, 99);
            _copias.Value = 1;
            Controls.Add(_copias);
            var imprimir = Ui.Boton("Imprimir", 216, 512, 150, 48, (s, e) => Imprimir());
            Tema.Pastilla(imprimir, true);
            Controls.Add(imprimir);
            Controls.Add(Ui.Boton("Cerrar (Esc)", 376, 512, 128, 48, (s, e) => Close()));
            Controls.Add(new Label { Text = "Sale por la impresora de tickets. Pegue la etiqueta en el producto o su bolsa.", Left = 16, Top = 574, Width = 488, Height = 46, Font = Tema.Normal, ForeColor = Tema.Humo, BackColor = Color.Transparent });
        }

        void Imprimir()
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                Etiquetas.Imprimir(new List<Producto> { _p }, (int)_copias.Value);
                Cursor = Cursors.Default;
                Ui.Info("Etiqueta enviada a la impresora.", "Etiquetas");
            }
            catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex, "No se pudo imprimir la etiqueta"); }
        }

        public static void Mostrar(Producto p)
        {
            Bitmap vista;
            try { vista = Etiquetas.Dibujar(p, Config.Actual); }
            catch (Exception ex) { Ui.Error(ex, "No se pudo preparar la etiqueta"); return; }
            using (vista)
            using (var f = new FormVistaEtiqueta(p, vista)) f.ShowDialog();
        }
    }
}
