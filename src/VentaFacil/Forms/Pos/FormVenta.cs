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

        // ---- módulos (F1 Caja · F2 Turno · F3 Catálogo · F4 Almacén · F5 Resultados · F6 Ajustes) ----
        BarraModulos _barraMod;
        Panel _host, _panelCaja;
        ModuloVista _modulo;
        int _modActivo;
        Usuario _autoriza;   // quien prestó su clave para entrar a un módulo (se olvida al salir a Caja/Turno o al cambiar de usuario)

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
            _titulo = new PanelTitulo { Titulo = "Punto de Venta", Height = 64 };
            _lblUsuario = new Label { AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent, Font = Tema.Negrita, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _lblReloj = new Label { AutoSize = true, ForeColor = Color.White, BackColor = Color.Transparent, Font = Tema.Normal, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _titulo.Controls.Add(_lblUsuario);
            _titulo.Controls.Add(_lblReloj);
            _titulo.Resize += (s, e) => UbicarEncabezado();

            // ---- Zona inferior: captura + total ----
            var abajo = new Panel { Dock = DockStyle.Bottom, Height = 150, BackColor = Tema.AzulFondo, Padding = new Padding(8) };

            var panelTotal = new Panel { Dock = DockStyle.Right, Width = 430, BackColor = Tema.Crema, BorderStyle = BorderStyle.FixedSingle };
            panelTotal.Controls.Add(Ui.Etiqueta("TOTAL:", 12, 14, Tema.Grande, Tema.Azul));
            _lblTotal = new Label { Text = "$0.00", Font = Tema.Enorme, ForeColor = Tema.Azul, AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Left = 120, Top = 2, Width = 296, Height = 60, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            panelTotal.Controls.Add(_lblTotal);
            var btnCobrar = new Button { Text = "COBRAR  ( + / Ctrl+F1 )", Font = Tema.Mediana, BackColor = Tema.Azul, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Left = 12, Top = 72, Width = 404, Height = 58, TabStop = false, Cursor = Cursors.Hand };
            btnCobrar.Click += (s, e) => Cobrar();
            Tema.Pastilla(btnCobrar, true);
            panelTotal.Controls.Add(btnCobrar);
            abajo.Controls.Add(panelTotal);

            var panelCaptura = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Crema, BorderStyle = BorderStyle.FixedSingle };
            panelCaptura.Controls.Add(Ui.Etiqueta("Código de barras, cantidad* o nombre del artículo:", 10, 8, Tema.Negrita, Tema.Azul));
            _captura = new TextBox { Left = 10, Top = 32, Width = 420, Font = Tema.Captura, CharacterCasing = CharacterCasing.Normal, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _captura.KeyDown += CapturaKeyDown;
            _captura.KeyPress += CapturaKeyPress;
            panelCaptura.Controls.Add(_captura);
            _lblCantidad = Ui.Etiqueta("Cantidad: 1", 10, 76, Tema.Mediana, Tema.Azul);
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
            _grid.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
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

            // Orden de acople (el último que se agrega queda más arriba): centro, abajo, banner, barra de botones, menú, encabezado
            var acciones = ConstruirBarraAcciones();
            _panelCaja = new Panel { Dock = DockStyle.Fill, BackColor = Tema.AzulFondo };
            _panelCaja.Controls.Add(centro);
            _panelCaja.Controls.Add(abajo);
            _panelCaja.Controls.Add(_lblBanner);
            _panelCaja.Controls.Add(acciones);
            _host = new Panel { Dock = DockStyle.Fill, BackColor = Tema.AzulFondo };
            _host.Controls.Add(_panelCaja);
            _barraMod = new BarraModulos();
            foreach (var m in new[] { new[] { "F1", "Caja" }, new[] { "F2", "Turno" }, new[] { "F3", "Catálogo" }, new[] { "F4", "Almacén" }, new[] { "F5", "Resultados" }, new[] { "F6", "Ajustes" } })
                _barraMod.Agregar(m[0], m[1]);
            _barraMod.Elegido += IrAModulo;

            // Marca de agua discreta y elegante en la base del programa: alanmendez.dev
            var pie = new Panel { Dock = DockStyle.Bottom, Height = 22, BackColor = Tema.Crema };
            pie.Paint += (s, e) =>
            {
                using (var pen = new Pen(Tema.Piedra, 1f))
                    e.Graphics.DrawLine(pen, 0, 0, pie.Width, 0);
            };
            var lblMarca = new Label
            {
                Text = "alanmendez.dev",
                AutoSize = true,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 135, 155),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            lblMarca.Click += (s, e) => { try { System.Diagnostics.Process.Start("https://alanmendez.dev"); } catch { } };
            pie.Controls.Add(lblMarca);
            pie.Resize += (s, e) => { lblMarca.Left = (pie.ClientSize.Width - lblMarca.Width) / 2; lblMarca.Top = 2; };

            Controls.Add(_host);
            Controls.Add(pie);
            Controls.Add(_barraMod);
            Controls.Add(_titulo);

            _reloj = new Timer { Interval = 1000 };
            _reloj.Tick += (s, e) => { _lblReloj.Text = DateTime.Now.ToString("dddd dd/MMM/yyyy  hh:mm:ss tt", new System.Globalization.CultureInfo("es-MX")); UbicarEncabezado(); };
            _reloj.Start();
        }

        // =====================================================================
        //  Menú superior y barra de botones (todo hace lo mismo que las teclas F)
        // =====================================================================
        // ---------------------------------------------------------------------
        //  Módulos: cada uno con su barra de atajos (Ctrl+F1, Ctrl+F2…)
        // ---------------------------------------------------------------------
        static readonly string[] PermisoDeModulo = { null, Permisos.Turno, Permisos.Catalogo, Permisos.Almacen, Permisos.Resultados, Permisos.Ajustes };

        /// <summary>El usuario de la sesión, o quien prestó su clave para entrar al módulo, tiene el permiso.</summary>
        bool Puede(string permiso) { return Permisos.Tiene(Sesion.Usuario, permiso) || Permisos.Tiene(_autoriza, permiso); }

        void IrAModulo(int n)
        {
            if (n == _modActivo) { if (n == 0) _captura.Focus(); return; }
            if (n >= 2 && _lineas.Count > 0) { Ui.Aviso("Termine la venta actual antes de entrar a este módulo."); return; }
            string permiso = PermisoDeModulo[n];
            var autoriza = n <= 1 ? null : _autoriza;
            if (n > 0 && !Permisos.Tiene(Sesion.Usuario, permiso) && !Permisos.Tiene(autoriza, permiso))
            {
                autoriza = Ui.Autorizar(permiso, "Este módulo requiere autorización");
                if (autoriza == null) { if (_modActivo == 0) _captura.Focus(); return; }
            }
            _autoriza = autoriza;

            if (_modulo != null) { _host.Controls.Remove(_modulo); _modulo.Dispose(); _modulo = null; }
            _modActivo = n;
            _barraMod.Activo = n;
            if (n == 0)
            {
                _panelCaja.Visible = true;
                Config.Cargar();
                ActualizarEncabezado();
                _captura.Focus();
                return;
            }
            _panelCaja.Visible = false;
            _modulo = ConstruirModulo(n);
            _host.Controls.Add(_modulo);
            _modulo.BringToFront();
            _modulo.Construir();
        }

        /// <summary>Ejecuta algo del módulo y refresca su vista (y el encabezado) después.</summary>
        Action Y(Action a) { return () => { a(); if (_modulo != null) _modulo.Recargar(); if (!IsDisposed) ActualizarEncabezado(); }; }

        ModuloVista ConstruirModulo(int n)
        {
            ModuloVista m;
            switch (n)
            {
                case 1:
                    m = new ModuloVista("Turno", "turno, dinero y cortes");
                    m.AgregarVista("Resumen del turno", VistaTurno);
                    m.AgregarAccion("Entradas / salidas", Y(MovimientoCaja));
                    m.AgregarAccion("Cortes X / Z", Y(Cortes));
                    m.AgregarAccion("Abrir cajón", AbrirCajon);
                    m.AgregarAccion("Reimprimir ticket", ReimprimirUltimo);
                    if (!Envios.EsOficina) m.AgregarAccion("Recibir envío", Y(RecibirEnvio));
                    m.AgregarAccion("Cambiar usuario (F10)", CambiarUsuario);
                    break;
                case 2:
                    m = new ModuloVista("Catálogo", "productos, precios y códigos");
                    m.AgregarVista("Productos", () => new Admin.SeccionProductos());
                    m.AgregarVista("Grupos", () => new Admin.SeccionDepartamentos());
                    m.AgregarAccion("Códigos de barras", () => { m.Mostrar(0); var sp = m.Actual as Admin.SeccionProductos; if (sp != null) sp.CodigosDeBarras(); });
                    break;
                case 3:
                    m = new ModuloVista("Almacén", "existencias, entradas y conteo");
                    if (Envios.EsOficina)
                    {   // en la oficina no hay existencias reales: se captura lo que llegó y se manda a la tienda
                        m.AgregarVista("Llegó mercancía", () => new Admin.SeccionMercancia());
                        m.AgregarVista("Envíos", () => new Admin.SeccionEnviosOficina());
                        break;
                    }
                    m.AgregarVista("Inventario", () => new Admin.SeccionInventario());
                    if (Envios.EsTienda) m.AgregarVista("Envíos recibidos", () => new Admin.SeccionEnviosTienda());
                    break;
                case 4:
                    m = new ModuloVista("Resultados", "ventas y reportes");
                    m.AgregarVista("Reportes", () => new Admin.SeccionReportes());
                    break;
                default:
                    m = new ModuloVista("Ajustes", "configuración del sistema");
                    m.AgregarVista("Negocio", () => new Admin.SeccionConfig());
                    if (Puede(Permisos.SoloAdmin))
                    {
                        m.AgregarVista("Usuarios", () => new Admin.SeccionUsuarios());
                        m.AgregarVista("Respaldos", () => new Admin.SeccionRespaldos());
                        m.AgregarVista("Envíos", () => new Admin.SeccionEnlace());
                    }
                    m.AgregarAccion("Impresora", ConfigurarImpresora);
                    m.AgregarAccion("Licencia", () => Licenciamiento.FormLicencia.Mostrar(Licencia.Evaluar(), true));
                    m.AgregarAccion("Teclas rápidas", () => { using (var f = new FormAyudaTeclas()) f.ShowDialog(this); });
                    m.AgregarAccion("Acerca de", AcercaDe);
                    m.AgregarAccion("Salir del programa", () => Close());
                    break;
            }
            return m;
        }

        Control VistaTurno()
        {
            var p = new Panel { BackColor = Tema.Crema };
            var t = Sesion.Turno;
            var c = Config.Actual;
            int y = 24;
            Action<string, string> fila = (k, v) =>
            {
                p.Controls.Add(Ui.Etiqueta(k, 24, y, Tema.Negrita, Tema.Humo));
                p.Controls.Add(Ui.Etiqueta(v, 220, y - 3, Tema.Mediana, Tema.Texto));
                y += 44;
            };
            fila("Cajero:", Sesion.Usuario != null ? Sesion.Usuario.Nombre : "");
            fila("Caja:", c.Caja.ToString());
            if (t == null) fila("Turno:", "No hay un turno abierto");
            else
            {
                fila("Turno número:", t.Id.ToString());
                fila("Abierto el:", t.Apertura.ToString("dd/MM/yyyy HH:mm"));
                fila("Fondo inicial:", Fmt.Dinero(t.Fondo));
            }
            p.Controls.Add(Ui.Etiqueta("Entradas y salidas de efectivo, cortes X y Z, el cajón y el cambio de usuario están en los botones de arriba.", 24, y + 16, Tema.Normal, Tema.Humo));
            return p;
        }

        /// <summary>Barra de botones con lo que más se usa en el mostrador (el resto está en el menú).</summary>
        Control ConstruirBarraAcciones()
        {
            var tabla = new TableLayoutPanel { Dock = DockStyle.Top, Height = 68, ColumnCount = 8, RowCount = 1, BackColor = Tema.AzulFondo, Padding = new Padding(8, 6, 8, 4) };
            for (int i = 0; i < 8; i++) tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 8f));
            tabla.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Action<int, string, string, EventHandler> boton = (col, tecla, texto, click) =>
                tabla.Controls.Add(new BotonF(tecla, texto, click) { Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) }, col, 0);
            boton(0, "Ctrl+F1", "Cobrar", (s, e) => Cobrar());
            boton(1, "Ctrl+F2", "Buscar", (s, e) => Buscar());
            boton(2, "Ctrl+F3", "Quitar", (s, e) => QuitarArticulo());
            boton(3, "Ctrl+F4", "Precio", (s, e) => ConsultarPrecio());
            boton(4, "Ctrl+F5", "En espera", (s, e) => Espera());
            boton(5, "Ctrl+F6", "Descuento", (s, e) => Descuento());
            boton(6, "Ctrl+F7", "Varios", (s, e) => ArticuloComun());
            boton(7, "Ctrl+F8", "Cancelar", (s, e) => CancelarTicket());
            return tabla;
        }

        void AcercaDe()
        {
            var v = typeof(FormVenta).Assembly.GetName().Version;
            var l = Licencia.Evaluar();
            string estado = l.Estado == EstadoLicencia.Activa ? "Activada" : l.Estado == EstadoLicencia.Prueba ? "Prueba: " + l.DiasRestantes + " días" : "Vencida";
            Ui.Info("VentaFácil  " + v.Major + "." + v.Minor + "\nPunto de venta para tiendas.\n\nLicencia: " + estado + "\nDatos: " + Db.Carpeta, "Acerca de VentaFácil");
            _captura.Focus();
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
            _titulo.Subtitulo = Envios.EsOficina ? "OFICINA" : "Caja " + c.Caja + (Sesion.Turno != null ? "  ·  Turno " + Sesion.Turno.Id : "");
            _titulo.Invalidate();
            _lblUsuario.Text = "Cajero: " + Sesion.Usuario.Nombre + (Sesion.Usuario.EsAdmin ? " (Admin)" : "");
            _lblBanner.Text = c.Banner;
            _lblBanner.Visible = !string.IsNullOrWhiteSpace(c.Banner);
            for (int i = 1; i < PermisoDeModulo.Length; i++) _barraMod.Bloquear(i, !Permisos.Tiene(Sesion.Usuario, PermisoDeModulo[i]));
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
                // F1..F6: cambiar de módulo
                case Keys.F1: IrAModulo(0); return true;
                case Keys.F2: IrAModulo(1); return true;
                case Keys.F3: IrAModulo(2); return true;
                case Keys.F4: IrAModulo(3); return true;
                case Keys.F5: IrAModulo(4); return true;
                case Keys.F6: IrAModulo(5); return true;
                case Keys.F10: CambiarUsuario(); return true;
                case Keys.Escape: if (_modActivo != 0) { IrAModulo(0); return true; } break;
                case Keys.Control | Keys.P: ReimprimirUltimo(); return true;
                case Keys.Control | Keys.I: ConfigurarImpresora(); return true;
            }
            // Ctrl+F1..F12: los atajos del módulo en el que se está
            if ((keyData & Keys.Control) == Keys.Control && (keyData & Keys.Shift) == 0)
            {
                int f = (int)(keyData & Keys.KeyCode) - (int)Keys.F1 + 1;
                if (f >= 1 && f <= 12)
                {
                    if (_modActivo != 0) return _modulo != null && _modulo.Atajo(f);
                    switch (f)
                    {
                        case 1: Cobrar(); return true;
                        case 2: Buscar(); return true;
                        case 3: QuitarArticulo(); return true;
                        case 4: ConsultarPrecio(); return true;
                        case 5: Espera(); return true;
                        case 6: Descuento(); return true;
                        case 7: ArticuloComun(); return true;
                        case 8: CancelarTicket(); return true;
                    }
                    return true;
                }
            }
            if (_modActivo == 0)
            {
                switch (keyData)
                {
                    case Keys.Control | Keys.D: AbrirCajon(); return true;
                    case Keys.Shift | Keys.Subtract: AbrirCajon(); return true;
                    case Keys.Shift | Keys.OemMinus: AbrirCajon(); return true;
                    case Keys.Control | Keys.Delete: CancelarVentaActual(); return true;
                    case Keys.Shift | Keys.Delete: CancelarVentaActual(); return true;
                    case Keys.Add: Cobrar(); return true;
                }
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

        void Mensaje(string t) { _lblMensaje.Text = t; if (_modulo != null) _modulo.Estado(t); }

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
            if (Ui.Autorizar(Permisos.Descuentos, "Descuentos: se requiere autorización") == null) { _captura.Focus(); return; }
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
            if (Ui.Autorizar(Permisos.Movimientos, "Entradas y salidas: se requiere autorización") == null) { _captura.Focus(); return; }
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

        /// <summary>Aplica un envío de la oficina desde la USB (cambios de precio, mercancía…).</summary>
        void RecibirEnvio()
        {
            if (_lineas.Count > 0) { Ui.Aviso("Termine o ponga en espera la venta actual antes de recibir un envío."); return; }
            if (!Envios.EsTienda) { Ui.Aviso("Esta caja todavía no está enlazada con una oficina.\n\nUn administrador la enlaza en F6 Ajustes → Envíos."); return; }
            if (Ui.Autorizar(Permisos.Envios, "Recibir envíos requiere autorización") == null) return;
            using (var f = new Admin.FormRecibirEnvio()) f.ShowDialog(this);
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
                    _autoriza = null;       // el nuevo usuario no hereda la autorización del anterior
                    ActualizarEncabezado();
                    // Sale a Caja después de que termine el clic (el botón que se pulsó vive dentro del módulo que se va a cerrar)
                    if (_modActivo != 0)
                    {
                        if (_modulo != null) _modulo.Visible = false;
                        BeginInvoke((Action)(() => IrAModulo(0)));
                    }
                    Mensaje("Bienvenido(a), " + Sesion.Usuario.Nombre);
                }
            }
            _captura.Focus();
        }

        void CancelarTicket()
        {
            if (Ui.Autorizar(Permisos.Cancelar, "Cancelar ticket: se requiere autorización") == null) { _captura.Focus(); return; }
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
            if (!Puede(Permisos.Ajustes) && Ui.Autorizar(Permisos.Ajustes, "Impresora: se requiere autorización") == null) { _captura.Focus(); return; }
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
            if (Ui.Autorizar(Permisos.Cajon, "Abrir cajón: se requiere autorización") == null) { _captura.Focus(); return; }
            try { Cajon.Abrir(Config.Actual); Mensaje("Cajón abierto."); }
            catch (Exception ex) { Ui.Error(ex, "No se pudo abrir el cajón"); }
        }

        // =====================================================================
        //  Cobro
        // =====================================================================
        void Cobrar()
        {
            if (Envios.EsOficina) { Ui.Aviso("Este equipo es la oficina: aquí no se cobra.\n\nLas ventas se hacen en la caja de la tienda."); _captura.Focus(); return; }
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
                    if (Config.Actual.PedirFondoAlAbrir && !Envios.EsOficina)
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
            if (Admin.SeccionMercancia.SinGuardar > 0 && e.CloseReason == CloseReason.UserClosing &&
                !Ui.Confirmar("Hay " + Admin.SeccionMercancia.SinGuardar + " renglones de mercancía sin guardar (F4 Almacén). Si sale se pierden.\n¿Salir de todas formas?")) { e.Cancel = true; return; }
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
