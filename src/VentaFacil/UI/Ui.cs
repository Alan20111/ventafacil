using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;

namespace VentaFacil.UI
{
    /// <summary>
    /// Sistema visual: papel crema, bandas taupe, bordes de 1 px color piedra, botones en píldora
    /// y títulos ligeros (Segoe UI Light). Negro como color principal; sin acentos decorativos.
    /// </summary>
    public static class Tema
    {
        // Paleta: azul del icono como color dominante, con fondos claros y negro para el texto
        public static readonly Color Azul = Color.FromArgb(0, 70, 160);
        public static readonly Color AzulOscuro = Color.FromArgb(0, 46, 112);
        public static readonly Color AzulClaro2 = Color.FromArgb(200, 218, 242);
        public static readonly Color Crema = Color.FromArgb(253, 252, 252);
        public static readonly Color Taupe = Color.FromArgb(240, 244, 250);
        public static readonly Color Piedra = Color.FromArgb(190, 206, 230);
        public static readonly Color Grafito = AzulOscuro;
        public static readonly Color Humo = Color.FromArgb(70, 80, 96);
        public static readonly Color Ceniza = Color.FromArgb(120, 130, 145);
        public static readonly Color Texto = Color.FromArgb(10, 12, 18);
        public static readonly Color Rojo = Color.FromArgb(190, 20, 20);
        public static readonly Color Verde = Color.FromArgb(20, 120, 50);
        public static readonly Color Gris = Taupe;

        // Alias para no tocar todo el código
        public static readonly Color AzulClaro = Piedra;
        public static readonly Color AzulFondo = Taupe;

        const string Fuente = "Segoe UI";
        // Letras grandes y gruesas para que se lean de lejos
        public static readonly Font Normal = new Font(Fuente, 11f, FontStyle.Bold);
        public static readonly Font Negrita = new Font(Fuente, 11f, FontStyle.Bold);
        public static readonly Font Mediana = new Font(Fuente, 14f, FontStyle.Bold);
        public static readonly Font Grande = new Font(Fuente, 22f, FontStyle.Bold);
        public static readonly Font Enorme = new Font(Fuente, 42f, FontStyle.Bold);
        public static readonly Font Captura = new Font(Fuente, 20f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 12f, FontStyle.Bold);

        /// <summary>Fondo de los botones secundarios: se distinguen por contraste, sin contorno.</summary>
        public static readonly Color Suave = Color.FromArgb(214, 227, 246);

        /// <summary>Animaciones de botones y de la barra de módulos (ninguna dura más de ~150 ms).</summary>
        public static bool Animaciones { get { return Config.Actual == null || Config.Actual.Animaciones; } }

        /// <summary>
        /// Botón plano: sin borde, esquinas suaves y texto SIEMPRE centrado. Se dibuja completo a mano, así que no
        /// depende de cómo Windows acomode el texto ni del contorno de «botón con foco».
        /// </summary>
        public static void Pastilla(Button b, bool principal = false)
        {
            if (principal) Plano(b, Azul, AzulOscuro, Color.FromArgb(0, 34, 84));
            else Plano(b, Suave, Color.FromArgb(196, 214, 241), Color.FromArgb(176, 200, 234));
        }

        /// <summary>Acciones de peligro (cierre de turno, eliminar, cancelar ticket).</summary>
        public static void BotonPeligro(Button b) { Plano(b, Rojo, Color.FromArgb(160, 15, 15), Color.FromArgb(130, 10, 10)); }

        static void Plano(Button b, Color reposo, Color hover, Color abajo)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.UseVisualStyleBackColor = false;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.Cursor = Cursors.Hand;
            FundidoBoton.De(b).Definir(reposo, hover, abajo);
            b.Paint -= Dibujar;
            b.Paint += Dibujar;
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int atributo, ref int valor, int tam);

