using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Usuarios del sistema: administradores y cajeros.</summary>
    public class SeccionUsuarios : UserControl
    {
        readonly DataGridView _grid;

        public SeccionUsuarios()
        {
            BackColor = Tema.Crema;
            _grid = AdminUtil.Grid();
            _grid.Columns.Add(Ui.Columna("usuario", "Usuario", 25));
            _grid.Columns.Add(Ui.Columna("nombre", "Nombre", 45));
            _grid.Columns.Add(Ui.Columna("rol", "Perfil", 20));
            _grid.Columns.Add(Ui.Columna("activo", "Activo", 10));
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Modificar(); };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Modificar(); } };
            Controls.Add(_grid);

            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("Nuevo", (s, e) => Nuevo(), 100, true));
            barra.Controls.Add(AdminUtil.BotonBarra("Modificar", (s, e) => Modificar(), 100));
            barra.Controls.Add(AdminUtil.BotonBarra("Mi contraseña", (s, e) => MiClave()));
            Controls.Add(barra);
            Load += (s, e) => { Cargar(); _grid.Focus(); };
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.N)) { Nuevo(); return true; }
            if (keyData == (Keys.Control | Keys.M)) { Modificar(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void Cargar()
        {
            try
            {
                _grid.Rows.Clear();
                foreach (var u in UsuarioRepo.Todos())
                {
                    int i = _grid.Rows.Add(u.Login, u.Nombre, Permisos.PerfilDe(u, Config.Actual.CajeroPuedeCancelar), u.Activo ? "Sí" : "No");
                    _grid.Rows[i].Tag = u;
                    if (!u.Activo) _grid.Rows[i].DefaultCellStyle.ForeColor = Tema.Humo;
                }
            }
            catch (Exception ex) { Ui.Error(ex, "Cargar usuarios"); }
        }

        void Nuevo()
        {
            using (var f = new FormUsuario(null)) if (f.ShowDialog(this) == DialogResult.OK) Cargar();
        }

        void Modificar()
        {
            if (_grid.CurrentRow == null) return;
            var u = _grid.CurrentRow.Tag as Usuario;
            if (u == null) return;
            using (var f = new FormUsuario(u)) if (f.ShowDialog(this) == DialogResult.OK) Cargar();
        }

        void MiClave()
        {
            var yo = Sesion.Usuario;
            if (yo == null) return;
            var actual = Ui.PedirTexto("Cambiar contraseña", "Contraseña actual:", "", true);
            if (actual == null) return;
            if (UsuarioRepo.Validar(yo.Login, actual) == null) { Ui.Aviso("La contraseña actual no es correcta."); return; }
            var n1 = Ui.PedirTexto("Cambiar contraseña", "Contraseña nueva:", "", true);
            if (string.IsNullOrEmpty(n1)) return;
            var n2 = Ui.PedirTexto("Cambiar contraseña", "Repita la contraseña nueva:", "", true);
            if (n1 != n2) { Ui.Aviso("Las contraseñas no coinciden."); return; }
            try
            {
                var u = UsuarioRepo.Obtener(yo.Id);
                UsuarioRepo.Guardar(u, n1);
                Ui.Listo("Contraseña cambiada");
            }
            catch (Exception ex) { Ui.Error(ex, "Cambiar contraseña"); }
        }
    }

    public class FormUsuario : Dialogo
    {
        readonly Usuario _u;
        readonly TextBox _login, _nombre, _clave, _clave2;
        readonly ComboBox _rol;
        readonly CheckBox _activo;
        readonly System.Collections.Generic.Dictionary<string, CheckBox> _casillas = new System.Collections.Generic.Dictionary<string, CheckBox>();

        public FormUsuario(Usuario u) : base(u == null ? "Nuevo usuario" : "Modificar usuario", 760, 476)
        {
            _u = u ?? new Usuario();
            int x = 188, y = 16;
            Controls.Add(Ui.Etiqueta("Usuario (para entrar):", 16, y + 4, Tema.Negrita));
            _login = Ui.Caja(x, y, 250); _login.Text = _u.Login; Controls.Add(_login);
            y += 40;
            Controls.Add(Ui.Etiqueta("Nombre completo:", 16, y + 4));
            _nombre = Ui.Caja(x, y, 250); _nombre.Text = _u.Nombre; Controls.Add(_nombre);
            y += 40;
            Controls.Add(Ui.Etiqueta("Perfil:", 16, y + 4));
            _rol = new ComboBox { Left = x, Top = y, Width = 250, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            foreach (var p in new[] { Permisos.PerfilCajero, Permisos.PerfilEncargado, Permisos.PerfilPersonalizado, Permisos.PerfilAdmin }) _rol.Items.Add(p);
            Controls.Add(_rol);
            y += 40;
            Controls.Add(Ui.Etiqueta("Contraseña:", 16, y + 4));
            _clave = Ui.Caja(x, y, 250); _clave.UseSystemPasswordChar = true; Controls.Add(_clave);
            y += 40;
            Controls.Add(Ui.Etiqueta("Repetir contraseña:", 16, y + 4));
            _clave2 = Ui.Caja(x, y, 250); _clave2.UseSystemPasswordChar = true; Controls.Add(_clave2);
            y += 36;
            if (_u.Id != 0) Controls.Add(Ui.Etiqueta("(deje vacío para no cambiarla)", x, y - 6, null, Tema.Humo));
            _activo = new CheckBox { Text = "Activo", Left = 16, Top = y + 12, AutoSize = true, Checked = _u.Id == 0 || _u.Activo };
            Controls.Add(_activo);

            // ---- casillas: el perfil las marca y después se pueden ajustar (el administrador puede todo, siempre) ----
            var grupo = new GroupBox { Text = "Puede usar", Left = 456, Top = 10, Width = 288, Height = 396, Font = Tema.Negrita, BackColor = Color.Transparent };
            int py = 26;
            foreach (var p in Permisos.Todos)
            {
                var c = new CheckBox { Text = p.Nombre, Left = 14, Top = py, Width = 264, Height = 26, Font = Tema.Normal };
                _casillas[p.Clave] = c;
                c.CheckedChanged += (s, e) => AlCambiarCasilla();
                grupo.Controls.Add(c);
                py += 28;
            }
            Controls.Add(grupo);
            _nota = new Label { Left = 16, Top = 318, Width = 426, Height = 92, Font = Tema.Normal, ForeColor = Tema.Humo, BackColor = Color.Transparent };
            Controls.Add(_nota);
            var inicial = new Usuario { Rol = "CAJERO", Permisos = _u.Permisos };
            Marcar(_u.EsAdmin ? Permisos.DePerfil(Permisos.PerfilAdmin, CPC) : Permisos.Todos.Where(p => Permisos.Tiene(inicial, p.Clave, CPC)).Select(p => p.Clave).ToList());
            _rol.SelectedItem = Permisos.PerfilDe(_u, CPC);
            MostrarPerfil();
            _rol.SelectedIndexChanged += (s, e) => { if (!_llenando) { var l = Permisos.DePerfil(Perfil, CPC); if (l != null) Marcar(l); MostrarPerfil(); } };

            var ok = Ui.Boton("Guardar", 486, ClientSize.Height - 50, 130, 38, (s, e) => Guardar());
            var no = Ui.Boton("Cancelar", 622, ClientSize.Height - 50, 120, 38, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            Shown += (s, e) => _login.Focus();
        }

        bool _llenando;
        Label _nota;
        static bool CPC { get { return Config.Actual.CajeroPuedeCancelar; } }
        string Perfil { get { return Convert.ToString(_rol.SelectedItem); } }
        System.Collections.Generic.List<string> Marcadas() { return _casillas.Where(k => k.Value.Checked).Select(k => k.Key).ToList(); }

        void Marcar(System.Collections.Generic.ICollection<string> claves)
        {
            _llenando = true;
            try { foreach (var kv in _casillas) kv.Value.Checked = claves.Contains(kv.Key); }
            finally { _llenando = false; }
        }

        /// <summary>Al tocar una casilla, el perfil pasa solo al que corresponda (o a Personalizado).</summary>
        void AlCambiarCasilla()
        {
            if (_llenando || Perfil == Permisos.PerfilAdmin) return;
            _llenando = true;
            try { _rol.SelectedItem = Permisos.PerfilDe(Marcadas(), CPC); }
            finally { _llenando = false; }
            MostrarPerfil();
        }

        void MostrarPerfil()
        {
            bool admin = Perfil == Permisos.PerfilAdmin;
            foreach (var kv in _casillas) kv.Value.Enabled = !admin;
            _nota.Text = admin ? "Puede todo, siempre. Es el único que entra a Usuarios, Respaldos y el enlace de Envíos."
                : Perfil == Permisos.PerfilEncargado ? "Todos los módulos y acciones, menos Usuarios, Respaldos y el enlace de Envíos."
                : Perfil == Permisos.PerfilCajero ? "Lo básico para cobrar. Puede marcarle más casillas."
                : "Solo lo que esté marcado.\nCobrar y su propio corte siempre puede.";
        }

        void Guardar()
        {
            if (_login.Text.Trim() == "") { Ui.Aviso("Escriba el nombre de usuario."); _login.Focus(); return; }
            if (_nombre.Text.Trim() == "") { Ui.Aviso("Escriba el nombre completo."); _nombre.Focus(); return; }
            if (_u.Id == 0 && _clave.Text == "") { Ui.Aviso("Escriba una contraseña."); _clave.Focus(); return; }
            if (_clave.Text != _clave2.Text) { Ui.Aviso("Las contraseñas no coinciden."); _clave2.Focus(); return; }
            string rol = Perfil == Permisos.PerfilAdmin ? "ADMIN" : "CAJERO";
            try
            {
                bool eraAdminActivo = _u.Id != 0 && _u.EsAdmin && _u.Activo;
                if (eraAdminActivo && (rol != "ADMIN" || !_activo.Checked) && UsuarioRepo.AdminsActivos() <= 1)
                {
                    Ui.Aviso("Este es el único administrador activo.\nNo se puede desactivar ni cambiar a otro perfil.");
                    return;
                }
                _u.Login = _login.Text.Trim();
                _u.Nombre = _nombre.Text.Trim();
                _u.Rol = rol;
                if (rol != "ADMIN") _u.Permisos = Permisos.Guardar(Marcadas(), CPC);
                _u.Activo = _activo.Checked;
                UsuarioRepo.Guardar(_u, _clave.Text == "" ? null : _clave.Text);
                if (Sesion.Usuario != null && Sesion.Usuario.Id == _u.Id) { Sesion.Usuario.Nombre = _u.Nombre; Sesion.Usuario.Login = _u.Login; Sesion.Usuario.Rol = _u.Rol; Sesion.Usuario.Permisos = _u.Permisos; Sesion.Usuario.Activo = _u.Activo; }
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Ui.Error(ex, "Guardar usuario (¿el usuario ya existe?)"); }
        }
    }
}
