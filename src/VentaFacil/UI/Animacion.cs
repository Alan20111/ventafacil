using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VentaFacil.UI
{
    /// <summary>
    /// Motor único de animaciones: un solo reloj para todas (antes cada botón tenía el suyo) y avance por TIEMPO, no por
    /// cuadros, así duran lo mismo en cualquier computadora. Curva de salida suave (empieza rápido y frena).
    /// Se apagan solas si Windows pide reducir movimiento, por escritorio remoto, o si la computadora no da abasto.
    /// </summary>
    public static class Animacion
    {
        class Item { public Control C; public string Clave; public int Ms; public Stopwatch Reloj; public Action<float> Paso; public Action Fin; }

        // por hilo: la pantalla de cierre corre en el suyo
        [ThreadStatic] static List<Item> _items;
        [ThreadStatic] static Timer _t;
        [ThreadStatic] static Stopwatch _entre;
        [ThreadStatic] static int _lentos, _vistos;
        static bool _muyLenta;

        [DllImport("user32.dll")] static extern bool SystemParametersInfo(int accion, int p, ref int valor, int f);
        static bool? _sistema;
        static bool Sistema
        {
            get
            {
                if (_sistema.HasValue) return _sistema.Value;
                int v = 1;
                try { if (!SystemParametersInfo(0x1042, 0, ref v, 0)) v = 1; } catch (Exception) { v = 1; }   // «Animar controles y elementos»
                _sistema = v != 0 && !SystemInformation.TerminalServerSession;
                return _sistema.Value;
            }
        }

        public static bool Activas { get { return Tema.Animaciones && !_muyLenta && Sistema; } }

        /// <summary>Anima de 0 a 1. Si el mismo control ya animaba lo mismo, la nueva toma su lugar.</summary>
        public static void Ejecutar(Control c, string clave, int ms, Action<float> paso, Action fin = null)
        {
            Parar(c, clave);
            if (!Activas || c.IsDisposed) { paso(1f); if (fin != null) fin(); return; }
            if (_items == null) { _items = new List<Item>(); _entre = new Stopwatch(); _t = new Timer { Interval = 15 }; _t.Tick += Tic; }
            _items.Add(new Item { C = c, Clave = clave, Ms = Math.Max(1, ms), Reloj = Stopwatch.StartNew(), Paso = paso, Fin = fin });
            if (!_t.Enabled) { _entre.Reset(); _entre.Start(); _vistos = 0; _lentos = 0; _t.Start(); }
        }

        public static void Parar(Control c, string clave)
        {
            if (_items != null) _items.RemoveAll(i => i.C == c && i.Clave == clave);
        }

        static void Tic(object sender, EventArgs e)
        {
            // ¿se está atorando? Si 12 de 30 cuadros seguidos tardan más de 90 ms, se apagan por hoy
            if (_entre.ElapsedMilliseconds > 90) _lentos++;
            if (++_vistos >= 30) { if (_lentos >= 12) _muyLenta = true; _vistos = 0; _lentos = 0; }
            _entre.Reset(); _entre.Start();

            foreach (var i in _items.ToArray())
            {
                if (!_items.Contains(i)) continue;
                if (i.C.IsDisposed) { _items.Remove(i); continue; }
                float p = Math.Min(1f, i.Reloj.ElapsedMilliseconds / (float)i.Ms);
                if (_muyLenta) p = 1f;
                i.Paso(Salida(p));
                if (p >= 1f) { _items.Remove(i); if (i.Fin != null) i.Fin(); }
            }
            if (_items.Count == 0) _t.Stop();
        }

        /// <summary>Cúbica de salida.</summary>
        public static float Salida(float p) { float q = 1f - p; return 1f - q * q * q; }
        public static float Entre(float a, float b, float p) { return a + (b - a) * p; }
    }
}
