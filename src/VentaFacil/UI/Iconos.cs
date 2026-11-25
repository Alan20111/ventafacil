using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace VentaFacil.UI
{
    /// <summary>Iconos dibujados por código (sin archivos de imagen): así van dentro del único .exe.</summary>
    public static class Iconos
    {
        static Image _billete;
        static Icon _ventana;

        /// <summary>El billete en grande (256 px), incrustado en el programa.</summary>
        public static Image Billete
        {
            get
            {
                if (_billete != null) return _billete;
                try
                {
                    using (var st = typeof(Iconos).Assembly.GetManifestResourceStream("billete.png"))
                        if (st != null) using (var i = Image.FromStream(st)) _billete = new Bitmap(i);
                }
                catch (Exception ex) { Log.Error(ex, "Imagen del billete"); }
                return _billete;
            }
        }

        /// <summary>El icono de las ventanas (barra de título y barra de tareas).</summary>
        public static Icon Ventana
        {
            get
            {
                if (_ventana != null) return _ventana;
                try
                {
                    using (var st = typeof(Iconos).Assembly.GetManifestResourceStream("VentaFacil.ico"))
                        if (st != null) _ventana = new Icon(st);
                }
                catch (Exception ex) { Log.Error(ex, "Icono de ventana"); }
                return _ventana;
            }
        }

        /// <summary>Una base de datos (cilindro) con una flecha hacia abajo: «traer datos de otro programa».</summary>
        public static Bitmap Importar(int tam, Color color)
        {
            var bmp = new Bitmap(tam, tam);
            using (var g = Graphics.FromImage(bmp))
            using (var lapiz = new Pen(color, Math.Max(1.6f, tam / 11f)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var relleno = new SolidBrush(Color.FromArgb(40, color)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float x = tam * 0.12f, w = tam * 0.60f, ancho = w, h = tam * 0.20f;
                // cilindro: tapa de arriba, dos bandas y fondo
                float yTop = tam * 0.10f, yBase = tam * 0.62f;
                g.FillEllipse(relleno, x, yTop, ancho, h);
                g.DrawEllipse(lapiz, x, yTop, ancho, h);
                g.DrawLine(lapiz, x, yTop + h / 2, x, yBase);
                g.DrawLine(lapiz, x + ancho, yTop + h / 2, x + ancho, yBase);
                g.DrawArc(lapiz, x, yTop + (yBase - yTop) / 2 - h / 2, ancho, h, 0, 180);
                g.DrawArc(lapiz, x, yBase - h / 2, ancho, h, 0, 180);
                // flecha hacia abajo, abajo a la derecha
                float cx = tam * 0.74f, y1 = tam * 0.46f, y2 = tam * 0.90f, a = tam * 0.15f;
                g.DrawLine(lapiz, cx, y1, cx, y2);
                g.DrawLine(lapiz, cx - a, y2 - a, cx, y2);
                g.DrawLine(lapiz, cx + a, y2 - a, cx, y2);
            }
            return bmp;
        }
    }
}
