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
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = autorizacion ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;
            MaximizeBox = false; MinimizeBox = !autorizacion;
            ShowInTaskbar = !autorizacion;
            ClientSize = new Size(420, autorizacion ? 270 : 310);
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
            _usuario = Ui.Caja(140, y, 250, Tema.Mediana);
            Controls.Add(_usuario);
            y += 48;
            Controls.Add(Ui.Etiqueta("Contraseña:", 24, y + 4, Tema.Mediana));
            _clave = Ui.Caja(140, y, 250, Tema.Mediana);
            _clave.UseSystemPasswordChar = true;
            Controls.Add(_clave);
            y += 56;

            var ok = Ui.Boton("Entrar (Enter)", 140, y, 130, 40, (s, e) => Entrar(), true);
            var no = Ui.Boton(autorizacion ? "Cancelar" : "Salir", 280, y, 110, 40, (s, e) => { DialogResult = DialogResult.Cancel; Close(); });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;

            _usuario.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _clave.Focus(); } };
            if (!autorizacion)
            {
                if (UsuarioRepo.Validar("admin", "admin") != null)
                {
                    var pista = Ui.Etiqueta("Primera vez: usuario admin, contraseña admin", 0, 246, new Font("Segoe UI", 9.5f), Tema.Humo);
                    pista.Left = (ClientSize.Width - pista.Width) / 2;
                    Controls.Add(pista);
                }
                var marca = Ui.Etiqueta("alanmendez.dev", 0, 280, new Font("Segoe UI", 8.5f), Color.FromArgb(135, 150, 170));
                marca.Left = (ClientSize.Width - marca.Width) / 2;
                marca.Cursor = Cursors.Hand;
                marca.Click += (s, e) => { try { System.Diagnostics.Process.Start("https://alanmendez.dev"); } catch { } };
                Controls.Add(marca);
            }
            Shown += (s, e) => _usuario.Focus();
        }

        void Entrar()
        {
            if (_usuario.Text.Trim() == "") { _usuario.Focus(); return; }
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
