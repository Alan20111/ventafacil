using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Printing;

namespace VentaFacil.UI
{
    /// <summary>
    /// Elegir impresora de tickets sin complicaciones:
    ///  - Detectar automáticamente (elige la térmica, o la predeterminada de Windows)
    ///  - Automático: usar siempre la predeterminada de Windows
    ///  - Manual: escoger de la lista de impresoras instaladas
    /// Todo va por la cola de impresión de Windows: no hay que configurar puertos.
    /// </summary>
    public class FormImpresoras : Dialogo
    {
        readonly Config _c;
        readonly Label _actual, _resultado;
        readonly RadioButton _auto, _manual, _r58, _r80, _modoWin, _modoEsc;
        readonly DataGridView _grid;
        List<InfoImpresora> _lista = new List<InfoImpresora>();

        /// <summary>Abre la ventana sobre la configuración actual y guarda si el usuario acepta.</summary>
        public static bool Configurar(IWin32Window dueno, bool detectarAlAbrir = false)
        {
            using (var f = new FormImpresoras(Config.Actual.Copia(), detectarAlAbrir))
                return f.ShowDialog(dueno) == DialogResult.OK;
        }

        FormImpresoras(Config c, bool detectarAlAbrir) : base("Impresora de tickets", 860, 610)
        {
            _c = c;
            var cab = new PanelTitulo { Titulo = "Impresora de tickets", Subtitulo = "elija o detecte", Height = 48 };
            Controls.Add(cab);

            _actual = Ui.Etiqueta("", 16, 58, Tema.Mediana, Tema.Azul);
            Controls.Add(_actual);

            var detectar = new Button { Text = "DETECTAR AUTOMÁTICAMENTE  (F2)", Left = 16, Top = 92, Width = 400, Height = 50, Font = Tema.Mediana, BackColor = Tema.Verde, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            detectar.Click += (s, e) => DetectarAuto();
            Controls.Add(detectar);
            _resultado = new Label { Left = 428, Top = 92, Width = 416, Height = 50, Font = Tema.Negrita, ForeColor = Tema.Verde, TextAlign = ContentAlignment.MiddleLeft };
            Controls.Add(_resultado);

            _auto = new RadioButton { Text = "Automático: usar siempre la impresora PREDETERMINADA de Windows", Left = 16, Top = 152, Width = 820, Font = Tema.Negrita };
            _manual = new RadioButton { Text = "Manual: usar la impresora que elija de esta lista  (doble clic o Enter)", Left = 16, Top = 178, Width = 820, Font = Tema.Negrita };
            Controls.Add(_auto); Controls.Add(_manual);

            _grid = new DataGridView { Left = 16, Top = 208, Width = 828, Height = 220 };
            Ui.EstiloGrid(_grid);
            _grid.Columns.Add(Ui.Columna("nombre", "Impresora", 38));
            _grid.Columns.Add(Ui.Columna("tipo", "Tipo", 20));
            _grid.Columns.Add(Ui.Columna("conexion", "Conexión", 12));
            _grid.Columns.Add(Ui.Columna("estado", "Estado", 18));
            _grid.Columns.Add(Ui.Columna("pred", "Predet.", 8));
            Controls.Add(_grid);

            // Ajuste manual sencillo
            var grupoPapel = new GroupBox { Text = "Ancho del papel", Left = 16, Top = 436, Width = 260, Height = 58, Font = Tema.Negrita };
            _r58 = new RadioButton { Text = "58 mm", Left = 14, Top = 24, Width = 90, Font = Tema.Normal };
            _r80 = new RadioButton { Text = "80 mm", Left = 120, Top = 24, Width = 90, Font = Tema.Normal };
            grupoPapel.Controls.Add(_r58); grupoPapel.Controls.Add(_r80);
            Controls.Add(grupoPapel);
            var grupoModo = new GroupBox { Text = "Forma de imprimir", Left = 288, Top = 436, Width = 556, Height = 58, Font = Tema.Negrita };
            _modoWin = new RadioButton { Text = "Normal (controlador de Windows)", Left = 14, Top = 24, Width = 260, Font = Tema.Normal };
            _modoEsc = new RadioButton { Text = "Directo térmica (ESC/POS, corta papel)", Left = 280, Top = 24, Width = 270, Font = Tema.Normal };
            grupoModo.Controls.Add(_modoWin); grupoModo.Controls.Add(_modoEsc);
            Controls.Add(grupoModo);

            int y = 504;
            Controls.Add(Ui.Boton("F3  Imprimir prueba", 16, y, 170, 42, (s, e) => Prueba()));
            Controls.Add(Ui.Boton("F4  Probar cajón", 192, y, 150, 42, (s, e) => ProbarCajon()));
            Controls.Add(Ui.Boton("F5  Actualizar lista", 348, y, 160, 42, (s, e) => Cargar()));
            Controls.Add(Ui.Boton("Impresoras de Windows…", 514, y, 190, 42, (s, e) => AbrirPanelWindows()));
            var guardar = Ui.Boton("Guardar", 16, y + 52, 170, 40, (s, e) => Guardar());
            guardar.BackColor = Tema.Azul; guardar.ForeColor = Color.White;
            Controls.Add(guardar);
            Controls.Add(Ui.Boton("Cancelar (Esc)", 192, y + 52, 150, 40, (s, e) => { DialogResult = DialogResult.Cancel; }));
            Controls.Add(Ui.Etiqueta("Si Windows imprime su página de prueba, aquí también funcionará. No hace falta configurar puertos.", 350, y + 62, null, Color.Gray));

            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ElegirSeleccionada(); };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ElegirSeleccionada(); } };
            _manual.CheckedChanged += (s, e) => { _grid.Enabled = true; MostrarActual(); };
            _auto.CheckedChanged += (s, e) => MostrarActual();
            KeyDown += (s, e) =>
            {
                switch (e.KeyCode)
                {
                    case Keys.F2: e.Handled = true; DetectarAuto(); break;
                    case Keys.F3: e.Handled = true; Prueba(); break;
                    case Keys.F4: e.Handled = true; ProbarCajon(); break;
                    case Keys.F5: e.Handled = true; Cargar(); break;
                }
            };

