using System;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Alta y modificación de un producto.</summary>
    public class FormProducto : Dialogo
    {
        public Producto Producto { get; private set; }
        readonly bool _nuevo;
        readonly TextBox _codigo, _codigo2, _descripcion, _precio, _costo, _utilidad, _mayoreo, _desde, _existencia, _minimo;
        readonly ComboBox _depto;
        readonly CheckBox _granel, _inventario, _activo;
        bool _calculando;

        public FormProducto(Producto p) : base(p == null ? "Nuevo producto" : "Modificar producto", 640, 520)
        {
            _nuevo = p == null;
            Producto = p ?? new Producto();

            int x1 = 20, x2 = 190, y = 16, paso = 40;
            Controls.Add(Ui.Etiqueta("Código de barras:", x1, y + 4, Tema.Negrita));
            _codigo = Ui.Caja(x2, y, 260, Tema.Mediana); Controls.Add(_codigo);
            y += paso;
            Controls.Add(Ui.Etiqueta("Código alterno:", x1, y + 4));
            _codigo2 = Ui.Caja(x2, y, 260); Controls.Add(_codigo2);
            y += paso;
            Controls.Add(Ui.Etiqueta("Descripción:", x1, y + 4, Tema.Negrita));
            _descripcion = Ui.Caja(x2, y, 420, Tema.Mediana); _descripcion.MaxLength = 120; Controls.Add(_descripcion);
            y += paso;
            Controls.Add(Ui.Etiqueta("Departamento:", x1, y + 4));
            _depto = new ComboBox { Left = x2, Top = y, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            Controls.Add(_depto);
            Controls.Add(Ui.Boton("+ Nuevo", x2 + 270, y - 2, 90, 30, (s, e) => NuevoDepartamento()));
            y += paso + 6;

            Controls.Add(Ui.Etiqueta("Costo:", x1, y + 4));
            _costo = Num(x2, y); Controls.Add(_costo);
            Controls.Add(Ui.Etiqueta("% Utilidad:", x2 + 140, y + 4));
            _utilidad = Num(x2 + 240, y); Controls.Add(_utilidad);
            y += paso;
            Controls.Add(Ui.Etiqueta("Precio de venta:", x1, y + 4, Tema.Negrita));
            _precio = Num(x2, y); _precio.Font = Tema.Mediana; Controls.Add(_precio);
            y += paso;
            Controls.Add(Ui.Etiqueta("Precio mayoreo:", x1, y + 4));
            _mayoreo = Num(x2, y); Controls.Add(_mayoreo);
            Controls.Add(Ui.Etiqueta("a partir de:", x2 + 140, y + 4));
            _desde = Num(x2 + 240, y); Controls.Add(_desde);
            Controls.Add(Ui.Etiqueta("piezas/kg", x2 + 370, y + 4, null, Color.Gray));
            y += paso + 6;

            _granel = new CheckBox { Text = "Se vende por kilo / a granel (pide peso)", Left = x1, Top = y, AutoSize = true, Font = Tema.Negrita };
            Controls.Add(_granel);
            y += 30;
            _inventario = new CheckBox { Text = "Controlar existencia (inventario)", Left = x1, Top = y, AutoSize = true, Checked = true };
            Controls.Add(_inventario);
            _activo = new CheckBox { Text = "Activo", Left = x2 + 240, Top = y, AutoSize = true, Checked = true };
            Controls.Add(_activo);
            y += 34;
            Controls.Add(Ui.Etiqueta(_nuevo ? "Existencia inicial:" : "Existencia actual:", x1, y + 4));
            _existencia = Num(x2, y); Controls.Add(_existencia);
            if (!_nuevo) { _existencia.ReadOnly = true; _existencia.BackColor = Tema.Gris; }
            Controls.Add(Ui.Etiqueta("Mínimo:", x2 + 140, y + 4));
            _minimo = Num(x2 + 240, y); Controls.Add(_minimo);
            if (!_nuevo) Controls.Add(Ui.Etiqueta("(se cambia en Inventario)", x1, y + 28, null, Color.Gray));

            var ok = Ui.Boton("Guardar (Enter)", 330, ClientSize.Height - 52, 150, 40, (s, e) => Guardar());
            var no = Ui.Boton("Cancelar (Esc)", 490, ClientSize.Height - 52, 130, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;

            _costo.TextChanged += (s, e) => CalcularUtilidad();
            _utilidad.TextChanged += (s, e) => CalcularPrecio();
            _precio.TextChanged += (s, e) => CalcularUtilidad();
            _inventario.CheckedChanged += (s, e) => { _minimo.Enabled = _inventario.Checked; if (_nuevo) _existencia.Enabled = _inventario.Checked; };

            CargarDepartamentos(Producto.DepartamentoId);
            Llenar();
            Shown += (s, e) => { (_nuevo ? _codigo : _descripcion).Focus(); };
        }

        /// <summary>El lector de código manda Enter: en los códigos sólo pasa al siguiente campo.</summary>
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter && (ActiveControl == _codigo || ActiveControl == _codigo2))
            {
                SelectNextControl(ActiveControl, true, true, true, true);
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        static TextBox Num(int x, int y)
        {
            return new TextBox { Left = x, Top = y, Width = 120, Font = Tema.Normal, TextAlign = HorizontalAlignment.Right };
        }

        void Llenar()
        {
            var p = Producto;
            _calculando = true;
            _codigo.Text = p.Codigo;
            _codigo2.Text = p.Codigo2;
            _descripcion.Text = p.Descripcion;
            _costo.Text = _nuevo ? "" : Fmt.Dec(p.Costo);
            _precio.Text = _nuevo ? "" : Fmt.Dec(p.Precio);
            _mayoreo.Text = p.PrecioMayoreo > 0 ? Fmt.Dec(p.PrecioMayoreo) : "";
            _desde.Text = p.MayoreoDesde > 0 ? Fmt.Dec(p.MayoreoDesde) : "";
            _granel.Checked = p.Granel;
            _inventario.Checked = p.UsaInventario;
            _activo.Checked = p.Activo;
            _existencia.Text = _nuevo ? "0" : Fmt.Dec(p.Existencia);
            _minimo.Text = Fmt.Dec(p.Minimo);
            _calculando = false;
            CalcularUtilidad();
        }

        void CargarDepartamentos(long seleccionar)
        {
            try
            {
                _depto.Items.Clear();
                foreach (var d in DepartamentoRepo.Todos())
                {
                    _depto.Items.Add(d);
                    if (d.Id == seleccionar || (seleccionar == 0 && _depto.SelectedIndex < 0 && d.Nombre == "General")) _depto.SelectedItem = d;
                }
                if (_depto.SelectedIndex < 0 && _depto.Items.Count > 0) _depto.SelectedIndex = 0;
            }
            catch (Exception ex) { Ui.Error(ex, "Departamentos"); }
        }

        void NuevoDepartamento()
        {
            var n = Ui.PedirTexto("Nuevo departamento", "Nombre del departamento:");
            if (string.IsNullOrWhiteSpace(n)) return;
            try
            {
                long id;
                using (var cn = Db.Abrir()) id = DepartamentoRepo.ObtenerOCrear(cn, null, n);
                CargarDepartamentos(id);
            }
            catch (Exception ex) { Ui.Error(ex, "Crear departamento"); }
        }

        void CalcularPrecio()
        {
            if (_calculando) return;
            decimal costo, util;
            if (!Fmt.TryDec(_costo.Text, out costo) || !Fmt.TryDec(_utilidad.Text, out util) || costo <= 0 || !_utilidad.Focused) return;
            _calculando = true;
            _precio.Text = Fmt.Dec(Fmt.R2(costo * (1 + util / 100m)));
            _calculando = false;
        }

        void CalcularUtilidad()
        {
            if (_calculando) return;
            decimal costo, precio;
            _calculando = true;
            if (Fmt.TryDec(_costo.Text, out costo) && Fmt.TryDec(_precio.Text, out precio) && costo > 0 && precio > 0)
                _utilidad.Text = Fmt.Dec(Math.Round((precio / costo - 1) * 100m, 2));
            else if (!_utilidad.Focused) _utilidad.Text = "";
            _calculando = false;
        }

        bool Leer(TextBox t, string nombre, out decimal valor, bool obligatorio = false)
        {
            valor = 0;
            if (t.Text.Trim() == "" && !obligatorio) return true;
            if (!Fmt.TryDec(t.Text, out valor) || valor < 0)
            {
                Ui.Aviso("Revise el campo \"" + nombre + "\": debe ser un número.");
                t.Focus(); t.SelectAll();
                return false;
            }
            return true;
        }

        void Guardar()
        {
            string codigo = _codigo.Text.Trim();
            if (codigo == "") { Ui.Aviso("Escriba o escanee el código del producto."); _codigo.Focus(); return; }
            if (_descripcion.Text.Trim() == "") { Ui.Aviso("Escriba la descripción."); _descripcion.Focus(); return; }
            decimal precio, costo, mayoreo, desde, existencia, minimo;
            if (!Leer(_precio, "Precio de venta", out precio, true)) return;
            if (!Leer(_costo, "Costo", out costo)) return;
            if (!Leer(_mayoreo, "Precio mayoreo", out mayoreo)) return;
            if (!Leer(_desde, "A partir de", out desde)) return;
            if (!Leer(_existencia, "Existencia", out existencia)) return;
            if (!Leer(_minimo, "Mínimo", out minimo)) return;
            if (precio == 0 && !Ui.Confirmar("El precio de venta es $0.00. ¿Guardar así?")) return;
            if (costo > 0 && precio > 0 && precio < costo && !Ui.Confirmar("El precio de venta es MENOR que el costo. ¿Guardar así?")) return;
            if (mayoreo > 0 && desde <= 0) { Ui.Aviso("Indique a partir de qué cantidad aplica el precio de mayoreo."); _desde.Focus(); return; }
            try
            {
                if (ProductoRepo.CodigoExiste(codigo, Producto.Id)) { Ui.Aviso("Ya existe otro producto con el código \"" + codigo + "\"."); _codigo.Focus(); _codigo.SelectAll(); return; }
                string c2 = _codigo2.Text.Trim();
                if (c2 != "" && (c2.Equals(codigo, StringComparison.OrdinalIgnoreCase) || ProductoRepo.CodigoExiste(c2, Producto.Id)))
                { Ui.Aviso("El código alterno \"" + c2 + "\" ya está en uso."); _codigo2.Focus(); return; }

                var p = Producto;
                p.Codigo = codigo;
                p.Codigo2 = c2;
                p.Descripcion = _descripcion.Text.Trim();
                var d = _depto.SelectedItem as Departamento;
                p.DepartamentoId = d != null ? d.Id : 0;
                p.Precio = precio; p.Costo = costo; p.PrecioMayoreo = mayoreo; p.MayoreoDesde = mayoreo > 0 ? desde : 0;
                p.Granel = _granel.Checked; p.UsaInventario = _inventario.Checked; p.Activo = _activo.Checked;
                p.Minimo = minimo;
                if (_nuevo) p.Existencia = _inventario.Checked ? existencia : 0;
                ProductoRepo.Guardar(p, Sesion.Usuario != null ? Sesion.Usuario.Id : 0);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Ui.Error(ex, "Guardar producto"); }
        }
    }
}
