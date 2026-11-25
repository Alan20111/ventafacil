using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace VentaFacil.UI
{
    public enum Tono { Info, Bien, Cuidado, Error }

    public static class Tonos
    {
        public static Color Fondo(Tono t) { return t == Tono.Error ? Color.FromArgb(253, 236, 236) : t == Tono.Cuidado ? Color.FromArgb(255, 247, 222) : t == Tono.Bien ? Color.FromArgb(230, 245, 234) : Color.FromArgb(230, 240, 252); }
        public static Color Fuerte(Tono t) { return t == Tono.Error ? Color.FromArgb(190, 30, 30) : t == Tono.Cuidado ? Color.FromArgb(176, 112, 0) : t == Tono.Bien ? Color.FromArgb(22, 122, 58) : Tema.Azul; }
        public static Color Tinta(Tono t) { return t == Tono.Error ? Color.FromArgb(110, 16, 16) : t == Tono.Cuidado ? Color.FromArgb(94, 62, 0) : t == Tono.Bien ? Color.FromArgb(14, 78, 38) : Tema.AzulOscuro; }

        /// <summary>Círculo de color con su signo: ! error y cuidado, ✓ bien, i información, ? pregunta.</summary>
        public static void Icono(Graphics g, Rectangle r, Tono t, bool pregunta = false)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Fuerte(t))) g.FillEllipse(b, r);
            float u = r.Width / 24f, cx = r.X + r.Width / 2f;
            if (pregunta)
            {
                using (var f = new Font("Segoe UI", r.Width * 0.58f, FontStyle.Bold, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g, "?", f, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                return;
            }
            using (var p = new Pen(Color.White, 2.6f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var bl = new SolidBrush(Color.White))
            {
                if (t == Tono.Bien) g.DrawLines(p, new[] { new PointF(r.X + 6.8f * u, r.Y + 12.6f * u), new PointF(r.X + 10.6f * u, r.Y + 16.4f * u), new PointF(r.X + 17.4f * u, r.Y + 8.4f * u) });
                else if (t == Tono.Info) { g.FillEllipse(bl, cx - 1.6f * u, r.Y + 5.6f * u, 3.2f * u, 3.2f * u); g.DrawLine(p, cx, r.Y + 11.4f * u, cx, r.Y + 17.4f * u); }
                else { g.DrawLine(p, cx, r.Y + 6.2f * u, cx, r.Y + 13f * u); g.FillEllipse(bl, cx - 1.6f * u, r.Y + 15.6f * u, 3.2f * u, 3.2f * u); }
            }
        }

        public static GraphicsPath Redondo(RectangleF r, float radio)
        {
            float d = Math.Min(radio * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    /// <summary>Banda de aviso: fondo suave, icono y texto que se acomoda solo. Sustituye al texto rojo suelto.</summary>
    public class Banda : Control
    {
        Tono _tono;
        const int Lado = 24, Margen = 14, Hueco = 12;

        public Banda(Tono tono, string texto, int x, int y, int ancho)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Font = Tema.Normal;
            _tono = tono;
            Left = x; Top = y; Width = ancho;
            Text = texto;
        }

        public Tono Tono { get { return _tono; } set { _tono = value; Invalidate(); } }

        public static int AltoPara(string texto, int ancho)
        {
            int h = TextRenderer.MeasureText(string.IsNullOrEmpty(texto) ? "x" : texto, Tema.Normal, new Size(Math.Max(40, ancho - Margen * 2 - Lado - Hueco), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            return Math.Max(Lado, h) + 20;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Height = AltoPara(Text, Width); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tema.FondoDe(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ruta = Tonos.Redondo(new RectangleF(0, 0, Width - 1, Height - 1), 10))
            using (var b = new SolidBrush(Tonos.Fondo(_tono))) g.FillPath(b, ruta);
            int x = Margen + Lado + Hueco;
            var tam = TextRenderer.MeasureText(Text, Font, new Size(Width - x - Margen, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            Tonos.Icono(g, new Rectangle(Margen, tam.Height > 34 ? 12 : (Height - Lado) / 2, Lado, Lado), _tono);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, Math.Max(8, (Height - tam.Height) / 2), Width - x - Margen, Height - 10), Tonos.Tinta(_tono), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
    }

    /// <summary>
    /// Etiqueta de estado: se usa como un Label normal, pero según su color se dibuja como una pastilla suave con icono
    /// (rojo = error, verde = bien, azul = información). Con otro color se ve como texto normal.
    /// </summary>
    public class EtiquetaEstado : Label
    {
        bool Con { get { return Text != "" && (ForeColor == Tema.Rojo || ForeColor == Tema.Verde || ForeColor == Tema.Azul); } }
        Tono Tono { get { return ForeColor == Tema.Rojo ? Tono.Error : ForeColor == Tema.Verde ? Tono.Bien : Tono.Info; } }
        readonly Padding _normal = Padding.Empty;

        public EtiquetaEstado() { BackColor = Color.Transparent; SetStyle(ControlStyles.OptimizedDoubleBuffer, true); UseMnemonic = false; }
        void Ajustar() { var p = Con ? new Padding(36, 4, 10, 4) : _normal; if (Padding != p) Padding = p; Invalidate(); }
        protected override void OnTextChanged(EventArgs e) { Ajustar(); base.OnTextChanged(e); }
        protected override void OnForeColorChanged(EventArgs e) { Ajustar(); base.OnForeColorChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!Con) { base.OnPaint(e); return; }
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var t = TextRenderer.MeasureText(Text, Font, new Size(Math.Max(20, Width - 46), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int alto = Math.Min(Height, Math.Max(28, t.Height + 10)), ancho = Math.Min(Width, t.Width + 48);
            int y = TextAlign == ContentAlignment.MiddleLeft ? (Height - alto) / 2 : 0;
            using (var ruta = Tonos.Redondo(new RectangleF(0, y, ancho - 1, alto - 1), 8))
            using (var b = new SolidBrush(Tonos.Fondo(Tono))) g.FillPath(b, ruta);
            Tonos.Icono(g, new Rectangle(9, t.Height > 30 ? y + 8 : y + (alto - 18) / 2, 18, 18), Tono);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(34, y, ancho - 38, alto), Tonos.Tinta(Tono), TextFormatFlags.WordBreak | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Renglón de una lista de revisión: icono, texto y una pastilla «Listo» o «Falta».</summary>
    public class FilaRevision : Control
    {
        readonly bool _bien;
        public FilaRevision(bool bien, string texto, int x, int y, int ancho)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false; _bien = bien; Font = Tema.Normal; Text = texto;
            SetBounds(x, y, ancho, 40);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var t = _bien ? Tono.Bien : Tono.Cuidado;
            g.Clear(Tema.FondoDe(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ruta = Tonos.Redondo(new RectangleF(0, 0, Width - 1, Height - 1), 10))
            using (var b = new SolidBrush(_bien ? Tema.Taupe : Tonos.Fondo(t))) g.FillPath(b, ruta);
            Tonos.Icono(g, new Rectangle(12, 9, 22, 22), t);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(44, 0, Width - 136, Height), _bien ? Tema.Texto : Tonos.Tinta(t), TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            var chip = new Rectangle(Width - 84, 8, 72, 24);
            using (var ruta = Tonos.Redondo(chip, 12))
            using (var b = new SolidBrush(Tonos.Fuerte(t))) g.FillPath(b, ruta);
            TextRenderer.DrawText(g, _bien ? "Listo" : "Falta", Font, chip, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Tarjeta con un dato grande: título chico, cifra y una nota.</summary>
    public class Tarjeta : Control
    {
        readonly string _titulo, _dato, _nota;
        public Tarjeta(string titulo, string dato, string nota, int x, int y, int ancho, int alto = 112)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            _titulo = titulo; _dato = dato; _nota = nota;
            Text = titulo + ": " + dato;
            SetBounds(x, y, ancho, alto);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tema.FondoDe(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var ruta = Tonos.Redondo(new RectangleF(0, 0, Width - 1, Height - 1), 12))
            using (var b = new SolidBrush(Tema.Taupe)) g.FillPath(b, ruta);
            using (var ruta = Tonos.Redondo(new RectangleF(0, 16, 5, Height - 32), 2.5f))
            using (var b = new SolidBrush(Tema.Azul)) g.FillPath(b, ruta);
            const TextFormatFlags f = TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, _titulo, Tema.Normal, new Rectangle(18, 14, Width - 30, 24), Tema.Humo, f);
            var letra = TextRenderer.MeasureText(_dato, Tema.Grande).Width <= Width - 30 ? Tema.Grande : Tema.Mediana;
            TextRenderer.DrawText(g, _dato, letra, new Rectangle(18, 38, Width - 30, 42), Tema.AzulOscuro, f | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, _nota, Tema.Normal, new Rectangle(18, 82, Width - 30, 24), Tema.Humo, f);
        }
    }

    /// <summary>El billete muy tenue como marca de agua.</summary>
    public static class Marca
    {
        public static T ConBillete<T>(this T c) where T : Control { Fondo(c); return c; }

        static readonly Dictionary<string, Bitmap> _cache = new Dictionary<string, Bitmap>();

        const float Alto = 0.625f;   // el billete es más ancho que alto

        /// <summary>
        /// Solo el billete, sin el cuadro azul del icono: lo blanco se vuelve del color pedido y lo azul, transparente.
        /// Se guarda ya hecho: dibujarlo después es solo copiar.
        /// </summary>
        static Bitmap Silueta(int ancho, Color color, float alfa)
        {
            string k = ancho + "|" + color.ToArgb() + "|" + alfa;
            Bitmap b;
            lock (_cache)
            {
                if (_cache.TryGetValue(k, out b)) return b;
                var img = Iconos.Billete;
                if (img == null || ancho < 16) return null;
                int alto = (int)(ancho * Alto);
                b = new Bitmap(ancho, alto, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(b))
                using (var at = new ImageAttributes())
                {
                    var m = new ColorMatrix(new[]
                    {
                        new float[] { 0, 0, 0, alfa, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 }, new float[] { 0, 0, 0, 0, 0 },
                        new float[] { color.R / 255f, color.G / 255f, color.B / 255f, 0, 1 }
                    });
                    at.SetColorMatrix(m);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(img, new Rectangle(0, 0, ancho, alto), img.Width * 0.1f, img.Height * 0.25f, img.Width * 0.8f, img.Height * 0.5f, GraphicsUnit.Pixel, at);
                }
                if (_cache.Count > 40) _cache.Clear();
                _cache[k] = b;
                return b;
            }
        }

        /// <summary>El billete blanco, para encabezados azules.</summary>
        public static void Blanco(Graphics g, int x, int y, int ancho)
        {
            var b = Silueta(ancho, Color.White, 1f);
            if (b != null) g.DrawImageUnscaled(b, x, y);
        }

        public static void Dibujar(Graphics g, Rectangle area, int ancho, float alfa = 0.10f)
        {
            ancho = (int)Math.Min(ancho, Math.Min(area.Width - 8, (area.Height - 8) / Alto)) / 8 * 8;
            var b = Silueta(ancho, Tema.Azul, alfa);
            if (b != null) g.DrawImageUnscaled(b, area.X + (area.Width - ancho) / 2, area.Y + (area.Height - b.Height) / 2);
        }

        /// <summary>Pone el billete tenue abajo a la derecha de una página.</summary>
        public static void Fondo(Control c, int ancho = 300)
        {
            try { typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(c, true, null); } catch (Exception) { }
            c.Resize += (s, e) => c.Invalidate();
            var sc = c as ScrollableControl;
            if (sc != null) sc.Scroll += (s, e) => c.Invalidate();
            c.Paint += (s, e) =>
            {
                int n = Math.Min(ancho, Math.Min(c.ClientSize.Width, c.ClientSize.Height) - 40);
                if (n < 120) return;
                int alto = (int)(n * Alto);
                Dibujar(e.Graphics, new Rectangle(c.ClientSize.Width - n - 28, c.ClientSize.Height - alto - 24, n + 8, alto + 8), n);
            };
        }

        /// <summary>En una tabla: el billete tenue y un texto en el hueco que dejan los renglones.</summary>
        public static void Tabla(DataGridView g, string vacia)
        {
            g.RowsAdded += (s, e) => g.Invalidate();
            g.RowsRemoved += (s, e) => g.Invalidate();
            g.Paint += (s, e) =>
            {
                int y = g.ColumnHeadersVisible ? g.ColumnHeadersHeight : 0;
                int usado = g.Rows.GetRowsHeight(DataGridViewElementStates.Displayed);
                var hueco = new Rectangle(0, y + usado, g.ClientSize.Width, g.ClientSize.Height - y - usado);
                if (hueco.Height < 90 || hueco.Width < 160) return;
                bool texto = g.Rows.Count == 0 && !string.IsNullOrEmpty(vacia);
                var area = texto ? new Rectangle(hueco.X, hueco.Y, hueco.Width, hueco.Height - 34) : hueco;
                const int ancho = 300;
                Dibujar(e.Graphics, area, ancho);
                if (texto) TextRenderer.DrawText(e.Graphics, vacia, Tema.Normal, new Rectangle(hueco.X, hueco.Y + (area.Height + Math.Min((int)(ancho * Alto), area.Height - 8)) / 2 + 6, hueco.Width, 26), Tema.Ceniza, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
        }
    }

    /// <summary>Mensaje con el estilo de VentaFácil (información, cuidado, error o pregunta).</summary>
    public class FormMensaje : Dialogo
    {
        /// <summary>Para las pruebas: si se pone, se llama en lugar de mostrar la ventana. Devuelve lo que se «contestaría».</summary>
        public static Func<string, string, bool> Interceptar;

        readonly Tono _tono;
        readonly bool _pregunta;

        FormMensaje(string texto, string titulo, Tono tono, bool pregunta) : base(titulo, 560, 200)
        {
            _tono = tono; _pregunta = pregunta;
            if (Form.ActiveForm == null) { StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = true; }
            texto = (texto ?? "").Trim();
            // El primer párrafo corto hace de título
            string cabeza, resto;
            int corte = texto.IndexOf("\n\n", StringComparison.Ordinal);
            if (corte > 0 && corte < 70 && texto.IndexOf('\n') == corte) { cabeza = texto.Substring(0, corte); resto = texto.Substring(corte).Trim(); }
            else if (texto.Length < 60 && texto.IndexOf('\n') < 0) { cabeza = texto; resto = ""; }
            else { cabeza = titulo != "" && titulo != "VentaFácil" && !pregunta ? titulo : tono == Tono.Error ? "Algo no salió bien" : pregunta ? "Confirme" : tono == Tono.Cuidado ? "Atención" : "Listo"; resto = texto; }
            const int x = 92, ancho = 440;
            int y = 26;
            int hc = TextRenderer.MeasureText(cabeza, Tema.Mediana, new Size(ancho, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            Controls.Add(new Label { Text = cabeza, UseMnemonic = false, Left = x, Top = resto == "" ? Math.Max(y, 26 + (48 - hc) / 2) : y, Width = ancho, Height = hc + 2, Font = Tema.Mediana, ForeColor = Tonos.Tinta(tono), BackColor = Color.Transparent });
            y += hc + 10;
            if (resto != "")
            {
                int h = TextRenderer.MeasureText(resto, Tema.Normal, new Size(ancho, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + 4;
                if (h > 380)
                {
                    Controls.Add(new TextBox { Text = resto.Replace("\r", "").Replace("\n", "\r\n"), Left = x, Top = y, Width = ancho, Height = 380, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, BackColor = Tema.Crema, Font = Tema.Normal, TabStop = false });
                    h = 380;
                }
                else Controls.Add(new Label { Text = resto, UseMnemonic = false, Left = x, Top = y, Width = ancho, Height = h, Font = Tema.Normal, BackColor = Color.Transparent });
                y += h;
            }
            y = Math.Max(y, 84) + 18;
            ClientSize = new Size(560, y + 62);
            if (pregunta)
            {
                var no = Ui.Boton("No", 268, y, 126, 44, (s, e) => { DialogResult = DialogResult.No; }, true);
                var si = Ui.Boton("Sí", 406, y, 126, 44, (s, e) => { DialogResult = DialogResult.Yes; });
                Controls.Add(no); Controls.Add(si);
                Shown += (s, e) => no.Focus();          // lo seguro es lo que responde a Enter
                KeyPress += (s, e) => { if (char.ToUpper(e.KeyChar) == 'S') DialogResult = DialogResult.Yes; else if (char.ToUpper(e.KeyChar) == 'N') DialogResult = DialogResult.No; };
            }
            else
            {
                var ok = Ui.Boton("Entendido", 392, y, 140, 44, (s, e) => { DialogResult = DialogResult.OK; }, true);
                Controls.Add(ok);
                AcceptButton = ok; CancelButton = ok;
                Shown += (s, e) => ok.Focus();
            }
            KeyDown += (s, e) => { if (e.KeyData == (Keys.Control | Keys.C)) try { Clipboard.SetText(texto); } catch (Exception) { } };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var b = new SolidBrush(Tonos.Fuerte(_tono))) e.Graphics.FillRectangle(b, 0, 0, ClientSize.Width, 5);
            Tonos.Icono(e.Graphics, new Rectangle(26, 26, 48, 48), _tono, _pregunta);
        }

        public static bool Mostrar(string texto, string titulo, Tono tono, bool pregunta)
        {
            if (Interceptar != null) return Interceptar(texto, titulo);
            try
            {
                using (var f = new FormMensaje(texto, titulo, tono, pregunta))
                {
                    var r = f.ShowDialog();
                    return r == DialogResult.Yes || r == DialogResult.OK;
                }
            }
            catch (Exception ex)
            {   // nunca quedarse sin decir algo
                Log.Error(ex, "Mensaje");
                var ic = tono == Tono.Error ? MessageBoxIcon.Error : pregunta ? MessageBoxIcon.Question : tono == Tono.Cuidado ? MessageBoxIcon.Warning : MessageBoxIcon.Information;
                var r = MessageBox.Show(texto, titulo, pregunta ? MessageBoxButtons.YesNo : MessageBoxButtons.OK, ic, pregunta ? MessageBoxDefaultButton.Button2 : MessageBoxDefaultButton.Button1);
                return r == DialogResult.Yes || r == DialogResult.OK;
            }
        }
    }

    /// <summary>Aviso breve que sube, se queda 2.4 s y se va solo. No quita el foco ni hay que cerrarlo.</summary>
    public class Tostada : Form
    {
        static Tostada _actual;
        readonly string _texto;
        readonly Tono _tono;
        readonly Timer _t = new Timer { Interval = 2400 };

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams { get { var c = base.CreateParams; c.ExStyle |= 0x08000000 | 0x00000080; return c; } }   // sin activar, sin barra de tareas

        Tostada(string texto, Tono tono)
        {
            _texto = texto; _tono = tono;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            BackColor = Tonos.Fondo(tono);
            int w = Math.Min(560, TextRenderer.MeasureText(texto, Tema.Normal).Width + 84);
            ClientSize = new Size(w, 52);
            using (var r = Tonos.Redondo(new RectangleF(0, 0, w, 52), 12)) Region = new Region(r);
            _t.Tick += (s, e) => { _t.Stop(); Animacion.Ejecutar(this, "ver", 180, p => { try { Opacity = 1 - p; } catch (Exception) { } }, Close); };
            FormClosed += (s, e) => { _t.Dispose(); if (_actual == this) _actual = null; };
            Click += (s, e) => Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Tonos.Icono(e.Graphics, new Rectangle(16, 14, 24, 24), _tono);
            TextRenderer.DrawText(e.Graphics, _texto, Tema.Normal, new Rectangle(52, 0, Width - 62, Height), Tonos.Tinta(_tono), TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        public static void Mostrar(string texto, Tono tono = Tono.Bien)
        {
            if (FormMensaje.Interceptar != null) { FormMensaje.Interceptar(texto, "tostada"); return; }
            try
            {
                var duena = Form.ActiveForm ?? Application.OpenForms.Cast<Form>().LastOrDefault(f => f.Visible && !(f is Tostada));
                if (duena == null) return;
                if (_actual != null && !_actual.IsDisposed) _actual.Close();
                var t = _actual = new Tostada(texto, tono);
                int x = duena.Left + (duena.Width - t.Width) / 2, y = duena.Bottom - t.Height - 64;
                t.Opacity = Animacion.Activas ? 0 : 1;
                t.Location = new Point(x, y + (Animacion.Activas ? 16 : 0));
                t.Show(duena);
                Animacion.Ejecutar(t, "ver", 200, p => { t.Opacity = p; t.Top = y + (int)(16 * (1 - p)); }, () => t._t.Start());
            }
            catch (Exception ex) { Log.Error(ex, "Aviso breve"); }
        }
    }
}
