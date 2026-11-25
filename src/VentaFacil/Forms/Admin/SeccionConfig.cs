using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Printing;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Configuración del negocio, impresora, cajón, báscula y operación.</summary>
    public class SeccionConfig : UserControl
    {
        readonly Config _c;

        // Negocio
        TextBox _nombre, _direccion, _telefono, _rfc, _encabezado, _pie, _banner, _carpetaRespaldo;
        NumericUpDown _caja;
        // Impresora
        ComboBox _impresora, _modo, _ancho, _fuente;
        NumericUpDown _columnas, _tamano, _avance, _copias;
        CheckBox _automatico, _preguntar, _cortar, _letra;
        // Cajón
        CheckBox _abrirCajon;
        ComboBox _cajonModo, _cajonPuerto, _cajonPin;
        // Báscula
        CheckBox _bascula;
        ComboBox _basPuerto, _basBaudios;
        TextBox _basComando;
        Label _peso;
        // Operación
        CheckBox _fondo, _declarar, _sinExistencia, _cajeroCancela, _avisar;

        public SeccionConfig()
        {
            BackColor = Tema.Crema;
            _c = Config.Actual.Copia();

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = Tema.Negrita, Padding = new Point(14, 6) };
            tabs.TabPages.Add(TabNegocio());
            tabs.TabPages.Add(TabImpresora());
            tabs.TabPages.Add(TabCajon());
            tabs.TabPages.Add(TabBascula());
            tabs.TabPages.Add(TabOperacion());
            Controls.Add(tabs);

            var barra = AdminUtil.Barra();
            var guardar = AdminUtil.BotonBarra("Guardar cambios", (s, e) => Guardar(), 140, true);
            barra.Controls.Add(guardar);
            barra.Controls.Add(AdminUtil.BotonBarra("Ticket prueba", (s, e) => Prueba(), 130));
            barra.Controls.Add(AdminUtil.BotonBarra("Probar cajón", (s, e) => ProbarCajon(), 115));
            barra.Controls.Add(AdminUtil.BotonBarra("Licencia", (s, e) => VentaFacil.Forms.Licenciamiento.FormLicencia.Mostrar(Licencia.Evaluar(), true), 95));
            barra.Controls.Add(AdminUtil.BotonBarra("Asistente", (s, e) => Asistente(), 110));
            Controls.Add(barra);
            Controls.Add(AdminUtil.TituloSeccion("Configuración"));
            Llenar();
        }

        /// <summary>El mismo asistente de la instalación, para repasar todo.</summary>
        void Asistente()
        {
            if (Ui.Autorizar(Permisos.SoloAdmin, "El asistente requiere un administrador") == null) return;
            bool restaurado;
            using (var f = new FormAsistente(false)) { f.ShowDialog(FindForm()); restaurado = f.Restaurado; }
            if (restaurado) { Application.Exit(); return; }
            Config.Cargar();
            Control m = Parent;
            while (m != null && !(m is ModuloVista)) m = m.Parent;
            if (m != null) BeginInvoke((Action)(() => ((ModuloVista)m).Recargar()));   // muestra lo que cambió
        }

        // ---------------- construcción de pestañas ----------------
        static TabPage Pagina(string t) { return new TabPage(t) { BackColor = Tema.Crema, Font = Tema.Normal, AutoScroll = true }; }

        static TextBox Texto(TabPage p, string etiqueta, ref int y, int ancho = 420)
        {
            p.Controls.Add(Ui.Etiqueta(etiqueta, 16, y + 4));
            var t = Ui.Caja(220, y, ancho);
            p.Controls.Add(t);
            y += 38;
            return t;
        }

        static CheckBox Casilla(TabPage p, string texto, ref int y)
        {
            var c = new CheckBox { Text = texto, Left = 16, Top = y, AutoSize = true };
            p.Controls.Add(c);
            y += 32;
            return c;
        }

        static ComboBox Combo(TabPage p, string etiqueta, ref int y, int ancho = 300, bool editable = false)
        {
            p.Controls.Add(Ui.Etiqueta(etiqueta, 16, y + 4));
            var c = new ComboBox { Left = 220, Top = y, Width = ancho, DropDownStyle = editable ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList, Font = Tema.Normal };
            p.Controls.Add(c);
            y += 38;
            return c;
        }

        static NumericUpDown Numero(TabPage p, string etiqueta, ref int y, decimal min, decimal max, int dec = 0)
        {
            p.Controls.Add(Ui.Etiqueta(etiqueta, 16, y + 4));
            var n = AdminUtil.Numero(220, y, 100, min, max, dec);
            p.Controls.Add(n);
            y += 38;
            return n;
        }

        TabPage TabNegocio()
        {
            var p = Pagina("Negocio");
            int y = 16;
            _nombre = Texto(p, "Nombre del negocio:", ref y);
            _direccion = Texto(p, "Dirección:", ref y);
            _telefono = Texto(p, "Teléfono:", ref y, 200);
            _rfc = Texto(p, "RFC:", ref y, 200);
            _encabezado = Texto(p, "Mensaje arriba del ticket:", ref y);
            _pie = Texto(p, "Mensaje al pie del ticket:", ref y);
            _banner = Texto(p, "Mensaje en pantalla de venta:", ref y);
            _caja = Numero(p, "Número de esta caja:", ref y, 1, 99);
            p.Controls.Add(Ui.Etiqueta("Carpeta de respaldos:", 16, y + 4));
            _carpetaRespaldo = Ui.Caja(220, y, 300);
            p.Controls.Add(_carpetaRespaldo);
            p.Controls.Add(Ui.Boton("Examinar…", 530, y - 4, 130, 36, (s, e) => ElegirCarpetaRespaldo()));
            y += 38;
            p.Controls.Add(Ui.Etiqueta("Si tiene varias computadoras, cada una debe tener un número de caja distinto.", 16, y, null, Tema.Humo));
            return p;
        }

        TabPage TabImpresora()
        {
            var p = Pagina("Impresora de tickets");
            int y = 12;
            var asistente = new Button { Text = "Elegir o detectar impresora…", Left = 16, Top = y, Width = 300, Height = 40, Font = Tema.Mediana, BackColor = Tema.Azul, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            asistente.Click += (s, e) => AsistenteImpresora();
            Tema.Pastilla(asistente, true);
            p.Controls.Add(asistente);
            p.Controls.Add(Ui.Etiqueta("Recomendado: detecta la impresora de tickets y la prueba.", 326, y + 12, null, Tema.Humo));
            y += 54;
            _impresora = Combo(p, "Impresora:", ref y, 380);
            _impresora.Items.Add(Automatica);
            try { foreach (var i in Impresora.Instaladas()) _impresora.Items.Add(i); } catch { }
            _modo = Combo(p, "Forma de imprimir:", ref y, 380);
            _modo.Items.Add("Usar controlador de Windows (cualquier impresora)");
            _modo.Items.Add("Directo ESC/POS (térmicas, más rápido)");
            _ancho = Combo(p, "Ancho del papel:", ref y, 140);
            _ancho.Items.Add("58 mm"); _ancho.Items.Add("80 mm");
            _ancho.SelectedIndexChanged += (s, e) => { if (_ancho.Focused) _columnas.Value = _ancho.SelectedIndex == 0 ? 32 : 42; };
            _columnas = Numero(p, "Letras por renglón:", ref y, 24, 64);
            p.Controls.Add(Ui.Etiqueta("58 mm ≈ 32   ·   80 mm ≈ 42 (48 en algunas)", 330, y - 34, null, Tema.Humo));
            _fuente = Combo(p, "Letra (modo Windows):", ref y, 200, true);
            foreach (var f in new[] { "Consolas", "Courier New", "Lucida Console", "Arial" }) _fuente.Items.Add(f);
            _tamano = Numero(p, "Tamaño de letra:", ref y, 5, 16, 1);
            _avance = Numero(p, "Líneas antes de cortar:", ref y, 0, 12);
            _copias = Numero(p, "Copias por venta:", ref y, 1, 3);
            _automatico = Casilla(p, "Imprimir ticket automáticamente al cobrar", ref y);
            _preguntar = Casilla(p, "Preguntar antes de imprimir cada ticket", ref y);
            _cortar = Casilla(p, "Cortar el papel al terminar (modo ESC/POS)", ref y);
            _letra = Casilla(p, "Imprimir importe con letra", ref y);
            p.Controls.Add(Ui.Etiqueta("Consejo: si el ticket sale con símbolos raros o en blanco, use \"Controlador de Windows\".", 16, y + 4, null, Tema.Humo));
            return p;
        }

        TabPage TabCajon()
        {
            var p = Pagina("Cajón de dinero");
            int y = 16;
            _abrirCajon = Casilla(p, "Abrir el cajón al cobrar en efectivo", ref y);
            _cajonModo = Combo(p, "Cajón conectado a:", ref y, 380);
            _cajonModo.Items.Add("La impresora de tickets (cable RJ11)");
            _cajonModo.Items.Add("Puerto serial (COM)");
            _cajonModo.Items.Add("No tengo cajón");
            _cajonPuerto = Combo(p, "Puerto COM:", ref y, 140, true);
            LlenarPuertos(_cajonPuerto);
            _cajonPin = Combo(p, "Pin del cajón:", ref y, 140);
            _cajonPin.Items.Add("Pin 2 (normal)"); _cajonPin.Items.Add("Pin 5");
            p.Controls.Add(Ui.Etiqueta("Use \"Probar cajón\" (arriba) para verificar. Si no abre, cambie a Pin 5.", 16, y + 4, null, Tema.Humo));
            return p;
        }

        TabPage TabBascula()
        {
            var p = Pagina("Báscula");
            int y = 16;
            _bascula = Casilla(p, "Tengo báscula conectada por puerto serial (COM / USB-serial)", ref y);
            _basPuerto = Combo(p, "Puerto:", ref y, 140, true);
            LlenarPuertos(_basPuerto);
            _basBaudios = Combo(p, "Velocidad (baudios):", ref y, 140, true);
            foreach (var b in new[] { "1200", "2400", "4800", "9600", "19200" }) _basBaudios.Items.Add(b);
            _basComando = Texto(p, "Comando para pedir peso:", ref y, 120);
            p.Controls.Add(Ui.Etiqueta("Torrey / Rhino: P   ·   Otras: W  o  vacío si la báscula transmite sola", 350, y - 34, null, Tema.Humo));
            p.Controls.Add(Ui.Boton("Leer peso", 16, y, 130, 36, (s, e) => LeerPeso()));
            _peso = Ui.Etiqueta("", 160, y + 6, Tema.Mediana, Tema.Azul);
            p.Controls.Add(_peso);
            y += 50;
            p.Controls.Add(Ui.Etiqueta("Sin báscula conectada, el sistema pide el peso a mano para productos a granel.", 16, y, null, Tema.Humo));
            return p;
        }

        TabPage TabOperacion()
        {
            var p = Pagina("Operación");
            int y = 16;
            _fondo = Casilla(p, "Pedir fondo de caja (cambio inicial) al abrir el turno", ref y);
            _declarar = Casilla(p, "Pedir el efectivo contado al hacer el corte (muestra faltante / sobrante)", ref y);
            _sinExistencia = Casilla(p, "Permitir vender productos sin existencia", ref y);
            _cajeroCancela = Casilla(p, "Los cajeros pueden cancelar y hacer descuentos (salvo los que tengan permisos propios)", ref y);
            _avisar = Casilla(p, "Mostrar aviso cuando un código no se encuentra", ref y);
            return p;
        }

        static void LlenarPuertos(ComboBox c)
        {
            try { foreach (var n in SerialPort.GetPortNames()) c.Items.Add(n); } catch { }
            if (c.Items.Count == 0) for (int i = 1; i <= 6; i++) c.Items.Add("COM" + i);
        }

        static void Elegir(ComboBox c, string valor)
        {
            if (string.IsNullOrEmpty(valor)) return;
            int i = c.FindStringExact(valor);
            if (i < 0 && c.DropDownStyle == ComboBoxStyle.DropDownList) { c.Items.Add(valor); i = c.Items.Count - 1; }
            if (i >= 0) c.SelectedIndex = i; else c.Text = valor;
        }

        static decimal Limitar(NumericUpDown n, decimal v) { return Math.Max(n.Minimum, Math.Min(n.Maximum, v)); }

        // ---------------- llenar / leer ----------------
        void Llenar()
        {
            _nombre.Text = _c.NegocioNombre; _direccion.Text = _c.NegocioDireccion; _telefono.Text = _c.NegocioTelefono; _rfc.Text = _c.NegocioRfc;
            _encabezado.Text = _c.TicketEncabezado; _pie.Text = _c.TicketPie; _banner.Text = _c.Banner; _caja.Value = Limitar(_caja, _c.Caja);
            _carpetaRespaldo.Text = _c.CarpetaRespaldo;

            Elegir(_impresora, string.IsNullOrEmpty(_c.Impresora) ? Automatica : _c.Impresora);
            _modo.SelectedIndex = _c.ModoImpresion == "ESCPOS" ? 1 : 0;
            _ancho.SelectedIndex = _c.AnchoTicket <= 58 ? 0 : 1;
            _columnas.Value = Limitar(_columnas, _c.Columnas);
            Elegir(_fuente, _c.Fuente);
            _tamano.Value = Limitar(_tamano, (decimal)_c.TamanoFuente);
            _avance.Value = Limitar(_avance, _c.LineasAvance);
            _copias.Value = Limitar(_copias, _c.Copias);
            _automatico.Checked = _c.ImprimirAutomatico; _preguntar.Checked = _c.PreguntarImprimir; _cortar.Checked = _c.Cortar; _letra.Checked = _c.ImporteConLetra;

            _abrirCajon.Checked = _c.AbrirCajon;
            _cajonModo.SelectedIndex = _c.CajonModo == "SERIAL" ? 1 : _c.CajonModo == "NINGUNO" ? 2 : 0;
            Elegir(_cajonPuerto, _c.CajonPuerto);
            _cajonPin.SelectedIndex = _c.CajonPin == 1 ? 1 : 0;

            _bascula.Checked = _c.BasculaActiva;
            Elegir(_basPuerto, _c.BasculaPuerto);
            Elegir(_basBaudios, _c.BasculaBaudios.ToString());
            _basComando.Text = _c.BasculaComando;

            _fondo.Checked = _c.PedirFondoAlAbrir; _declarar.Checked = _c.DeclararEfectivoEnCorte; _sinExistencia.Checked = _c.PermitirVentaSinExistencia;
            _cajeroCancela.Checked = _c.CajeroPuedeCancelar; _avisar.Checked = _c.AvisarNoEncontrado;
        }

        /// <summary>Pasa lo capturado a la copia de configuración.</summary>
        bool Leer()
        {
            if (_nombre.Text.Trim() == "") { Ui.Aviso("Escriba el nombre del negocio (pestaña Negocio)."); return false; }
            _c.NegocioNombre = _nombre.Text.Trim(); _c.NegocioDireccion = _direccion.Text.Trim(); _c.NegocioTelefono = _telefono.Text.Trim();
            _c.NegocioRfc = _rfc.Text.Trim(); _c.TicketEncabezado = _encabezado.Text.Trim(); _c.TicketPie = _pie.Text.Trim(); _c.Banner = _banner.Text.Trim();
            _c.Caja = (int)_caja.Value;
            _c.CarpetaRespaldo = _carpetaRespaldo.Text.Trim();
            _c.CarpetaRespaldoRevisada = true;

            _c.Impresora = _impresora.SelectedItem != null && _impresora.SelectedItem.ToString() != Automatica ? _impresora.SelectedItem.ToString() : "";
            _c.ModoImpresion = _modo.SelectedIndex == 1 ? "ESCPOS" : "WINDOWS";
            _c.AnchoTicket = _ancho.SelectedIndex == 0 ? 58 : 80;
            _c.Columnas = (int)_columnas.Value;
            _c.Fuente = _fuente.Text.Trim() == "" ? "Consolas" : _fuente.Text.Trim();
            _c.TamanoFuente = (float)_tamano.Value;
            _c.LineasAvance = (int)_avance.Value;
            _c.Copias = (int)_copias.Value;
            _c.ImprimirAutomatico = _automatico.Checked; _c.PreguntarImprimir = _preguntar.Checked; _c.Cortar = _cortar.Checked; _c.ImporteConLetra = _letra.Checked;

            _c.AbrirCajon = _abrirCajon.Checked;
            _c.CajonModo = _cajonModo.SelectedIndex == 1 ? "SERIAL" : _cajonModo.SelectedIndex == 2 ? "NINGUNO" : "IMPRESORA";
            _c.CajonPuerto = _cajonPuerto.Text.Trim();
            _c.CajonPin = _cajonPin.SelectedIndex == 1 ? 1 : 0;

            _c.BasculaActiva = _bascula.Checked;
            _c.BasculaPuerto = _basPuerto.Text.Trim();
            int baud;
            _c.BasculaBaudios = int.TryParse(_basBaudios.Text.Trim(), out baud) && baud > 0 ? baud : 9600;
            _c.BasculaComando = _basComando.Text;

            _c.PedirFondoAlAbrir = _fondo.Checked; _c.DeclararEfectivoEnCorte = _declarar.Checked; _c.PermitirVentaSinExistencia = _sinExistencia.Checked;
            _c.CajeroPuedeCancelar = _cajeroCancela.Checked; _c.AvisarNoEncontrado = _avisar.Checked;
            return true;
        }

        void ElegirCarpetaRespaldo()
        {
            using (var d = new FolderBrowserDialog { Description = "Elija la carpeta para los respaldos (escritorio o memoria USB)" })
            {
                if (!string.IsNullOrEmpty(_carpetaRespaldo.Text)) d.SelectedPath = _carpetaRespaldo.Text;
                if (d.ShowDialog() == DialogResult.OK) _carpetaRespaldo.Text = d.SelectedPath;
            }
        }

        void Guardar()
        {
            if (!Leer()) return;
            try
            {
                _c.Copia().Guardar();
                Ui.Info("Configuración guardada.");
            }
            catch (Exception ex) { Ui.Error(ex, "Guardar configuración"); }
        }

        void Prueba()
        {
            if (!Leer()) return;
            try
            {
                var c = _c.Copia(); c.Copias = 1;
                var t = new Ticket(c.Columnas);
                t.Centro(c.NegocioNombre, Ticket.Estilo.Negrita);
                t.Centro("TICKET DE PRUEBA", Ticket.Estilo.Grande);
                t.Separador();
                t.Linea("Impresora: " + (c.Impresora == "" ? "(predeterminada)" : c.Impresora));
                t.Linea("Modo: " + (c.ModoImpresion == "ESCPOS" ? "ESC/POS directo" : "Controlador de Windows"));
                t.Linea("Ancho: " + c.AnchoTicket + " mm, " + c.Columnas + " letras");
                t.Separador();
                t.Linea("Acentos: á é í ó ú  Ñ ñ  ¿? ¡!");
                t.Par("Artículo de prueba", Fmt.Dinero(12.5m));
                t.Par("Jamón de pavo 0.250 kg", Fmt.Dinero(45m));
                t.Par("TOTAL:", Fmt.Dinero(57.5m), Ticket.Estilo.Negrita);
                t.Linea(NumeroALetras.Pesos(57.5m));
                t.Linea("1234567890123456789012345678901234567890123456789012345678901234".Substring(0, c.Columnas));
                t.Separador();
                t.Centro("Si esta línea se ve completa, el ancho está bien");
                t.Linea(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                Impresora.Imprimir(t, c);
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo imprimir la prueba"); }
        }

        void ProbarCajon()
        {
            if (!Leer()) return;
            try
            {
                if (_c.CajonModo == "NINGUNO") { Ui.Aviso("Está configurado \"No tengo cajón\"."); return; }
                Cajon.Abrir(_c);
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo abrir el cajón"); }
        }

        const string Automatica = "(Automática: la predeterminada de Windows)";

        /// <summary>Abre el asistente de impresoras y trae lo que se guardó, sin perder lo capturado en otras pestañas.</summary>
        void AsistenteImpresora()
        {
            if (!Leer()) return;
            if (!FormImpresoras.Configurar(FindForm())) return;
            var g = Config.Actual;
            _c.Impresora = g.Impresora; _c.ModoImpresion = g.ModoImpresion; _c.AnchoTicket = g.AnchoTicket; _c.Columnas = g.Columnas;
            _c.TamanoFuente = g.TamanoFuente; _c.Cortar = g.Cortar; _c.CajonPin = g.CajonPin; _c.ImpresoraRevisada = true;
            Llenar();
            Ui.Info("Impresora guardada.");
        }

        void LeerPeso()
        {
            if (!Leer()) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                _peso.Text = Fmt.Cant(Bascula.LeerPeso(_c), true) + " kg";
            }
            catch (Exception ex) { _peso.Text = ""; Ui.Error(ex, "Báscula"); }
            finally { Cursor = Cursors.Default; }
        }
    }
}
