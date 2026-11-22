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
        // Paleta (de la guía de estilo)
        public static readonly Color Crema = Color.FromArgb(253, 252, 252);   // fondo de página
        public static readonly Color Taupe = Color.FromArgb(245, 243, 241);   // bandas y encabezados
        public static readonly Color Piedra = Color.FromArgb(235, 232, 228);  // bordes finos
        public static readonly Color Grafito = Color.FromArgb(68, 64, 59);
        public static readonly Color Humo = Color.FromArgb(119, 113, 105);    // texto secundario
        public static readonly Color Ceniza = Color.FromArgb(165, 159, 151);  // notas al pie

        public static readonly Color Azul = Color.Black;          // tinta principal y botón principal
        public static readonly Color Texto = Color.Black;
        public static readonly Color Rojo = Color.FromArgb(176, 32, 32);      // solo para alertas y errores
        public static readonly Color Verde = Color.FromArgb(38, 112, 64);     // solo para confirmaciones
        public static readonly Color Gris = Taupe;

        // Alias para no tocar todo el código: significan lo mismo que arriba
        public static readonly Color AzulClaro = Piedra;
        public static readonly Color AzulFondo = Taupe;

        const string Fuente = "Segoe UI";
        public static readonly Font Normal = new Font(Fuente, 9.5f);
        public static readonly Font Negrita = new Font(Fuente, 9.5f, FontStyle.Bold);
        public static readonly Font Mediana = new Font(Fuente, 12f, FontStyle.Bold);
        public static readonly Font Grande = new Font("Segoe UI Light", 20f);
        public static readonly Font Enorme = new Font("Segoe UI Light", 34f);
        public static readonly Font Captura = new Font(Fuente, 16f);
        public static readonly Font Mono = new Font("Consolas", 10.5f);

        /// <summary>Botón en píldora: fondo crema y borde piedra; el principal va en negro.</summary>
        public static void Pastilla(Button b, bool principal = false)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = principal ? Color.Black : Piedra;
            b.FlatAppearance.MouseOverBackColor = principal ? Grafito : Taupe;
            b.FlatAppearance.MouseDownBackColor = principal ? Grafito : Piedra;
            b.BackColor = principal ? Color.Black : Crema;
            b.ForeColor = principal ? Color.White : Color.Black;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            Redondear(b);
            b.SizeChanged -= AlCambiarTamano;
            b.SizeChanged += AlCambiarTamano;
        }

        static void AlCambiarTamano(object sender, EventArgs e) { Redondear((Control)sender); }

        /// <summary>Esquinas redondeadas en forma de píldora (radio = mitad de la altura).</summary>
        public static void Redondear(Control c)
        {
            if (c.Width <= 2 || c.Height <= 2) return;
            int d = Math.Min(c.Height, c.Width);
            using (var p = new System.Drawing.Drawing2D.GraphicsPath())
            {
                p.AddArc(0, 0, d, d, 90, 180);
                p.AddArc(c.Width - d - 1, 0, d, d, 270, 180);
                p.CloseFigure();
                c.Region = new Region(p);
            }
        }
    }

    /// <summary>Encabezado: banda taupe con título ligero y una línea fina abajo.</summary>
    public class PanelTitulo : Panel
    {
        public string Titulo = "";
        public string Subtitulo = "";
        public PanelTitulo() { DoubleBuffered = true; Height = 56; Dock = DockStyle.Top; BackColor = Tema.Taupe; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            e.Graphics.Clear(Tema.Taupe);
            using (var lapiz = new Pen(Tema.Piedra)) e.Graphics.DrawLine(lapiz, 0, Height - 1, Width, Height - 1);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            TextRenderer.DrawText(e.Graphics, Titulo, Tema.Grande, new Point(16, 6), Color.Black);
            if (!string.IsNullOrEmpty(Subtitulo))
            {
                var tam = TextRenderer.MeasureText(Titulo, Tema.Grande);
                TextRenderer.DrawText(e.Graphics, Subtitulo, Tema.Normal, new Point(22 + tam.Width, 22), Tema.Humo);
            }
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    }

    /// <summary>Botón grande con la tecla rápida arriba: "F2" / "Buscar".</summary>
    public class BotonF : Button
    {
        public string Tecla;
        public BotonF(string tecla, string texto, EventHandler click)
        {
            Tecla = tecla;
            Text = (string.IsNullOrEmpty(tecla) ? "" : tecla + "\n") + texto;
            Font = new Font("Segoe UI", 9f, FontStyle.Bold);
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
        /// Si el usuario actual no es administrador, pide usuario y clave de un administrador.
        /// Devuelve el usuario que autoriza o null.
        /// </summary>
        public static Usuario AutorizarAdmin(string motivo)
        {
            if (Sesion.Usuario != null && Sesion.Usuario.EsAdmin) return Sesion.Usuario;
            using (var f = new FormLogin(true, motivo))
            {
                if (f.ShowDialog() != DialogResult.OK) return null;
                if (!f.UsuarioValido.EsAdmin) { Aviso("Ese usuario no es administrador."); return null; }
                return f.UsuarioValido;
            }
        }

        public static Label Etiqueta(string texto, int x, int y, Font font = null, Color? color = null)
        {
            return new Label { Text = texto, Left = x, Top = y, AutoSize = true, Font = font ?? Tema.Normal, ForeColor = color ?? Tema.Texto, BackColor = Color.Transparent };
        }

        public static TextBox Caja(int x, int y, int ancho, Font font = null)
        {
            return new TextBox { Left = x, Top = y, Width = ancho, Font = font ?? Tema.Normal };
        }

        public static Button Boton(string texto, int x, int y, int ancho, int alto, EventHandler click)
        {
            var b = new Button { Text = texto, Left = x, Top = y, Width = ancho, Height = alto, Font = Tema.Negrita };
            Tema.Pastilla(b);
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
            g.ColumnHeadersDefaultCellStyle.BackColor = Tema.Taupe;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Tema.Humo;
            g.EnableHeadersVisualStyles = false;
            g.AlternatingRowsDefaultCellStyle.BackColor = Tema.Taupe;
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
