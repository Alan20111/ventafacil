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
        public class Modulo { public string Tecla, Nombre; public bool Bloqueado; }
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

        /// <summary>Marca un módulo con candado (el usuario no tiene permiso; al pulsarlo se pide autorización).</summary>
        public void Bloquear(int i, bool bloqueado) { if (i >= 0 && i < _items.Count && _items[i].Bloqueado != bloqueado) { _items[i].Bloqueado = bloqueado; Invalidate(); } }

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
                TextRenderer.DrawText(g, _items[i].Nombre, new Font("Segoe UI", 13f, FontStyle.Bold), new Rectangle(r.X + 48, r.Y, r.Width - (_items[i].Bloqueado ? 74 : 52), r.Height), _items[i].Bloqueado && !activo ? Color.FromArgb(140, 160, 195) : Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                if (_items[i].Bloqueado) Candado(g, r.Right - 22, r.Y + r.Height / 2 - 7);
            }
            using (var br = new SolidBrush(Color.White)) g.FillRectangle(br, _lineaX + 14, Height - 5, w - 30, 3);
        }

        /// <summary>Candado de 12x14 dibujado a mano (Windows 7 no trae bien los emojis).</summary>
        static void Candado(Graphics g, int x, int y)
        {
            using (var pluma = new Pen(Tema.Piedra, 2f)) g.DrawArc(pluma, x + 2, y, 8, 10, 180, 180);
            using (var br = new SolidBrush(Tema.Piedra)) g.FillRectangle(br, x, y + 6, 12, 8);
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
        readonly Panel _barra = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Tema.AzulFondo };
        readonly Label _estado;
        readonly string _titulo;
        Control _actual;
        int _vistaActual = -1;

        public ModuloVista(string titulo, string descripcion)
        {
            _titulo = titulo;
            Dock = DockStyle.Fill; BackColor = Tema.Crema;
            _estado = new Label { AutoSize = false, Height = 0, Visible = false };
            Controls.Add(_cuerpo);
            Controls.Add(_barra);
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
            _barra.Visible = _items.Count > 1;   // con una sola vista no hace falta botón
            for (int i = 0; i < _items.Count; i++)
            {
                int n = i;
                var b = new BotonF("Ctrl+F" + (i + 1), _items[i].Nombre, (s, e) => Atajo(n + 1));
                _items[i].Boton = b;
                _barra.Controls.Add(b);
            }
            _barra.Resize += (s, e) => Acomodar();
            Acomodar();
            for (int i = 0; i < _items.Count; i++) if (_items[i].Vista != null) { Mostrar(i); break; }
        }

        static readonly Font Letra11 = new Font("Segoe UI", 11f, FontStyle.Bold), Letra95 = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        static Font LetraBoton(float t) { return t > 10 ? Letra11 : Letra95; }

        /// <summary>Todos del mismo tamaño, pegados a la izquierda; si son muchos se reparten el ancho.</summary>
        void Acomodar()
        {
            if (_items.Count == 0 || _barra.Width < 50) return;
            const int hueco = 8, margen = 12;
            int ancho = Math.Min(190, (_barra.Width - margen * 2 - hueco * (_items.Count - 1)) / _items.Count);
            // si algún nombre no cabe, la letra de todos se achica igual
            float tam = 11f;
            foreach (var it in _items) if (TextRenderer.MeasureText(it.Nombre, LetraBoton(11f), Size.Empty, TextFormatFlags.NoPadding).Width > ancho - 10) tam = 9.5f;
            for (int i = 0; i < _items.Count; i++) { _items[i].Boton.Font = LetraBoton(tam); _items[i].Boton.SetBounds(margen + i * (ancho + hueco), 8, ancho, 52); }
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
