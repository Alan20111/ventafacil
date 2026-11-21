using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Ventana de administración (se abre desde el punto de venta).</summary>
    public class FormAdmin : Form
    {
        readonly Panel _contenido;
        readonly List<Button> _menu = new List<Button>();
        readonly Dictionary<string, Func<Control>> _secciones = new Dictionary<string, Func<Control>>();
        Control _actual;

        public FormAdmin()
        {
            Text = "VentaFácil - Administración";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1024, 700);
            Size = new Size(1100, 740);
            WindowState = FormWindowState.Maximized;
            BackColor = Color.White;
            Font = Tema.Normal;
            KeyPreview = true;
            ShowInTaskbar = false;

            _contenido = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            Controls.Add(_contenido);

            var menu = new FlowLayoutPanel
            {
                Dock = DockStyle.Left, Width = 200, BackColor = Tema.AzulFondo, FlowDirection = FlowDirection.TopDown,
                WrapContents = false, AutoScroll = true, Padding = new Padding(8, 10, 8, 8)
            };
            Controls.Add(menu);

            var titulo = new PanelTitulo { Titulo = "Administración", Subtitulo = (Sesion.Usuario != null ? Sesion.Usuario.Nombre : "") + "   ·   Esc para regresar a ventas" };
            Controls.Add(titulo);

            Agregar(menu, "Productos", () => new SeccionProductos());
            Agregar(menu, "Departamentos", () => new SeccionDepartamentos());
            Agregar(menu, "Inventario", () => new SeccionInventario());
            Agregar(menu, "Reportes", () => new SeccionReportes());
            Agregar(menu, "Usuarios", () => new SeccionUsuarios());
            Agregar(menu, "Configuración", () => new SeccionConfig());
            Agregar(menu, "Respaldos", () => new SeccionRespaldos());

            var salir = new Button
            {
                Text = "Regresar a ventas (Esc)", Width = 180, Height = 54, Font = Tema.Negrita, BackColor = Color.White, ForeColor = Tema.Rojo,
                Margin = new Padding(0, 24, 0, 0)
            };
            salir.Click += (s, e) => Close();
            menu.Controls.Add(salir);

            Shown += (s, e) => Abrir("Productos");
        }

        void Agregar(FlowLayoutPanel menu, string nombre, Func<Control> crear)
        {
            _secciones[nombre] = crear;
            var b = new Button
            {
                Text = nombre, Width = 180, Height = 54, Font = Tema.Mediana, BackColor = Color.White, ForeColor = Tema.Azul,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 0, 6), TextAlign = ContentAlignment.MiddleLeft, Tag = nombre
            };
            b.FlatAppearance.BorderColor = Tema.AzulClaro;
            b.Click += (s, e) => Abrir(nombre);
            _menu.Add(b);
            menu.Controls.Add(b);
        }

        void Abrir(string nombre)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _contenido.SuspendLayout();
                if (_actual != null) { _contenido.Controls.Remove(_actual); _actual.Dispose(); }
                _actual = _secciones[nombre]();
                _actual.Dock = DockStyle.Fill;
                _contenido.Controls.Add(_actual);
                _contenido.ResumeLayout();
                foreach (var b in _menu)
                {
                    bool activo = (string)b.Tag == nombre;
                    b.BackColor = activo ? Tema.Azul : Color.White;
                    b.ForeColor = activo ? Color.White : Tema.Azul;
                }
                _actual.Focus();
                _actual.SelectNextControl(_actual, true, true, true, true);
            }
            catch (Exception ex) { Ui.Error(ex, "Abrir " + nombre); }
            finally { Cursor = Cursors.Default; }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); return; }
            base.OnKeyDown(e);
        }
    }
}
