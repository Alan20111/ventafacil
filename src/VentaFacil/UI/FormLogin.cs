using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Data;

namespace VentaFacil.UI
{
    /// <summary>Panel azul con el billete grande y el nombre del negocio.</summary>
    public class PanelMarca : Control
    {
        readonly string _negocio;
        public PanelMarca(string negocio)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false; _negocio = negocio ?? "";
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var f = new System.Drawing.Drawing2D.LinearGradientBrush(ClientRectangle, Tema.Azul, Tema.AzulOscuro, 90f)) g.FillRectangle(f, ClientRectangle);
            int ancho = System.Math.Min(176, Width - 60), lado = ancho * 5 / 8;
            int y = Height / 2 - lado - 8;
            Marca.Blanco(g, (Width - ancho) / 2, y, ancho);
            const TextFormatFlags c = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(g, "VentaFácil", Tema.Grande, new Rectangle(0, y + lado + 16, Width, 44), Color.White, c | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, _negocio == "" ? "Punto de venta" : _negocio, Tema.Normal, new Rectangle(16, y + lado + 62, Width - 32, 50), Tema.AzulClaro2, c | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Entrada al sistema. También sirve para pedir autorización de un administrador.</summary>
    public class FormLogin : Form
    {
        readonly TextBox _usuario, _clave;
        readonly EtiquetaEstado _error;
        int? _x0;
        public Usuario UsuarioValido { get; private set; }

        public FormLogin(bool autorizacion, string motivo)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = autorizacion ? "Autorización" : "VentaFácil - Entrar";
            if (Iconos.Ventana != null) Icon = Iconos.Ventana;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = autorizacion ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = !autorizacion;
            ShowInTaskbar = !autorizacion;
            BackColor = Tema.Crema;
            Font = Tema.Normal;
            KeyPreview = true;

            int x = 24, y;
            if (autorizacion)
            {
                ClientSize = new Size(460, 300);
                Controls.Add(new PanelTitulo { Titulo = "VentaFácil", Subtitulo = "Autorización" });
                var banda = new Banda(Tono.Cuidado, motivo ?? "Se requiere un administrador", 24, 68, 412);
                Controls.Add(banda);
                y = banda.Bottom + 16;
                ClientSize = new Size(460, y + 188);
            }
            else
            {   // a la izquierda el billete y el negocio; a la derecha se entra
                ClientSize = new Size(760, 372);
                string negocio = Core.Config.Actual == null ? "" : Core.Config.Actual.NegocioNombre;
                var lado = new PanelMarca(negocio == "MI NEGOCIO" ? "" : negocio) { Left = 0, Top = 0, Width = 288, Height = 372 };
                Controls.Add(lado);
                x = 320;
                Controls.Add(Ui.Etiqueta("Bienvenido(a)", x - 4, 28, Tema.Grande, Tema.Azul));
                Controls.Add(Ui.Etiqueta("Escriba su usuario y su contraseña.", x, 72, null, Tema.Humo));
                y = 116;
            }

            Controls.Add(Ui.Etiqueta("Usuario:", x, y + 4, Tema.Mediana));
            _usuario = Ui.Caja(x + 144, y, 264, Tema.Mediana);
            Controls.Add(_usuario);
            y += 48;
            Controls.Add(Ui.Etiqueta("Contraseña:", x, y + 4, Tema.Mediana));
            _clave = Ui.Caja(x + 144, y, 264, Tema.Mediana);
            _clave.UseSystemPasswordChar = true;
            Controls.Add(_clave);
            y += 44;
            _error = new EtiquetaEstado { Left = x, Top = y, Width = 408, Height = 30, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, Font = Tema.Normal, ForeColor = Tema.Rojo };
            Controls.Add(_error);
            y += 38;

            var ok = Ui.Boton("Entrar (Enter)", x + 144, y, 144, 42, (s, e) => Entrar(), true);
            var no = Ui.Boton(autorizacion ? "Cancelar" : "Salir", x + 296, y, 112, 42, (s, e) => { DialogResult = DialogResult.Cancel; Close(); });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            System.EventHandler limpiar = (s, e) => { if (_error.Text != "") _error.Text = ""; };
            _usuario.TextChanged += limpiar; _clave.TextChanged += limpiar;

            if (!autorizacion)
            {
                if (UsuarioRepo.Validar("admin", "admin") != null)
                    Controls.Add(new Label { Text = "Primera vez: usuario admin, contraseña admin", Left = x, Top = 302, Width = 408, Height = 24, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9.5f), ForeColor = Tema.Humo, BackColor = Color.Transparent });
                var marca = new Label { Text = "alanmendez.dev", Left = x, Top = 338, Width = 408, Height = 22, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(110, 125, 145), BackColor = Color.Transparent };
                marca.Cursor = Cursors.Hand;
                marca.Click += (s, e) => { try { System.Diagnostics.Process.Start("https://alanmendez.dev"); } catch { } };
                Controls.Add(marca);
            }
            Shown += (s, e) => _usuario.Focus();
        }

        /// <summary>Un «no» corto con la ventana, además del aviso.</summary>
        void Sacudir()
        {
            if (!Animacion.Activas) return;
            if (_x0 == null) _x0 = Left;
            int x0 = _x0.Value;
            Animacion.Ejecutar(this, "no", 260, p => Left = x0 + (p >= 1 ? 0 :  (int)(System.Math.Sin(p * System.Math.PI * 5) * 8 * (1 - p))), () => _x0 = null);
        }

        protected override void OnHandleCreated(System.EventArgs e) { base.OnHandleCreated(e); Tema.VentanaModerna(this); }

        void Entrar()
        {
            if (_usuario.Text.Trim() == "") { _usuario.Focus(); return; }
            if (_clave.Text == "") { _clave.Focus(); return; }   // Enter en el usuario pasa a la contraseña
            var u = UsuarioRepo.Validar(_usuario.Text, _clave.Text);
            if (u == null)
            {
                _clave.SelectAll(); _clave.Focus();
                _error.Text = "Usuario o contraseña incorrectos";
                Sacudir();
                return;
            }
            UsuarioValido = u;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
