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

        /// <summary>
        /// Botón en píldora. El borde de Windows es rectangular y se veía "cuadrado" alrededor del botón redondo,
        /// así que aquí no se usa: el contorno redondo se dibuja a mano con la misma forma del botón.
        /// </summary>
        /// <summary>Animaciones de botones y de la barra de módulos (se pueden apagar en Ajustes; ninguna dura más de ~150 ms).</summary>
        public static bool Animaciones = true;

        public static void Pastilla(Button b, bool principal = false)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            var f = FundidoBoton.De(b);
            f.Definir(principal ? Azul : Crema, principal ? AzulOscuro : Color.FromArgb(230, 240, 252), principal ? AzulOscuro : Piedra);
            b.Paint -= DibujarContorno;
            b.Paint += DibujarContorno;
            b.SizeChanged -= AlCambiarTamano;
            b.SizeChanged += AlCambiarTamano;
            Redondear(b);
        }

        /// <summary>Estilo para acciones de peligro (cierre de turno, eliminar, cancelar ticket).</summary>
        public static void BotonPeligro(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            var f = FundidoBoton.De(b);
            f.Definir(Rojo, Color.FromArgb(160, 15, 15), Color.FromArgb(130, 10, 10));
            b.Paint -= DibujarContorno;
            b.Paint += DibujarContorno;
            b.SizeChanged -= AlCambiarTamano;
            b.SizeChanged -= AlCambiarTamano;
            Redondear(b);
        }

        static void AlCambiarTamano(object sender, EventArgs e) { Redondear((Control)sender); }

        /// <summary>
        /// Borde suave de 1 px con esquinas redondeadas.
        /// Los botones oscuros de acción principal o peligro no lo necesitan.
        /// </summary>
        static void DibujarContorno(object sender, PaintEventArgs e)
        {
            var b = (Control)sender;
            if (b.Width < 6 || b.Height < 6) return;
            var f = FundidoBoton.Buscar(b);
            if (f != null && f.Reposo.GetBrightness() < 0.45f) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            Color colorBorde = b.Enabled ? Color.FromArgb(175, 195, 222) : Ceniza;
            using (var pluma = new Pen(colorBorde, 1.2f))
            using (var forma = Forma(b.Width - 2, b.Height - 2, 7))
            {
                var m = new System.Drawing.Drawing2D.Matrix();
                m.Translate(0.5f, 0.5f);
                forma.Transform(m);
                e.Graphics.DrawPath(pluma, forma);
            }
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

        /// <summary>Recorta el botón con esquinas suaves de 8 px sin dientes de sierra.</summary>
        public static void Redondear(Control c, int radio = 8)
        {
            if (c.Width <= 2 || c.Height <= 2) return;
            using (var p = Forma(c.Width, c.Height, radio)) c.Region = new Region(p);
        }
    }

    /// <summary>Hover y clic con transición suave de color y garantía de alto contraste de texto.</summary>
    public class FundidoBoton
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, FundidoBoton> Tabla = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, FundidoBoton>();
        public Color Reposo = Tema.Crema, Hover = Color.FromArgb(230, 240, 252), Abajo = Tema.Piedra;
        readonly Button _b;
        readonly System.Windows.Forms.Timer _t = new System.Windows.Forms.Timer { Interval = 15 };
        float _x;          // 0 = reposo, 1 = hover
        int _dir;
        bool _abajo;

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
            _t.Tick += (s, e) => Paso();
            b.MouseEnter += (s, e) => { _dir = 1; Empezar(); };
            b.MouseLeave += (s, e) => { _dir = -1; _abajo = false; b.Padding = Padding.Empty; Empezar(); };
            b.MouseDown += (s, e) => { _abajo = true; Aplicar(); if (Tema.Animaciones) b.Padding = new Padding(0, 1, 0, 0); };
            b.MouseUp += (s, e) => { _abajo = false; b.Padding = Padding.Empty; Aplicar(); };
            b.Disposed += (s, e) => _t.Dispose();
        }

        public void Definir(Color reposo, Color hover, Color abajo)
        {
            Reposo = reposo; Hover = hover; Abajo = abajo;
            _x = 0; _dir = 0; _t.Stop();
            _b.FlatAppearance.MouseDownBackColor = abajo;
            _b.BackColor = reposo;
            Aplicar();
        }

        void Empezar() { if (Tema.Animaciones) _t.Start(); else { _x = _dir > 0 ? 1 : 0; Aplicar(); } }
        void Paso()
        {
            _x += _dir * 0.25f;
            if (_x >= 1) { _x = 1; _t.Stop(); }
            if (_x <= 0) { _x = 0; _t.Stop(); }
            Aplicar();
        }

        void Aplicar()
        {
            Color c = _abajo ? Abajo : Mezcla(Reposo, Hover, _x);
            _b.BackColor = c;
            _b.FlatAppearance.MouseOverBackColor = c;
            // Accesibilidad de contraste:
            // Fondo oscuro (brillo < 0.45) -> texto blanco.
            // Fondo claro -> texto azul oscuro (Tema.AzulOscuro) con contraste > 7:1.
            _b.ForeColor = c.GetBrightness() < 0.45f ? Color.White : Tema.AzulOscuro;
            _b.Invalidate();
        }

        static Color Mezcla(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    /// <summary>Encabezado azul con título blanco y grueso.</summary>
    public class PanelTitulo : Panel
    {
        public string Titulo = "";
        public string Subtitulo = "";
        public PanelTitulo() { DoubleBuffered = true; Height = 64; Dock = DockStyle.Top; BackColor = Tema.Azul; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            e.Graphics.Clear(Tema.Azul);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            TextRenderer.DrawText(e.Graphics, Titulo, Tema.Grande, new Point(16, 8), Color.White);
            if (!string.IsNullOrEmpty(Subtitulo))
            {
                var tam = TextRenderer.MeasureText(Titulo, Tema.Grande);
                TextRenderer.DrawText(e.Graphics, Subtitulo, Tema.Mediana, new Point(24 + tam.Width, 24), Color.FromArgb(220, 235, 255));
            }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    }

    /// <summary>Botón con atajo arriba y nombre abajo: "Ctrl+F1" / "Productos".</summary>
    public class BotonF : Button
    {
        public string Tecla;
        public BotonF(string tecla, string texto, EventHandler click)
        {
            Tecla = tecla;
            Text = (string.IsNullOrEmpty(tecla) ? "" : tecla + "\n") + texto;
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold);
            Tema.Pastilla(this);
            TabStop = false;
            if (click != null) Click += click;
        }
    }

    public static class Ui
    {
        public static void Info(string msg, string titulo = "VentaFácil") { MessageBox.Show(msg, titulo, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        public static void Aviso(string msg, string titulo = "VentaFácil") { MessageBox.Show(msg, titulo, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        public static void Error(string msg, string titulo = "VentaFácil") { MessageBox.Show(msg, titulo, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        public static void Error(Exception ex, string contexto = "") { Log.Error(ex, contexto); Error((contexto == "" ? "" : contexto + "\n\n") + ex.Message); }
        public static bool Confirmar(string msg, string titulo = "VentaFácil")
        {
            return MessageBox.Show(msg, titulo, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxButtonDefaultButton()) == DialogResult.Yes;
        }
        static MessageBoxDefaultButton MessageBoxButtonDefaultButton() { return MessageBoxDefaultButton.Button2; }

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
        public static void EstiloGrid(DataGridView g)
        {
            g.ReadOnly = true;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            g.MultiSelect = false;
            g.BackgroundColor = Tema.Crema;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.GridColor = Tema.Piedra;
            g.Font = Tema.Normal;
            g.RowTemplate.Height = 26;
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
        }

        public static DataGridViewTextBoxColumn Columna(string nombre, string titulo, int peso, bool derecha = false, string formato = null)
        {
            var c = new DataGridViewTextBoxColumn { Name = nombre, HeaderText = titulo, FillWeight = peso, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (derecha) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
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

        public FormPedir(string titulo, string etiqueta, string valor, bool clave, bool numero) : base(titulo, 420, 150)
        {
            _numero = numero;
            Controls.Add(Ui.Etiqueta(etiqueta, 16, 14, Tema.Negrita));
            _txt = Ui.Caja(16, 42, 388, Tema.Captura);
            _txt.Text = valor;
            if (clave) _txt.UseSystemPasswordChar = true;
            if (numero) _txt.TextAlign = HorizontalAlignment.Right;
            Controls.Add(_txt);
            var ok = Ui.Boton("Aceptar (Enter)", 150, 96, 130, 36, (s, e) => Aceptar());
            var no = Ui.Boton("Cancelar (Esc)", 288, 96, 116, 36, (s, e) => { DialogResult = DialogResult.Cancel; });
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
