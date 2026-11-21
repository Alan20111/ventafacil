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
            ClientSize = new Size(420, 290);
            BackColor = Color.White;
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

            Controls.Add(Ui.Etiqueta("Usuario:", 24, y + 6, Tema.Mediana));
            _usuario = Ui.Caja(140, y, 250, Tema.Captura);
            Controls.Add(_usuario);
            y += 52;
            Controls.Add(Ui.Etiqueta("Contraseña:", 24, y + 6, Tema.Mediana));
            _clave = Ui.Caja(140, y, 250, Tema.Captura);
            _clave.UseSystemPasswordChar = true;
            Controls.Add(_clave);
            y += 60;

            var ok = Ui.Boton("Entrar (Enter)", 140, y, 130, 40, (s, e) => Entrar());
            var no = Ui.Boton(autorizacion ? "Cancelar" : "Salir", 280, y, 110, 40, (s, e) => { DialogResult = DialogResult.Cancel; Close(); });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;

            _usuario.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _clave.Focus(); } };
            if (!autorizacion && UsuarioRepo.Validar("admin", "admin") != null)
                Controls.Add(Ui.Etiqueta("Primera vez: usuario admin, contraseña admin", 24, ClientSize.Height - 26, null, Color.Gray));
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
