using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>
    /// Ventana de espera para trabajos largos (restaurar un respaldo de 1 GB, copiarlo): barra de avance, tiempo y mensaje.
    /// No se puede cerrar hasta que el trabajo termine, para no dejar a medias una restauración de SQL Server.
    /// </summary>
    public class FormProgresoBk : Form
    {
        readonly ProgressBar _barra;
        readonly Label _mensaje, _tiempo;
        readonly System.Windows.Forms.Timer _reloj = new System.Windows.Forms.Timer { Interval = 1000 };
        readonly Action<Action<int, string>> _trabajo;
        readonly DateTime _inicio = DateTime.Now;
        volatile bool _terminado;
        Exception _error;

        FormProgresoBk(string titulo, Action<Action<int, string>> trabajo)
        {
            _trabajo = trabajo;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = titulo;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ControlBox = false; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            ClientSize = new Size(560, 170);
            Font = Tema.Normal;
            BackColor = Tema.Crema;

            Controls.Add(Ui.Etiqueta(titulo, 16, 14, Tema.Mediana, Tema.Azul));
            _barra = new ProgressBar { Left = 16, Top = 58, Width = 528, Height = 26, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Minimum = 0, Maximum = 100 };
            Controls.Add(_barra);
            _mensaje = new Label { Left = 16, Top = 94, Width = 528, Height = 24, Font = Tema.Normal, ForeColor = Tema.Humo, AutoEllipsis = true, Text = "Preparando…" };
            Controls.Add(_mensaje);
            _tiempo = new Label { Left = 16, Top = 124, Width = 528, Height = 34, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Tema.Ceniza, Text = "Puede tardar varios minutos. No apague la computadora." };
            Controls.Add(_tiempo);

            _reloj.Tick += (s, e) => { var t = DateTime.Now - _inicio; _tiempo.Text = "Transcurrido: " + ((int)t.TotalMinutes).ToString("00") + ":" + t.Seconds.ToString("00") + "   ·   no cierre el programa ni apague la computadora."; };
            Shown += (s, e) => Empezar();
        }

        void Empezar()
        {
            _reloj.Start();
            var hilo = new Thread(() =>
            {
                try { _trabajo(Avance); }
                catch (Exception ex) { _error = ex; Log.Error(ex, "Trabajo largo del importador"); }
                _terminado = true;
                try { BeginInvoke((Action)(() => Close())); } catch (Exception) { }
            });
            hilo.IsBackground = true;
            hilo.Start();
        }

        void Avance(int porciento, string mensaje)
        {
            if (!IsHandleCreated || IsDisposed) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (porciento >= 0)
                    {
                        if (_barra.Style != ProgressBarStyle.Continuous) _barra.Style = ProgressBarStyle.Continuous;
                        _barra.Value = Math.Min(100, porciento);
                    }
                    string m = (mensaje ?? "").Trim();
                    _mensaje.Text = porciento >= 0 ? porciento + " %" : (m == "" ? _mensaje.Text : m);
                }));
            }
            catch (Exception) { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_terminado) { e.Cancel = true; return; }
            _reloj.Stop();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _reloj.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>Corre el trabajo con la ventana de espera. Devuelve el error que tuvo, o null si salió bien.</summary>
        public static Exception Ejecutar(IWin32Window dueno, string titulo, Action<Action<int, string>> trabajo)
        {
            using (var f = new FormProgresoBk(titulo, trabajo))
            {
                f.ShowDialog(dueno);
                return f._error;
            }
        }
    }

    /// <summary>Muestra los comandos de SQL Server para revisar o restaurar el respaldo a mano (osql, Management Studio).</summary>
    public class FormComandosBk : Dialogo
    {
        public FormComandosBk(string texto) : base("Comandos de SQL Server", 760, 440)
        {
            Controls.Add(Ui.Etiqueta("Esto es exactamente lo que hace VentaFácil. Sirve para probarlo a mano en osql o Management Studio.", 16, 12, Tema.Normal, Tema.Humo));
            var caja = new TextBox
            {
                Left = 16, Top = 44, Width = 728, Height = 320, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
                Font = new Font("Consolas", 10.5f, FontStyle.Bold), BackColor = Color.White, Text = texto.Replace("\r\n", "\n").Replace("\n", "\r\n")
            };
            Controls.Add(caja);
            Controls.Add(Ui.Boton("Copiar", 16, 378, 150, 42, (s, e) => { try { Clipboard.SetText(caja.Text); } catch (Exception) { Ui.Aviso("No se pudo copiar."); } }));
            Controls.Add(Ui.Boton("Cerrar (Esc)", 594, 378, 150, 42, (s, e) => Close()));
            Shown += (s, e) => caja.Select(0, 0);
        }
    }
}
