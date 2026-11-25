using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace VentaFacil.UI
{
    /// <summary>
    /// Barra de módulos de arriba: F1 Caja · F2 Turno · F3 Catálogo · F4 Almacén · F5 Resultados · F6 Ajustes.
    /// La pastilla se desliza hacia el módulo elegido. Un módulo sin permiso no existe: ni se dibuja ni responde.
    /// </summary>
    public class BarraModulos : Control
    {
        class Modulo { public string Tecla, Nombre; public bool Visible = true; public float Encima; }
        static readonly Font LetraTecla = new Font("Segoe UI", 9.5f, FontStyle.Bold), LetraNombre = new Font("Segoe UI", 13f, FontStyle.Bold);
        static readonly Color Realce = Color.FromArgb(0, 58, 134);
        readonly List<Modulo> _items = new List<Modulo>();
        int _activo, _sobre = -1;
        float _x;            // dónde va la pastilla mientras se desliza
        bool _desliza;
        public event Action<int> Elegido;

        public BarraModulos()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top; Height = 52; BackColor = Tema.AzulOscuro; TabStop = false;
        }

        public void Agregar(string tecla, string nombre) { _items.Add(new Modulo { Tecla = tecla, Nombre = nombre }); Invalidate(); }

        public int Activo
        {
            get { return _activo; }
            set
            {
                float desde = _desliza ? _x : Lugar(_activo);
                _activo = value;
                if (!IsHandleCreated) { Invalidate(); return; }
                _desliza = true; _x = desde;
                Animacion.Ejecutar(this, "pastilla", 200, p => { _x = Animacion.Entre(desde, Lugar(_activo), p); Invalidate(); }, () => { _desliza = false; Invalidate(); });
            }
        }

        public bool Visto(int i) { return i >= 0 && i < _items.Count && _items[i].Visible; }

        /// <summary>Quita o pone un módulo. Los demás se recorren, pero cada quien conserva su tecla.</summary>
        public void Mostrar(int i, bool visible)
        {
            if (i < 0 || i >= _items.Count || _items[i].Visible == visible) return;
            _items[i].Visible = visible;
            _items[i].Encima = 0;
            _sobre = -1;
            Animacion.Parar(this, "pastilla");
            _desliza = false;
            Invalidate();
        }

        int Cuantos { get { int n = 0; foreach (var m in _items) if (m.Visible) n++; return Math.Max(1, n); } }
        float Ancho { get { return Math.Min(210f, (Width - 16f) / Cuantos); } }
        float Lugar(int i) { int n = 0; for (int k = 0; k < i && k < _items.Count; k++) if (_items[k].Visible) n++; return 8 + n * Ancho; }

        int Cual(Point p)
        {
            if (p.X < 8) return -1;
            int n = (int)((p.X - 8) / Ancho);
            for (int i = 0; i < _items.Count; i++) if (_items[i].Visible && n-- == 0) return i;
            return -1;
        }

        void Sobre(int i)
        {
            if (i == _sobre) return;
            int antes = _sobre;
            _sobre = i;
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
            if (antes >= 0) Encender(_items[antes], 0, 180);
            if (i >= 0) Encender(_items[i], 1, 120);
        }

        void Encender(Modulo m, float hasta, int ms)
        {
            float desde = m.Encima;
            Animacion.Ejecutar(this, "encima" + m.Tecla, ms, p => { m.Encima = Animacion.Entre(desde, hasta, p); Invalidate(); });
        }

        protected override void OnMouseMove(MouseEventArgs e) { Sobre(Cual(e.Location)); base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { Sobre(-1); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e) { int i = Cual(e.Location); if (i >= 0 && Elegido != null) Elegido(i); base.OnMouseClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Tema.AzulOscuro);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float w = Ancho;
            float px = _desliza ? _x : Lugar(_activo);
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].Visible && _items[i].Encima > 0.01f && i != _activo)
                    using (var br = new SolidBrush(FundidoBoton.Mezcla(Tema.AzulOscuro, Realce, _items[i].Encima)))
                    using (var ruta = Tonos.Redondo(new RectangleF(Lugar(i), 4, w - 2, Height - 8), 8)) g.FillPath(br, ruta);
            using (var br = new SolidBrush(Tema.Azul))
            using (var ruta = Tonos.Redondo(new RectangleF(px, 4, w - 2, Height - 8), 8)) g.FillPath(br, ruta);
            using (var br = new SolidBrush(Color.White)) g.FillRectangle(br, px + 14, Height - 8, w - 30, 3);
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].Visible) continue;
                var r = new Rectangle((int)Lugar(i), 2, (int)w - 2, Height - 8);
                TextRenderer.DrawText(g, _items[i].Tecla, LetraTecla, new Rectangle(r.X + 10, r.Y, 40, r.Height), i == _activo ? Tema.AzulClaro2 : Tema.Piedra, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, _items[i].Nombre, LetraNombre, new Rectangle(r.X + 48, r.Y, r.Width - 52, r.Height), Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }
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
                FondoConBillete(_actual);
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

        /// <summary>El billete tenue va en la hoja de la vista (o en el panel que la cubre por completo).</summary>
        static void FondoConBillete(Control v)
        {
            var hoja = v;
            foreach (Control h in v.Controls) if (h is Panel && h.Dock == DockStyle.Fill) hoja = h;
            if (hoja is TabControl) return;
            Marca.Fondo(hoja);
        }

        /// <summary>Vuelve a crear la vista actual (para refrescar los datos).</summary>
        public void Recargar() { if (_vistaActual >= 0) Mostrar(_vistaActual); }
    }
}