            Shown += (s, e) =>
            {
                Cargar();
                PonerOpciones();
                if (detectarAlAbrir) DetectarAuto();
                _grid.Focus();
            };
        }

        void Cargar()
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                _lista = Detector.Listar();
                _grid.Rows.Clear();
                foreach (var i in _lista)
                {
                    int r = _grid.Rows.Add(i.Nombre, i.Tipo, i.Conexion, i.Estado, i.Predeterminada ? "✔" : "");
                    var fila = _grid.Rows[r];
                    if (i.EsVirtual) fila.DefaultCellStyle.ForeColor = Color.Gray;
                    else if (i.Desconectada) fila.DefaultCellStyle.ForeColor = Tema.Rojo;
                    else if (i.EsTicket) fila.DefaultCellStyle.Font = Tema.Negrita;
                }
                SeleccionarEnLista(string.IsNullOrEmpty(_c.Impresora) ? Impresora.Predeterminada() : _c.Impresora);
                if (_lista.Count == 0) _resultado.Text = "No hay impresoras instaladas en Windows.";
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudieron leer las impresoras"); }
            finally { Cursor = Cursors.Default; }
        }

        void PonerOpciones()
        {
            _auto.Checked = string.IsNullOrEmpty(_c.Impresora);
            _manual.Checked = !_auto.Checked;
            _r58.Checked = _c.AnchoTicket <= 58; _r80.Checked = !_r58.Checked;
            _modoEsc.Checked = _c.ModoImpresion == "ESCPOS"; _modoWin.Checked = !_modoEsc.Checked;
            MostrarActual();
        }

        void SeleccionarEnLista(string nombre)
        {
            for (int i = 0; i < _lista.Count; i++)
                if (string.Equals(_lista[i].Nombre, nombre, StringComparison.OrdinalIgnoreCase))
                {
                    _grid.CurrentCell = _grid.Rows[i].Cells[0];
                    return;
                }
        }

        InfoImpresora Seleccionada()
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Index >= _lista.Count) return null;
            return _lista[_grid.CurrentRow.Index];
        }

        void MostrarActual()
        {
            string nombre = _auto.Checked ? Impresora.Predeterminada() + "  (predeterminada de Windows)" : (Seleccionada() != null ? Seleccionada().Nombre : _c.Impresora);
            _actual.Text = "Se usará: " + (string.IsNullOrEmpty(nombre) ? "(ninguna)" : nombre);
        }

        /// <summary>Pasa lo elegido en pantalla a la configuración.</summary>
        void Leer(Config c)
        {
            var sel = Seleccionada();
            c.Impresora = _auto.Checked || sel == null ? "" : sel.Nombre;
            c.AnchoTicket = _r58.Checked ? 58 : 80;
            if (c.AnchoTicket == 58 && c.Columnas > 36) { c.Columnas = 32; c.TamanoFuente = 7f; }
            if (c.AnchoTicket == 80 && c.Columnas < 40) { c.Columnas = 42; c.TamanoFuente = 8f; }
            c.ModoImpresion = _modoEsc.Checked ? "ESCPOS" : "WINDOWS";
        }

        void ElegirSeleccionada()
        {
            var sel = Seleccionada();
            if (sel == null) return;
            if (sel.EsVirtual && !Ui.Confirmar("\"" + sel.Nombre + "\" es una impresora virtual (no imprime en papel). ¿Usarla de todos modos?")) return;
            _manual.Checked = true;
            Detector.Aplicar(sel, _c, true);
            PonerOpciones();
            _resultado.ForeColor = Tema.Verde;
            _resultado.Text = "Elegida: " + sel.Nombre + ". Pulse F3 para imprimir una prueba.";
        }

        void DetectarAuto()
        {
            Cargar();
            var mejor = Detector.Recomendar(_lista);
            if (mejor == null)
            {
                _resultado.ForeColor = Tema.Rojo;
                _resultado.Text = "No se encontró ninguna impresora. Instálela en Windows y pulse F5.";
                return;
            }
            Detector.Aplicar(mejor, _c, true);
            // si la recomendada es la predeterminada y no es de tickets, se deja en automático
            if (mejor.Predeterminada && !mejor.EsTicket) _c.Impresora = "";
            SeleccionarEnLista(mejor.Nombre);
            PonerOpciones();
            _resultado.ForeColor = Tema.Verde;
            _resultado.Text = "Detectada: " + mejor.Nombre + " (" + mejor.Tipo + ")." +
                (mejor.Desconectada ? "  ¡Windows la marca sin conexión!" : "  Pulse F3 para probar.");
            if (mejor.Desconectada) _resultado.ForeColor = Tema.Rojo;
        }

        void Prueba()
        {
            var c = _c.Copia();
            Leer(c);
            c.Copias = 1;
            try
            {
                Cursor = Cursors.WaitCursor;
                var real = Impresora.Resuelta(c);
                var t = new Ticket(c.Columnas);
                t.Centro(c.NegocioNombre, Ticket.Estilo.Negrita);
                t.Centro("PRUEBA DE IMPRESORA", Ticket.Estilo.Grande);
                t.Separador();
                t.Linea("Impresora: " + real.Impresora);
                t.Linea("Modo: " + (c.ModoImpresion == "ESCPOS" ? "Directo térmica (ESC/POS)" : "Normal (Windows)"));
                t.Linea("Papel: " + c.AnchoTicket + " mm, " + c.Columnas + " letras");
                t.Separador();
                t.Linea("Acentos: á é í ó ú  Ñ ñ  ¿? ¡!");
                t.Par("Cacahuate japonés 0.500 kg", Fmt.Dinero(45m));
                t.Par("TOTAL:", Fmt.Dinero(45m), Ticket.Estilo.Negrita);
                t.Linea("1234567890123456789012345678901234567890123456789012345678901234".Substring(0, c.Columnas));
                t.Centro("Si la línea de números se ve completa,");
                t.Centro("el ancho está bien.");
                t.Linea(DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                Impresora.Imprimir(t, c);
                Cursor = Cursors.Default;
                if (Ui.Confirmar("Se mandó la prueba a \"" + real.Impresora + "\".\n\n¿Salió bien el ticket?", "Prueba de impresora"))
                {
                    _resultado.ForeColor = Tema.Verde;
                    _resultado.Text = "Prueba correcta. Pulse Guardar.";
                }
                else
                {
                    string sugerencia = c.ModoImpresion == "ESCPOS"
                        ? "Cambie a \"Normal (controlador de Windows)\" y pruebe otra vez."
                        : "Si salió cortado o muy chico, cambie el ancho del papel. Si no salió nada, pruebe \"Directo térmica (ESC/POS)\" o elija otra impresora de la lista.";
                    Ui.Info(sugerencia, "Sugerencia");
                }
            }
            catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex, "No se pudo imprimir la prueba"); }
        }

        void ProbarCajon()
        {
            var c = _c.Copia();
            Leer(c);
            if (c.CajonModo == "NINGUNO") c.CajonModo = "IMPRESORA";
            try
            {
                Cajon.Abrir(c);
                if (!Ui.Confirmar("¿Se abrió el cajón?", "Probar cajón"))
                {
                    _c.CajonPin = _c.CajonPin == 1 ? 0 : 1;
                    c.CajonPin = _c.CajonPin;
                    Cajon.Abrir(c);
                    Ui.Info(Ui.Confirmar("Se probó con el otro conector (pin " + (c.CajonPin == 1 ? "5" : "2") + "). ¿Se abrió ahora?", "Probar cajón")
                        ? "Listo. Se quedará con esa opción al guardar."
                        : "Revise que el cable del cajón esté conectado a la impresora (conector tipo teléfono).", "Cajón");
                }
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo abrir el cajón"); }
        }

        void AbrirPanelWindows()
        {
            try { Process.Start("control.exe", "printers"); } catch (Exception ex) { Ui.Error(ex, "Panel de impresoras"); }
        }

        void Guardar()
        {
            Leer(_c);
            try
            {
                var c = Config.Actual.Copia();
                c.Impresora = _c.Impresora; c.ModoImpresion = _c.ModoImpresion; c.AnchoTicket = _c.AnchoTicket; c.Columnas = _c.Columnas;
                c.TamanoFuente = _c.TamanoFuente; c.Cortar = _c.Cortar; c.CajonPin = _c.CajonPin; c.ImpresoraRevisada = true;
                c.Guardar();
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Ui.Error(ex, "Guardar impresora"); }
        }
    }
}
