using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;

namespace VentaFacil.UI
{
    /// <summary>Colores y letras de toda la aplicación (inspirado en Zafirah: azul, blanco y botones grandes).</summary>
    public static class Tema
    {
        public static readonly Color Azul = Color.FromArgb(0, 70, 160);
        public static readonly Color AzulClaro = Color.FromArgb(60, 130, 220);
        public static readonly Color AzulFondo = Color.FromArgb(225, 236, 250);
        public static readonly Color Rojo = Color.FromArgb(200, 20, 20);
        public static readonly Color Verde = Color.FromArgb(0, 130, 60);
        public static readonly Color Gris = Color.FromArgb(240, 240, 240);
        public static readonly Color Texto = Color.FromArgb(20, 20, 20);

        public static readonly Font Normal = new Font("Tahoma", 10f);
        public static readonly Font Negrita = new Font("Tahoma", 10f, FontStyle.Bold);
        public static readonly Font Mediana = new Font("Tahoma", 13f, FontStyle.Bold);
        public static readonly Font Grande = new Font("Tahoma", 20f, FontStyle.Bold);
        public static readonly Font Enorme = new Font("Tahoma", 34f, FontStyle.Bold);
        public static readonly Font Captura = new Font("Tahoma", 18f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Courier New", 11f, FontStyle.Bold);
    }

    /// <summary>Panel con degradado azul para encabezados.</summary>
    public class PanelTitulo : Panel
    {
        public string Titulo = "";
        public string Subtitulo = "";
        public PanelTitulo() { DoubleBuffered = true; Height = 56; Dock = DockStyle.Top; }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            using (var br = new LinearGradientBrush(ClientRectangle, Tema.AzulClaro, Tema.Azul, LinearGradientMode.Vertical))
                e.Graphics.FillRectangle(br, ClientRectangle);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            TextRenderer.DrawText(e.Graphics, Titulo, Tema.Grande, new Point(12, 6), Color.White);
            if (!string.IsNullOrEmpty(Subtitulo))
            {
                var tam = TextRenderer.MeasureText(Titulo, Tema.Grande);
                TextRenderer.DrawText(e.Graphics, Subtitulo, Tema.Normal, new Point(18 + tam.Width, 18), Color.FromArgb(220, 230, 255));
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
            Font = new Font("Tahoma", 9f, FontStyle.Bold);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderColor = Tema.AzulClaro;
            BackColor = Color.White;
            ForeColor = Tema.Azul;
            TabStop = false;
            Cursor = Cursors.Hand;
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
            var b = new Button { Text = texto, Left = x, Top = y, Width = ancho, Height = alto, Font = Tema.Negrita, BackColor = Color.White, FlatStyle = FlatStyle.Standard };
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
            g.BackgroundColor = Color.White;
            g.BorderStyle = BorderStyle.FixedSingle;
            g.Font = Tema.Normal;
            g.RowTemplate.Height = 26;
            g.ColumnHeadersDefaultCellStyle.Font = Tema.Negrita;
            g.ColumnHeadersDefaultCellStyle.BackColor = Tema.AzulFondo;
            g.EnableHeadersVisualStyles = false;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(245, 248, 255);
            g.DefaultCellStyle.SelectionBackColor = Tema.AzulClaro;
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
            Text = titulo;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(ancho, alto);
            Font = Tema.Normal;
            BackColor = Color.White;
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
