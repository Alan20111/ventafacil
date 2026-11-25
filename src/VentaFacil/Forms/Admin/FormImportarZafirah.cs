using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>
    /// Asistente para traer los productos de otro programa (Zafirah) a VentaFácil.
    ///   1) Origen: la base de datos de Zafirah (SQL Server, solo lectura) o un archivo de Excel/CSV.
    ///   2) Qué columna es cada dato: se propone sola y se puede cambiar (con un ejemplo de cada una).
    ///   3) Vista previa con avisos. Solo al pulsar «Importar» se guarda algo, y antes se hace un respaldo.
    /// No adivina en silencio: lo que no se sabe con certeza (por ejemplo, desde qué cantidad aplica el precio de
    /// mayoreo) se deja para que la persona lo elija viendo los datos.
    /// </summary>
    public class FormImportarZafirah : Dialogo
    {
        // ---- origen: SQL Server ----
        readonly ComboBox _servidor, _base, _tabla;
        readonly RadioButton _authWin, _authSql;
        readonly TextBox _usuario, _clave;
        readonly Label _estadoSql, _estadoArchivo, _origenActual;

        // ---- origen: respaldo .BK ----
        readonly TabControl _pestanas;
        readonly TabPage _pagSql, _pagBk;
        readonly Label _servidorBk, _estadoBk;
        readonly Button _restaurarBk, _comandosBk;
        string _rutaBk, _rutaUsada, _copiaTmp, _bdTmp;
        InfoRespaldo _infoBk;

        // ---- perfiles ----
        readonly ComboBox _perfiles;
        List<PerfilImportacion> _listaPerfiles = new List<PerfilImportacion>();
        PerfilImportacion Perfil { get { return _perfiles.SelectedItem as PerfilImportacion ?? PerfilImportacion.Zafirah(); } }
        static string CarpetaPerfiles { get { return Path.Combine(Db.Carpeta, "perfiles"); } }

        // ---- mapeo ----
        readonly Dictionary<CampoZ, ComboBox> _combos = new Dictionary<CampoZ, ComboBox>();
        readonly Dictionary<CampoZ, Label> _ejemplos = new Dictionary<CampoZ, Label>();
        readonly CheckBox _sumar;
        readonly ComboBox _almacen;

        // ---- opciones, vista previa ----
        readonly CheckBox _actualizar, _actualizarExistencias, _sobrescribir;
        readonly DataGridView _vista;
        readonly Label _resumen;
        readonly TextBox _avisos;
        readonly Button _importar;
        readonly Timer _debounce = new Timer { Interval = 250 };

        DataTable _origen;
        Dictionary<string, string> _proveedores;
        ResultadoZ _resultado;
        bool _cargando;

        public bool Importado { get; private set; }

        public FormImportarZafirah() : base("Importar productos de Zafirah", 1240, 696)
        {
            var tip = new ToolTip { AutoPopDelay = 9000 };

            Controls.Add(new PictureBox { Image = Iconos.Importar(40, Tema.Azul), SizeMode = PictureBoxSizeMode.AutoSize, Left = 16, Top = 6 });
            Controls.Add(Ui.Etiqueta("Importar productos de Zafirah", 64, 8, Tema.Mediana, Tema.Azul));
            _origenActual = Ui.Etiqueta("1. Elija de dónde traer los productos", 66, 38, Tema.Negrita, Tema.Humo);
            Controls.Add(_origenActual);

            // =================== 1. ORIGEN ===================
            _pestanas = new TabControl { Left = 16, Top = 66, Width = 560, Height = 290, Font = Tema.Negrita };
            var pagSql = new TabPage("Base de Zafirah") { BackColor = Tema.Crema, Font = Tema.Normal };
            _pagSql = pagSql;
            _pagBk = new TabPage("Respaldo .BK") { BackColor = Tema.Crema, Font = Tema.Normal };
            var pagArchivo = new TabPage("Excel o CSV") { BackColor = Tema.Crema, Font = Tema.Normal };
            _pestanas.TabPages.Add(pagSql);
            _pestanas.TabPages.Add(_pagBk);
            _pestanas.TabPages.Add(pagArchivo);
            _pestanas.SelectedIndexChanged += (s, e) => ActualizarServidorBk();
            Controls.Add(_pestanas);

            pagSql.Controls.Add(Ui.Etiqueta("Servidor:", 8, 16));
            _servidor = new ComboBox { Left = 96, Top = 12, Width = 300, DropDownStyle = ComboBoxStyle.DropDown, Font = Tema.Normal };
            foreach (var s in ZafirahSql.ServidoresProbables()) _servidor.Items.Add(s);
            _servidor.Text = "(local)";
            pagSql.Controls.Add(_servidor);
            tip.SetToolTip(_servidor, "Donde está SQL Server. Si Zafirah está en esta misma computadora, casi siempre es «(local)» o «.\\SQLEXPRESS».");

            _authWin = new RadioButton { Text = "Usuario de Windows", Left = 8, Top = 50, Width = 220, Checked = true };
            _authSql = new RadioButton { Text = "Usuario y contraseña de SQL", Left = 240, Top = 50, Width = 300 };
            pagSql.Controls.Add(_authWin);
            pagSql.Controls.Add(_authSql);
            pagSql.Controls.Add(Ui.Etiqueta("Usuario:", 8, 88));
            _usuario = Ui.Caja(96, 84, 150, Tema.Normal);
            pagSql.Controls.Add(_usuario);
            pagSql.Controls.Add(Ui.Etiqueta("Contraseña:", 262, 88));
            _clave = Ui.Caja(366, 84, 170, Tema.Normal);
            _clave.UseSystemPasswordChar = true;
            pagSql.Controls.Add(_clave);
            _usuario.Enabled = _clave.Enabled = false;
            _authSql.CheckedChanged += (s, e) => { _usuario.Enabled = _clave.Enabled = _authSql.Checked; };

            pagSql.Controls.Add(Ui.Boton("Conectar", 8, 120, 130, 38, (s, e) => Conectar()));
            _estadoSql = new Label { Left = 150, Top = 122, Width = 390, Height = 40, Font = Tema.Negrita, ForeColor = Tema.Humo, BackColor = Color.Transparent, Text = "Solo se LEE; no se cambia nada en Zafirah." };
            pagSql.Controls.Add(_estadoSql);

            pagSql.Controls.Add(Ui.Etiqueta("Base:", 8, 176));
            _base = new ComboBox { Left = 96, Top = 172, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            _base.SelectedIndexChanged += (s, e) => { if (!_cargando) LlenarTablas(); };
            pagSql.Controls.Add(_base);
            pagSql.Controls.Add(Ui.Etiqueta("Tabla:", 8, 212));
            _tabla = new ComboBox { Left = 96, Top = 208, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            pagSql.Controls.Add(_tabla);
            pagSql.Controls.Add(Ui.Boton("Leer tabla", 408, 202, 130, 40, (s, e) => LeerTabla()));
            tip.SetToolTip(_tabla, "En Zafirah, los productos están en «ZFRADMARTICULOS».");

            // ---- pestaña del respaldo .BK ----
            _servidorBk = new Label { Left = 8, Top = 8, Width = 424, Height = 28, Font = Tema.Negrita, ForeColor = Tema.Azul, BackColor = Color.Transparent, AutoEllipsis = true };
            _pagBk.Controls.Add(_servidorBk);
            _pagBk.Controls.Add(Ui.Boton("Cambiar", 440, 4, 98, 32, (s, e) => _pestanas.SelectedTab = _pagSql));
            _pagBk.Controls.Add(Ui.Boton("Elegir respaldo…", 8, 44, 190, 42, (s, e) => ElegirRespaldo()));
            _restaurarBk = Ui.Boton("Restaurar y leer", 206, 44, 190, 42, (s, e) => RestaurarBk());
            Tema.Pastilla(_restaurarBk, true);
            _restaurarBk.Enabled = false;
            _pagBk.Controls.Add(_restaurarBk);
            _comandosBk = Ui.Boton("Comandos…", 404, 44, 134, 42, (s, e) => { using (var f = new FormComandosBk(ComandosTexto())) f.ShowDialog(this); });
            _comandosBk.Enabled = false;
            _pagBk.Controls.Add(_comandosBk);
            _estadoBk = new Label
            {
                Left = 8, Top = 96, Width = 530, Height = 160, Font = Tema.Normal, ForeColor = Tema.Humo, BackColor = Color.Transparent,
                Text = "Para leer un respaldo de SQL Server (.BK, .BAK, .1BK, .2BK, .3BK) se restaura en una base TEMPORAL y se lee de ahí; al terminar se borra. La base real de Zafirah no se toca.\n\nNecesita SQL Server (Express sirve) en esta computadora y espacio libre parecido al tamaño del respaldo."
            };
            _pagBk.Controls.Add(_estadoBk);
            ActualizarServidorBk();

            pagArchivo.Controls.Add(Ui.Boton("Elegir archivo…", 8, 16, 220, 42, (s, e) => ElegirArchivo()));
            _estadoArchivo = new Label
            {
                Left = 8, Top = 72, Width = 530, Height = 180, Font = Tema.Normal, ForeColor = Tema.Humo, BackColor = Color.Transparent,
                Text = "Sirve para un reporte de productos exportado de Zafirah a Excel, o para cualquier lista en Excel (.xlsx) o CSV.\n\n" +
                       "La primera fila con títulos se toma como encabezado; lo de arriba (título del reporte, renglones vacíos) se ignora."
            };
            pagArchivo.Controls.Add(_estadoArchivo);

            // =================== 2. MAPEO ===================
            Controls.Add(Ui.Etiqueta("2. ¿Qué columna es cada dato?", 16, 364, Tema.Negrita, Tema.Azul));
            _perfiles = new ComboBox { Left = 262, Top = 359, Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            _perfiles.SelectedIndexChanged += (s, e) => { if (!_cargando) CambioPerfil(); };
            Controls.Add(_perfiles);
            tip.SetToolTip(_perfiles, "Un perfil dice dónde buscar cada dato en un programa. Puede guardar el suyo con «Guardar…» o editar los archivos de la carpeta de perfiles.");
            var bGuardar = Ui.Boton("Guardar…", 408, 357, 86, 28, (s, e) => GuardarPerfil());
            bGuardar.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            Controls.Add(bGuardar);
            var bCarpeta = Ui.Boton("Carpeta", 500, 357, 76, 28, (s, e) => AbrirCarpetaPerfiles());
            bCarpeta.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            Controls.Add(bCarpeta);
            tip.SetToolTip(bGuardar, "Guarda las columnas que eligió como un perfil reutilizable (para otro programa u otra base).");
            tip.SetToolTip(bCarpeta, "Abre la carpeta de perfiles (.json). Ahí puede editar o copiar perfiles con el Bloc de notas.");
            RecargarPerfiles(null);
            var panelMapeo = new Panel { Left = 16, Top = 388, Width = 560, Height = 262, AutoScroll = true, BackColor = Tema.Crema, BorderStyle = BorderStyle.FixedSingle };
            Controls.Add(panelMapeo);
            int y = 6;
            foreach (var campo in ZafirahMapeo.Campos)
            {
                var campoActual = campo.Campo;
                var etiqueta = new Label
                {
                    Text = campo.Nombre + (campo.Obligatorio ? " *" : ""), Left = 6, Top = y + 6, Width = 192, Height = 24,
                    Font = campo.Obligatorio ? Tema.Negrita : Tema.Normal, BackColor = Color.Transparent, AutoEllipsis = true
                };
                var combo = new ComboBox { Left = 200, Top = y + 2, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
                combo.Items.Add("(ninguna)");
                combo.SelectedIndex = 0;
                var ejemplo = new Label { Left = 408, Top = y + 6, Width = 126, Height = 24, ForeColor = Tema.Humo, BackColor = Color.Transparent, AutoEllipsis = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
                tip.SetToolTip(etiqueta, campo.Ayuda);
                tip.SetToolTip(combo, campo.Ayuda);
                combo.SelectedIndexChanged += (s, e) =>
                {
                    if (_cargando) return;
                    ActualizarEjemplo(campoActual);
                    if (campoActual == CampoZ.Almacen) ActualizarAlmacenes();
                    Programar();
                };
                _combos[campoActual] = combo;
                _ejemplos[campoActual] = ejemplo;
                panelMapeo.Controls.Add(etiqueta);
                panelMapeo.Controls.Add(combo);
                panelMapeo.Controls.Add(ejemplo);
                y += 34;
            }
            _sumar = new CheckBox { Text = "Sumar las existencias de todos los almacenes", Left = 6, Top = y + 4, Width = 440, Checked = true, Visible = false };
            _almacen = new ComboBox { Left = 6, Top = y + 34, Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal, Visible = false };
            _sumar.CheckedChanged += (s, e) => { _almacen.Visible = _sumar.Visible && !_sumar.Checked; Programar(); };
            _almacen.SelectedIndexChanged += (s, e) => { if (!_cargando) Programar(); };
            panelMapeo.Controls.Add(_sumar);
            panelMapeo.Controls.Add(_almacen);

            // =================== 3. VISTA PREVIA ===================
            Controls.Add(Ui.Etiqueta("3. Vista previa  (nada se guarda hasta pulsar «Importar»)", 596, 12, Tema.Negrita, Tema.Azul));
            _resumen = Ui.Etiqueta("", 596, 40, Tema.Mediana, Tema.Texto);
            Controls.Add(_resumen);

            _vista = new DataGridView { Left = 596, Top = 76, Width = 628, Height = 320 };
            Ui.EstiloGrid(_vista);
            _vista.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _vista.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _vista.Columns.Add(Ui.Columna("codigo", "Código", 9));
            _vista.Columns.Add(Ui.Columna("barras", "Barras", 13));
            _vista.Columns.Add(Ui.Columna("producto", "Producto", 27));
            _vista.Columns.Add(Ui.Columna("grupo", "Grupo", 12));
            _vista.Columns.Add(Ui.Columna("costo", "Costo", 9, true));
            _vista.Columns.Add(Ui.Columna("menudeo", "Menud.", 10, true));
            _vista.Columns.Add(Ui.Columna("medio", "Medio", 11, true));
            _vista.Columns.Add(Ui.Columna("mayoreo", "Mayoreo", 11, true));
            _vista.Columns.Add(Ui.Columna("existencia", "Stock", 8, true));
            Controls.Add(_vista);

            _avisos = new TextBox
            {
                Left = 596, Top = 404, Width = 628, Height = 112, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold), BackColor = Tema.Crema, ForeColor = Tema.Rojo
            };
            Controls.Add(_avisos);

            _actualizar = new CheckBox { Text = "Si el producto ya existe, actualizar su precio y su costo", Left = 596, Top = 526, Width = 628, Checked = true };
            _actualizarExistencias = new CheckBox { Text = "… y también ajustar su existencia a la que dice Zafirah", Left = 596, Top = 554, Width = 628 };
            _sobrescribir = new CheckBox { Text = "Si un código ya es de OTRO producto, reemplazarlo (no recomendado)", Left = 596, Top = 582, Width = 628 };
            Controls.Add(_actualizar);
            Controls.Add(_actualizarExistencias);
            Controls.Add(_sobrescribir);
            Controls.Add(Ui.Etiqueta("Antes de importar se guarda un respaldo automático. Zafirah no se modifica: solo se lee.", 596, 616, null, Tema.Humo));

            _importar = Ui.Boton("Importar", 820, 640, 250, 48, (s, e) => Importar());
            Tema.Pastilla(_importar, true);
            _importar.Enabled = false;
            Controls.Add(_importar);
            Controls.Add(Ui.Boton("Cerrar (Esc)", 1080, 640, 144, 48, (s, e) => Close()));

            _debounce.Tick += (s, e) => { _debounce.Stop(); Actualizar(); };
            Shown += (s, e) => Actualizar();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel) LimpiarTemporales();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _debounce.Dispose();
            base.Dispose(disposing);
        }

        void Programar() { _debounce.Stop(); _debounce.Start(); }

        // ===================================================================
        //  SQL Server
        // ===================================================================
        string Usuario() { return _authSql.Checked ? _usuario.Text.Trim() : ""; }
        string Clave() { return _authSql.Checked ? _clave.Text : ""; }
        string Cadena() { return ZafirahSql.Cadena(_servidor.Text.Trim(), _base.SelectedItem as string, Usuario(), Clave()); }

        void Conectar()
        {
            string servidor = _servidor.Text.Trim();
            if (servidor == "") { Ui.Aviso("Escriba el nombre del servidor de SQL Server."); return; }
            if (_authSql.Checked && Usuario() == "") { Ui.Aviso("Escriba el usuario de SQL, o elija «Usuario de Windows»."); return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                var bases = ZafirahSql.Bases(servidor, Usuario(), Clave());
                _cargando = true;
                _base.Items.Clear();
                _tabla.Items.Clear();
                foreach (var b in bases) _base.Items.Add(b);
                _cargando = false;
                if (bases.Count == 0)
                {
                    _estadoSql.ForeColor = Tema.Rojo;
                    _estadoSql.Text = "Conectó, pero este usuario no ve ninguna base de datos.";
                    return;
                }
                _estadoSql.ForeColor = Tema.Verde;
                _estadoSql.Text = "✔ Conectado. Elija la base y la tabla de productos.";
                int idx = Perfil.Base == "" ? -1 : bases.FindIndex(b => b.Equals(Perfil.Base, StringComparison.OrdinalIgnoreCase));
                _base.SelectedIndex = idx >= 0 ? idx : 0; // dispara LlenarTablas
            }
            catch (Exception ex)
            {
                _cargando = false;
                _estadoSql.ForeColor = Tema.Rojo;
                _estadoSql.Text = "No se pudo conectar.";
                Log.Error(ex, "Importar Zafirah: conectar");
                Ui.Aviso(ZafirahSql.Explicar(ex), "Conectar a SQL Server");
            }
            finally { Cursor = Cursors.Default; }
        }

        void LlenarTablas()
        {
            string bd = _base.SelectedItem as string;
            if (bd == null) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                var tablas = ZafirahSql.Tablas(Cadena());
                _tabla.Items.Clear();
                foreach (var t in tablas) _tabla.Items.Add(t);
                int idx = -1, mejor = 0;
                for (int i = 0; i < tablas.Count; i++) { int pt = Perfil.PuntajeTabla(tablas[i].Nombre); if (pt > mejor) { mejor = pt; idx = i; } }
                if (_tabla.Items.Count > 0) _tabla.SelectedIndex = idx >= 0 ? idx : 0;
                _estadoSql.ForeColor = idx >= 0 ? Tema.Verde : Tema.Rojo;
                _estadoSql.Text = (idx >= 0 ? "✔ " : "") + tablas.Count + " tablas en «" + bd + "»." + (idx >= 0 ? " Pulse «Leer tabla»." : " No encontré la de artículos: elíjala.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Importar Zafirah: tablas");
                Ui.Aviso(ZafirahSql.Explicar(ex), "Leer las tablas");
            }
            finally { Cursor = Cursors.Default; }
        }

        void LeerTabla()
        {
            var t = _tabla.SelectedItem as TablaSql;
            if (t == null) { Ui.Aviso("Primero conéctese y elija la tabla de productos."); return; }
            try
            {
                Cursor = Cursors.WaitCursor;
                var dt = ZafirahSql.Leer(Cadena(), t);
                if (dt.Rows.Count == 0) { Ui.Aviso("La tabla «" + t.Nombre + "» está vacía."); return; }

                // El nombre del proveedor suele estar en otra tabla: se intenta leer, y si no se puede se sigue sin él
                _proveedores = null;
                try
                {
                    var tp = _tabla.Items.OfType<TablaSql>().FirstOrDefault(x => x.Tipo == "Tabla" && Perfil.TablaProveedores != "" && x.Nombre.Equals(Perfil.TablaProveedores, StringComparison.OrdinalIgnoreCase));
                    if (tp != null)
                    {
                        var d = ZafirahMapeo.DiccionarioProveedores(ZafirahSql.Leer(Cadena(), tp, 20000));
                        if (d.Count > 0) _proveedores = d;
                    }
                }
                catch (Exception ex2) { Log.Error(ex2, "Importar Zafirah: proveedores"); }

                UsarOrigen(dt, "Origen: Zafirah · " + (_base.SelectedItem as string) + " · " + t.Nombre + "  (" + dt.Rows.Count + " filas)");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Importar Zafirah: leer tabla");
                Ui.Aviso(ZafirahSql.Explicar(ex), "Leer la tabla");
            }
            finally { Cursor = Cursors.Default; }
        }

        // ===================================================================
        //  Perfiles
        // ===================================================================
        void RecargarPerfiles(string seleccionar)
        {
            List<string> problemas;
            _listaPerfiles = PerfilImportacion.Cargar(CarpetaPerfiles, out problemas);
            bool antes = _cargando;
            _cargando = true;
            _perfiles.Items.Clear();
            foreach (var p in _listaPerfiles) _perfiles.Items.Add(p);
            int idx = 0;
            if (seleccionar != null) for (int i = 0; i < _listaPerfiles.Count; i++) if (_listaPerfiles[i].Nombre == seleccionar) idx = i;
            _perfiles.SelectedIndex = idx;
            _cargando = antes;
            if (problemas.Count > 0) Ui.Aviso("Algunos perfiles no se pudieron leer y se omitieron:\n\n" + string.Join("\n", problemas.ToArray()), "Perfiles de importación");
        }

        void CambioPerfil()
        {
            if (_origen != null) UsarOrigen(_origen, _origenActual.Text);   // vuelve a proponer las columnas con el perfil nuevo
        }

        void GuardarPerfil()
        {
            if (_origen == null) { Ui.Aviso("Primero lea una tabla o un archivo y deje cada dato en su columna; eso es lo que se guarda."); return; }
            var mapeo = LeerMapeo();
            if (mapeo.Count == 0) { Ui.Aviso("No hay ninguna columna elegida para guardar."); return; }
            string nombre = Ui.PedirTexto("Guardar perfil", "Nombre del perfil (por ejemplo, el del otro programa):", "");
            if (string.IsNullOrWhiteSpace(nombre)) return;
            nombre = nombre.Trim();
            var p = PerfilImportacion.DesdeMapeo(nombre, mapeo, !_sumar.Visible || _sumar.Checked);
            var t = _tabla.SelectedItem as TablaSql;
            if (_pestanas.SelectedTab == _pagSql && t != null) { p.Tablas.Add(t.Nombre); }
            var actual = Perfil;
            if (actual.UnidadesGranel != null) p.UnidadesGranel = new List<string>(actual.UnidadesGranel);
            if (actual.TablaProveedores != "") p.TablaProveedores = actual.TablaProveedores;
            try
            {
                Directory.CreateDirectory(CarpetaPerfiles);
                string ruta = Path.Combine(CarpetaPerfiles, PerfilImportacion.ArchivoPara(nombre));
                if (File.Exists(ruta) && !Ui.Confirmar("Ya hay un perfil con ese nombre. ¿Reemplazarlo?")) return;
                File.WriteAllText(ruta, p.ComoJson(), new UTF8Encoding(true));
                RecargarPerfiles(nombre);
                Ui.Info("Perfil «" + nombre + "» guardado.\n\nAparece en la lista y se puede editar con el Bloc de notas:\n" + ruta, "Perfil guardado");
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo guardar el perfil"); }
        }

        void AbrirCarpetaPerfiles()
        {
            try
            {
                Directory.CreateDirectory(CarpetaPerfiles);
                string ejemplo = Path.Combine(CarpetaPerfiles, "_ejemplo (copielo y quite el guion bajo).json.txt");
                if (!File.Exists(ejemplo)) File.WriteAllText(ejemplo, PerfilImportacion.Zafirah().ComoJson(), new UTF8Encoding(true));
                Process.Start("explorer.exe", "\"" + CarpetaPerfiles + "\"");
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo abrir la carpeta de perfiles"); }
        }

        // ===================================================================
        //  Respaldo .BK
        // ===================================================================
        string CadenaMaster() { return ZafirahSql.Cadena(_servidor.Text.Trim(), "master", Usuario(), Clave()); }

        void ActualizarServidorBk()
        {
            _servidorBk.Text = "Servidor: " + (_servidor.Text.Trim() == "" ? "(sin escribir)" : _servidor.Text.Trim()) + " · " + (_authSql.Checked ? "usuario SQL «" + Usuario() + "»" : "usuario de Windows");
        }

        string ComandosTexto()
        {
            if (_rutaUsada == null) return "";
            string tmp = "vf_import_tmp_XXXXXXXX";
            var sb = new StringBuilder();
            sb.AppendLine("-- 1) Revisar el respaldo (qué base trae, de qué versión, qué archivos):");
            sb.AppendLine(ZafirahBk.ComandosRevisar(_rutaUsada));
            if (_infoBk != null)
            {
                sb.AppendLine();
                sb.AppendLine("-- 2) Restaurarlo en una base temporal (VentaFácil lo hace solo; los archivos van a la carpeta de datos de SQL Server):");
                sb.AppendLine(ZafirahBk.ComandoRestaurar(tmp, _rutaUsada, _infoBk.Posicion, _infoBk.Archivos, "<carpeta de datos de SQL Server>"));
                sb.AppendLine("GO");
                sb.AppendLine();
                sb.AppendLine("-- 3) Al terminar se borra la temporal:");
                sb.AppendLine("DROP DATABASE [" + tmp + "]");
                sb.AppendLine("GO");
            }
            return sb.ToString();
        }

        void ElegirRespaldo()
        {
            if (_servidor.Text.Trim() == "") { Ui.Aviso("Escriba el nombre del servidor de SQL Server en la pestaña «Base de Zafirah»."); _pestanas.SelectedTab = _pagSql; return; }
            if (_authSql.Checked && Usuario() == "") { Ui.Aviso("Escriba el usuario de SQL, o elija «Usuario de Windows»."); _pestanas.SelectedTab = _pagSql; return; }
            using (var d = new OpenFileDialog { Filter = "Respaldos de SQL Server (*.bk;*.bak;*.1bk;*.2bk;*.3bk;*.4bk)|*.bk;*.bak;*.1bk;*.2bk;*.3bk;*.4bk|Todos los archivos (*.*)|*.*", Title = "Elija el respaldo de la base de Zafirah" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                LimpiarTemporales();
                _rutaBk = _rutaUsada = d.FileName;
                _infoBk = null;
                _restaurarBk.Enabled = _comandosBk.Enabled = false;
                Inspeccionar(true);
            }
        }

        void Inspeccionar(bool puedeCopiar)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                _estadoBk.ForeColor = Tema.Humo;
                _estadoBk.Text = "Revisando el respaldo…";
                Application.DoEvents();
                _infoBk = ZafirahBk.Inspeccionar(CadenaMaster(), _rutaUsada);
                _estadoBk.ForeColor = _infoBk.Imposible ? Tema.Rojo : Tema.Verde;
                _estadoBk.Text = (_infoBk.Imposible ? "✘ " : "✔ ") + Path.GetFileName(_rutaBk) + "\n" + _infoBk.Resumen() +
                    (_infoBk.Aviso != "" ? "\n\n" + _infoBk.Aviso : "\n\nPulse «Restaurar y leer»: se restaura en una base temporal (tarda según el tamaño) y se borra al terminar.");
                _restaurarBk.Enabled = true;
                _comandosBk.Enabled = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Importar BK: revisar");
                _estadoBk.ForeColor = Tema.Rojo;
                _estadoBk.Text = "No se pudo leer el respaldo.\n\n" + ZafirahBk.Explicar(ex);
                _comandosBk.Enabled = _rutaUsada != null;
                var sql = ex as SqlException;
                if (puedeCopiar && sql != null && sql.Number == 3201) { Cursor = Cursors.Default; if (OfrecerCopia()) Inspeccionar(false); }
            }
            finally { Cursor = Cursors.Default; }
        }

        /// <summary>El servicio de SQL Server a veces no puede leer el Escritorio o una USB: se ofrece copiar el respaldo a C:\Users\Public.</summary>
        bool OfrecerCopia()
        {
            string destino;
            try
            {
                long tam = new FileInfo(_rutaBk).Length;
                destino = Path.Combine(Environment.GetEnvironmentVariable("PUBLIC") ?? @"C:\Users\Public", @"VentaFacil\importar");
                if (!Ui.Confirmar("SQL Server no tiene permiso para leer el respaldo en esa carpeta.\n\n¿Copiarlo a «" + destino + "» (" + Fmt.Tamano(tam) + ") y probar desde ahí?\nLa copia se borra al terminar.")) return false;
                Directory.CreateDirectory(destino);
                destino = Path.Combine(destino, "respaldo_" + Guid.NewGuid().ToString("N").Substring(0, 8) + Path.GetExtension(_rutaBk));
            }
            catch (Exception ex) { Ui.Error(ex, "No se pudo preparar la copia"); return false; }
            string origen = _rutaBk;
            var err = FormProgresoBk.Ejecutar(this, "Copiando el respaldo…", prog => CopiarConAvance(origen, destino, prog));
            if (err != null) { try { if (File.Exists(destino)) File.Delete(destino); } catch (Exception) { } Ui.Error(err.Message, "No se pudo copiar el respaldo"); return false; }
            _copiaTmp = destino;
            _rutaUsada = destino;
            return true;
        }

        static void CopiarConAvance(string origen, string destino, Action<int, string> prog)
        {
            using (var a = new FileStream(origen, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var b = new FileStream(destino, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buf = new byte[1 << 20];
                long total = a.Length, hecho = 0; int n, ultimo = -1;
                while ((n = a.Read(buf, 0, buf.Length)) > 0)
                {
                    b.Write(buf, 0, n); hecho += n;
                    int p = total > 0 ? (int)(hecho * 100 / total) : 0;
                    if (p != ultimo) { ultimo = p; prog(p, "Copiando…"); }
                }
            }
        }

        void RestaurarBk()
        {
            if (_infoBk == null || _rutaUsada == null) return;
            if (_infoBk.Imposible && !Ui.Confirmar(_infoBk.Aviso + "\n\nSQL Server lo rechazará casi seguro. ¿Intentar de todos modos?")) return;
            if (!Ui.Confirmar("Se va a restaurar el respaldo en una base TEMPORAL de SQL Server para leer los productos.\n\n• Puede tardar varios minutos (el respaldo pesa " + Fmt.Tamano(_infoBk.TamanoBytes) + ").\n• Necesita espacio libre parecido en el disco de SQL Server.\n• La base real de Zafirah NO se toca, y la temporal se borra al terminar.\n\n¿Continuar?")) return;
            QuitarTemporal();
            string cadena = CadenaMaster(), tmp = ZafirahBk.NombreTemporal(), ruta = _rutaUsada;
            var info = _infoBk;
            var err = FormProgresoBk.Ejecutar(this, "Restaurando el respaldo…", prog =>
            {
                try { foreach (var h in ZafirahBk.Huerfanas(cadena)) { try { ZafirahBk.Eliminar(cadena, h); } catch (Exception) { } } } catch (Exception) { }
                ZafirahBk.Restaurar(cadena, ruta, info, tmp, prog);
            });
            if (err != null)
            {
                try { ZafirahBk.Eliminar(cadena, tmp); } catch (Exception) { }   // si quedó a medias
                var sql = err as SqlException;
                if (sql != null && sql.Number == 3201 && _copiaTmp == null)
                {
                    if (OfrecerCopia()) { _estadoBk.Text = "Copiado. Pulse «Restaurar y leer» otra vez."; Inspeccionar(false); }
                    return;
                }
                _estadoBk.ForeColor = Tema.Rojo;
                _estadoBk.Text = "No se pudo restaurar el respaldo.\n\n" + ZafirahBk.Explicar(err);
                Ui.Aviso(ZafirahBk.Explicar(err), "Restaurar el respaldo");
                return;
            }
            _bdTmp = tmp;
            _cargando = true;
            _base.Items.Clear();
            _tabla.Items.Clear();
            _base.Items.Add(tmp);
            _cargando = false;
            _estadoBk.ForeColor = Tema.Verde;
            _estadoBk.Text = "✔ Respaldo restaurado en una base temporal. Leyendo los productos…";
            _base.SelectedIndex = 0;                 // llena las tablas y elige la de artículos según el perfil
            if (_tabla.SelectedItem != null) LeerTabla();
            else _estadoBk.Text = "Respaldo restaurado, pero no encontré la tabla de productos: elíjala en la pestaña «Base de Zafirah» y pulse «Leer tabla».";
            if (_origen != null) _estadoBk.Text = "✔ Respaldo leído. Revise a la derecha las columnas y la vista previa.\nLa base temporal se borra al cerrar esta ventana.";
        }

        /// <summary>Borra la base temporal (si hay) sin importar si el servidor contesta.</summary>
        void QuitarTemporal()
        {
            if (_bdTmp == null) return;
            string tmp = _bdTmp; _bdTmp = null;
            try { Cursor = Cursors.WaitCursor; ZafirahBk.Eliminar(CadenaMaster(), tmp); }
            catch (Exception ex) { Log.Error(ex, "Importar BK: borrar temporal " + tmp); }
            finally { Cursor = Cursors.Default; }
        }

        void LimpiarTemporales()
        {
            QuitarTemporal();
            if (_copiaTmp != null)
            {
                try { if (File.Exists(_copiaTmp)) File.Delete(_copiaTmp); } catch (Exception ex) { Log.Error(ex, "Importar BK: borrar copia"); }
                _copiaTmp = null;
            }
        }

        // ===================================================================
        //  Archivo Excel / CSV
        // ===================================================================
        void ElegirArchivo()
        {
            using (var d = new OpenFileDialog { Filter = "Excel o CSV (*.xlsx;*.csv;*.txt)|*.xlsx;*.csv;*.txt", Title = "Elija el archivo con la lista de productos" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Cursor = Cursors.WaitCursor;
                    var dt = TablaArchivo.Leer(d.FileName);
                    _proveedores = null;
                    _estadoArchivo.ForeColor = Tema.Verde;
                    _estadoArchivo.Text = "✔ " + Path.GetFileName(d.FileName) + ": " + dt.Rows.Count + " filas y " + dt.Columns.Count + " columnas.\n\nRevise a la derecha que cada dato esté en la columna correcta.";
                    UsarOrigen(dt, "Origen: archivo " + Path.GetFileName(d.FileName) + "  (" + dt.Rows.Count + " filas)");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Importar Zafirah: archivo");
                    Ui.Error(ex, "No se pudo leer el archivo");
                }
                finally { Cursor = Cursors.Default; }
            }
        }

        // ===================================================================
        //  Mapeo
        // ===================================================================
        void UsarOrigen(DataTable dt, string descripcion)
        {
            _origen = dt;
            _origenActual.Text = descripcion;
            _cargando = true;
            var nombres = dt.Columns.Cast<DataColumn>().Select(c => (object)c.ColumnName).ToArray();
            var sugerido = Perfil.Sugerir(dt);
            _sumar.Checked = Perfil.SumarAlmacenes;
            foreach (var campo in ZafirahMapeo.Campos)
            {
                var cb = _combos[campo.Campo];
                cb.Items.Clear();
                cb.Items.Add("(ninguna)");
                cb.Items.AddRange(nombres);
                cb.SelectedIndex = 0;
                string col;
                if (sugerido.TryGetValue(campo.Campo, out col)) cb.SelectedItem = col;
            }
            _cargando = false;
            foreach (var campo in ZafirahMapeo.Campos) ActualizarEjemplo(campo.Campo);
            ActualizarAlmacenes();
            Programar();
        }

        Dictionary<CampoZ, string> LeerMapeo()
        {
            var m = new Dictionary<CampoZ, string>();
            foreach (var kv in _combos) if (kv.Value.SelectedIndex > 0) m[kv.Key] = (string)kv.Value.SelectedItem;
            return m;
        }

        void ActualizarEjemplo(CampoZ campo)
        {
            var cb = _combos[campo];
            var lbl = _ejemplos[campo];
            lbl.Text = "";
            if (_origen == null || cb.SelectedIndex <= 0) return;
            string col = (string)cb.SelectedItem;
            foreach (DataRow r in _origen.Rows.Cast<DataRow>().Take(80))
            {
                string v = ZafirahMapeo.Texto(r[col]);
                if (v != "") { lbl.Text = v; return; }
            }
        }

        /// <summary>Si la tabla trae una fila por almacén: sumar todos o elegir uno.</summary>
        void ActualizarAlmacenes()
        {
            bool hay = _origen != null && _combos[CampoZ.Almacen].SelectedIndex > 0;
            _sumar.Visible = hay;
            _almacen.Visible = hay && !_sumar.Checked;
            if (!hay) return;
            string col = (string)_combos[CampoZ.Almacen].SelectedItem;
            var valores = _origen.Rows.Cast<DataRow>().Select(r => ZafirahMapeo.Texto(r[col])).Where(v => v != "").Distinct().Take(60).ToList();
            _cargando = true;
            _almacen.Items.Clear();
            foreach (var v in valores) _almacen.Items.Add(v);
            if (_almacen.Items.Count > 0) _almacen.SelectedIndex = 0;
            _cargando = false;
        }

        // ===================================================================
        //  Vista previa e importación
        // ===================================================================
        void Actualizar()
        {
            _vista.Rows.Clear();
            if (_origen == null)
            {
                _resultado = null;
                _resumen.Text = "Primero lea la tabla de productos (o elija un archivo).";
                _avisos.Text = "";
                _importar.Enabled = false;
                _importar.Text = "Importar";
                return;
            }
            try
            {
                var op = Perfil.AplicarA(new OpcionesZ { SumarAlmacenes = !_sumar.Visible || _sumar.Checked, AlmacenElegido = _almacen.Text, ProveedoresPorClave = _proveedores });
                var res = ZafirahMapeo.Transformar(_origen, LeerMapeo(), op);
                _resultado = res;
                int n = 0;
                foreach (var p in res.Productos)
                {
                    if (n++ >= 300) break;
                    _vista.Rows.Add(p.Codigo, p.Barras, p.Descripcion + (p.Granel ? "" : "  (pz)"), p.Grupo, Fmt.Dinero(p.Costo), Fmt.Dinero(p.Menudeo),
                        p.Medio > 0 ? Fmt.Dinero(p.Medio) + " ≥" + Fmt.Dec(p.DesdeMedio) : "",
                        p.Mayoreo > 0 ? Fmt.Dinero(p.Mayoreo) + " ≥" + Fmt.Dec(p.DesdeMayoreo) : "",
                        p.Existencia.HasValue ? Fmt.Dec(p.Existencia.Value) : "");
                }
                _resumen.ForeColor = res.Valido ? Tema.Texto : Tema.Rojo;
                _resumen.Text = res.Valido
                    ? res.Productos.Count + " productos listos  (de " + res.Filas + " filas)" + (res.Productos.Count > 300 ? " · se muestran 300" : "")
                    : "Todavía no hay nada que importar";
                _avisos.Text = string.Join("\r\n", res.Avisos.Select(a => "• " + a).ToArray());
                _importar.Enabled = res.Valido;
                _importar.Text = res.Valido ? "Importar " + res.Productos.Count + " productos" : "Importar";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Importar Zafirah: vista previa");
                _resultado = null;
                _avisos.Text = "No se pudo preparar la vista previa: " + ex.Message;
                _importar.Enabled = false;
            }
        }

        void Importar()
        {
            if (_resultado == null || !_resultado.Valido) return;
            int n = _resultado.Productos.Count;
            if (!Ui.Confirmar("Se van a importar " + n + " productos a VentaFácil.\n\nAntes se guarda un respaldo automático, y Zafirah no se modifica.\n\n¿Continuar?")) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                var r = ImportarProductosZ.Aplicar(_resultado,
                    new OpcionesAplicarZ { ActualizarExistentes = _actualizar.Checked, ActualizarExistencias = _actualizarExistencias.Checked, SobrescribirConflictos = _sobrescribir.Checked },
                    Sesion.Usuario != null ? Sesion.Usuario.Id : 0);
                Cursor = Cursors.Default;

                var sb = new StringBuilder();
                sb.AppendLine("Productos nuevos: " + r.Nuevos);
                sb.AppendLine("Actualizados: " + r.Actualizados);
                if (r.Omitidos > 0) sb.AppendLine("Omitidos (ya existían): " + r.Omitidos);
                if (r.BarrasRepetidos > 0) sb.AppendLine("Códigos de barras que ya eran de otro producto (no se cambiaron): " + r.BarrasRepetidos);
                if (r.Conflictos.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("CÓDIGOS QUE YA ERAN DE OTRO PRODUCTO (no se tocaron): " + r.Conflictos.Count);
                    foreach (var c in r.Conflictos.Take(8)) sb.AppendLine("  " + c);
                    if (r.Conflictos.Count > 8) sb.AppendLine("  …");
                }
                if (r.Errores.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Con error: " + r.Errores.Count);
                    foreach (var e in r.Errores.Take(8)) sb.AppendLine("  " + e);
                }
                sb.AppendLine();
                sb.AppendLine("Los productos nuevos conservan el código de Zafirah (el que se teclea en caja).");
                sb.AppendLine("Respaldo hecho antes de importar:");
                sb.AppendLine(r.Respaldo);
                Ui.Info(sb.ToString(), "Importación terminada");

                Importado = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                Ui.Error(ex, "No se pudo importar. No se cambió nada (hay un respaldo hecho antes de empezar).");
            }
        }
    }
}
