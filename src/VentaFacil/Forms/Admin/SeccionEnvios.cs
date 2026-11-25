using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    static class EnvioUtil
    {
        /// <summary>Memorias USB conectadas.</summary>
        public static List<string> Usbs()
        {
            var l = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
                try { if (d.DriveType == DriveType.Removable && d.IsReady) l.Add(d.RootDirectory.FullName); } catch { /* lector vacío o sin permiso */ }
            return l;
        }

        /// <summary>Siempre pregunta la carpeta, empezando por la primera USB conectada.</summary>
        public static string PedirCarpeta(IWin32Window dueno, string descripcion)
        {
            using (var d = new FolderBrowserDialog { Description = descripcion, ShowNewFolderButton = true })
            {
                var u = Usbs();
                if (u.Count > 0) d.SelectedPath = u[0];
                return d.ShowDialog(dueno) == DialogResult.OK ? d.SelectedPath : null;
            }
        }

        /// <summary>Los archivos con esa extensión en las USB (raíz y carpetas de primer nivel), del más nuevo al más viejo.</summary>
        public static List<string> BuscarEnUsb(string patron)
        {
            var l = new List<string>();
            foreach (var u in Usbs())
                try
                {
                    l.AddRange(Directory.GetFiles(u, patron));
                    foreach (var c in Directory.GetDirectories(u)) try { l.AddRange(Directory.GetFiles(c, patron)); } catch { /* carpeta sin permiso */ }
                }
                catch { /* se sacó la USB */ }
            return l.OrderByDescending(f => { try { return File.GetLastWriteTime(f); } catch { return DateTime.MinValue; } }).ToList();
        }

        public static string PedirArchivo(IWin32Window dueno, string titulo, string filtro)
        {
            using (var d = new OpenFileDialog { Title = titulo, Filter = filtro })
            {
                var u = Usbs();
                if (u.Count > 0) d.InitialDirectory = u[0];
                return d.ShowDialog(dueno) == DialogResult.OK ? d.FileName : null;
            }
        }

        public static DataGridView GridLineas()
        {
            var g = AdminUtil.Grid();
            g.Columns.Add(Ui.Columna("t", "Cambio", 12));
            g.Columns.Add(Ui.Columna("c", "Código", 12));
            g.Columns.Add(Ui.Columna("p", "Producto", 32));
            g.Columns.Add(Ui.Columna("d", "Dato", 16));
            g.Columns.Add(Ui.Columna("a", "Antes", 11, true));
            g.Columns.Add(Ui.Columna("n", "Ahora", 11, true));
            g.Columns.Add(Ui.Columna("o", "Nota", 28));
            return g;
        }

        public static void Llenar(DataGridView g, IEnumerable<LineaEnvio> lineas)
        {
            g.Rows.Clear();
            // Los problemas primero: es lo que hay que ver
            foreach (var l in lineas.OrderBy(x => x.Tipo == "Problema" ? 0 : 1))
            {
                bool mercancia = l.Tipo == "Entrada" || l.Tipo == "Salida";
                string ahora = mercancia && l.Despues == "" ? (l.Cantidad > 0 ? "+" : "") + Fmt.Dec(l.Cantidad) : Envios.Bonito(l.Campo, l.Despues);
                string nota = mercancia && l.Despues != "" ? (l.Cantidad > 0 ? "+" : "") + Fmt.Dec(l.Cantidad) + (l.Nota == "" ? "" : "  ·  " + l.Nota) : l.Nota;
                int i = g.Rows.Add(l.Tipo, l.Codigo, l.Producto, Envios.TituloCampo(l.Campo), Envios.Bonito(l.Campo, l.Antes), ahora, nota);
                if (l.Tipo == "Problema" || l.Tipo == "Baja") g.Rows[i].DefaultCellStyle.ForeColor = Tema.Rojo;
                else if (l.Nota.StartsWith("En la tienda")) g.Rows[i].DefaultCellStyle.ForeColor = Tema.Azul;
            }
        }
    }

    // ==================================================================== CAJA
    /// <summary>Recibir un envío: elegir el archivo, ver qué cambia y aplicar. Nada se cambia hasta pulsar Aplicar.</summary>
    public class FormRecibirEnvio : Dialogo
    {
        readonly ComboBox _archivos;
        readonly Label _resumen, _datos;
        readonly TextBox _alertas;
        readonly DataGridView _grid;
        readonly Button _aplicar;
        readonly Panel _cuerpo;
        ResultadoEnvio _r;
        string _ruta;

        class Archivo { public string Ruta; public override string ToString() { return Path.GetFileName(Ruta) + "    (" + Path.GetPathRoot(Ruta) + ")"; } }

        public FormRecibirEnvio(string ruta = null) : base("Recibir envío de la oficina", 1000, 640)
        {
            Controls.Add(Ui.Etiqueta("Envío:", 16, 18, Tema.Negrita));
            _archivos = new ComboBox { Left = 80, Top = 14, Width = 560, DropDownStyle = ComboBoxStyle.DropDownList, Font = Tema.Normal };
            _archivos.SelectedIndexChanged += (s, e) => { var a = _archivos.SelectedItem as Archivo; if (a != null) Revisar(a.Ruta); };
            Controls.Add(_archivos);
            Controls.Add(Ui.Boton("Buscar en USB", 650, 10, 150, 36, (s, e) => BuscarUsb()));
            Controls.Add(Ui.Boton("Otro archivo…", 808, 10, 176, 36, (s, e) => Otro()));
            _datos = Ui.Etiqueta("", 16, 56, null, Tema.Humo); Controls.Add(_datos);
            _resumen = new Label { Left = 16, Top = 80, Width = 968, Height = 50, Font = Tema.Mediana, ForeColor = Tema.Azul, BackColor = Color.Transparent }; Controls.Add(_resumen);
            _cuerpo = new Panel { Left = 16, Top = 134, Width = 968, Height = 340 };
            _grid = EnvioUtil.GridLineas(); _grid.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _cuerpo.Controls.Add(_grid);
            Controls.Add(_cuerpo);
            _alertas = new TextBox { Left = 16, Top = 482, Width = 968, Height = 84, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(255, 247, 214), ForeColor = Tema.Texto, Font = Tema.Normal, BorderStyle = BorderStyle.FixedSingle, Visible = false };
            Controls.Add(_alertas);
            _aplicar = Ui.Boton("Aplicar", 680, 582, 180, 44, (s, e) => Aplicar(), true);
            _aplicar.Enabled = false;
            Controls.Add(_aplicar);
            var no = Ui.Boton("Cerrar (Esc)", 868, 582, 116, 44, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(no);
            CancelButton = no;
            Shown += (s, e) => { if (ruta == null) BuscarUsb(); else Abrir(ruta); };
        }

        void Abrir(string f)
        {
            _archivos.Items.Insert(0, new Archivo { Ruta = f });
            _archivos.SelectedIndex = 0;
        }

        void BuscarUsb()
        {
            var l = EnvioUtil.BuscarEnUsb("*.vfenvio");
            _archivos.Items.Clear();
            foreach (var f in l) _archivos.Items.Add(new Archivo { Ruta = f });
            if (l.Count > 0) _archivos.SelectedIndex = 0;
            else Vacio("No encontré envíos en ninguna USB. Conéctela y presione «Buscar en USB», o elija «Otro archivo».");
        }

        void Otro()
        {
            string f = EnvioUtil.PedirArchivo(this, "Elija el envío de la oficina", "Envío de VentaFácil (*.vfenvio)|*.vfenvio");
            if (f != null) Abrir(f);
        }

        void Vacio(string texto, bool error = false)
        {
            _r = null; _ruta = null; _grid.Rows.Clear(); _datos.Text = ""; _alertas.Visible = false; _aplicar.Enabled = false;
            _resumen.ForeColor = error ? Tema.Rojo : Tema.Humo; _resumen.Text = texto;
        }

        void Revisar(string ruta)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                var r = Envios.Revisar(ruta);
                _r = r; _ruta = ruta;
                _datos.Text = "Envío " + r.Num + "  ·  " + r.Oficina + "  ·  lo hizo " + r.Usuario + " el " + r.Fecha;
                EnvioUtil.Llenar(_grid, r.Lineas);
                _aplicar.Enabled = !r.YaAplicado;
                _resumen.ForeColor = r.YaAplicado ? Tema.Humo : r.Problemas > 0 ? Tema.Rojo : Tema.Azul;
                _resumen.Text = r.YaAplicado ? "Este envío ya se había aplicado. No hay nada que hacer." : r.Resumen;
                _alertas.Visible = r.Alertas.Count > 0;
                if (r.YaAplicado) DejarAcuse(ruta);
                _alertas.Text = "REVISE ANTES DE APLICAR:\r\n" + string.Join("\r\n", r.Alertas.ToArray());
            }
            catch (Exception ex)
            {
                if (!(ex is EnvioInvalidoException) && !(ex is InvalidOperationException)) Log.Error(ex, "Revisar envío");
                Vacio(ex.Message, true);
                if (ex.Message.StartsWith("Falta un envío")) DejarAcuse(ruta);   // si falta un envío, la oficina lo sabrá por el acuse
            }
            finally { Cursor = Cursors.Default; }
        }

        static string DejarAcuse(string ruta)
        {
            try { if (Envios.Dato("oficina_id") == "") return null; return Envios.EscribirAcuse(Path.GetDirectoryName(ruta)); }
            catch (Exception ex) { Log.Error(ex, "Acuse del envío"); return null; }
        }

        void Aplicar()
        {
            if (_r == null || _ruta == null) return;
            if (_r.PrimerEnvio)
            {   // la primera vez se queda reconocida la oficina: lo confirma un administrador
                if (Ui.Autorizar(Permisos.SoloAdmin, "Primer envío: un administrador confirma la oficina") == null) return;
                if (!Ui.Confirmar("Es el primer envío de la oficina «" + _r.Oficina + "».\n\nDesde ahora esta caja solo aceptará envíos de esa oficina.\n¿Es correcto?")) return;
            }
            else if (Config.Actual.EnviosPedirClave && Ui.Autorizar(Permisos.SoloAdmin, "Aplicar envío: clave de un administrador") == null) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                _aplicar.Enabled = false;
                var r = Envios.Aplicar(_ruta, Sesion.Usuario.Id, Sesion.Usuario.Nombre);
                string acuse = DejarAcuse(_ruta);
                Cursor = Cursors.Default;
                Ui.Info(r.YaAplicado ? "Este envío ya se había aplicado." : "Envío " + r.Num + " aplicado:\n\n" + r.Resumen +
                    (acuse != null ? "\n\nSe dejó el acuse en la USB. Regrésela a la oficina." : "\n\nNo se pudo dejar el acuse en la USB (¿está protegida o se sacó?). El envío SÍ quedó aplicado."), "Envío");
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex, "No se aplicó nada del envío"); Revisar(_ruta); }
        }
    }

    /// <summary>Almacén → Envíos, en la CAJA: historial, detalle y deshacer el último.</summary>
    public class SeccionEnviosTienda : UserControl
    {
        readonly DataGridView _grid, _det;
        List<EnvioRecibido> _lista = new List<EnvioRecibido>();

        public SeccionEnviosTienda()
        {
            BackColor = Tema.Crema;
            _det = EnvioUtil.GridLineas();
            _grid = AdminUtil.Grid();
            _grid.Columns.Add(Ui.Columna("n", "Envío", 8, true));
            _grid.Columns.Add(Ui.Columna("f", "Aplicado", 18));
            _grid.Columns.Add(Ui.Columna("u", "Por", 16));
            _grid.Columns.Add(Ui.Columna("r", "Qué trajo", 46));
            _grid.Columns.Add(Ui.Columna("e", "Estado", 12));
            _grid.Dock = DockStyle.Top; _grid.Height = 220;
            _grid.SelectionChanged += (s, e) => { var x = Elegido(); if (x != null) EnvioUtil.Llenar(_det, Envios.Detalle(x.Orden)); else _det.Rows.Clear(); };
            Controls.Add(_det);
            Controls.Add(_grid);
            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("Recibir envío", (s, e) => { using (var f = new FormRecibirEnvio()) f.ShowDialog(FindForm()); Cargar(); }, 140, true));
            var des = AdminUtil.BotonBarra("Deshacer el último", (s, e) => Deshacer(), 165);
            Tema.BotonPeligro(des);
            barra.Controls.Add(des);
            barra.Controls.Add(AdminUtil.BotonBarra("Guardar acuse…", (s, e) => Acuse(), 150));
            Controls.Add(barra);
            Controls.Add(AdminUtil.TituloSeccion("Envíos recibidos de la oficina"));
            Load += (s, e) => Cargar();
        }

        EnvioRecibido Elegido() { return _grid.CurrentRow == null ? null : _grid.CurrentRow.Tag as EnvioRecibido; }

        void Cargar()
        {
            try
            {
                _lista = Envios.Recibidos();
                _det.Rows.Clear(); _grid.Rows.Clear();
                foreach (var x in _lista)
                {
                    int i = _grid.Rows.Add(x.Num, x.Fecha, x.Usuario, x.Resumen, x.Estado == "APLICADO" ? "Aplicado" : "Deshecho");
                    _grid.Rows[i].Tag = x;
                    if (x.Estado != "APLICADO") _grid.Rows[i].DefaultCellStyle.ForeColor = Tema.Humo;
                }
                if (_lista.Count > 0) EnvioUtil.Llenar(_det, Envios.Detalle(_lista[0].Orden));
            }
            catch (Exception ex) { Ui.Error(ex, "Envíos recibidos"); }
        }

        void Deshacer()
        {
            var x = _lista.FirstOrDefault(e => e.Estado == "APLICADO");
            if (x == null) { Ui.Aviso("No hay envíos por deshacer."); return; }
            if (Ui.Autorizar(Permisos.SoloAdmin, "Deshacer envío: clave de un administrador") == null) return;
            if (!Ui.Confirmar("Se revertirá el envío " + x.Num + ":\n\n" + x.Resumen + "\n\nLas ventas no se tocan. Lo que se haya cambiado después se respeta.\n¿Continuar?")) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                var no = Envios.Deshacer(x.Orden, Sesion.Usuario.Id);
                Cursor = Cursors.Default;
                Ui.Info("Envío " + x.Num + " deshecho." + (no.Count == 0 ? "" : "\n\nNo se pudo revertir:\n" + string.Join("\n", no.Take(12).ToArray()) + (no.Count > 12 ? "\n… y " + (no.Count - 12) + " más" : "")) +
                    "\n\nGuarde el acuse en la USB y llévelo a la oficina para que lo sepa.", "Envío");
            }
            catch (Exception ex) { Ui.Error(ex, "No se deshizo nada"); }
            finally { Cursor = Cursors.Default; }
            Cargar();
        }

        void Acuse()
        {
            if (Envios.Dato("oficina_id") == "") { Ui.Aviso("Todavía no se ha aplicado ningún envío."); return; }
            string c = EnvioUtil.PedirCarpeta(FindForm(), "¿En qué USB o carpeta guardo el acuse para la oficina?");
            if (c == null) return;
            try { Ui.Info("Acuse guardado:\n" + Envios.EscribirAcuse(c)); } catch (Exception ex) { Ui.Error(ex, "Guardar acuse"); }
        }
    }

    // ==================================================================== OFICINA
    /// <summary>Almacén → Llegó mercancía, en la OFICINA: captura lo que se sumará en la tienda.</summary>
    public class SeccionMercancia : UserControl
    {
        class Renglon { public Producto P; public decimal Cantidad; public decimal? Costo; }
        static readonly List<Renglon> _lista = new List<Renglon>();   // sobrevive al cambiar de módulo: no se pierde lo capturado
        public static int SinGuardar { get { return _lista.Count; } }
        public static void Olvidar() { _lista.Clear(); }
        readonly TextBox _codigo, _cantidad, _costo, _referencia;
        readonly DataGridView _grid;
        readonly Label _total;

        public SeccionMercancia()
        {
            BackColor = Tema.Crema;
            _grid = AdminUtil.Grid();
            _grid.Columns.Add(Ui.Columna("codigo", "Código", 18));
            _grid.Columns.Add(Ui.Columna("descripcion", "Producto", 44));
            _grid.Columns.Add(Ui.Columna("cantidad", "Cantidad", 14, true));
            _grid.Columns.Add(Ui.Columna("costo", "Costo nuevo", 14, true));
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) { Quitar(); e.Handled = true; } };
            Controls.Add(_grid);

            var abajo = new Panel { Dock = DockStyle.Bottom, Height = 56 };
            abajo.Controls.Add(Ui.Etiqueta("Factura / proveedor:", 10, 18));
            _referencia = Ui.Caja(170, 14, 230); abajo.Controls.Add(_referencia);
            abajo.Controls.Add(Ui.Boton("Quitar (Supr)", 410, 10, 120, 38, (s, e) => Quitar()));
            abajo.Controls.Add(Ui.Boton("Guardar para el envío", 538, 10, 210, 38, (s, e) => Guardar(), true));
            _total = Ui.Etiqueta("", 758, 18, Tema.Negrita); abajo.Controls.Add(_total);
            Controls.Add(abajo);

            var arriba = new Panel { Dock = DockStyle.Top, Height = 92 };
            arriba.Controls.Add(Ui.Etiqueta("Código:", 10, 10, Tema.Negrita));
            _codigo = Ui.Caja(10, 32, 220, Tema.Mediana); arriba.Controls.Add(_codigo);
            arriba.Controls.Add(Ui.Etiqueta("Cantidad:", 240, 10, Tema.Negrita));
            _cantidad = Ui.Caja(240, 32, 100, Tema.Mediana); _cantidad.TextAlign = HorizontalAlignment.Right; _cantidad.Text = "1"; arriba.Controls.Add(_cantidad);
            arriba.Controls.Add(Ui.Etiqueta("Costo nuevo (opcional):", 350, 10));
            _costo = Ui.Caja(350, 32, 120, Tema.Mediana); _costo.TextAlign = HorizontalAlignment.Right; arriba.Controls.Add(_costo);
            arriba.Controls.Add(Ui.Boton("Agregar (Enter)", 480, 30, 130, 34, (s, e) => Agregar(), true));
            arriba.Controls.Add(Ui.Boton("Buscar (Ctrl+B)", 616, 30, 130, 34, (s, e) => Buscar()));
            arriba.Controls.Add(Ui.Boton("Cargar Excel…", 752, 30, 130, 34, (s, e) => Excel()));
            arriba.Controls.Add(Ui.Etiqueta("En la tienda se SUMA a lo que haya. Para corregir o dar salida, escriba la cantidad con signo menos.", 10, 70, null, Tema.Humo));
            Controls.Add(arriba);
            Controls.Add(AdminUtil.TituloSeccion("Llegó mercancía"));

            Pintar();
            _codigo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _cantidad.Focus(); _cantidad.SelectAll(); } else if (e.KeyData == (Keys.Control | Keys.B)) { e.SuppressKeyPress = true; Buscar(); } };
            _cantidad.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Agregar(); } };
            _costo.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Agregar(); } };
        }

        void Buscar()
        {
            var p = FormElegirProducto.Pedir(FindForm(), "");
            if (p != null) { _codigo.Text = p.Codigo; _cantidad.Focus(); _cantidad.SelectAll(); }
        }

        void Agregar()
        {
            if (_codigo.Text.Trim() == "") { _codigo.Focus(); return; }
            var p = FormElegirProducto.Pedir(FindForm(), _codigo.Text);
            if (p == null) { _codigo.Focus(); _codigo.SelectAll(); return; }
            if (!p.UsaInventario) { Ui.Aviso("\"" + p.Descripcion + "\" no controla existencia.\nActívelo en el producto si quiere llevar inventario."); return; }
            decimal cant, costo; decimal? nuevo = null;
            if (!Fmt.TryDec(_cantidad.Text, out cant) || cant == 0) { Ui.Aviso("Cantidad inválida."); _cantidad.Focus(); _cantidad.SelectAll(); return; }
            if (_costo.Text.Trim() != "")
            {
                if (!Fmt.TryDec(_costo.Text, out costo) || costo < 0) { Ui.Aviso("Costo inválido."); _costo.Focus(); return; }
                nuevo = costo;
            }
            Sumar(p, cant, nuevo);
            Pintar();
            _codigo.Text = ""; _cantidad.Text = "1"; _costo.Text = "";
            _codigo.Focus();
        }

        void Sumar(Producto p, decimal cant, decimal? costo)
        {
            var ya = _lista.FirstOrDefault(r => r.P.Id == p.Id);
            if (ya != null) { ya.Cantidad += cant; if (costo.HasValue) ya.Costo = costo; }
            else _lista.Add(new Renglon { P = p, Cantidad = cant, Costo = costo });
        }

        void Quitar()
        {
            if (_grid.CurrentRow == null) return;
            _lista.Remove(_grid.CurrentRow.Tag as Renglon);
            Pintar();
        }

        void Pintar()
        {
            _grid.Rows.Clear();
            foreach (var r in _lista)
            {
                int i = _grid.Rows.Add(r.P.Codigo, r.P.Descripcion, (r.Cantidad > 0 ? "+" : "") + Fmt.Cant(r.Cantidad, r.P.Granel), r.Costo.HasValue ? Fmt.Dinero(r.Costo.Value) : "");
                _grid.Rows[i].Tag = r;
                if (r.Cantidad < 0) _grid.Rows[i].DefaultCellStyle.ForeColor = Tema.Rojo;
            }
            _total.Text = _lista.Count + " productos";
        }

        /// <summary>Excel o CSV con columnas CÓDIGO y CANTIDAD (y COSTO, opcional).</summary>
        void Excel()
        {
            string f = null;
            using (var d = new OpenFileDialog { Title = "Excel con las columnas CÓDIGO y CANTIDAD (COSTO es opcional)", Filter = "Excel o CSV|*.xlsx;*.csv" })
                if (d.ShowDialog(FindForm()) == DialogResult.OK) f = d.FileName;
            if (f == null) return;
            try
            {
                var t = TablaArchivo.Leer(f);
                int cCod = Col(t, "codigo"), cCant = Col(t, "cantidad", "entra", "entrada", "piezas"), cCosto = Col(t, "costo");
                if (cCod < 0 || cCant < 0) { Ui.Aviso("El archivo debe tener las columnas CÓDIGO y CANTIDAD."); return; }
                int bien = 0; var mal = new List<string>();
                foreach (DataRow r in t.Rows)
                {
                    string cod = r[cCod] is double ? Math.Round((double)r[cCod]).ToString("0", Fmt.Inv) : Convert.ToString(r[cCod], Fmt.Inv).Trim();
                    if (cod == "" || Convert.ToString(r[cCant], Fmt.Inv).Trim() == "") continue;
                    decimal q, k; decimal? costo = null;
                    var p = ProductoRepo.PorCodigo(cod);
                    if (p == null) { mal.Add(cod + " (no existe)"); continue; }
                    if (!p.UsaInventario) { mal.Add(cod + " (no controla existencia)"); continue; }
                    if (!Num(r[cCant], out q) || q == 0) { mal.Add(cod + " (cantidad)"); continue; }
                    if (cCosto >= 0 && Convert.ToString(r[cCosto], Fmt.Inv).Trim() != "") { if (!Num(r[cCosto], out k) || k < 0) { mal.Add(cod + " (costo)"); continue; } costo = k; }
                    Sumar(p, q, costo); bien++;
                }
                Pintar();
                Ui.Info(bien + " renglones cargados. Revíselos y presione «Guardar para el envío»." + (mal.Count == 0 ? "" : "\n\nNo se cargaron " + mal.Count + ":\n" + string.Join(", ", mal.Take(15).ToArray()) + (mal.Count > 15 ? "…" : "")));
            }
            catch (Exception ex) { Ui.Error(ex, "Cargar Excel"); }
        }

        static int Col(DataTable t, params string[] nombres)
        {
            for (int i = 0; i < t.Columns.Count; i++) if (nombres.Contains(ZafirahMapeo.Norm(t.Columns[i].ColumnName))) return i;
            return -1;
        }
        static bool Num(object o, out decimal v)
        {
            if (o is double) { v = Dinero.Leer(o); return true; }
            return Fmt.TryDec(Convert.ToString(o, Fmt.Inv), out v);
        }

        bool GuardarLista(List<Renglon> l)
        {
            try { Envios.RegistrarMercancia(l.Select(r => new RenglonMercancia { ProductoId = r.P.Id, Cantidad = r.Cantidad, Costo = r.Costo }).ToList(), _referencia.Text.Trim()); return true; }
            catch (Exception ex) { Ui.Error(ex, "No se guardó la mercancía"); return false; }
        }

        void Guardar()
        {
            if (_lista.Count == 0) { Ui.Aviso("No hay productos capturados."); return; }
            if (!GuardarLista(_lista)) return;
            Ui.Info(_lista.Count + " productos guardados para el próximo envío.\n\nCuando termine, vaya a «Envíos» (Ctrl+F2) y presione «Crear envío».");
            _lista.Clear(); _referencia.Text = "";
            Pintar();
            _codigo.Focus();
        }
    }

    /// <summary>Almacén → Envíos, en la OFICINA: lo pendiente, crear el archivo, leer acuses e historial.</summary>
    public class SeccionEnviosOficina : UserControl
    {
        readonly Label _resumen;
        readonly TextBox _alertas;
        readonly DataGridView _pend, _hist;

        public SeccionEnviosOficina()
        {
            BackColor = Tema.Crema;
            var tabs = new TabControl { Dock = DockStyle.Fill, Font = Tema.Negrita, Padding = new Point(14, 6) };
            var t1 = new TabPage("Por enviar") { BackColor = Tema.Crema };
            _pend = EnvioUtil.GridLineas();
            t1.Controls.Add(_pend);
            _alertas = new TextBox { Dock = DockStyle.Bottom, Height = 90, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(255, 247, 214), Font = Tema.Normal, Visible = false };
            t1.Controls.Add(_alertas);
            _resumen = new Label { Dock = DockStyle.Top, Height = 60, Font = Tema.Mediana, ForeColor = Tema.Azul, Padding = new Padding(8, 6, 8, 0) };
            t1.Controls.Add(_resumen);
            tabs.TabPages.Add(t1);

            var t2 = new TabPage("Historial") { BackColor = Tema.Crema };
            _hist = AdminUtil.Grid();
            _hist.Columns.Add(Ui.Columna("n", "Envío", 7, true));
            _hist.Columns.Add(Ui.Columna("f", "Creado", 16));
            _hist.Columns.Add(Ui.Columna("u", "Por", 13));
            _hist.Columns.Add(Ui.Columna("r", "Qué lleva", 40));
            _hist.Columns.Add(Ui.Columna("e", "En la tienda", 24));
            _hist.Columns.Add(Ui.Columna("p", "Problemas", 9, true));
            _hist.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) VerProblemas(); };
            t2.Controls.Add(_hist);
            var b2 = AdminUtil.Barra();
            b2.Controls.Add(AdminUtil.BotonBarra("Ver problemas", (s, e) => VerProblemas(), 140));
            b2.Controls.Add(AdminUtil.BotonBarra("Volver a enviar desde este", (s, e) => Reenviar(), 220));
            t2.Controls.Add(b2);
            tabs.TabPages.Add(t2);
            Controls.Add(tabs);

            var barra = AdminUtil.Barra();
            barra.Controls.Add(AdminUtil.BotonBarra("Crear envío…", (s, e) => Crear(), 140, true));
            barra.Controls.Add(AdminUtil.BotonBarra("Leer acuse…", (s, e) => LeerAcuse(), 130));
            barra.Controls.Add(AdminUtil.BotonBarra("Actualizar", (s, e) => Cargar(), 110));
            Controls.Add(barra);
            Controls.Add(AdminUtil.TituloSeccion("Envíos a la tienda  ·  " + Envios.Dato("negocio")));
            Load += (s, e) => Cargar();
        }

        void Cargar()
        {
            try
            {
                var r = Envios.VistaPendientes();
                long sin, ya; Envios.ContarPendientes(out sin, out ya);
                EnvioUtil.Llenar(_pend, r.Lineas);
                _resumen.Text = sin == 0 ? "No hay nada por enviar." : r.Resumen + (ya > 0 ? "\n(incluye lo que ya se mandó y aún no tiene acuse; en la tienda no se aplica dos veces)" : "");
                _alertas.Visible = r.Alertas.Count > 0;
                _alertas.Text = "REVISE ANTES DE ENVIAR:\r\n" + string.Join("\r\n", r.Alertas.ToArray());
                _hist.Rows.Clear();
                foreach (var h in Envios.Hechos())
                {
                    int n = h.Problemas == "" ? 0 : h.Problemas.Split('\n').Length;
                    int i = _hist.Rows.Add(h.Num, h.Fecha, h.Usuario, h.Resumen, h.Estado == "CREADO" ? "Sin acuse" : (h.Estado == "APLICADO" ? "Aplicado " : "Deshecho ") + h.Aplicado, n == 0 ? "" : n.ToString());
                    _hist.Rows[i].Tag = h;
                    if (n > 0) _hist.Rows[i].DefaultCellStyle.ForeColor = Tema.Rojo;
                }
            }
            catch (Exception ex) { Ui.Error(ex, "Envíos"); }
        }

        void Crear()
        {
            long sin, ya; Envios.ContarPendientes(out sin, out ya);
            if (sin == 0) { Ui.Aviso("No hay cambios por enviar."); return; }
            string c = EnvioUtil.PedirCarpeta(FindForm(), "¿En qué USB o carpeta guardo el envío?");
            if (c == null) return;
            try
            {
                foreach (var a in Directory.GetFiles(c, "*.vfacuse"))   // si la USB ya trae el acuse, se lee primero
                    try { Envios.LeerAcuse(a); } catch (Exception ex) { Log.Error(ex, "Acuse al crear envío"); Ui.Aviso("El acuse de la USB no se usó:\n\n" + ex.Message); }
                Envios.ContarPendientes(out sin, out ya);
                if (sin == 0) { Ui.Info("Según el acuse de la USB, la tienda ya tiene todo. No hace falta otro envío."); Cargar(); return; }
                var r = Envios.VistaPendientes();
                if (r.Alertas.Count > 0 && !Ui.Confirmar("Hay " + r.Alertas.Count + " avisos en este envío:\n\n" + string.Join("\n", r.Alertas.Take(8).ToArray()) + (r.Alertas.Count > 8 ? "\n…" : "") + "\n\n¿Crear el envío de todos modos?")) return;
                string f = Envios.Crear(c, Sesion.Usuario.Nombre);
                Ui.Info("Envío guardado:\n" + f + "\n\n" + r.Resumen + "\n\nEn la caja: F2 Turno → Recibir envío.", "Envío");
            }
            catch (Exception ex) { Ui.Error(ex, "No se creó el envío"); }
            Cargar();
        }

        void LeerAcuse()
        {
            var enUsb = EnvioUtil.BuscarEnUsb("*.vfacuse");
            string f = enUsb.Count == 1 ? enUsb[0] : EnvioUtil.PedirArchivo(FindForm(), "Elija el acuse de la tienda", "Acuse de VentaFácil (*.vfacuse)|*.vfacuse");
            if (f == null) return;
            try { Ui.Info(Envios.LeerAcuse(f), "Acuse"); } catch (Exception ex) { Ui.Error(ex, "Leer acuse"); }
            Cargar();
        }

        EnvioHecho Elegido() { return _hist.CurrentRow == null ? null : _hist.CurrentRow.Tag as EnvioHecho; }

        void VerProblemas()
        {
            var h = Elegido();
            if (h == null) return;
            Ui.Info(h.Problemas == "" ? "El envío " + h.Num + " no tiene problemas reportados." : h.Problemas.Replace("\n", "\n\n"), "Envío " + h.Num);
        }

        void Reenviar()
        {
            var h = Elegido();
            if (h == null) return;
            if (!Ui.Confirmar("Se volverá a incluir en el próximo envío todo desde el envío " + h.Num + ".\n\nEn la tienda no se duplica: se salta lo que ya tiene.\n¿Continuar?")) return;
            try { Envios.Reenviar(h.Id); } catch (Exception ex) { Ui.Error(ex, "Volver a enviar"); }
            Cargar();
        }
    }

    // ==================================================================== AJUSTES
    /// <summary>Ajustes → Envíos: decidir si este equipo es caja u oficina y enlazarlos. Solo administradores.</summary>
    public class SeccionEnlace : UserControl
    {
        public SeccionEnlace()
        {
            BackColor = Tema.Crema;
            var p = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            Controls.Add(p);
            Controls.Add(AdminUtil.TituloSeccion("Envíos entre oficina y tienda"));
            bool oficina = Envios.EsOficina, tienda = Envios.EsTienda, conocida = Envios.Dato("oficina_id") != "";
            int y = 16;
            p.Controls.Add(Ui.Etiqueta(oficina ? "Este equipo es la OFICINA de: " + Envios.Dato("negocio")
                : tienda ? (conocida ? "Esta caja está enlazada con su oficina." : "Esta caja ya sacó su archivo de enlace. Falta recibir el primer envío.")
                : "Este equipo es una caja normal (sin enlazar).", 16, y, Tema.Mediana, Tema.Azul));
            y += 44;
            if (!oficina)
            {
                p.Controls.Add(Ui.Etiqueta("EN LA CAJA de la tienda: guarde en la USB el archivo con el que la oficina se enlaza.\nLleva una copia del catálogo; queda protegido con la contraseña que usted ponga.", 16, y, null, Tema.Humo)); y += 52;
                p.Controls.Add(Ui.Boton("1. Crear archivo de enlace…", 16, y, 280, 42, (s, e) => CrearEnlace(), true)); y += 56;
                var chk = new CheckBox { Text = "Pedir la clave de un administrador para aplicar cada envío", Left = 16, Top = y, AutoSize = true, Font = Tema.Normal, Checked = Config.Actual.EnviosPedirClave };
                chk.CheckedChanged += (s, e) => { try { var c = Config.Actual.Copia(); c.EnviosPedirClave = chk.Checked; c.Guardar(); } catch (Exception ex) { Ui.Error(ex, "Guardar"); } };
                p.Controls.Add(chk); y += 56;
            }
            if (!oficina && !tienda)
            {
                p.Controls.Add(Ui.Etiqueta("EN LA COMPUTADORA DE LA OFICINA (instalación nueva, sin ventas): abra el archivo de enlace de la tienda.", 16, y, null, Tema.Humo)); y += 30;
                p.Controls.Add(Ui.Boton("2. Convertir este equipo en la oficina…", 16, y, 320, 42, (s, e) => Convertir())); y += 70;
            }
            if (oficina)
            {
                p.Controls.Add(Ui.Etiqueta("Los cambios del Catálogo se anotan solos. La mercancía se captura en F4 Almacén → Llegó mercancía.\nEn la oficina no se cobra. Las existencias de aquí no son las de la tienda: las reales se ven en la caja.\nLas existencias que vengan en un Excel o CSV de productos YA dados de alta no viajan; use Llegó mercancía.", 16, y, null, Tema.Humo)); y += 84;
                p.Controls.Add(Ui.Etiqueta("Si cambia de computadora o restaura un respaldo viejo aquí, hay que volver a enlazar con la tienda.", 16, y, null, Tema.Humo)); y += 40;
            }
            if (oficina || tienda)
            {
                var b = Ui.Boton("Quitar el enlace", 16, y, 200, 42, (s, e) => Quitar());
                Tema.BotonPeligro(b);
                p.Controls.Add(b);
            }
        }

        void Repintar() { var m = Parent; while (m != null && !(m is ModuloVista)) m = m.Parent; if (m != null) BeginInvoke((Action)(() => ((ModuloVista)m).Recargar())); }

        void CrearEnlace()
        {
            if (Envios.EsTienda && !Ui.Confirmar("Esta caja ya tenía un enlace.\n\nSi crea otro, la oficina anterior deja de servir hasta que se vuelva a enlazar con el archivo nuevo.\n¿Continuar?")) return;
            string c1 = Ui.PedirTexto("Contraseña del enlace", "Contraseña (6 o más caracteres):", "", true);
            if (c1 == null) return;
            if (c1.Length < 6) { Ui.Aviso("La contraseña debe tener al menos 6 caracteres."); return; }
            if (Ui.PedirTexto("Contraseña del enlace", "Escríbala otra vez:", "", true) != c1) { Ui.Aviso("Las contraseñas no coinciden."); return; }
            string c = EnvioUtil.PedirCarpeta(FindForm(), "¿En qué USB o carpeta guardo el archivo de enlace?");
            if (c == null) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                string ruta = Path.Combine(c, "ENLACE_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".vfenlace");
                int n = Envios.CrearEnlace(ruta, c1, Config.Actual.NegocioNombre);
                Cursor = Cursors.Default;
                Ui.Info("Archivo de enlace guardado con " + n + " productos:\n" + ruta + "\n\nÁbralo en la computadora de la oficina y después bórrelo de la USB.");
                Repintar();
            }
            catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex, "No se creó el enlace"); }
        }

        void Convertir()
        {
            string f = EnvioUtil.PedirArchivo(FindForm(), "Elija el archivo de enlace de la tienda", "Enlace de VentaFácil (*.vfenlace)|*.vfenlace");
            if (f == null) return;
            if (!Ui.Confirmar("Este equipo será la OFICINA:\n\n• El catálogo de aquí se reemplaza por el de la tienda (antes se guarda un respaldo).\n• Aquí ya no se podrá cobrar.\n\n¿Continuar?")) return;
            string c = Ui.PedirTexto("Contraseña del enlace", "La contraseña que se puso en la caja:", "", true);
            if (c == null) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                int n = Envios.ConvertirEnOficina(f, c);
                SeccionMercancia.Olvidar();
                if (Envios.Dato("negocio") != "") { var cfg = Config.Actual.Copia(); cfg.NegocioNombre = Envios.Dato("negocio"); cfg.Guardar(); }
                Cursor = Cursors.Default;
                Ui.Info("Listo: este equipo es la oficina, con " + n + " productos de la tienda.\n\nSalga a Caja (Esc) y vuelva a entrar a F4 Almacén para ver «Llegó mercancía» y «Envíos».");
                Repintar();
            }
            catch (Exception ex) { Cursor = Cursors.Default; Ui.Error(ex, "No se convirtió en oficina"); }
        }

        void Quitar()
        {
            if (!Ui.Confirmar("Se quita el enlace de este equipo. Productos y ventas no se tocan.\n\nPara volver a usar envíos habrá que enlazar de nuevo" + (Envios.EsOficina ? " y lo que no se haya enviado se queda sin mandar" : "") + ".\n¿Continuar?")) return;
            try { Envios.QuitarEnlace(); Repintar(); } catch (Exception ex) { Ui.Error(ex, "Quitar enlace"); }
        }
    }
}
