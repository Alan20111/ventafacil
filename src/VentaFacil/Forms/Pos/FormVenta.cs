using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Printing;
using VentaFacil.UI;

namespace VentaFacil.Forms.Pos
{
    /// <summary>
    /// Pantalla principal de venta. Pensada para usarse 100% con teclado y lector de código de barras.
    /// </summary>
    public class FormVenta : Form
    {
        readonly List<LineaVenta> _lineas = new List<LineaVenta>();
        decimal _multiplicador = 1;
        bool _forzarSalida;

        PanelTitulo _titulo;
        DataGridView _grid;
        TextBox _captura;
        Label _lblTotal, _lblArticulos, _lblCantidad, _lblMensaje, _lblUsuario, _lblReloj, _lblUltimo, _lblBanner;
        Timer _reloj;

        public FormVenta()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "VentaFácil - Punto de Venta";
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1000, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Tema.AzulFondo;
            Font = Tema.Normal;
            KeyPreview = true;
            ConstruirPantalla();
            Load += (s, e) => { if (!AsegurarTurno()) { _forzarSalida = true; Close(); return; } ActualizarEncabezado(); };
            Shown += (s, e) => _captura.Focus();
            FormClosing += AlCerrar;
        }

        // =====================================================================
        //  Construcción de la pantalla
        // =====================================================================
        void ConstruirPantalla()
        {
            _titulo = new PanelTitulo { Titulo = "Punto de Venta", Height = 60 };
            _lblUsuario = new Label { AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent, Font = Tema.Negrita, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _lblReloj = new Label { AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent, Font = Tema.Normal, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _titulo.Controls.Add(_lblUsuario);
            _titulo.Controls.Add(_lblReloj);
            _titulo.Resize += (s, e) => UbicarEncabezado();

            // ---- Columna derecha de botones (como Zafirah) ----
            var derecha = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 150, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(6), BackColor = Tema.AzulFondo };
            Action<string, string, EventHandler> boton = (tecla, texto, click) =>
                derecha.Controls.Add(new BotonF(tecla, texto, click) { Width = 132, Height = 46, Margin = new Padding(0, 0, 0, 4) });
            boton("F2", "Buscar artículo", (s, e) => Buscar());
            boton("F3", "Quitar artículo", (s, e) => QuitarArticulo());
            boton("F4", "Consultar precio", (s, e) => ConsultarPrecio());
            boton("F5", "Venta en espera", (s, e) => Espera());
            boton("F6", "Descuento / precio", (s, e) => Descuento());
            boton("F7", "Entradas / salidas", (s, e) => MovimientoCaja());
            boton("F8", "Artículo varios", (s, e) => ArticuloComun());
            boton("F9", "Cortes de caja", (s, e) => Cortes());
            boton("F10", "Cambiar usuario", (s, e) => CambiarUsuario());
            boton("F11", "Administración", (s, e) => Administracion());
            boton("F12", "Cancelar ticket", (s, e) => CancelarTicket());
            boton("Ctrl+P", "Reimprimir último", (s, e) => ReimprimirUltimo());
            boton("Ctrl+D", "Abrir cajón", (s, e) => AbrirCajon());
            boton("Ctrl+I", "Impresora", (s, e) => ConfigurarImpresora());

            // ---- Zona inferior: captura + total ----
            var abajo = new Panel { Dock = DockStyle.Bottom, Height = 150, BackColor = Tema.AzulFondo, Padding = new Padding(8) };

            var panelTotal = new Panel { Dock = DockStyle.Right, Width = 430, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            panelTotal.Controls.Add(Ui.Etiqueta("TOTAL:", 12, 14, Tema.Grande, Tema.Azul));
            _lblTotal = new Label { Text = "$0.00", Font = Tema.Enorme, ForeColor = Tema.Azul, AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Left = 120, Top = 2, Width = 296, Height = 60, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            panelTotal.Controls.Add(_lblTotal);
            var btnCobrar = new Button { Text = "COBRAR  ( + )", Font = Tema.Grande, BackColor = Tema.Verde, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Left = 12, Top = 72, Width = 404, Height = 58, TabStop = false, Cursor = Cursors.Hand };
            btnCobrar.Click += (s, e) => Cobrar();
            panelTotal.Controls.Add(btnCobrar);
            abajo.Controls.Add(panelTotal);

            var panelCaptura = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            panelCaptura.Controls.Add(Ui.Etiqueta("Código de barras, cantidad* o nombre del artículo:", 10, 8, Tema.Negrita, Tema.Azul));
            _captura = new TextBox { Left = 10, Top = 32, Width = 420, Font = Tema.Captura, CharacterCasing = CharacterCasing.Normal, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _captura.KeyDown += CapturaKeyDown;
            _captura.KeyPress += CapturaKeyPress;
            panelCaptura.Controls.Add(_captura);
            _lblCantidad = Ui.Etiqueta("Cantidad: 1", 10, 76, Tema.Mediana, Tema.Texto);
            panelCaptura.Controls.Add(_lblCantidad);
            _lblArticulos = Ui.Etiqueta("Artículos: 0", 200, 76, Tema.Mediana, Tema.Texto);
            panelCaptura.Controls.Add(_lblArticulos);
            _lblMensaje = Ui.Etiqueta("", 10, 106, Tema.Negrita, Tema.Rojo);
            panelCaptura.Controls.Add(_lblMensaje);
            _lblUltimo = new Label { AutoSize = false, Width = 260, Height = 60, Top = 72, Font = Tema.Mediana, ForeColor = Tema.Verde, TextAlign = ContentAlignment.MiddleRight, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            panelCaptura.Controls.Add(_lblUltimo);
            panelCaptura.Resize += (s, e) => { _lblUltimo.Left = panelCaptura.ClientSize.Width - _lblUltimo.Width - 8; };
            abajo.Controls.Add(panelCaptura);

            _lblBanner = new Label { Dock = DockStyle.Bottom, Height = 24, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.White, BackColor = Tema.Azul, Font = Tema.Negrita };

            // ---- Centro: el ticket en pantalla ----
            var centro = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 8, 0, 0), BackColor = Tema.AzulFondo };
            _grid = new DataGridView { Dock = DockStyle.Fill };
            Ui.EstiloGrid(_grid);
            _grid.Font = new Font("Tahoma", 12f);
            _grid.RowTemplate.Height = 30;
            _grid.ColumnHeadersHeight = 32;
            _grid.Columns.Add(Ui.Columna("codigo", "Código", 18));
            _grid.Columns.Add(Ui.Columna("descripcion", "Descripción", 46));
            _grid.Columns.Add(Ui.Columna("cantidad", "Cant.", 10, true));
            _grid.Columns.Add(Ui.Columna("precio", "Precio", 12, true));
            _grid.Columns.Add(Ui.Columna("importe", "Importe", 14, true));
            _grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape || (e.KeyValue >= 48 && e.KeyValue <= 90 && !e.Control)) { _captura.Focus(); }
                if (e.KeyCode == Keys.Delete) { e.Handled = true; QuitarArticulo(); }
            };
            centro.Controls.Add(_grid);

            Controls.Add(centro);
            Controls.Add(derecha);
            Controls.Add(abajo);
            Controls.Add(_lblBanner);
            Controls.Add(_titulo);

            _reloj = new Timer { Interval = 1000 };
            _reloj.Tick += (s, e) => { _lblReloj.Text = DateTime.Now.ToString("dddd dd/MMM/yyyy  hh:mm:ss tt", new System.Globalization.CultureInfo("es-MX")); UbicarEncabezado(); };
            _reloj.Start();
        }

        void UbicarEncabezado()
        {
            _lblUsuario.Left = _titulo.Width - _lblUsuario.Width - 16; _lblUsuario.Top = 8;
            _lblReloj.Left = _titulo.Width - _lblReloj.Width - 16; _lblReloj.Top = 32;
        }

        void ActualizarEncabezado()
        {
            var c = Config.Actual;
            _titulo.Titulo = c.NegocioNombre;
            _titulo.Subtitulo = "Caja " + c.Caja + (Sesion.Turno != null ? "  ·  Turno " + Sesion.Turno.Id : "");
            _titulo.Invalidate();
            _lblUsuario.Text = "Cajero: " + Sesion.Usuario.Nombre + (Sesion.Usuario.EsAdmin ? " (Admin)" : "");
            _lblBanner.Text = c.Banner;
            _lblBanner.Visible = !string.IsNullOrWhiteSpace(c.Banner);
            UbicarEncabezado();
        }

        // =====================================================================
        //  Teclado
        // =====================================================================
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Los atajos solo aplican cuando esta ventana está activa (no dentro de diálogos)
            if (Form.ActiveForm != this) return base.ProcessCmdKey(ref msg, keyData);
            switch (keyData)
            {
                case Keys.F1: Cobrar(); return true;
                case Keys.F2: Buscar(); return true;
                case Keys.F3: QuitarArticulo(); return true;
                case Keys.F4: ConsultarPrecio(); return true;
                case Keys.F5: Espera(); return true;
                case Keys.F6: Descuento(); return true;
                case Keys.F7: MovimientoCaja(); return true;
                case Keys.F8: ArticuloComun(); return true;
                case Keys.F9: Cortes(); return true;
                case Keys.F10: CambiarUsuario(); return true;
                case Keys.F11: Administracion(); return true;
                case Keys.F12: CancelarTicket(); return true;
                case Keys.Control | Keys.P: ReimprimirUltimo(); return true;
                case Keys.Control | Keys.D: AbrirCajon(); return true;
                case Keys.Control | Keys.I: ConfigurarImpresora(); return true;
                case Keys.Shift | Keys.Subtract: AbrirCajon(); return true;
                case Keys.Shift | Keys.OemMinus: AbrirCajon(); return true;
                case Keys.Control | Keys.Delete: CancelarVentaActual(); return true;
                case Keys.Shift | Keys.Delete: CancelarVentaActual(); return true;
                case Keys.Add: Cobrar(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void CapturaKeyPress(object sender, KeyPressEventArgs e)
        {
            // '*' fija la cantidad: "3*" y luego escanear
            if (e.KeyChar == '*')
            {
                e.Handled = true;
                decimal q;
                if (Fmt.TryDec(_captura.Text, out q) && q > 0)
                {
                    _multiplicador = q;
                    _captura.Clear();
                    MostrarCantidad();
                }
                else Mensaje("Escriba la cantidad y luego *  (ejemplo: 3*)");
            }
            else if (e.KeyChar == '+') { e.Handled = true; Cobrar(); }
        }

        void CapturaKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Capturar(_captura.Text.Trim());
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                MoverSeleccion(e.KeyCode == Keys.Up ? -1 : 1);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                _captura.Clear();
                if (_multiplicador != 1) { _multiplicador = 1; MostrarCantidad(); }
                Mensaje("");
            }
            else if (e.KeyCode == Keys.Delete && _captura.Text == "")
            {
                e.SuppressKeyPress = true;
                QuitarArticulo();
            }
        }

        void MoverSeleccion(int delta)
        {
            if (_grid.Rows.Count == 0) return;
            int i = _grid.CurrentRow == null ? _grid.Rows.Count - 1 : _grid.CurrentRow.Index + delta;
            i = Math.Max(0, Math.Min(_grid.Rows.Count - 1, i));
            _grid.CurrentCell = _grid.Rows[i].Cells[1];
        }

        // =====================================================================
        //  Captura de artículos
        // =====================================================================
        void Capturar(string texto)
        {
            Mensaje("");
            if (texto == "") return;
            try
            {
                // "3*7501234" en una sola captura
                int estrella = texto.IndexOf('*');
                if (estrella > 0)
                {
                    decimal q;
                    if (Fmt.TryDec(texto.Substring(0, estrella), out q) && q > 0) _multiplicador = q;
                    texto = texto.Substring(estrella + 1).Trim();
                    if (texto == "") { _captura.Clear(); MostrarCantidad(); return; }
                }

                decimal? cantidadEtiqueta = null;
                var p = ProductoRepo.PorCodigo(texto);
                if (p == null) p = EtiquetaBascula(texto, out cantidadEtiqueta);
                if (p == null)
                {
                    bool esCodigo = texto.All(char.IsDigit);
                    if (!esCodigo)
                    {
                        // texto: abrir búsqueda con lo escrito
                        _captura.Clear();
                        var elegido = FormBuscar.Elegir(this, texto, false);
                        if (elegido != null) Agregar(elegido, null);
                        return;
                    }
                    System.Media.SystemSounds.Hand.Play();
                    if (Config.Actual.AvisarNoEncontrado) Ui.Aviso("El artículo \"" + texto + "\" no está registrado.");
                    else Mensaje("Artículo no encontrado: " + texto);
                    _captura.SelectAll();
                    return;
                }
                _captura.Clear();
                Agregar(p, cantidadEtiqueta);
            }
            catch (Exception ex) { Ui.Error(ex, "Al capturar el artículo"); }
        }

        /// <summary>
        /// Etiquetas de báscula (EAN-13 que empiezan con 2): 2 + PLU + importe + verificador.
        /// Prueba PLU de 5 y 6 dígitos; el importe viene en centavos.
        /// </summary>
        Producto EtiquetaBascula(string codigo, out decimal? cantidad)
        {
            cantidad = null;
            if (codigo.Length != 13 || codigo[0] != '2' || !codigo.All(char.IsDigit)) return null;
            var intentos = new[] { codigo.Substring(1, 6), codigo.Substring(2, 5), codigo.Substring(1, 6).TrimStart('0'), codigo.Substring(2, 5).TrimStart('0') };
            foreach (var plu in intentos.Distinct())
            {
                if (plu == "") continue;
                var p = ProductoRepo.PorCodigo(plu);
                if (p == null || p.Precio <= 0) continue;
                decimal importe = decimal.Parse(codigo.Substring(7, 5)) / 100m;
                cantidad = Math.Round(importe / p.Precio, 3);
                return p;
            }
            return null;
        }

        void Agregar(Producto p, decimal? cantidadFija)
        {
            decimal cantidad = cantidadFija ?? _multiplicador;
            if (p.Granel && !cantidadFija.HasValue && _multiplicador == 1)
            {
                var peso = FormPeso.Pedir(this, p);
                if (!peso.HasValue) { _captura.Focus(); return; }
                cantidad = peso.Value;
            }
            if (cantidad <= 0) return;

            // Validar existencia
            if (!Config.Actual.PermitirVentaSinExistencia && p.UsaInventario)
            {
                decimal yaEnVenta = _lineas.Where(l => l.ProductoId == p.Id).Sum(l => l.Cantidad);
                if (yaEnVenta + cantidad > p.Existencia)
                {
                    Ui.Aviso("No hay existencia suficiente de \"" + p.Descripcion + "\".\nExistencia: " + Fmt.Dec(p.Existencia));
                    return;
                }
            }

            // Si ya está en la venta (y no es a granel), suma la cantidad
            var existente = p.Granel ? null : _lineas.LastOrDefault(l => l.ProductoId == p.Id && !l.PrecioManual && l.Descuento == 0);
            if (existente != null)
            {
                existente.Cantidad += cantidad;
                existente.Precio = p.PrecioPara(existente.Cantidad);
                _lineas.Remove(existente);
                _lineas.Add(existente);
            }
            else
            {
                _lineas.Add(new LineaVenta
                {
                    ProductoId = p.Id, Codigo = p.Codigo, Descripcion = p.Descripcion, Cantidad = cantidad,
                    PrecioLista = p.Precio, Precio = p.PrecioPara(cantidad), Costo = p.Costo, Granel = p.Granel
                });
            }
            _multiplicador = 1;
            _lblUltimo.Text = "";
            Refrescar(_lineas.Count - 1);
        }

        void Refrescar(int seleccionar = -1)
        {
            _grid.Rows.Clear();
            foreach (var l in _lineas)
            {
                int i = _grid.Rows.Add(l.Codigo, l.Descripcion + (l.Descuento > 0 ? "  (desc. " + Fmt.Dinero(l.Descuento) + ")" : ""),
                    Fmt.Cant(l.Cantidad, l.Granel), Fmt.Dinero(l.Precio), Fmt.Dinero(l.Importe));
                if (l.PrecioManual || l.Descuento > 0) _grid.Rows[i].DefaultCellStyle.ForeColor = Tema.Rojo;
            }
            if (_grid.Rows.Count > 0)
            {
                int i = seleccionar < 0 || seleccionar >= _grid.Rows.Count ? _grid.Rows.Count - 1 : seleccionar;
                _grid.CurrentCell = _grid.Rows[i].Cells[1];
                _grid.FirstDisplayedScrollingRowIndex = Math.Max(0, i - 8);
            }
            _lblTotal.Text = Fmt.Dinero(Total);
            _lblArticulos.Text = "Artículos: " + Fmt.Dec(_lineas.Sum(l => l.Granel ? 1 : l.Cantidad));
            MostrarCantidad();
            _captura.Focus();
        }

        decimal Total { get { return Fmt.R2(_lineas.Sum(l => l.Importe)); } }

        void MostrarCantidad()
        {
            _lblCantidad.Text = "Cantidad: " + Fmt.Dec(_multiplicador);
            _lblCantidad.ForeColor = _multiplicador != 1 ? Tema.Rojo : Tema.Texto;
        }

        void Mensaje(string t) { _lblMensaje.Text = t; }

        LineaVenta LineaSeleccionada()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Index >= _lineas.Count) return null;
            return _lineas[_grid.CurrentRow.Index];
        }

        // =====================================================================
        //  Funciones (teclas F)
        // =====================================================================
        void Buscar()
        {
            var p = FormBuscar.Elegir(this, _captura.Text.Trim(), false);
            _captura.Clear();
            if (p != null) Agregar(p, null);
            _captura.Focus();
        }

        void ConsultarPrecio()
        {
            var p = FormBuscar.Elegir(this, _captura.Text.Trim(), true);
            _captura.Clear();
            if (p != null) Agregar(p, null);
            _captura.Focus();
        }

        void QuitarArticulo()
        {
            var l = LineaSeleccionada();
            if (l == null) { Mensaje("No hay artículos en la venta."); return; }
            int i = _lineas.IndexOf(l);
            if (!l.Granel && l.Cantidad > 1)
            {
                var q = Ui.PedirNumero("Quitar artículo", "¿Cuántos quita de \"" + l.Descripcion + "\"? (tiene " + Fmt.Dec(l.Cantidad) + ")", l.Cantidad);
                if (!q.HasValue || q.Value <= 0) { _captura.Focus(); return; }
                if (q.Value < l.Cantidad)
                {
                    l.Cantidad -= q.Value;
                    if (!l.PrecioManual) l.Precio = PrecioActual(l);
                    Refrescar(i);
                    return;
                }
            }
            _lineas.RemoveAt(i);
            Mensaje("Se quitó: " + l.Descripcion);
            Refrescar(Math.Min(i, _lineas.Count - 1));
        }

        decimal PrecioActual(LineaVenta l)
        {
            var p = l.ProductoId > 0 ? ProductoRepo.Obtener(l.ProductoId) : null;
            return p == null ? l.Precio : p.PrecioPara(l.Cantidad);
        }

        void CancelarVentaActual()
        {
            if (_lineas.Count == 0) return;
            if (!Ui.Confirmar("¿Cancelar toda la venta actual?")) { _captura.Focus(); return; }
            _lineas.Clear();
            _multiplicador = 1;
            Refrescar();
            Mensaje("Venta cancelada.");
        }

        void Descuento()
        {
            if (_lineas.Count == 0) { Mensaje("No hay artículos en la venta."); return; }
            if (!Config.Actual.CajeroPuedeCancelar && Ui.AutorizarAdmin("Descuentos: se requiere autorización") == null) { _captura.Focus(); return; }
            var l = LineaSeleccionada();
            using (var f = new FormDescuento(l, Total))
            {
                if (f.ShowDialog(this) != DialogResult.OK) { _captura.Focus(); return; }
                if (f.ATodaLaVenta)
                {
                    foreach (var x in _lineas)
                        x.Descuento = Fmt.R2(x.Cantidad * x.Precio * f.Porcentaje / 100m);
                }
                else if (l != null)
                {
                    if (f.NuevoPrecio.HasValue) { l.Precio = f.NuevoPrecio.Value; l.PrecioManual = true; l.Descuento = 0; }
                    else if (f.Porcentaje > 0) l.Descuento = Fmt.R2(l.Cantidad * l.Precio * f.Porcentaje / 100m);
                    else if (f.Importe > 0) l.Descuento = Math.Min(f.Importe, Fmt.R2(l.Cantidad * l.Precio));
                    else l.Descuento = 0;
                }
            }
            Refrescar(l == null ? -1 : _lineas.IndexOf(l));
        }

        void ArticuloComun()
        {
            using (var f = new FormArticuloComun(_multiplicador))
            {
                if (f.ShowDialog(this) != DialogResult.OK) { _captura.Focus(); return; }
                _lineas.Add(new LineaVenta { ProductoId = 0, Codigo = "VARIOS", Descripcion = f.Descripcion, Cantidad = f.Cantidad, PrecioLista = f.Precio, Precio = f.Precio, PrecioManual = true });
            }
            _multiplicador = 1;
            Refrescar();
        }

        void Espera()
        {
            using (var f = new FormEspera(_lineas.Count > 0))
            {
                var r = f.ShowDialog(this);
                if (r == DialogResult.Yes)
                {
                    long folio = SuspendidaRepo.Guardar(_lineas, f.Nota, Sesion.Usuario.Id);
                    decimal total = Total;
                    _lineas.Clear();
                    Refrescar();
                    Mensaje("Venta puesta en espera con el número " + folio + ".");
                    if (Config.Actual.ImprimirAutomatico)
                        try { Impresora.Imprimir(Tickets.Suspendida(folio, total, Config.Actual)); } catch (Exception ex) { Log.Error(ex, "Ticket espera"); }
                }
                else if (r == DialogResult.OK && f.Elegida > 0)
                {
                    if (_lineas.Count > 0) { Ui.Aviso("Termine o ponga en espera la venta actual antes de recuperar otra."); return; }
                    _lineas.AddRange(SuspendidaRepo.Recuperar(f.Elegida));
                    Refrescar();
                    Mensaje("Venta en espera recuperada.");
                }
            }
            _captura.Focus();
        }

        void MovimientoCaja()
        {
            using (var f = new FormMovimiento())
            {
                if (f.ShowDialog(this) != DialogResult.OK) { _captura.Focus(); return; }
                try
                {
                    TurnoRepo.Movimiento(Sesion.Turno.Id, Sesion.Usuario.Id, f.Tipo, f.Monto, f.Concepto);
                    var t = Tickets.Movimiento(f.Tipo, f.Monto, f.Concepto, Config.Actual);
                    t.AbrirCajon = true;
                    try { Impresora.Imprimir(t); }
                    catch (Exception ex) { Ui.Error(ex, "El movimiento se guardó, pero no se pudo imprimir"); }
                    Mensaje((f.Tipo == "ENTRADA" ? "Entrada" : "Salida") + " registrada: " + Fmt.Dinero(f.Monto));
                }
                catch (Exception ex) { Ui.Error(ex, "No se pudo guardar el movimiento"); }
            }
            _captura.Focus();
        }

        void Cortes()
        {
            if (_lineas.Count > 0) { Ui.Aviso("Termine la venta actual antes de hacer un corte."); return; }
            using (var f = new FormCortes())
            {
                f.ShowDialog(this);
                if (f.TurnoCerrado)
                {
                    Sesion.Turno = null;
                    if (!AsegurarTurno()) { _forzarSalida = true; Close(); return; }
                    ActualizarEncabezado();
                }
            }
            _captura.Focus();
        }

        void CambiarUsuario()
        {
            if (_lineas.Count > 0) { Ui.Aviso("Termine o ponga en espera la venta actual antes de cambiar de usuario."); return; }
            using (var f = new FormLogin(false, null))
            {
                f.StartPosition = FormStartPosition.CenterParent;
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    Sesion.Usuario = f.UsuarioValido;
                    ActualizarEncabezado();
                    Mensaje("Bienvenido(a), " + Sesion.Usuario.Nombre);
                }
            }
            _captura.Focus();
        }

        void Administracion()
        {
            if (_lineas.Count > 0) { Ui.Aviso("Termine la venta actual antes de entrar a Administración."); return; }
            if (Ui.AutorizarAdmin("Administración: se requiere un administrador") == null) { _captura.Focus(); return; }
            using (var f = new Admin.FormAdmin()) f.ShowDialog(this);
            Config.Cargar();
            ActualizarEncabezado();
            _captura.Focus();
        }

        void CancelarTicket()
        {
            if (!Config.Actual.CajeroPuedeCancelar && Ui.AutorizarAdmin("Cancelar ticket: se requiere autorización") == null) { _captura.Focus(); return; }
            var folio = Ui.PedirNumero("Cancelar ticket", "Número de ticket a cancelar:", VentaRepo.UltimoTicket(Config.Actual.Caja));
            if (!folio.HasValue) { _captura.Focus(); return; }
            try
            {
                var v = VentaRepo.Obtener((long)folio.Value);
                if (v == null) { Ui.Aviso("No existe el ticket " + folio.Value + "."); return; }
                if (v.Estado == "CANCELADA") { Ui.Aviso("El ticket " + v.Id + " ya estaba cancelado."); return; }
                string resumen = "Ticket " + v.Id + "  del " + v.Fecha.ToString("dd/MM/yyyy HH:mm") + "\n" +
                                 "Total: " + Fmt.Dinero(v.Total) + "  (" + Tickets.NombrePago(v.FormaPago) + ")\n" +
                                 "Artículos: " + v.Lineas.Count + "\n\n" +
                                 "Se regresará la mercancía al inventario y el dinero se descontará de este turno.\n¿Cancelar el ticket? Esto no se puede deshacer.";
                if (!Ui.Confirmar(resumen, "Cancelar ticket")) return;
                VentaRepo.Cancelar(v.Id, Sesion.Turno.Id, Sesion.Usuario.Id);
                var t = Tickets.Cancelacion(v, Config.Actual);
                t.AbrirCajon = v.FormaPago == "EFECTIVO";
                try { Impresora.Imprimir(t); } catch (Exception ex) { Ui.Error(ex, "El ticket se canceló, pero no se pudo imprimir el comprobante"); }
                Mensaje("Ticket " + v.Id + " cancelado. Devuelva " + Fmt.Dinero(v.Total));
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo cancelar el ticket"); }
            finally { _captura.Focus(); }
        }

        void ReimprimirUltimo()
        {
            try
            {
                long id = VentaRepo.UltimoTicket(Config.Actual.Caja);
                if (id == 0) { Mensaje("Todavía no hay tickets."); return; }
                Impresora.Imprimir(Tickets.Venta(VentaRepo.Obtener(id), Config.Actual, true));
                Mensaje("Se reimprimió el ticket " + id + ".");
            }
            catch (Exception ex) { ErrorImpresion(ex, "No se pudo reimprimir."); }
        }

        void ConfigurarImpresora()
        {
            if (Ui.AutorizarAdmin("Impresora: se requiere un administrador") == null) { _captura.Focus(); return; }
            if (FormImpresoras.Configurar(this)) { Config.Cargar(); Mensaje("Impresora guardada."); }
            _captura.Focus();
        }

        /// <summary>Si falla la impresión, ofrece arreglarlo en ese momento.</summary>
        void ErrorImpresion(Exception ex, string contexto)
        {
            Log.Error(ex, contexto);
            if (Ui.Confirmar(contexto + "\n\n" + ex.Message + "\n\n¿Quiere revisar o cambiar la impresora ahora?", "Problema con la impresora"))
                ConfigurarImpresora();
        }

        void AbrirCajon()
        {
            try { Cajon.Abrir(Config.Actual); Mensaje("Cajón abierto."); }
            catch (Exception ex) { Ui.Error(ex, "No se pudo abrir el cajón"); }
        }

        // =====================================================================
        //  Cobro
        // =====================================================================
        void Cobrar()
        {
            if (_lineas.Count == 0) { Mensaje("No hay artículos para cobrar."); _captura.Focus(); return; }
            decimal total = Total;
            using (var f = new FormCobro(total))
            {
                if (f.ShowDialog(this) != DialogResult.OK) { _captura.Focus(); return; }
                var v = new Venta
                {
                    TurnoId = Sesion.Turno.Id,
                    Caja = Config.Actual.Caja,
                    UsuarioId = Sesion.Usuario.Id,
                    Cajero = Sesion.Usuario.Nombre,
                    Lineas = new List<LineaVenta>(_lineas),
                    Articulos = _lineas.Sum(l => l.Granel ? 1 : l.Cantidad),
                    Subtotal = Fmt.R2(_lineas.Sum(l => l.Cantidad * l.Precio)),
                    Descuento = Fmt.R2(_lineas.Sum(l => l.Descuento)),
                    Total = total,
                    FormaPago = f.FormaPago,
                    PagoCon = f.PagoCon,
                    Cambio = f.Cambio,
                    Referencia = f.Referencia
                };
                try
                {
                    VentaRepo.Guardar(v, Sesion.Usuario.Id);
                }
                catch (Exception ex)
                {
                    Ui.Error(ex, "No se pudo guardar la venta. Intente cobrar de nuevo.");
                    return;
                }

                _lineas.Clear();
                _multiplicador = 1;
                Refrescar();
                _lblUltimo.Text = v.Cambio > 0 ? "Cambio: " + Fmt.Dinero(v.Cambio) : "Ticket " + v.Id;
                Mensaje("Venta " + v.Id + " registrada: " + Fmt.Dinero(v.Total));

                var c = Config.Actual;
                bool abrir = c.AbrirCajon && v.FormaPago == "EFECTIVO";
                bool imprimir = c.ImprimirAutomatico && (!c.PreguntarImprimir || Ui.Confirmar("¿Imprimir ticket?"));
                try
                {
                    if (imprimir)
                    {
                        var t = Tickets.Venta(v, c);
                        t.AbrirCajon = abrir;
                        Impresora.Imprimir(t);
                    }
                    else if (abrir) Cajon.Abrir(c);
                }
                catch (Exception ex)
                {
                    ErrorImpresion(ex, "La venta SÍ se guardó, pero falló la impresora/cajón. Puede reimprimir con Ctrl+P.");
                }
            }
            _captura.Focus();
        }

        // =====================================================================
        //  Turno
        // =====================================================================
        /// <summary>Busca el turno abierto de la caja o abre uno nuevo pidiendo el fondo.</summary>
        bool AsegurarTurno()
        {
            try
            {
                var t = TurnoRepo.Abierto(Config.Actual.Caja);
                if (t == null)
                {
                    decimal fondo = 0;
                    if (Config.Actual.PedirFondoAlAbrir)
                    {
                        var f = Ui.PedirNumero("Abrir caja", "Inicio de turno. ¿Con cuánto efectivo (fondo) inicia la caja?", 0);
                        if (!f.HasValue) return false;
                        fondo = f.Value;
                    }
                    t = TurnoRepo.Abrir(Config.Actual.Caja, Sesion.Usuario.Id, fondo);
                }
                Sesion.Turno = t;
                return true;
            }
            catch (Exception ex)
            {
                Ui.Error(ex, "No se pudo abrir el turno de caja");
                return false;
            }
        }

        void AlCerrar(object sender, FormClosingEventArgs e)
        {
            if (_forzarSalida) { _reloj.Stop(); return; }
            if (_lineas.Count > 0 && e.CloseReason == CloseReason.UserClosing)
            {
                if (!Ui.Confirmar("Hay una venta sin cobrar. Si sale se perderá.\n¿Salir de todas formas?")) { e.Cancel = true; return; }
            }
            else if (e.CloseReason == CloseReason.UserClosing && !Ui.Confirmar("¿Salir de VentaFácil?"))
            {
                e.Cancel = true;
                return;
            }
            _reloj.Stop();
        }
    }
}
