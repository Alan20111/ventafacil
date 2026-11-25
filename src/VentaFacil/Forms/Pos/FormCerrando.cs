using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Pos
{
    /// <summary>El billete de VentaFácil. Gira como una moneda (o está quieto).</summary>
    public class Billete : Control
    {
        readonly System.Windows.Forms.Timer _t = new System.Windows.Forms.Timer { Interval = 30 };
        double _angulo;

        public Billete()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            _t.Tick += (s, e) => { _angulo += 0.14; Invalidate(); };
        }

        public bool Girando { get { return _t.Enabled; } set { _t.Enabled = value; if (!value) { _angulo = 0; Invalidate(); } } }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent == null ? BackColor : Parent.BackColor);
            var img = Iconos.Billete;
            if (img == null) return;
            int lado = Math.Min(Width, Height);
            double c = Math.Cos(_angulo);
            int ancho = Math.Max(3, (int)(lado * Math.Abs(c)));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(img, new Rectangle((Width - ancho) / 2, (Height - lado) / 2, ancho, lado));
        }

        protected override void Dispose(bool disposing) { if (disposing) _t.Dispose(); base.Dispose(disposing); }
    }

    /// <summary>¿Salir de VentaFácil? Con lo que se perdería, si hay algo. Esc = seguir trabajando.</summary>
    public class FormSalir : Dialogo
    {
        public FormSalir(IList<string> avisos) : base("Salir", 560, 230 + avisos.Count * 34)
        {
            StartPosition = FormStartPosition.CenterParent;
            Controls.Add(new PanelTitulo { Titulo = "VentaFácil", Subtitulo = "cerrar el programa", Height = 64 });
            Controls.Add(new Billete { Left = 24, Top = 84, Width = 80, Height = 80 });
            Controls.Add(Ui.Etiqueta("¿Salir de VentaFácil?", 124, 86, Tema.Mediana, Tema.Azul));
            Controls.Add(Ui.Etiqueta("Antes de cerrar se hace un respaldo de sus datos.", 124, 120));
            int y = 154;
            foreach (var a in avisos)
            {
                Controls.Add(Ui.Etiqueta("•  " + a, 124, y, null, Tema.Rojo));
                y += 34;
            }
            int by = ClientSize.Height - 62;
            var seguir = Ui.Boton("Seguir trabajando (Esc)", 124, by, 230, 46, (s, e) => { DialogResult = DialogResult.Cancel; }, true);
            var salir = Ui.Boton("Salir", 366, by, 170, 46, (s, e) => { DialogResult = DialogResult.OK; });
            if (avisos.Count > 0) Tema.BotonPeligro(salir);
            Controls.Add(seguir); Controls.Add(salir);
            CancelButton = seguir;
            Shown += (s, e) => seguir.Focus();   // Enter no cierra por accidente
        }
    }

    /// <summary>
    /// Pantalla de cierre: el billete gira mientras se hace el respaldo. No se puede cerrar antes de tiempo.
    /// Si algo falla lo dice y deja reintentar o salir: nunca atrapa a la persona.
    /// </summary>
    public class FormCerrando : Form
    {
        readonly Billete _billete;
        readonly Label _titulo, _detalle;
        readonly Button _otra, _salir;
        bool _puedeCerrar;
        DateTime _inicio;

        /// <summary>
        /// Muestra la pantalla y espera a que termine. Va en su propio hilo porque, después de Application.Exit, el hilo
        /// principal ya tiene pedido «terminar» y una ventana ahí se cerraría sola sin respaldar (visto en Windows).
        /// Si la pantalla no pudo, se respalda sin ella.
        /// </summary>
        public static void Ejecutar(Action<FormCerrando> alCrear = null)
        {
            if (Respaldo.CierreHecho) return;
            var hilo = new Thread(() =>
            {
                try
                {
                    using (var f = new FormCerrando())
                    {
                        if (alCrear != null) alCrear(f);
                        Application.Run(f);
                    }
                }
                catch (Exception ex) { Log.Error(ex, "Pantalla de cierre"); }
            }) { Name = "VentaFacil-pantalla-cierre" };
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            while (!hilo.Join(40)) Application.DoEvents();   // la ventana de atrás se sigue dibujando (no sale «No responde»)
            if (Respaldo.CierreHecho) return;
            try { Respaldo.AlCerrar(null); } catch (Exception ex) { Log.Error(ex, "Respaldo al cerrar"); }
        }

        public FormCerrando()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "VentaFácil";
            Icon = Iconos.Ventana;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ClientSize = new Size(480, 372);
            BackColor = Tema.AzulOscuro;
            Font = Tema.Normal;
            Cursor = Cursors.WaitCursor;
            _billete = new Billete { Left = 176, Top = 44, Width = 128, Height = 128 };
            Controls.Add(_billete);
            _titulo = new Label { Left = 20, Top = 196, Width = 440, Height = 40, TextAlign = ContentAlignment.MiddleCenter, Font = Tema.Grande, ForeColor = Color.White, Text = "Haciendo respaldo…" };
            Controls.Add(_titulo);
            _detalle = new Label { Left = 20, Top = 240, Width = 440, Height = 66, TextAlign = ContentAlignment.MiddleCenter, Font = Tema.Normal, ForeColor = Tema.AzulClaro2, Text = "No apague la computadora." };
            Controls.Add(_detalle);
            _otra = Ui.Boton("Reintentar", 54, 314, 170, 44, (s, e) => Empezar());
            _salir = Ui.Boton("Salir de todos modos", 236, 314, 190, 44, (s, e) => { _puedeCerrar = true; Close(); });
            _otra.Visible = _salir.Visible = false;
            Controls.Add(_otra); Controls.Add(_salir);
            FormClosing += (s, e) => { if (!_puedeCerrar && e.CloseReason != CloseReason.WindowsShutDown) e.Cancel = true; };
            Shown += (s, e) => { AlFrente(); Empezar(); };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr despues, int x, int y, int w, int alto, uint banderas);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);

        /// <summary>Encima de todo: la persona debe ver que se está respaldando antes de apagar.</summary>
        void AlFrente()
        {
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);   // siempre arriba, sin mover ni cambiar tamaño
            SetForegroundWindow(Handle);
            Activate();
        }

        void Empezar()
        {
            _otra.Visible = _salir.Visible = false;
            Cursor = Cursors.WaitCursor;
            _titulo.Text = "Haciendo respaldo…";
            _detalle.Text = "No apague la computadora.";
            _billete.Girando = true;
            _inicio = DateTime.Now;
            new Thread(() =>
            {
                Exception error = null; Respaldo.Cierre r = null;
                try { r = Respaldo.AlCerrar(null); }
                catch (Exception ex) { error = ex; Log.Error(ex, "Respaldo al cerrar"); }
                // que se alcance a ver, aunque la base sea chica
                int falta = 1400 - (int)(DateTime.Now - _inicio).TotalMilliseconds;
                if (falta > 0) Thread.Sleep(falta);
                try { BeginInvoke((Action)(() => Termino(r, error))); } catch (InvalidOperationException) { /* Windows se está apagando */ }
            }) { Name = "VentaFacil-cierre" }.Start();
        }

        void Termino(Respaldo.Cierre r, Exception error)
        {
            _billete.Girando = false;
            Cursor = Cursors.Default;
            if (error == null && r.CopiaBien)
            {
                _puedeCerrar = true;
                _titulo.Text = "Listo";
                Close();
                return;
            }
            _titulo.Text = error != null ? "No se hizo el respaldo" : "Faltó una copia";
            _detalle.Text = error != null ? error.Message : "El respaldo sí quedó en esta computadora, pero no se pudo copiar a " + r.Carpeta + " (¿USB desconectada?).";
            _otra.Visible = _salir.Visible = true;
            _otra.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Tema.Azul, 4f)) e.Graphics.DrawRectangle(p, 2, 2, ClientSize.Width - 4, ClientSize.Height - 4);
        }
    }
}