        /// <summary>Windows 11: barra de título del azul de VentaFácil y esquinas redondas. En los demás no hace nada.</summary>
        public static void VentanaModerna(Form f)
        {
            try
            {
                int esquinas = 2, azul = ColorTranslator.ToWin32(AzulOscuro), blanco = ColorTranslator.ToWin32(Color.White);
                DwmSetWindowAttribute(f.Handle, 33, ref esquinas, 4);
                DwmSetWindowAttribute(f.Handle, 35, ref azul, 4);
                DwmSetWindowAttribute(f.Handle, 36, ref blanco, 4);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        public static Color FondoDe(Control c)
        {
            for (var p = c.Parent; p != null; p = p.Parent) if (p.BackColor.A == 255) return p.BackColor;
            return Crema;
        }

        static void Dibujar(object sender, PaintEventArgs e)
        {
            var b = (Button)sender;
            var f = FundidoBoton.Buscar(b);
            if (f == null || b.Width < 4 || b.Height < 4) return;
            var g = e.Graphics;
            Color fondo = !b.Enabled ? Color.FromArgb(228, 232, 238) : f.Actual;
            if (b.Enabled && b.Focused && b.TabStop && !f.Oprimido) fondo = FundidoBoton.Mezcla(fondo, f.Abajo, 0.45f);   // foco del teclado: más oscuro, sin borde
            Color tinta = !b.Enabled ? Color.FromArgb(140, 148, 160) : fondo.GetBrightness() < 0.5f ? Color.White : AzulOscuro;
            g.Clear(FondoDe(b));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(fondo))
            using (var ruta = Forma(b.Width, b.Height, 8)) g.FillPath(br, ruta);
            int baja = f.Oprimido && Animaciones ? 1 : 0;
            var bf = b as BotonF;
            if (bf != null && !string.IsNullOrEmpty(bf.Tecla))
            {   // atajo chico arriba y nombre abajo, los dos centrados como un bloque
                var t1 = TextRenderer.MeasureText(bf.Tecla, BotonF.LetraTecla, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                var t2 = TextRenderer.MeasureText(bf.Nombre, b.Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                int y = (b.Height - t1.Height - t2.Height) / 2 + baja;
                var fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
                TextRenderer.DrawText(g, bf.Tecla, BotonF.LetraTecla, new Rectangle(0, y, b.Width, t1.Height), FundidoBoton.Mezcla(tinta, fondo, 0.30f), fl);
                TextRenderer.DrawText(g, bf.Nombre, b.Font, new Rectangle(2, y + t1.Height, b.Width - 4, t2.Height), tinta, fl);
                return;
            }
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            var tam = TextRenderer.MeasureText(b.Text, b.Font, new Size(b.Width - 8, int.MaxValue), flags);
            TextRenderer.DrawText(g, b.Text, b.Font, new Rectangle(4, (b.Height - tam.Height) / 2 + baja, b.Width - 8, tam.Height), tinta, flags);
        }

        /// <summary>Rectángulo redondeado suave (radio estándar de 8 px para interfaces modernas).</summary>
        static System.Drawing.Drawing2D.GraphicsPath Forma(int ancho, int alto, int radio = 8)
        {
            int r = Math.Max(2, Math.Min(radio, Math.Min(ancho, alto) / 2));
            int d = r * 2;
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(0, 0, d, d, 180, 90);
            p.AddArc(ancho - d, 0, d, d, 270, 90);
            p.AddArc(ancho - d, alto - d, d, d, 0, 90);
            p.AddArc(0, alto - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

    }

    /// <summary>Color de un botón: pasa suave de reposo a «encima» y de un estado a otro. Usa el motor único de animaciones.</summary>
    public class FundidoBoton
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, FundidoBoton> Tabla = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, FundidoBoton>();
        public Color Reposo = Tema.Crema, Hover = Color.FromArgb(230, 240, 252), Abajo = Tema.Piedra;
        readonly Button _b;
        float _x;               // 0 = reposo, 1 = encima
        Color _antes; float _cambio = 1f;   // paso de un estilo a otro (p. ej. vista elegida)
        bool _abajo;
        public Color Actual { get; private set; }
        public bool Oprimido { get { return _abajo; } }

        public static FundidoBoton Buscar(Control c) { FundidoBoton f; return Tabla.TryGetValue(c, out f) ? f : null; }
        public static FundidoBoton De(Button b)
        {
            FundidoBoton f;
            if (!Tabla.TryGetValue(b, out f)) { f = new FundidoBoton(b); Tabla.Add(b, f); }
            return f;
        }

        FundidoBoton(Button b)
        {
            _b = b;
            b.MouseEnter += (s, e) => Hacia(1f, 120);
            b.MouseLeave += (s, e) => { _abajo = false; Hacia(0f, 180); };
            b.MouseDown += (s, e) => { _abajo = true; Aplicar(); };
            b.MouseUp += (s, e) => { _abajo = false; Aplicar(); };
            b.GotFocus += (s, e) => b.Invalidate();
            b.LostFocus += (s, e) => b.Invalidate();
        }

        public void Definir(Color reposo, Color hover, Color abajo)
        {
            bool nuevo = Actual.IsEmpty || !_b.IsHandleCreated;
            _antes = Actual;
            Reposo = reposo; Hover = hover; Abajo = abajo;
            _b.FlatAppearance.MouseDownBackColor = abajo;
            if (nuevo) { _cambio = 1f; Aplicar(); return; }
            Animacion.Ejecutar(_b, "estilo", 160, p => { _cambio = p; Aplicar(); });
        }

        void Hacia(float destino, int ms)
        {
            float desde = _x;
            Animacion.Ejecutar(_b, "encima", ms, p => { _x = Animacion.Entre(desde, destino, p); Aplicar(); });
        }

        void Aplicar()
        {
            var c = _abajo ? Abajo : Mezcla(Reposo, Hover, _x);
            if (_cambio < 1f && !_antes.IsEmpty) c = Mezcla(_antes, c, _cambio);
            if (c == Actual) return;
            Actual = c;
            _b.BackColor = c;
            _b.FlatAppearance.MouseOverBackColor = c;
            _b.Invalidate();
        }

        public static Color Mezcla(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>Encabezado azul con el billete, título blanco y grueso.</summary>
    public class PanelTitulo : Panel
    {
        public bool ConLogo = true;
        public string Titulo = "";
        public string Subtitulo = "";
        public PanelTitulo() { DoubleBuffered = true; Height = 64; Dock = DockStyle.Top; BackColor = Tema.Azul; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            e.Graphics.Clear(Tema.Azul);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // centrado en la altura que tenga; en encabezados bajos la letra es menor para que no se corte
            var letra = Height >= 60 ? Tema.Grande : Tema.Mediana;
            var tam = TextRenderer.MeasureText(Titulo, letra);
            int y = (Height - tam.Height) / 2;
            int x0 = 16;
            if (ConLogo && Iconos.Billete != null)
            {
                int ancho = Height >= 60 ? 56 : 40;
                Marca.Blanco(e.Graphics, 14, (Height - ancho * 5 / 8) / 2, ancho);
                x0 += ancho + 4;
            }
            TextRenderer.DrawText(e.Graphics, Titulo, letra, new Point(x0, y), Color.White);
            if (!string.IsNullOrEmpty(Subtitulo))
            {
                var chica = Height >= 60 ? Tema.Mediana : Tema.Normal;
                var t2 = TextRenderer.MeasureText(Subtitulo, chica);
                TextRenderer.DrawText(e.Graphics, Subtitulo, chica, new Point(x0 + 8 + tam.Width, y + tam.Height - t2.Height - (letra == Tema.Grande ? 4 : 1)), Color.FromArgb(220, 235, 255));
            }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    }

    /// <summary>Botón con atajo arriba y nombre abajo: "Ctrl+F1" / "Productos".</summary>
    public class BotonF : Button
    {
        public static readonly Font LetraTecla = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        public string Tecla, Nombre;
        public BotonF(string tecla, string texto, EventHandler click)
        {
            Tecla = tecla; Nombre = texto;
            Text = (string.IsNullOrEmpty(tecla) ? "" : tecla + "\n") + texto;   // para lectores de pantalla
            Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            Tema.Pastilla(this);
            TabStop = false;
            if (click != null) Click += click;
        }
    }

    public static class Ui
    {
        public static void Info(string msg, string titulo = "VentaFácil") { FormMensaje.Mostrar(msg, titulo, Tono.Bien, false); }
        /// <summary>Dato o explicación, sin decir que algo «salió bien».</summary>
        public static void Nota(string msg, string titulo) { FormMensaje.Mostrar(msg, titulo, Tono.Info, false); }
        /// <summary>Aviso breve de que algo salió bien: se va solo.</summary>
        public static void Listo(string msg) { Tostada.Mostrar(msg); }
        public static void Aviso(string msg, string titulo = "VentaFácil") { FormMensaje.Mostrar(msg, titulo, Tono.Cuidado, false); }
        public static void Error(string msg, string titulo = "VentaFácil") { FormMensaje.Mostrar(msg, titulo, Tono.Error, false); }
        public static void Error(Exception ex, string contexto = "") { Log.Error(ex, contexto); Error((contexto == "" ? "" : contexto + "\n\n") + ex.Message); }
        public static bool Confirmar(string msg, string titulo = "VentaFácil")
        {
            return FormMensaje.Mostrar(msg, titulo, Tono.Info, true);
        }

        /// <summary>Pide un texto. Devuelve null si se cancela.</summary>
        public static string PedirTexto(string titulo, string etiqueta, string valor = "", bool clave = false)
        {
            using (var f = new FormPedir(titulo, etiqueta, valor, clave, false))
                return f.ShowDialog() == DialogResult.OK ? f.Valor : null;
        }

        /// <summary>Pide un número (acepta decimales). Devuelve null si se cancela.</summary>
        public static decimal? PedirNumero(string titulo, string etiqueta, decimal? valor = null)
        {
            using (var f = new FormPedir(titulo, etiqueta, valor.HasValue ? Fmt.Dec(valor.Value) : "", false, true))
            {
                if (f.ShowDialog() != DialogResult.OK) return null;
                return Fmt.ParseDec(f.Valor);
            }
        }

        /// <summary>
        /// Si el usuario actual tiene el permiso, él mismo. Si no, pide usuario y clave de alguien que sí lo tenga.
        /// Devuelve el usuario que autoriza o null.
        /// </summary>
        public static Usuario Autorizar(string permiso, string motivo)
        {
            if (Permisos.Tiene(Sesion.Usuario, permiso)) return Sesion.Usuario;
            using (var f = new FormLogin(true, motivo))
            {
                if (f.ShowDialog() != DialogResult.OK) return null;
                if (!Permisos.Tiene(f.UsuarioValido, permiso)) { Aviso("Ese usuario no tiene permiso para esto."); return null; }
                return f.UsuarioValido;
            }
        }

        public static Label Etiqueta(string texto, int x, int y, Font font = null, Color? color = null)
        {
            return new Label { Text = texto, Left = x, Top = y, AutoSize = true, Font = font ?? Tema.Normal, ForeColor = color ?? Tema.Texto, BackColor = Color.Transparent };
        }

        public static TextBox Caja(int x, int y, int ancho, Font font = null)
        {
            return new TextBox { Left = x, Top = y, Width = ancho, Font = font ?? Tema.Normal, BorderStyle = BorderStyle.FixedSingle, BackColor = Color.White };
        }

        public static Button Boton(string texto, int x, int y, int ancho, int alto, EventHandler click, bool principal = false)
        {
            var b = new Button { Text = texto, Left = x, Top = y, Width = ancho, Height = alto, Font = Tema.Negrita };
            Tema.Pastilla(b, principal);
            if (click != null) b.Click += click;
            return b;
        }

        /// <summary>Configura una cuadrícula de sólo lectura, cómoda para teclado.</summary>
        public static void EstiloGrid(DataGridView g, string vacia = "No hay nada que mostrar")
        {
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.BackgroundColor = Tema.Crema;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = Tema.Piedra;
            g.Font = Tema.Normal;
            g.RowTemplate.Height = 28;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersDefaultCellStyle.Font = Tema.Negrita;
            g.ColumnHeadersDefaultCellStyle.BackColor = Tema.Azul;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            g.EnableHeadersVisualStyles = false;
            g.AlternatingRowsDefaultCellStyle.BackColor = Tema.Taupe;
            g.ColumnHeadersHeight = 36;
            g.DefaultCellStyle.BackColor = Tema.Crema;
            g.DefaultCellStyle.ForeColor = Color.Black;
            g.DefaultCellStyle.SelectionBackColor = Tema.Piedra;
            g.DefaultCellStyle.SelectionForeColor = Color.Black;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            g.StandardTab = true;
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            g.SizeChanged += (s, e) => Repartir(g);
            g.ColumnAdded += (s, e) => Repartir(g);
            g.ColumnStateChanged += (s, e) => { if (e.StateChanged == DataGridViewElementStates.Visible) Repartir(g); };
            Marca.Tabla(g, vacia);
            foreach (Control h in g.Controls) if (h is VScrollBar) h.VisibleChanged += (s, e) => Repartir(g);
        }

        /// <summary>Reparte el ancho según el peso de cada columna, sin que ninguna quede más angosta que su encabezado.</summary>
        static void Repartir(DataGridView g)
        {
            if (g.Width < 200 || g.Columns.Count == 0) return;
            var cols = new System.Collections.Generic.List<DataGridViewColumn>();
            foreach (DataGridViewColumn c in g.Columns) if (c.Visible && c.Tag is int) cols.Add(c);
            if (cols.Count == 0) return;
            int libre = g.ClientSize.Width - 2;
            foreach (Control h in g.Controls) if (h is VScrollBar && h.Visible) libre -= h.Width;
            var fijo = new int[cols.Count];
            var letra = g.ColumnHeadersDefaultCellStyle.Font ?? g.Font;
            for (bool cambio = true; cambio; )
            {
                cambio = false;
                int queda = libre; double pesos = 0;
                for (int i = 0; i < cols.Count; i++) if (fijo[i] > 0) queda -= fijo[i]; else pesos += (int)cols[i].Tag;
                for (int i = 0; i < cols.Count; i++)
                {
                    if (fijo[i] > 0) continue;
                    int minimo = TextRenderer.MeasureText(cols[i].HeaderText, letra).Width + 12;
                    if (queda * (int)cols[i].Tag / pesos < minimo) { fijo[i] = minimo; cambio = true; break; }
                }
                if (cambio) continue;
                int usado = 0, ultimo = -1;
                for (int i = 0; i < cols.Count; i++) { if (fijo[i] == 0) { fijo[i] = -(int)(queda * (int)cols[i].Tag / pesos); ultimo = i; } usado += Math.Abs(fijo[i]); }
                if (ultimo >= 0) fijo[ultimo] -= Math.Max(0, libre - usado);   // el sobrante del redondeo
            }
            for (int i = 0; i < cols.Count; i++) if (cols[i].Width != Math.Abs(fijo[i])) cols[i].Width = Math.Max(5, Math.Abs(fijo[i]));
        }

        public static DataGridViewTextBoxColumn Columna(string nombre, string titulo, int peso, bool derecha = false, string formato = null)
        {
            var c = new DataGridViewTextBoxColumn { Name = nombre, HeaderText = titulo, FillWeight = peso, Tag = peso, AutoSizeMode = DataGridViewAutoSizeColumnMode.None, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (derecha) { c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight; }
            if (formato != null) c.DefaultCellStyle.Format = formato;
            return c;
        }
    }

    /// <summary>Ventana base para diálogos: Enter acepta, Esc cancela.</summary>
    public class Dialogo : Form
    {
        public Dialogo(string titulo, int ancho, int alto)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = titulo;
            if (Iconos.Ventana != null) Icon = Iconos.Ventana;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(ancho, alto);
            Font = Tema.Normal;
            BackColor = Tema.Crema;
            KeyPreview = true;
        }

        /// <summary>El botón que responde a Enter siempre se ve como el principal.</summary>
        protected override void OnLoad(EventArgs e)
        {
            var b = AcceptButton as Button;
            var f = b == null ? null : FundidoBoton.Buscar(b);
            if (f != null && f.Reposo == Tema.Suave) Tema.Pastilla(b, true);
            base.OnLoad(e);
            if (Animacion.Activas) Opacity = 0;      // aparece en ~120 ms
        }

        protected override void OnShown(EventArgs e)
        {
            if (Opacity < 1) Animacion.Ejecutar(this, "aparece", 120, p => { try { Opacity = p; } catch (Exception) { } }, () => Opacity = 1);
            base.OnShown(e);
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Tema.VentanaModerna(this); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); e.Handled = true; return; }
            base.OnKeyDown(e);
        }
    }

    public class FormPedir : Dialogo
    {
        readonly TextBox _txt;
        readonly bool _numero;
        public string Valor { get { return _txt.Text; } }

        public FormPedir(string titulo, string etiqueta, string valor, bool clave, bool numero) : base(titulo, 440, 156)
        {
            _numero = numero;
            int alto = TextRenderer.MeasureText(etiqueta, Tema.Negrita, new Size(408, int.MaxValue), TextFormatFlags.WordBreak).Height;
            Controls.Add(new Label { Text = etiqueta, Left = 16, Top = 14, Width = 408, Height = alto + 2, Font = Tema.Negrita, BackColor = Color.Transparent });
            _txt = Ui.Caja(16, 22 + alto, 408, Tema.Captura);
            _txt.Text = valor;
            if (clave) _txt.UseSystemPasswordChar = true;
            if (numero) _txt.TextAlign = HorizontalAlignment.Right;
            Controls.Add(_txt);
            int y = 22 + alto + _txt.PreferredHeight + 16;
            ClientSize = new Size(440, y + 56);
            var ok = Ui.Boton("Aceptar (Enter)", 128, y, 148, 40, (s, e) => Aceptar(), true);
            var no = Ui.Boton("Cancelar (Esc)", 284, y, 140, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            Shown += (s, e) => { _txt.SelectAll(); _txt.Focus(); };
        }

        void Aceptar()
        {
            decimal d;
            if (_numero && !Fmt.TryDec(_txt.Text, out d)) { Ui.Aviso("Escriba un número válido."); _txt.Focus(); _txt.SelectAll(); return; }
            DialogResult = DialogResult.OK;
        }
    }
}
