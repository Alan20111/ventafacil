using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Data;

namespace VentaFacil.UI
{
    /// <summary>Entrada al sistema. También sirve para pedir autorización de un administrador.</summary>
    public class FormLogin : Form
    {
        readonly TextBox _usuario, _clave;
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
            ClientSize = new Size(460, autorizacion ? 270 : 310);
            BackColor = Tema.Crema;
            Font = Tema.Normal;
            KeyPreview = true;

            var titulo = new PanelTitulo { Titulo = "VentaFácil", Subtitulo = autorizacion ? "Autorización" : "Punto de venta" };
            Controls.Add(titulo);

            int y = 72;
            if (autorizacion)
            {
                Controls.Add(Ui.Etiqueta(motivo ?? "Se requiere un administrador", 24, y, Tema.Negrita, Tema.Rojo));
                y += 28;
            }
            else y += 10;

            Controls.Add(Ui.Etiqueta("Usuario:", 24, y + 4, Tema.Mediana));
            _usuario = Ui.Caja(168, y, 264, Tema.Mediana);
            Controls.Add(_usuario);
            y += 48;
            Controls.Add(Ui.Etiqueta("Contraseña:", 24, y + 4, Tema.Mediana));
            _clave = Ui.Caja(168, y, 264, Tema.Mediana);
            _clave.UseSystemPasswordChar = true;
            Controls.Add(_clave);
            y += 56;

            var ok = Ui.Boton("Entrar (Enter)", 168, y, 144, 42, (s, e) => Entrar(), true);
            var no = Ui.Boton(autorizacion ? "Cancelar" : "Salir", 320, y, 112, 42, (s, e) => { DialogResult = DialogResult.Cancel; Close(); });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;

            if (!autorizacion)
            {
                if (UsuarioRepo.Validar("admin", "admin") != null)
                {
                    Controls.Add(new Label { Text = "Primera vez: usuario admin, contraseña admin", Left = 0, Top = 244, Width = ClientSize.Width, Height = 22, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9.5f), ForeColor = Tema.Humo, BackColor = Color.Transparent });
                }
                var marca = new Label { Text = "alanmendez.dev", Left = 0, Top = 278, Width = ClientSize.Width, Height = 22, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(110, 125, 145), BackColor = Color.Transparent };
                marca.Cursor = Cursors.Hand;
                marca.Click += (s, e) => { try { System.Diagnostics.Process.Start("https://alanmendez.dev"); } catch { } };
                Controls.Add(marca);
            }
            Shown += (s, e) => _usuario.Focus();
        }

        void Entrar()
        {
            if (_usuario.Text.Trim() == "") { _usuario.Focus(); return; }
            if (_clave.Text == "") { _clave.Focus(); return; }   // Enter en el usuario pasa a la contraseña
            var u = UsuarioRepo.Validar(_usuario.Text, _clave.Text);
            if (u == null)
            {
                Ui.Aviso("Usuario o contraseña incorrectos.");
                _clave.SelectAll(); _clave.Focus();
                return;
            }
            UsuarioValido = u;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
