using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Printing;
using VentaFacil.UI;

namespace VentaFacil.Forms.Pos
{
    // =========================================================================
    //  F2 / F4  Búsqueda de artículos y consulta de precios
    // =========================================================================
    public class FormBuscar : Dialogo
    {
        readonly TextBox _txt;
        readonly DataGridView _grid;
        readonly Label _detalle;
        readonly bool _consulta;
        string _ultimoConsultado;
        List<Producto> _datos = new List<Producto>();
        readonly Timer _demora = new Timer { Interval = 250 };
        public Producto Elegido { get; private set; }

        public static Producto Elegir(IWin32Window dueno, string texto, bool consulta)
        {
            using (var f = new FormBuscar(texto, consulta))
                return f.ShowDialog(dueno) == DialogResult.OK ? f.Elegido : null;
        }

        FormBuscar(string texto, bool consulta) : base(consulta ? "F4 - Consultar precio" : "F2 - Buscar artículo", 820, 540)
        {
            _consulta = consulta;
            Controls.Add(Ui.Etiqueta(consulta ? "Escanee el artículo o escriba parte del nombre:" : "Escriba parte del nombre o código (varias palabras sirven):", 12, 10, Tema.Negrita, Tema.Azul));
            _txt = Ui.Caja(12, 34, 796, Tema.Captura);
            _txt.Text = texto;
            Controls.Add(_txt);
            _grid = new DataGridView { Left = 12, Top = 78, Width = 796, Height = consulta ? 300 : 390 };
            Ui.EstiloGrid(_grid);
            _grid.Columns.Add(Ui.Columna("codigo", "Código", 22));
            _grid.Columns.Add(Ui.Columna("descripcion", "Descripción", 50));
            _grid.Columns.Add(Ui.Columna("precio", "Precio", 14, true));
            _grid.Columns.Add(Ui.Columna("existencia", "Existencia", 14, true));
            Controls.Add(_grid);
            _detalle = new Label { Left = 12, Top = 386, Width = 796, Height = 100, Font = Tema.Mediana, ForeColor = Tema.Azul, Visible = consulta };
            Controls.Add(_detalle);
            Controls.Add(Ui.Etiqueta(consulta ? "Enter = vender   ·   Esc = solo consultar / salir" : "↑↓ = moverse   ·   Enter = vender   ·   Esc = salir", 12, ClientSize.Height - 34, Tema.Negrita, Tema.Humo));

            _demora.Tick += (s, e) => { _demora.Stop(); Llenar(); };
            _txt.TextChanged += (s, e) => { _demora.Stop(); _demora.Start(); };
            _txt.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up)
                {
                    e.SuppressKeyPress = true;
                    if (_grid.Rows.Count == 0) return;
                    int i = _grid.CurrentRow == null ? 0 : _grid.CurrentRow.Index + (e.KeyCode == Keys.Down ? 1 : -1);
                    i = Math.Max(0, Math.Min(_grid.Rows.Count - 1, i));
                    _grid.CurrentCell = _grid.Rows[i].Cells[1];
                }
                else if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; EnterEnTexto(); }
            };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Aceptar(); } };
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Aceptar(); };
            _grid.SelectionChanged += (s, e) => MostrarDetalle();
            Shown += (s, e) => { _txt.Focus(); _txt.SelectionStart = _txt.Text.Length; Llenar(); };
            FormClosed += (s, e) => _demora.Dispose();
        }

        void EnterEnTexto()
        {
            _demora.Stop();
            // ¿Es un código exacto? (lector de código de barras)
            string texto = _txt.Text.Trim();
            // En consulta, el segundo Enter sobre lo mismo = vender
            if (_consulta && texto != "" && texto == _ultimoConsultado && _grid.Rows.Count > 0) { Aceptar(); return; }
            var exacto = ProductoRepo.PorCodigo(texto);
            if (exacto != null)
            {
                _datos = new List<Producto> { exacto };
                Pintar();
                if (_consulta) { _ultimoConsultado = texto; _txt.SelectAll(); return; } // muestra el precio; otro Enter vende
                Elegido = exacto; DialogResult = DialogResult.OK; return;
            }
            Llenar();
            if (_consulta) { _ultimoConsultado = texto; _txt.SelectAll(); return; }
            if (_grid.Rows.Count > 0) Aceptar();
        }

        void Llenar()
        {
            try
            {
                _datos = _txt.Text.Trim() == "" ? ProductoRepo.Buscar("", false, 200) : ProductoRepo.Buscar(_txt.Text.Trim());
                Pintar();
            }
            catch (Exception ex) { Ui.Error(ex, "Búsqueda"); }
        }

        void Pintar()
        {
            _grid.Rows.Clear();
            foreach (var p in _datos)
                _grid.Rows.Add(p.Codigo, p.Descripcion + (p.Granel ? "  (kg)" : ""), Fmt.Dinero(p.Precio), p.UsaInventario ? Fmt.Dec(p.Existencia) : "-");
            if (_grid.Rows.Count > 0) _grid.CurrentCell = _grid.Rows[0].Cells[1];
            MostrarDetalle();
        }

        void MostrarDetalle()
        {
            if (!_consulta) return;
            if (_grid.CurrentRow == null || _grid.CurrentRow.Index >= _datos.Count) { _detalle.Text = ""; return; }
            var p = _datos[_grid.CurrentRow.Index];
            string u = p.Granel ? " kg" : " pz";
            _detalle.Text = p.Descripcion + "\nMenudeo: " + Fmt.Dinero(p.Precio) + (p.Granel ? " /kg" : "") +
                (p.PrecioMedio > 0 && p.MedioDesde > 0 ? "   Medio: " + Fmt.Dinero(p.PrecioMedio) + " (" + Fmt.Dec(p.MedioDesde) + u + "+)" : "") +
                (p.PrecioMayoreo > 0 && p.MayoreoDesde > 0 ? "   Mayoreo: " + Fmt.Dinero(p.PrecioMayoreo) + " (" + Fmt.Dec(p.MayoreoDesde) + u + "+)" : "");
        }

        void Aceptar()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Index >= _datos.Count) return;
            Elegido = _datos[_grid.CurrentRow.Index];
            DialogResult = DialogResult.OK;
        }
    }

    // =========================================================================
    //  Peso para artículos a granel
    // =========================================================================
    public class FormPeso : Dialogo
    {
        readonly TextBox _txt;
        readonly Label _importe;
        readonly Producto _p;
        bool _porImporte;
        public decimal Peso { get; private set; }

        public static decimal? Pedir(IWin32Window dueno, Producto p)
        {
            using (var f = new FormPeso(p)) return f.ShowDialog(dueno) == DialogResult.OK ? f.Peso : (decimal?)null;
        }

        FormPeso(Producto p) : base("Venta a granel", 480, 250)
        {
            _p = p;
            Controls.Add(Ui.Etiqueta(p.Descripcion, 16, 12, Tema.Mediana, Tema.Azul));
            Controls.Add(Ui.Etiqueta("Precio por kg: " + Fmt.Dinero(p.Precio), 16, 40, Tema.Negrita));
            var lbl = Ui.Etiqueta("Peso (kg):", 16, 78, Tema.Mediana);
            Controls.Add(lbl);
            _txt = Ui.Caja(170, 72, 290, Tema.Captura);
            _txt.TextAlign = HorizontalAlignment.Right;
            Controls.Add(_txt);
            _importe = Ui.Etiqueta("Importe: $0.00", 16, 122, Tema.Grande, Tema.Verde);
            Controls.Add(_importe);
            Controls.Add(Ui.Etiqueta("F2 = cambiar a importe en $  ·  F3 = leer báscula", 16, 166, null, Tema.Humo));
            var ok = Ui.Boton("Aceptar (Enter)", 196, 196, 130, 40, (s, e) => Aceptar());
            var no = Ui.Boton("Cancelar (Esc)", 332, 196, 130, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            _txt.TextChanged += (s, e) => Calcular();
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2) { _porImporte = !_porImporte; lbl.Text = _porImporte ? "Importe ($):" : "Peso (kg):"; Calcular(); _txt.Focus(); e.Handled = true; }
                if (e.KeyCode == Keys.F3) { LeerBascula(); e.Handled = true; }
            };
            Shown += (s, e) => { if (Config.Actual.BasculaActiva) LeerBascula(); _txt.Focus(); _txt.SelectAll(); };
        }

        void LeerBascula()
        {
            if (!Config.Actual.BasculaActiva) { Ui.Aviso("La báscula no está activada en Configuración."); return; }
            try
            {
                var peso = Bascula.LeerPeso(Config.Actual);
                _porImporte = false;
                _txt.Text = Fmt.Cant(peso, true);
                _txt.SelectAll();
            }
            catch (Exception ex) { Ui.Error(ex, "Báscula"); }
        }

        decimal PesoCapturado()
        {
            decimal v = Fmt.ParseDec(_txt.Text);
            if (_porImporte) return _p.Precio > 0 ? Math.Round(v / _p.Precio, 3) : 0;
            // si escriben gramos (p. ej. 750) se asume gramos
            if (v >= 100 && _txt.Text.IndexOf('.') < 0) v = v / 1000m;
            return v;
        }

        void Calcular()
        {
            decimal peso = PesoCapturado();
            _importe.Text = (_porImporte ? "Peso: " + Fmt.Cant(peso, true) + " kg   " : "Importe: ") + Fmt.Dinero(Fmt.R2(peso * _p.Precio));
        }

        void Aceptar()
        {
            decimal peso = PesoCapturado();
            if (peso <= 0) { Ui.Aviso("Escriba el peso."); _txt.Focus(); return; }
            Peso = peso;
            DialogResult = DialogResult.OK;
        }
    }

    // =========================================================================
    //  Cobro (+)
    // =========================================================================
    public class FormCobro : Dialogo
    {
        readonly decimal _total;
        readonly TextBox _paga, _ref;
        readonly Label _cambio;
        public string FormaPago { get; private set; }
        public decimal PagoCon { get; private set; }
        public decimal Cambio { get; private set; }
        public string Referencia { get { return _ref.Text.Trim(); } }

        public FormCobro(decimal total) : base("Cobrar", 560, 420)
        {
            _total = total;
            var cab = new PanelTitulo { Titulo = "Total a cobrar", Height = 44 };
            Controls.Add(cab);
            Controls.Add(new Label { Text = Fmt.Dinero(total), Font = Tema.Enorme, ForeColor = Tema.Azul, Left = 16, Top = 52, Width = 528, Height = 64, TextAlign = ContentAlignment.MiddleRight });
            Controls.Add(Ui.Etiqueta("Paga con:", 16, 134, Tema.Grande));
            _paga = Ui.Caja(200, 128, 344, new Font("Segoe UI", 24f, FontStyle.Bold));
            _paga.TextAlign = HorizontalAlignment.Right;
            _paga.Text = Fmt.Dec(total);
            Controls.Add(_paga);
            Controls.Add(Ui.Etiqueta("Cambio:", 16, 190, Tema.Grande, Tema.Verde));
            _cambio = new Label { Text = "$0.00", Font = Tema.Enorme, ForeColor = Tema.Verde, Left = 200, Top = 178, Width = 344, Height = 60, TextAlign = ContentAlignment.MiddleRight };
            Controls.Add(_cambio);
            Controls.Add(Ui.Etiqueta("Referencia / autorización (tarjeta o transferencia):", 16, 244, null, Tema.Humo));
            _ref = Ui.Caja(16, 266, 528);
            Controls.Add(_ref);

            int x = 16, y = 306;
            Func<string, string, Button> b = (texto, forma) =>
            {
                var bt = Ui.Boton(texto, x, y, 130, 50, (s, e) => Cobrar(forma));
                x += 134;
                Controls.Add(bt);
                return bt;
            };
            var efectivo = b("F2 / Enter\nEfectivo", "EFECTIVO");
            efectivo.BackColor = Tema.Azul; efectivo.ForeColor = Color.White;
            b("F3\nTarjeta", "TARJETA");
            b("F4\nTransferencia", "TRANSFERENCIA");
            b("F5\nVales", "VALES");
            Controls.Add(Ui.Etiqueta("Esc = regresar a la venta", 16, 386, null, Tema.Humo));

            _paga.TextChanged += (s, e) => Calcular();
            _paga.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Cobrar("EFECTIVO"); } };
            KeyDown += (s, e) =>
            {
                switch (e.KeyCode)
                {
                    case Keys.F2: e.Handled = true; Cobrar("EFECTIVO"); break;
                    case Keys.F3: e.Handled = true; Cobrar("TARJETA"); break;
                    case Keys.F4: e.Handled = true; Cobrar("TRANSFERENCIA"); break;
                    case Keys.F5: e.Handled = true; Cobrar("VALES"); break;
                }
            };
            Shown += (s, e) => { _paga.Focus(); _paga.SelectAll(); };
            Calcular();
        }

        void Calcular()
        {
            decimal paga = Fmt.ParseDec(_paga.Text);
            decimal cambio = paga - _total;
            _cambio.Text = cambio >= 0 ? Fmt.Dinero(cambio) : "Faltan " + Fmt.Dinero(-cambio);
            _cambio.ForeColor = cambio >= 0 ? Tema.Verde : Tema.Rojo;
        }

        void Cobrar(string forma)
        {
            decimal paga;
            if (!Fmt.TryDec(_paga.Text, out paga) || _paga.Text.Trim() == "") paga = _total;
            if (forma == "EFECTIVO")
            {
                if (paga < _total) { Ui.Aviso("El pago es menor al total."); _paga.Focus(); _paga.SelectAll(); return; }
                PagoCon = paga;
                Cambio = Fmt.R2(paga - _total);
            }
            else
            {
                // tarjeta / transferencia / vales: se cobra exacto
                PagoCon = _total;
                Cambio = 0;
            }
            FormaPago = forma;
            DialogResult = DialogResult.OK;
        }
    }

    // =========================================================================
    //  F6 Descuentos y ajustes de precio
    // =========================================================================
    public class FormDescuento : Dialogo
    {
        readonly TextBox _valor;
        readonly RadioButton _rPorc, _rImporte, _rPrecio, _rTotal;
        public bool ATodaLaVenta { get; private set; }
        public decimal Porcentaje { get; private set; }
        public decimal Importe { get; private set; }
        public decimal? NuevoPrecio { get; private set; }

        public FormDescuento(LineaVenta l, decimal total) : base("F6 - Descuento / ajuste de precio", 480, 330)
        {
            Controls.Add(Ui.Etiqueta(l == null ? "(sin artículo seleccionado)" : l.Descripcion, 16, 12, Tema.Mediana, Tema.Azul));
            if (l != null) Controls.Add(Ui.Etiqueta("Cantidad " + Fmt.Cant(l.Cantidad, l.Granel) + "  ×  " + Fmt.Dinero(l.Precio), 16, 40));
            _rPorc = new RadioButton { Text = "F2  % de descuento al artículo", Left = 16, Top = 70, Width = 440, Checked = l != null, Enabled = l != null, Font = Tema.Negrita };
            _rImporte = new RadioButton { Text = "F3  Descuento en pesos al artículo", Left = 16, Top = 98, Width = 440, Enabled = l != null, Font = Tema.Negrita };
            _rPrecio = new RadioButton { Text = "F4  Cambiar precio unitario del artículo", Left = 16, Top = 126, Width = 440, Enabled = l != null, Font = Tema.Negrita };
            _rTotal = new RadioButton { Text = "F5  % de descuento a TODA la venta (" + Fmt.Dinero(total) + ")", Left = 16, Top = 154, Width = 440, Checked = l == null, Font = Tema.Negrita };
            Controls.AddRange(new Control[] { _rPorc, _rImporte, _rPrecio, _rTotal });
            Controls.Add(Ui.Etiqueta("Valor:", 16, 200, Tema.Mediana));
            _valor = Ui.Caja(100, 194, 200, Tema.Captura);
            _valor.TextAlign = HorizontalAlignment.Right;
            Controls.Add(_valor);
            Controls.Add(Ui.Etiqueta("Valor 0 = quitar descuento", 310, 204, null, Tema.Humo));
            var ok = Ui.Boton("Aplicar (Enter)", 196, 270, 130, 40, (s, e) => Aceptar());
            var no = Ui.Boton("Cancelar (Esc)", 332, 270, 130, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2 && _rPorc.Enabled) _rPorc.Checked = true;
                else if (e.KeyCode == Keys.F3 && _rImporte.Enabled) _rImporte.Checked = true;
                else if (e.KeyCode == Keys.F4 && _rPrecio.Enabled) _rPrecio.Checked = true;
                else if (e.KeyCode == Keys.F5) _rTotal.Checked = true;
                else return;
                e.Handled = true; _valor.Focus(); _valor.SelectAll();
            };
            Shown += (s, e) => _valor.Focus();
        }

        void Aceptar()
        {
            decimal v;
            if (!Fmt.TryDec(_valor.Text, out v) || v < 0) { Ui.Aviso("Escriba un valor válido."); _valor.Focus(); return; }
            if ((_rPorc.Checked || _rTotal.Checked) && v > 100) { Ui.Aviso("El porcentaje no puede ser mayor a 100."); return; }
            ATodaLaVenta = _rTotal.Checked;
            if (_rPorc.Checked || _rTotal.Checked) Porcentaje = v;
            else if (_rImporte.Checked) Importe = v;
            else if (_rPrecio.Checked) { if (v <= 0) { Ui.Aviso("El precio debe ser mayor a cero."); return; } NuevoPrecio = v; }
            DialogResult = DialogResult.OK;
        }
    }

    // =========================================================================
    //  F8 Artículo varios (sin código / venta por departamento)
    // =========================================================================
    public class FormArticuloComun : Dialogo
    {
        readonly TextBox _desc, _precio, _cant;
        public string Descripcion { get { return _desc.Text.Trim() == "" ? "VARIOS" : _desc.Text.Trim(); } }
        public decimal Precio { get; private set; }
        public decimal Cantidad { get; private set; }

        public FormArticuloComun(decimal cantidad) : base("F8 - Artículo varios", 460, 250)
        {
            Controls.Add(Ui.Etiqueta("Descripción:", 16, 20, Tema.Negrita));
            _desc = Ui.Caja(140, 16, 300); _desc.Text = "VARIOS"; Controls.Add(_desc);
            Controls.Add(Ui.Etiqueta("Precio:", 16, 70, Tema.Negrita));
            _precio = Ui.Caja(140, 62, 300, Tema.Captura); _precio.TextAlign = HorizontalAlignment.Right; Controls.Add(_precio);
            Controls.Add(Ui.Etiqueta("Cantidad:", 16, 124, Tema.Negrita));
            _cant = Ui.Caja(140, 116, 300, Tema.Captura); _cant.TextAlign = HorizontalAlignment.Right; _cant.Text = Fmt.Dec(cantidad); Controls.Add(_cant);
            var ok = Ui.Boton("Agregar (Enter)", 176, 190, 130, 40, (s, e) => Aceptar());
            var no = Ui.Boton("Cancelar (Esc)", 312, 190, 130, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            Shown += (s, e) => _precio.Focus();
        }

        void Aceptar()
        {
            decimal p, c;
            if (!Fmt.TryDec(_precio.Text, out p) || p <= 0) { Ui.Aviso("Escriba el precio."); _precio.Focus(); return; }
            if (!Fmt.TryDec(_cant.Text, out c) || c <= 0) { Ui.Aviso("Escriba la cantidad."); _cant.Focus(); return; }
            Precio = p; Cantidad = c;
            DialogResult = DialogResult.OK;
        }
    }

    // =========================================================================
    //  F5 Ventas en espera
    // =========================================================================
    public class FormEspera : Dialogo
    {
        readonly DataGridView _grid;
        readonly TextBox _nota;
        readonly List<SuspendidaRepo.Item> _items;
        public long Elegida { get; private set; }
        public string Nota { get { return _nota.Text.Trim(); } }

        /// <summary>DialogResult.Yes = poner en espera la actual; OK = recuperar Elegida.</summary>
        public FormEspera(bool hayVenta) : base("F5 - Ventas en espera", 640, 420)
        {
            Controls.Add(Ui.Etiqueta("Nota (nombre del cliente, opcional):", 16, 14, Tema.Negrita));
            _nota = Ui.Caja(16, 38, 400); _nota.Enabled = hayVenta; Controls.Add(_nota);
            var poner = Ui.Boton("F3  Poner en espera\nla venta actual", 428, 12, 196, 52, (s, e) => Poner());
            poner.Enabled = hayVenta;
            Controls.Add(poner);
            Controls.Add(Ui.Etiqueta("Ventas en espera (Enter = recuperar):", 16, 80, Tema.Negrita, Tema.Azul));
            _grid = new DataGridView { Left = 16, Top = 104, Width = 608, Height = 260 };
            Ui.EstiloGrid(_grid);
            _grid.Columns.Add(Ui.Columna("id", "No.", 10));
            _grid.Columns.Add(Ui.Columna("fecha", "Hora", 20));
            _grid.Columns.Add(Ui.Columna("nota", "Nota", 40));
            _grid.Columns.Add(Ui.Columna("total", "Total", 20, true));
            Controls.Add(_grid);
            _items = SuspendidaRepo.Todas();
            foreach (var i in _items) _grid.Rows.Add(i.Id, i.Fecha.ToString("dd/MM HH:mm"), i.Nota + (i.Usuario != "" ? " (" + i.Usuario + ")" : ""), Fmt.Dinero(i.Total));
            Controls.Add(Ui.Etiqueta("Esc = salir", 16, 376, null, Tema.Humo));
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Recuperar(); } };
            _grid.CellDoubleClick += (s, e) => Recuperar();
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F3 && hayVenta) { e.Handled = true; Poner(); } };
            Shown += (s, e) => { if (hayVenta) _nota.Focus(); else _grid.Focus(); };
            _nota.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Poner(); } };
        }

        void Poner() { DialogResult = DialogResult.Yes; }

        void Recuperar()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Index >= _items.Count) return;
            Elegida = _items[_grid.CurrentRow.Index].Id;
            DialogResult = DialogResult.OK;
        }
    }

    // =========================================================================
    //  F7 Entradas y salidas de efectivo
    // =========================================================================
    public class FormMovimiento : Dialogo
    {
        readonly RadioButton _entrada, _salida;
        readonly TextBox _monto, _concepto;
        public string Tipo { get { return _entrada.Checked ? "ENTRADA" : "SALIDA"; } }
        public decimal Monto { get; private set; }
        public string Concepto { get { return _concepto.Text.Trim(); } }

        public FormMovimiento() : base("F7 - Entradas y salidas de efectivo", 480, 290)
        {
            _salida = new RadioButton { Text = "F3  SALIDA / retiro (pago a proveedor, gasto, retiro)", Left = 16, Top = 14, Width = 450, Checked = true, Font = Tema.Negrita };
            _entrada = new RadioButton { Text = "F2  ENTRADA (más cambio / fondo)", Left = 16, Top = 42, Width = 450, Font = Tema.Negrita };
            Controls.Add(_salida); Controls.Add(_entrada);
            Controls.Add(Ui.Etiqueta("Importe:", 16, 90, Tema.Mediana));
            _monto = Ui.Caja(130, 84, 330, Tema.Captura); _monto.TextAlign = HorizontalAlignment.Right; Controls.Add(_monto);
            Controls.Add(Ui.Etiqueta("Concepto:", 16, 146, Tema.Mediana));
            _concepto = Ui.Caja(130, 142, 330); Controls.Add(_concepto);
            var ok = Ui.Boton("Guardar (Enter)", 196, 226, 130, 44, (s, e) => Aceptar());
            var no = Ui.Boton("Cancelar (Esc)", 332, 226, 130, 44, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.F2) { _entrada.Checked = true; e.Handled = true; _monto.Focus(); }
                if (e.KeyCode == Keys.F3) { _salida.Checked = true; e.Handled = true; _monto.Focus(); }
            };
            Shown += (s, e) => _monto.Focus();
        }

        void Aceptar()
        {
            decimal m;
            if (!Fmt.TryDec(_monto.Text, out m) || m <= 0) { Ui.Aviso("Escriba el importe."); _monto.Focus(); return; }
            if (Concepto == "") { Ui.Aviso("Escriba el concepto (para qué es el dinero)."); _concepto.Focus(); return; }
            Monto = m;
            DialogResult = DialogResult.OK;
        }
    }
}
