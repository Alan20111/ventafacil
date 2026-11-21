using System;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>
    /// Alta y modificación de un producto, al estilo de la lista de costos de la semillera:
    /// costo del bulto ÷ lo que trae = costo por kg; tres precios (menudeo, medio mayoreo, mayoreo) con su % de ganancia.
    /// </summary>
    public class FormProducto : Dialogo
    {
        public Producto Producto { get; private set; }
        readonly bool _nuevo;
        readonly TextBox _codigo, _codigo2, _descripcion, _bulto, _trae, _costo, _existencia, _minimo;
        readonly TextBox[] _precio = new TextBox[3], _porc = new TextBox[3], _desde = new TextBox[3];
        readonly ComboBox _depto, _proveedor;
        readonly CheckBox _granel, _inventario, _activo, _mantener;
        readonly Label[] _unidad = new Label[3];
        bool _calculando;

        const int Menudeo = 0, Medio = 1, Mayoreo = 2;

        public FormProducto(Producto p) : base(p == null ? "Nuevo producto" : "Modificar producto", 740, 620)
        {
            _nuevo = p == null;
            Producto = p ?? new Producto { Granel = true };

            int x1 = 20, x2 = 170, y = 14, paso = 38;
            Controls.Add(Ui.Etiqueta("Código:", x1, y + 4, Tema.Negrita));
            _codigo = Ui.Caja(x2, y, 200, Tema.Mediana); Controls.Add(_codigo);
            Controls.Add(Ui.Etiqueta("Código alterno:", x2 + 220, y + 4));
            _codigo2 = Ui.Caja(x2 + 340, y, 210); Controls.Add(_codigo2);
            y += paso + 2;
            Controls.Add(Ui.Etiqueta("Producto:", x1, y + 4, Tema.Negrita));
            _descripcion = Ui.Caja(x2, y, 550, Tema.Mediana); _descripcion.MaxLength = 120; _descripcion.CharacterCasing = CharacterCasing.Upper; Controls.Add(_descripcion);
            y += paso + 2;
            Controls.Add(Ui.Etiqueta("Proveedor:", x1, y + 4));
            _proveedor = new ComboBox { Left = x2, Top = y, Width = 200, DropDownStyle = ComboBoxStyle.DropDown, Font = Tema.Normal, AutoCompleteMode = AutoCompleteMode.SuggestAppend, AutoCompleteSource = AutoCompleteSource.ListItems };
            Controls.Add(_proveedor);
            Controls.Add(Ui.Etiqueta("Grupo:", x2 + 220, y + 4));
            _depto = new ComboBox { Left = x2 + 280, Top = y, Width = 190, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            Controls.Add(_depto);
            Controls.Add(Ui.Boton("+ Nuevo", x2 + 476, y - 2, 74, 30, (s, e) => NuevoDepartamento()));
            y += paso + 8;

            // ---------------- Costo ----------------
            var gCosto = new GroupBox { Text = "Costo", Left = x1, Top = y, Width = 700, Height = 92, Font = Tema.Negrita };
            gCosto.Controls.Add(Ui.Etiqueta("Costo del bulto / caja:", 12, 28, Tema.Normal));
            _bulto = Num(170, 24); gCosto.Controls.Add(_bulto);
            gCosto.Controls.Add(Ui.Etiqueta("÷  trae:", 296, 28, Tema.Normal));
            _trae = Num(356, 24, 70); gCosto.Controls.Add(_trae);
            gCosto.Controls.Add(Ui.Etiqueta("=  costo por kg/pz:", 432, 28, Tema.Normal));
            _costo = Num(570, 24, 110); _costo.Font = Tema.Negrita; gCosto.Controls.Add(_costo);
            _mantener = new CheckBox { Text = "Al cambiar el costo, mantener el % de ganancia (los precios se actualizan solos)", Left = 12, Top = 58, Width = 670, Checked = true, Font = Tema.Normal };
            gCosto.Controls.Add(_mantener);
            Controls.Add(gCosto);
            y += 102;

            // ---------------- Precios ----------------
            var gPrecio = new GroupBox { Text = "Precios", Left = x1, Top = y, Width = 700, Height = 150, Font = Tema.Negrita };
            gPrecio.Controls.Add(Ui.Etiqueta("Precio", 200, 22, Tema.Negrita, Tema.Azul));
            gPrecio.Controls.Add(Ui.Etiqueta("% ganancia", 340, 22, Tema.Negrita, Tema.Azul));
            gPrecio.Controls.Add(Ui.Etiqueta("A partir de", 480, 22, Tema.Negrita, Tema.Azul));
            string[] nombres = { "Menudeo:", "Medio mayoreo:", "Mayoreo:" };
            for (int i = 0; i < 3; i++)
            {
                int fy = 46 + i * 32;
                gPrecio.Controls.Add(Ui.Etiqueta(nombres[i], 12, fy + 4, i == Menudeo ? Tema.Negrita : Tema.Normal));
                _precio[i] = Num(170, fy, 130); gPrecio.Controls.Add(_precio[i]);
                _porc[i] = Num(330, fy, 100); gPrecio.Controls.Add(_porc[i]);
                if (i == Menudeo) gPrecio.Controls.Add(Ui.Etiqueta("cualquier cantidad", 470, fy + 4, Tema.Normal, Color.Gray));
                else
                {
                    _desde[i] = Num(470, fy, 80); gPrecio.Controls.Add(_desde[i]);
                    _unidad[i] = Ui.Etiqueta("kg", 556, fy + 4, Tema.Normal); gPrecio.Controls.Add(_unidad[i]);
                }
            }
            _precio[Menudeo].Font = Tema.Mediana;
            Controls.Add(gPrecio);
            y += 160;

            _granel = new CheckBox { Text = "Se vende por kilo / a granel (pide peso)", Left = x1, Top = y, AutoSize = true, Font = Tema.Negrita };
            Controls.Add(_granel);
            _inventario = new CheckBox { Text = "Controlar existencia", Left = 380, Top = y, AutoSize = true, Checked = true };
            Controls.Add(_inventario);
            _activo = new CheckBox { Text = "Activo", Left = 580, Top = y, AutoSize = true, Checked = true };
            Controls.Add(_activo);
            y += 34;
            Controls.Add(Ui.Etiqueta(_nuevo ? "Existencia inicial:" : "Existencia actual:", x1, y + 4));
            _existencia = Num(x2, y); Controls.Add(_existencia);
            if (!_nuevo) { _existencia.ReadOnly = true; _existencia.BackColor = Tema.Gris; Controls.Add(Ui.Etiqueta("(se cambia en Inventario)", x2, y + 28, null, Color.Gray)); }
            Controls.Add(Ui.Etiqueta("Stock mínimo:", 380, y + 4));
            _minimo = Num(490, y); Controls.Add(_minimo);

            var ok = Ui.Boton("Guardar (Enter)", 430, ClientSize.Height - 52, 150, 40, (s, e) => Guardar());
            var no = Ui.Boton("Cancelar (Esc)", 590, ClientSize.Height - 52, 130, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;

            _bulto.TextChanged += (s, e) => { if (_bulto.Focused) CostoDesdeBulto(); };
            _trae.TextChanged += (s, e) => { if (_trae.Focused) CostoDesdeBulto(); };
            _costo.TextChanged += (s, e) => { if (_costo.Focused) CambioCosto(); };
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                _precio[i].TextChanged += (s, e) => { if (_precio[n].Focused) PorcentajeDe(n); };
                _porc[i].TextChanged += (s, e) => { if (_porc[n].Focused) PrecioDe(n); };
            }
            _granel.CheckedChanged += (s, e) => { foreach (var u in _unidad) if (u != null) u.Text = _granel.Checked ? "kg" : "pz"; };
            _inventario.CheckedChanged += (s, e) => { _minimo.Enabled = _inventario.Checked; if (_nuevo) _existencia.Enabled = _inventario.Checked; };

            CargarListas();
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

        static TextBox Num(int x, int y, int ancho = 120)
        {
            return new TextBox { Left = x, Top = y, Width = ancho, Font = Tema.Normal, TextAlign = HorizontalAlignment.Right };
        }

        static string D(decimal v) { return v > 0 ? Fmt.Dec(v) : ""; }
        static decimal V(TextBox t) { decimal d; return t != null && Fmt.TryDec(t.Text, out d) ? d : 0m; }

        void Llenar()
        {
            var p = Producto;
            _calculando = true;
            _codigo.Text = _nuevo ? SiguienteCodigo() : p.Codigo;
            _codigo2.Text = p.Codigo2;
            _descripcion.Text = p.Descripcion;
            _proveedor.Text = p.Proveedor;
            _bulto.Text = D(p.CostoBulto);
            _trae.Text = D(p.CantBulto);
            _costo.Text = D(p.Costo);
            _precio[Menudeo].Text = D(p.Precio);
            _precio[Medio].Text = D(p.PrecioMedio); _desde[Medio].Text = D(p.MedioDesde);
            _precio[Mayoreo].Text = D(p.PrecioMayoreo); _desde[Mayoreo].Text = D(p.MayoreoDesde);
            _granel.Checked = p.Granel;
            _inventario.Checked = p.UsaInventario;
            _activo.Checked = p.Activo;
            _existencia.Text = _nuevo ? "0" : Fmt.Dec(p.Existencia);
            _minimo.Text = Fmt.Dec(p.Minimo);
            _calculando = false;
            for (int i = 0; i < 3; i++) PorcentajeDe(i);
        }

        /// <summary>Código corto sugerido para productos sin código de barras (fácil de teclear en caja).</summary>
        static string SiguienteCodigo()
        {
            try
            {
                var max = Db.Scalar("SELECT MAX(CAST(codigo AS INTEGER)) FROM productos WHERE codigo GLOB '[0-9]*' AND LENGTH(codigo) <= 6");
                return ((max == null ? 0 : Convert.ToInt64(max)) + 1).ToString();
            }
            catch { return ""; }
        }

        void CargarListas()
        {
            try
            {
                _depto.Items.Clear();
                long sel = Producto.DepartamentoId;
                foreach (var d in DepartamentoRepo.Todos())
                {
                    _depto.Items.Add(d);
                    if (d.Id == sel || (sel == 0 && _depto.SelectedIndex < 0 && d.Nombre == "General")) _depto.SelectedItem = d;
                }
                if (_depto.SelectedIndex < 0 && _depto.Items.Count > 0) _depto.SelectedIndex = 0;
                foreach (var pr in ProductoRepo.Proveedores()) _proveedor.Items.Add(pr);
            }
            catch (Exception ex) { Ui.Error(ex, "Listas"); }
        }

        void NuevoDepartamento()
        {
            var n = Ui.PedirTexto("Nuevo grupo", "Nombre del grupo / departamento:");
            if (string.IsNullOrWhiteSpace(n)) return;
            try
            {
                long id;
                using (var cn = Db.Abrir()) id = DepartamentoRepo.ObtenerOCrear(cn, null, n);
                Producto.DepartamentoId = id;
                CargarListas();
            }
            catch (Exception ex) { Ui.Error(ex, "Crear grupo"); }
        }

        // ---------------- cálculos ----------------
        void CostoDesdeBulto()
        {
            if (_calculando) return;
            decimal bulto = V(_bulto), trae = V(_trae);
            if (bulto <= 0 || trae <= 0) return;
            _calculando = true;
            _costo.Text = Fmt.Dec(Math.Round(bulto / trae, 4));
            _calculando = false;
            CambioCosto();
        }

        /// <summary>Cambió el costo por kg: o se recalculan precios (mismo %), o se recalcula el %.</summary>
        void CambioCosto()
        {
            if (_calculando) return;
            for (int i = 0; i < 3; i++)
            {
                if (_mantener.Checked && V(_porc[i]) != 0) PrecioDe(i);
                else PorcentajeDe(i);
            }
        }

        void PrecioDe(int i)
        {
            if (_calculando) return;
            decimal costo = V(_costo);
            decimal pct;
            if (costo <= 0 || !Fmt.TryDec(_porc[i].Text, out pct)) return;
            _calculando = true;
            _precio[i].Text = Fmt.Dec(Fmt.R2(costo * (1 + pct / 100m)));
            _calculando = false;
        }

        void PorcentajeDe(int i)
        {
            if (_calculando) return;
            decimal costo = V(_costo), precio = V(_precio[i]);
            _calculando = true;
            _porc[i].Text = costo > 0 && precio > 0 ? Fmt.Dec(Math.Round((precio / costo - 1) * 100m, 1)) : "";
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
            if (_descripcion.Text.Trim() == "") { Ui.Aviso("Escriba el nombre del producto."); _descripcion.Focus(); return; }
            decimal precio, costo, bulto, trae, medio, medioDesde, mayoreo, mayoreoDesde, existencia, minimo;
            if (!Leer(_precio[Menudeo], "Precio menudeo", out precio, true)) return;
            if (!Leer(_costo, "Costo por kg/pz", out costo)) return;
            if (!Leer(_bulto, "Costo del bulto", out bulto)) return;
            if (!Leer(_trae, "Trae", out trae)) return;
            if (!Leer(_precio[Medio], "Precio medio mayoreo", out medio)) return;
            if (!Leer(_desde[Medio], "Medio mayoreo a partir de", out medioDesde)) return;
            if (!Leer(_precio[Mayoreo], "Precio mayoreo", out mayoreo)) return;
            if (!Leer(_desde[Mayoreo], "Mayoreo a partir de", out mayoreoDesde)) return;
            if (!Leer(_existencia, "Existencia", out existencia)) return;
            if (!Leer(_minimo, "Stock mínimo", out minimo)) return;
            if (precio == 0 && !Ui.Confirmar("El precio de menudeo es $0.00. ¿Guardar así?")) return;
            if (costo > 0 && precio > 0 && precio < costo && !Ui.Confirmar("El precio de menudeo es MENOR que el costo. ¿Guardar así?")) return;
            if (medio > 0 && medioDesde <= 0) { Ui.Aviso("Indique a partir de cuánto aplica el medio mayoreo."); _desde[Medio].Focus(); return; }
            if (mayoreo > 0 && mayoreoDesde <= 0) { Ui.Aviso("Indique a partir de cuánto aplica el mayoreo."); _desde[Mayoreo].Focus(); return; }
            if (medio > 0 && mayoreo > 0 && mayoreoDesde <= medioDesde) { Ui.Aviso("El mayoreo debe empezar en una cantidad MAYOR que el medio mayoreo."); _desde[Mayoreo].Focus(); return; }
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
                p.Proveedor = _proveedor.Text.Trim().ToUpperInvariant();
                var d = _depto.SelectedItem as Departamento;
                p.DepartamentoId = d != null ? d.Id : 0;
                p.CostoBulto = bulto; p.CantBulto = trae; p.Costo = costo;
                p.Precio = precio;
                p.PrecioMedio = medio; p.MedioDesde = medio > 0 ? medioDesde : 0;
                p.PrecioMayoreo = mayoreo; p.MayoreoDesde = mayoreo > 0 ? mayoreoDesde : 0;
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
