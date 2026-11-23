using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VentaFacil.UI
{
    /// <summary>
    /// Barra de módulos de arriba: F1 Caja · F2 Turno · F3 Catálogo · F4 Almacén · F5 Resultados · F6 Ajustes.
    /// La línea blanca de abajo se desliza hacia el módulo elegido.
    /// </summary>
    public class BarraModulos : Control
    {
        public class Modulo { public string Tecla, Nombre; }
        readonly List<Modulo> _items = new List<Modulo>();
        readonly System.Windows.Forms.Timer _t = new System.Windows.Forms.Timer { Interval = 15 };
        int _activo, _sobre = -1;
        float _lineaX = -1;       // posición animada de la línea
        public event Action<int> Elegido;

        public BarraModulos()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top; Height = 52; BackColor = Tema.AzulOscuro; Cursor = Cursors.Hand; TabStop = false;
            _t.Tick += (s, e) => Paso();
        }

        public void Agregar(string tecla, string nombre) { _items.Add(new Modulo { Tecla = tecla, Nombre = nombre }); Invalidate(); }
        public int Activo { get { return _activo; } set { _activo = value; if (Tema.Animaciones && IsHandleCreated) _t.Start(); else { _lineaX = Destino(); Invalidate(); } } }

        float Ancho { get { return _items.Count == 0 ? 1 : Math.Min(210f, (Width - 16f) / _items.Count); } }
        float Destino() { return 8 + _activo * Ancho; }

        void Paso()
        {
            float d = Destino();
            if (_lineaX < 0) _lineaX = d;
            _lineaX += (d - _lineaX) * 0.35f;
            if (Math.Abs(d - _lineaX) < 1) { _lineaX = d; _t.Stop(); }
            Invalidate();
        }

        int Cual(Point p) { int i = (int)((p.X - 8) / Ancho); return p.X < 8 || i < 0 || i >= _items.Count ? -1 : i; }
        protected override void OnMouseMove(MouseEventArgs e) { int i = Cual(e.Location); if (i != _sobre) { _sobre = i; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { _sobre = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { int i = Cual(e.Location); if (i >= 0 && Elegido != null) Elegido(i); base.OnMouseClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tema.AzulOscuro);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (_lineaX < 0) _lineaX = Destino();
            float w = Ancho;
            for (int i = 0; i < _items.Count; i++)
            {
                var r = new Rectangle((int)(8 + i * w), 4, (int)w - 2, Height - 8);
                bool activo = i == _activo;
                if (activo || i == _sobre)
                    using (var br = new SolidBrush(activo ? Tema.Azul : Color.FromArgb(0, 58, 134)))
                    using (var ruta = Redondo(r, 8)) g.FillPath(br, ruta);
                TextRenderer.DrawText(g, _items[i].Tecla, new Font("Segoe UI", 9.5f, FontStyle.Bold), new Rectangle(r.X + 10, r.Y, 40, r.Height), activo ? Tema.AzulClaro2 : Tema.Piedra, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, _items[i].Nombre, new Font("Segoe UI", 13f, FontStyle.Bold), new Rectangle(r.X + 48, r.Y, r.Width - 52, r.Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            using (var br = new SolidBrush(Color.White)) g.FillRectangle(br, _lineaX + 14, Height - 5, w - 30, 3);
        }

        static System.Drawing.Drawing2D.GraphicsPath Redondo(Rectangle r, int d)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            p.AddArc(r.X, r.Y, d * 2, d * 2, 180, 90); p.AddArc(r.Right - d * 2, r.Y, d * 2, d * 2, 270, 90);
            p.AddArc(r.Right - d * 2, r.Bottom - d * 2, d * 2, d * 2, 0, 90); p.AddArc(r.X, r.Bottom - d * 2, d * 2, d * 2, 90, 90);
            p.CloseFigure(); return p;
        }

        protected override void Dispose(bool disposing) { if (disposing) _t.Dispose(); base.Dispose(disposing); }
    }

    /// <summary>
    /// Un módulo: título, el estado y una barra de atajos (Ctrl+F1, Ctrl+F2…) arriba, y debajo la vista elegida.
    /// Cada atajo cambia de vista o ejecuta una acción; así el módulo declara arriba lo que se puede hacer.
    /// </summary>
    public class ModuloVista : UserControl
    {
        class Elemento { public string Nombre; public Func<Control> Vista; public Action Accion; public BotonF Boton; }
        readonly List<Elemento> _items = new List<Elemento>();
        readonly Panel _cuerpo = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Crema };
        readonly TableLayoutPanel _barra = new TableLayoutPanel { Dock = DockStyle.Top, Height = 68, RowCount = 1, BackColor = Tema.AzulFondo, Padding = new Padding(8, 6, 8, 4) };
        readonly Label _estado;
        readonly string _titulo;
        Control _actual;
        int _vistaActual = -1;

        public ModuloVista(string titulo, string descripcion)
        {
            _titulo = titulo;
            Dock = DockStyle.Fill; BackColor = Tema.Crema;
            var cab = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Tema.AzulFondo };
            cab.Controls.Add(new Label { Text = titulo.ToUpperInvariant(), Left = 12, Top = 6, AutoSize = true, Font = Tema.Mediana, ForeColor = Tema.Azul });
            cab.Controls.Add(new Label { Text = descripcion, Left = 12 + TextRenderer.MeasureText(titulo.ToUpperInvariant(), Tema.Mediana).Width + 14, Top = 11, AutoSize = true, Font = Tema.Normal, ForeColor = Tema.Humo });
            _estado = new Label { AutoSize = false, Width = 420, Height = 28, Top = 8, TextAlign = ContentAlignment.MiddleRight, Font = Tema.Negrita, ForeColor = Tema.Verde, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            cab.Controls.Add(_estado);
            cab.Resize += (s, e) => _estado.Left = cab.Width - _estado.Width - 12;
            Controls.Add(_cuerpo);
            Controls.Add(_barra);
            Controls.Add(cab);
        }

        public Control Actual { get { return _actual; } }
        public string Titulo { get { return _titulo; } }

        public void Estado(string texto) { _estado.Text = texto ?? ""; }

        /// <summary>Un atajo que cambia de vista. El primero que se agrega es la vista inicial.</summary>
        public void AgregarVista(string nombre, Func<Control> crear) { _items.Add(new Elemento { Nombre = nombre, Vista = crear }); }
        /// <summary>Un atajo que ejecuta una acción (abrir un diálogo, importar…).</summary>
        public void AgregarAccion(string nombre, Action accion) { _items.Add(new Elemento { Nombre = nombre, Accion = accion }); }

        /// <summary>Dibuja la barra de atajos y muestra la primera vista.</summary>
        public void Construir()
        {
            _barra.ColumnCount = Math.Max(1, _items.Count);
            for (int i = 0; i < _items.Count; i++)
            {
                _barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / _items.Count));
                int n = i;
                var b = new BotonF("Ctrl+F" + (i + 1), _items[i].Nombre, (s, e) => Atajo(n + 1)) { Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 0) };
                _items[i].Boton = b;
                _barra.Controls.Add(b, i, 0);
            }
            for (int i = 0; i < _items.Count; i++) if (_items[i].Vista != null) { Mostrar(i); break; }
        }

        /// <summary>Ctrl+F(n): n empieza en 1. Devuelve false si el módulo no tiene tantos atajos.</summary>
        public bool Atajo(int n)
        {
            if (n < 1 || n > _items.Count) return false;
            var it = _items[n - 1];
            if (it.Vista != null) Mostrar(n - 1);
            else if (it.Accion != null) { Estado(""); it.Accion(); }
            return true;
        }

        public void Mostrar(int indice)
        {
            if (indice < 0 || indice >= _items.Count || _items[indice].Vista == null) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                _cuerpo.SuspendLayout();
                if (_actual != null) { _cuerpo.Controls.Remove(_actual); _actual.Dispose(); _actual = null; }
                _actual = _items[indice].Vista();
                _actual.Dock = DockStyle.Fill;
                _cuerpo.Controls.Add(_actual);
                _cuerpo.ResumeLayout();
                _vistaActual = indice;
                for (int i = 0; i < _items.Count; i++) if (_items[i].Vista != null) Tema.Pastilla(_items[i].Boton, i == indice);
                _actual.Focus();
                _actual.SelectNextControl(_actual, true, true, true, true);
            }
            catch (Exception ex) { Ui.Error(ex, "Abrir " + _items[indice].Nombre); }
            finally { Cursor = Cursors.Default; }
        }

        /// <summary>Vuelve a crear la vista actual (para refrescar los datos).</summary>
        public void Recargar() { if (_vistaActual >= 0) Mostrar(_vistaActual); }
    }
}
