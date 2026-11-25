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
            _grid.Columns.Add(Ui.Columna("rol", "Tipo", 20));
            _grid.Columns.Add(Ui.Columna("activo", "Activo", 10));
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Modificar(); };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Modificar(); } };
            Controls.Add(_grid);

            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("Nuevo (Ctrl+N)", (s, e) => Nuevo(), 140));
            barra.Controls.Add(AdminUtil.BotonBarra("Modificar (Enter)", (s, e) => Modificar(), 155));
            barra.Controls.Add(AdminUtil.BotonBarra("Mi contraseña", (s, e) => MiClave(), 125));
            Controls.Add(barra);
            Controls.Add(AdminUtil.TituloSeccion("Usuarios"));
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
                    int i = _grid.Rows.Add(u.Login, u.Nombre, u.EsAdmin ? "Administrador" : u.Permisos == "" ? "Cajero" : "Cajero (permisos propios)", u.Activo ? "Sí" : "No");
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
                Ui.Info("Contraseña cambiada.");
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

        public FormUsuario(Usuario u) : base(u == null ? "Nuevo usuario" : "Modificar usuario", 760, 440)
        {
            _u = u ?? new Usuario();
            int x = 170, y = 16;
            Controls.Add(Ui.Etiqueta("Usuario (para entrar):", 16, y + 4, Tema.Negrita));
            _login = Ui.Caja(x, y, 260); _login.Text = _u.Login; Controls.Add(_login);
            y += 40;
            Controls.Add(Ui.Etiqueta("Nombre completo:", 16, y + 4));
            _nombre = Ui.Caja(x, y, 260); _nombre.Text = _u.Nombre; Controls.Add(_nombre);
            y += 40;
            Controls.Add(Ui.Etiqueta("Tipo:", 16, y + 4));
            _rol = new ComboBox { Left = x, Top = y, Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            _rol.Items.Add("Cajero"); _rol.Items.Add("Administrador");
            _rol.SelectedIndex = _u.EsAdmin ? 1 : 0;
            Controls.Add(_rol);
            y += 40;
            Controls.Add(Ui.Etiqueta("Contraseña:", 16, y + 4));
            _clave = Ui.Caja(x, y, 260); _clave.UseSystemPasswordChar = true; Controls.Add(_clave);
            y += 40;
            Controls.Add(Ui.Etiqueta("Repetir contraseña:", 16, y + 4));
            _clave2 = Ui.Caja(x, y, 260); _clave2.UseSystemPasswordChar = true; Controls.Add(_clave2);
            y += 36;
            if (_u.Id != 0) Controls.Add(Ui.Etiqueta("(deje vacío para no cambiarla)", x, y - 6, null, Tema.Humo));
            _activo = new CheckBox { Text = "Activo", Left = 16, Top = y + 12, AutoSize = true, Checked = _u.Id == 0 || _u.Activo };
            Controls.Add(_activo);

            // ---- permisos (solo para cajeros; el administrador puede todo) ----
            var grupo = new GroupBox { Text = "Puede usar", Left = 456, Top = 10, Width = 288, Height = 360, Font = Tema.Negrita, BackColor = Color.Transparent };
            int py = 26;
            foreach (var p in Permisos.Todos)
            {
                var c = new CheckBox { Text = p.Nombre, Left = 14, Top = py, Width = 264, Height = 26, Font = Tema.Normal };
                _casillas[p.Clave] = c;
                grupo.Controls.Add(c);
                py += 28;
            }
            Controls.Add(grupo);
            Controls.Add(Ui.Etiqueta("Cobrar y su propio corte siempre puede.", 16, 336, null, Tema.Humo));
            _rol.SelectedIndexChanged += (s, e) => MostrarPermisos();
            MostrarPermisos();

            var ok = Ui.Boton("Guardar", 486, ClientSize.Height - 50, 130, 38, (s, e) => Guardar());
            var no = Ui.Boton("Cancelar", 622, ClientSize.Height - 50, 120, 38, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(ok); Controls.Add(no);
            AcceptButton = ok; CancelButton = no;
            Shown += (s, e) => _login.Focus();
        }

        /// <summary>Administrador: todo marcado y fijo. Cajero: lo que tiene hoy (o lo de fábrica), editable.</summary>
        void MostrarPermisos()
        {
            bool admin = _rol.SelectedIndex == 1;
            var cajero = new Usuario { Rol = "CAJERO", Permisos = _u.Permisos };
            foreach (var kv in _casillas) { kv.Value.Checked = admin || Permisos.Tiene(cajero, kv.Key); kv.Value.Enabled = !admin; }
        }

        void Guardar()
        {
            if (_login.Text.Trim() == "") { Ui.Aviso("Escriba el nombre de usuario."); _login.Focus(); return; }
            if (_nombre.Text.Trim() == "") { Ui.Aviso("Escriba el nombre completo."); _nombre.Focus(); return; }
            if (_u.Id == 0 && _clave.Text == "") { Ui.Aviso("Escriba una contraseña."); _clave.Focus(); return; }
            if (_clave.Text != _clave2.Text) { Ui.Aviso("Las contraseñas no coinciden."); _clave2.Focus(); return; }
            string rol = _rol.SelectedIndex == 1 ? "ADMIN" : "CAJERO";
            try
            {
                bool eraAdminActivo = _u.Id != 0 && _u.EsAdmin && _u.Activo;
                if (eraAdminActivo && (rol != "ADMIN" || !_activo.Checked) && UsuarioRepo.AdminsActivos() <= 1)
                {
                    Ui.Aviso("Este es el único administrador activo.\nNo se puede desactivar ni cambiar a cajero.");
                    return;
                }
                _u.Login = _login.Text.Trim();
                _u.Nombre = _nombre.Text.Trim();
                _u.Rol = rol;
                if (rol != "ADMIN") _u.Permisos = Permisos.Guardar(_casillas.Where(k => k.Value.Checked).Select(k => k.Key).ToList(), Config.Actual.CajeroPuedeCancelar);
                _u.Activo = _activo.Checked;
                UsuarioRepo.Guardar(_u, _clave.Text == "" ? null : _clave.Text);
                if (Sesion.Usuario != null && Sesion.Usuario.Id == _u.Id) { Sesion.Usuario.Nombre = _u.Nombre; Sesion.Usuario.Login = _u.Login; Sesion.Usuario.Rol = _u.Rol; Sesion.Usuario.Permisos = _u.Permisos; Sesion.Usuario.Activo = _u.Activo; }
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Ui.Error(ex, "Guardar usuario (¿el usuario ya existe?)"); }
        }
    }
}
