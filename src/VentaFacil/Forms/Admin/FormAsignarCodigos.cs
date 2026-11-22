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
    /// Lo que no lleva código de barras (granel, verdura) se marca con F3 o se le imprime una etiqueta con F5.
    /// </summary>
    public class FormAsignarCodigos : Dialogo
    {
        readonly DataGridView _grid;
        readonly TextBox _buscar, _escaner;
        readonly ComboBox _grupo;
        readonly CheckBox _conCodigo, _marcados;
        readonly Label _producto, _resumen, _mensaje;
        List<Producto> _todos = new List<Producto>();
        bool _llenando;

        public FormAsignarCodigos() : base("Códigos de barras", 1040, 690)
        {
            Controls.Add(Ui.Etiqueta("Asignar códigos de barras", 16, 10, Tema.Mediana, Tema.Azul));
            _resumen = Ui.Etiqueta("", 420, 16, Tema.Negrita, Tema.Humo);
            Controls.Add(_resumen);

            // ---- filtros ----
            Controls.Add(Ui.Etiqueta("Buscar:", 16, 56, Tema.Negrita));
            _buscar = Ui.Caja(96, 52, 280, Tema.Normal);
            Controls.Add(_buscar);
            Controls.Add(Ui.Etiqueta("Grupo:", 394, 56, Tema.Negrita));
            _grupo = new ComboBox { Left = 468, Top = 52, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            Controls.Add(_grupo);
            _conCodigo = new CheckBox { Text = "Ver también los que ya tienen", Left = 734, Top = 54, AutoSize = true };
            _marcados = new CheckBox { Text = "Ver los marcados «sin código»", Left = 734, Top = 80, AutoSize = true };
            Controls.Add(_conCodigo); Controls.Add(_marcados);

            // ---- lista ----
            _grid = new DataGridView { Left = 16, Top = 112, Width = 1008, Height = 290 };
            Ui.EstiloGrid(_grid);
            _grid.Columns.Add(Ui.Columna("producto", "Producto", 40));
            _grid.Columns.Add(Ui.Columna("grupo", "Grupo", 22));
            _grid.Columns.Add(Ui.Columna("corto", "Código corto", 12));
            _grid.Columns.Add(Ui.Columna("barras", "Código de barras", 22));
            _grid.Columns.Add(Ui.Columna("estado", "Estado", 14));
            Controls.Add(_grid);

            // ---- escaneo ----
            Controls.Add(Ui.Etiqueta("Escanee el empaque de:", 16, 414, Tema.Negrita));
            _producto = new Label { Left = 16, Top = 440, Width = 1008, Height = 40, Font = Tema.Mediana, ForeColor = Tema.Azul, BackColor = Color.Transparent, Text = "" };
            Controls.Add(_producto);
            _escaner = new TextBox { Left = 16, Top = 488, Width = 560, Font = Tema.Captura, MaxLength = 40 };
            Controls.Add(_escaner);
            _mensaje = new Label { Left = 592, Top = 490, Width = 432, Height = 44, Font = Tema.Negrita, ForeColor = Tema.Verde, BackColor = Color.Transparent };
            Controls.Add(_mensaje);

            // ---- botones ----
            int by = 556;
            Controls.Add(Ui.Boton("F2  Saltar", 16, by, 150, 46, (s, e) => Siguiente()));
            Controls.Add(Ui.Boton("F3  Sin código de barras", 176, by, 260, 46, (s, e) => SinBarras()));
            Controls.Add(Ui.Boton("F4  Quitar código", 446, by, 200, 46, (s, e) => Quitar()));
            Controls.Add(Ui.Boton("F5  Etiqueta…", 656, by, 190, 46, (s, e) => Etiqueta()));
            var cerrar = Ui.Boton("Cerrar (Esc)", 856, by, 168, 46, (s, e) => Close());
            Controls.Add(cerrar);
            Controls.Add(Ui.Etiqueta("Tome el producto y pase el lector: el código se guarda solo y salta al siguiente. Flechas ↑ ↓ para moverse.", 16, 620, null, Tema.Humo));
            Controls.Add(Ui.Etiqueta("Lo que no trae código (granel, verdura): F3 lo marca, o F5 imprime una etiqueta con el código corto.", 16, 646, null, Tema.Humo));

            _buscar.TextChanged += (s, e) => Filtrar(null);
            _grupo.SelectedIndexChanged += (s, e) => Filtrar(null);
            _conCodigo.CheckedChanged += (s, e) => Filtrar(null);
            _marcados.CheckedChanged += (s, e) => Filtrar(null);
            _grid.SelectionChanged += (s, e) => { if (!_llenando) { MostrarActual(); BeginInvoke((Action)(() => _escaner.Focus())); } };
            _grid.CellClick += (s, e) => _escaner.Focus();
            _escaner.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Asignar(); }
                else if (e.KeyCode == Keys.Down) { e.SuppressKeyPress = true; Mover(1); }
                else if (e.KeyCode == Keys.Up) { e.SuppressKeyPress = true; Mover(-1); }
            };
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2) { e.Handled = true; Siguiente(); }
                else if (e.KeyCode == Keys.F3) { e.Handled = true; SinBarras(); }
                else if (e.KeyCode == Keys.F4) { e.Handled = true; Quitar(); }
                else if (e.KeyCode == Keys.F5) { e.Handled = true; Etiqueta(); }
            };
            Shown += (s, e) => { Cargar(); _escaner.Focus(); };
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
                _resumen.Text = "Con código de barras: " + con + "     Pendientes: " + pend + "     Sin código: " + marc;
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
            var panel = new Panel { Left = 16, Top = 44, Width = 488, Height = 460, AutoScroll = true, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            panel.Controls.Add(new PictureBox { Image = vista, SizeMode = PictureBoxSizeMode.AutoSize, Left = 0, Top = 0 });
            Controls.Add(panel);
            Controls.Add(Ui.Etiqueta("Copias:", 16, 524, Tema.Negrita));
            _copias = AdminUtil.Numero(100, 520, 80, 1, 99);
            _copias.Value = 1;
            Controls.Add(_copias);
            var imprimir = Ui.Boton("Imprimir", 216, 512, 150, 48, (s, e) => Imprimir());
            Tema.Pastilla(imprimir, true);
            Controls.Add(imprimir);
            Controls.Add(Ui.Boton("Cerrar (Esc)", 376, 512, 128, 48, (s, e) => Close()));
            Controls.Add(Ui.Etiqueta("Sale por la impresora de tickets configurada. Pegue la etiqueta en el producto o su bolsa.", 16, 576, null, Tema.Humo));
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
