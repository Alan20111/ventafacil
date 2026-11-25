using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Forms.Admin;
using VentaFacil.Forms.Licenciamiento;
using VentaFacil.Forms.Pos;
using VentaFacil.UI;

namespace VentaFacil
{
    /// <summary>
    /// Verificador de pantallas: las abre de verdad (Windows), guarda una captura de cada una y revisa las reglas de diseño:
    /// nada encimado, nada fuera de su contenedor, textos que caben, etiquetas centradas con su campo, botones de una barra parejos.
    /// Uso:  VerificarUi.exe [carpeta de salida]      Sale con 1 si hay fallas.
    /// </summary>
    static class VerificarUi
    {
        static string _salida;
        static readonly StringBuilder _rep = new StringBuilder();
        static int _fallas, _pantallas;
        static volatile bool _fin;

        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint hilo, EnumWin f, IntPtr p);
        [DllImport("user32.dll")] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumWin f, IntPtr p);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int n);
        delegate bool EnumWin(IntPtr h, IntPtr p);

        [STAThread]
        static int Main(string[] args)
        {
            _salida = Path.GetFullPath(args.Length > 0 ? args[0] : "capturas");
            Directory.CreateDirectory(_salida);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => Falla("(excepción)", e.Exception.ToString());
            CerrarAvisos(GetCurrentThreadId());
            FormMensaje.Interceptar = (texto, titulo) => { lock (_rep) _rep.AppendLine("AVISO: [" + titulo + "] " + texto.Replace("\r", "").Replace("\n", " / ")); return false; };
            try { Correr(); }
            catch (Exception ex) { Falla("(general)", ex.ToString()); }
            _fin = true;
            string cab = "Pantallas: " + _pantallas + "   Fallas: " + _fallas + "   Pantalla: " + Screen.PrimaryScreen.Bounds + "   DPI: " + Dpi() + "   Windows: " + Environment.OSVersion + "   CLR: " + Environment.Version + "\r\n\r\n";
            File.WriteAllText(Path.Combine(_salida, "reporte.txt"), cab + _rep, Encoding.UTF8);
            Console.WriteLine(cab + _rep);
            Environment.Exit(_fallas == 0 ? 0 : 1);
            return 0;
        }

        static int Dpi() { using (var g = Graphics.FromHwnd(IntPtr.Zero)) return (int)g.DpiX; }

        /// <summary>Cualquier aviso que se abra solo se anota y se cierra (si no, se quedaría esperando).</summary>
        static void CerrarAvisos(uint hilo)
        {
            new Thread(() =>
            {
                while (!_fin)
                {
                    Thread.Sleep(400);
                    EnumThreadWindows(hilo, (h, p) =>
                    {
                        var c = new StringBuilder(64); GetClassName(h, c, 64);
                        if (c.ToString() != "#32770") return true;
                        var t = new StringBuilder(256); GetWindowText(h, t, 256);
                        var d = new StringBuilder();
                        EnumChildWindows(h, (x, q) => { var y = new StringBuilder(512); GetWindowText(x, y, 512); if (y.Length > 12) d.Append(y).Append(" | "); return true; }, IntPtr.Zero);
                        lock (_rep) _rep.AppendLine("AVISO cerrado: [" + t + "] " + d);
                        PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);
                        return true;
                    }, IntPtr.Zero);
                }
            }) { IsBackground = true }.Start();
        }

        static void Falla(string pantalla, string texto) { _fallas++; lock (_rep) _rep.AppendLine("FALLA  " + pantalla + ": " + texto); }
        static object Nuevo(Type t, params object[] a) { return Activator.CreateInstance(t, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, a, null); }
        static object Campo(object o, string n) { var t = o.GetType(); FieldInfo f = null; while (t != null && f == null) { f = t.GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); t = t.BaseType; } return f == null ? null : f.GetValue(o); }
        static void PonerCampo(object o, string n, object v) { o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(o, v); }
        static object Llamar(object o, string m, params object[] a) { return o.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).First(x => x.Name == m && x.GetParameters().Length == a.Length).Invoke(o, a); }
        static void Esperar(int ms) { var t = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < t) { Application.DoEvents(); Thread.Sleep(10); } }

        // ------------------------------------------------------------------ datos de prueba
        static void Preparar()
        {
            Db.PrepararRutas();
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            if (Directory.Exists(Db.Carpeta)) foreach (var f in Directory.GetFiles(Db.Carpeta, "ventafacil.db*")) File.Delete(f);
            Db.Inicializar();
            Config.Cargar();
            var c = Config.Actual.Copia();
            c.NegocioNombre = "SEMILLAS Y BOTANAS"; c.PedirFondoAlAbrir = false; c.ImpresoraRevisada = true; c.CarpetaRespaldoRevisada = true; c.CarpetaRespaldo = Path.Combine(Db.Carpeta, "resp");
            Directory.CreateDirectory(c.CarpetaRespaldo);
            c.Guardar();
        }

        static void Sembrar()
        {
            var adm = UsuarioRepo.Validar("admin", "admin");
            Sesion.Usuario = adm;
            UsuarioRepo.Guardar(new Usuario { Login = "cajera", Nombre = "María Fernanda López Hernández", Rol = "CAJERO" }, "1234");
            UsuarioRepo.Guardar(new Usuario { Login = "encargado", Nombre = "Encargado de tienda", Rol = "CAJERO", Permisos = "*" }, "1234");
            for (int i = 1; i <= 40; i++)
                ProductoRepo.Guardar(new Producto { Codigo = i.ToString(), Codigo2 = i % 3 == 0 ? "7501234567" + i.ToString("000") : "", Descripcion = "PRODUCTO DE PRUEBA CON NOMBRE LARGO " + i, Precio = 1234.50m + i, Costo = 800, PrecioMayoreo = 1100, MayoreoDesde = 25, PrecioMedio = 1150, MedioDesde = 5, Granel = i % 2 == 0, Existencia = 10 + i, Minimo = 20, Proveedor = "PROVEEDOR " + (i % 4) }, adm.Id);
            var t = TurnoRepo.Abrir(1, adm.Id, 500);
            Sesion.Turno = t;
            for (int i = 1; i <= 5; i++)
            {
                var p = ProductoRepo.PorCodigo(i.ToString());
                var v = new Venta { TurnoId = t.Id, Caja = 1, Articulos = 2 };
                v.Lineas.Add(new LineaVenta { ProductoId = p.Id, Codigo = p.Codigo, Descripcion = p.Descripcion, Cantidad = 2, Precio = p.Precio, PrecioLista = p.Precio, Costo = p.Costo });
                v.Subtotal = v.Total = v.Lineas[0].Importe; v.PagoCon = v.Total;
                VentaRepo.Guardar(v, adm.Id);
            }
            VentaRepo.Cancelar(1, t.Id, adm.Id);
            TurnoRepo.Movimiento(t.Id, adm.Id, "SALIDA", 50, "Pago de agua");
        }

        // ------------------------------------------------------------------ recorrido
        static void Correr()
        {
            Preparar();
            Form1("01 asistente nueva", () => new FormAsistente(true), f => Asistente((FormAsistente)f, "01 asistente nueva"));
            Sembrar();
            Form1("02 login", () => new FormLogin(false, null), null);
            Form1("02 login error", () => new FormLogin(false, null), f => { ((TextBox)Campo(f, "_usuario")).Text = "nadie"; ((TextBox)Campo(f, "_clave")).Text = "x"; int x = f.Left; Llamar(f, "Entrar"); Llamar(f, "Entrar"); Esperar(500); Foto(f, "02 login error"); if (f.Left != x) Falla("02 login error", "la ventana no regresó a su lugar"); });
            Form1("02 login autorizacion", () => new FormLogin(true, "Este módulo requiere autorización"), null);
            Principal("10 caja");
            Principal("70 caja 1024", 1008);
            Dialogos();
            Mensajes();
            SinPermiso();
            Form1("60 asistente actualizar", () => new FormAsistente(false), f => Asistente((FormAsistente)f, "60 asistente actualizar"));
            Form1("61 actualizado", () => new FormActualizado("1.24"), null);

            // enlazada como tienda
            string enl = Path.Combine(_salida, "e.vfenlace");
            Envios.CrearEnlace(enl, "secreta1", "SEMILLAS Y BOTANAS");
            Principal("40 tienda");
            Form1("41 recibir envio", () => new FormRecibirEnvio(), null);
            // oficina: base nueva
            Preparar();
            Sesion.Usuario = UsuarioRepo.Validar("admin", "admin");
            Envios.ConvertirEnOficina(enl, "secreta1");
            var pr = ProductoRepo.PorCodigo("1"); pr.Precio = 99; ProductoRepo.Guardar(pr, 1);
            Envios.RegistrarMercancia(new List<RenglonMercancia> { new RenglonMercancia { ProductoId = pr.Id, Cantidad = 50 } }, "Factura 123");
            Principal("50 oficina");
            File.Delete(enl);

            // ---- salir y cierre
            Form1("80 salir", () => new FormSalir(new List<string>()), null);
            Form1("80 salir con avisos", () => new FormSalir(new List<string> { "Hay una venta sin cobrar: se perdería.", "Hay 12 renglones de mercancía sin guardar." }), null);
            Cierre("81 cerrando", false);
            Cierre("82 cerrando sin usb", true);
        }

        /// <summary>El cierre de verdad, después de Application.Exit (como cuando se sale del programa).</summary>
        static void Cierre(string n, bool sinCarpeta)
        {
            string carpeta = Path.Combine(Db.Carpeta, "respaldos");
            var c = Config.Actual.Copia();
            string malo = Path.Combine(_salida, "x.tmp");
            if (sinCarpeta) { File.WriteAllText(malo, "x"); c.CarpetaRespaldo = malo; }
            c.Guardar();
            Thread.Sleep(1100);
            int antes = Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, "ventafacil_*.db").Length : 0;
            Form fondo = null;
            if (sinCarpeta)
            {   // el caso difícil: después de Application.Exit, el hilo principal ya tiene pedido terminar
                var tonta = new Form { ShowInTaskbar = false, Opacity = 0 };
                tonta.Shown += (s, e) => Application.Exit();
                Application.Run(tonta);
            }
            else { fondo = new Form { Text = "fondo", StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), Size = new Size(1366, 728) }; fondo.Show(); fondo.Activate(); Esperar(300); }

            int tomas = 0; bool rechazo = true;
            typeof(Respaldo).GetProperty("CierreHecho").GetSetMethod(true).Invoke(null, new object[] { false });
            FormCerrando.Ejecutar(f =>
            {
                var t = new System.Windows.Forms.Timer { Interval = 350 };
                t.Tick += (s, e) =>
                {
                    t.Stop();      // la foto atiende eventos: sin esto el reloj se encimaría
                    tomas++;
                    if (tomas <= 3 && f.Visible) Foto(f, n + " " + tomas);
                    if (tomas == 1 && !sinCarpeta && (GetWindowLong(f.Handle, -20) & 8) == 0) Falla(n, "no quedó encima de las demás ventanas");
                    if (tomas == 1) { f.Close(); rechazo = f.Visible; }        // a medio respaldo no se deja cerrar
                    if (sinCarpeta && tomas == 8)
                    {
                        Foto(f, n + " falla");
                        if (!((Button)Campo(f, "_salir")).Visible) Falla(n, "no ofreció salir cuando falló la copia");
                        ((Button)Campo(f, "_salir")).PerformClick();
                    }
                    if (tomas > 60) { Falla(n, "no terminó"); PonerCampo(f, "_puedeCerrar", true); f.Close(); }
                    if (!f.IsDisposed && f.Visible) t.Start(); else t.Dispose();
                };
                f.Shown += (s, e) => t.Start();
            });
            if (fondo != null) { fondo.Close(); fondo.Dispose(); }
            if (!rechazo) Falla(n, "se dejó cerrar a medio respaldo");
            int ahora = Directory.GetFiles(carpeta, "ventafacil_*.db").Length;
            if (ahora <= antes) Falla(n, "no hizo respaldo (" + antes + " → " + ahora + ")");
            if (!Respaldo.CierreHecho) Falla(n, "no marcó el cierre");
            if (sinCarpeta) File.Delete(malo);
        }

        static void Asistente(FormAsistente f, string n)
        {
            var pasos = (System.Collections.IList)Campo(f, "_pasos");
            for (int i = 0; i < pasos.Count; i++)
            {
                if (!((Func<bool>)Campo(pasos[i], "Visible"))()) continue;
                Llamar(f, "Ir", i, false);
                Foto(f, n + " " + i.ToString("00") + " " + Campo(pasos[i], "Titulo"));
            }
            PonerCampo(f, "_terminado", true);
        }

        static void Dialogos()
        {
            var p = ProductoRepo.PorCodigo("2");
            var l = new LineaVenta { ProductoId = p.Id, Codigo = "2", Descripcion = p.Descripcion, Cantidad = 1.5m, Precio = p.Precio, PrecioLista = p.Precio, Granel = true };
            Form1("21 etiqueta", () => (Form)Nuevo(typeof(FormVistaEtiqueta), p, VentaFacil.Printing.Etiquetas.Dibujar(p, Config.Actual)), null);
            Form1("20 cobro", () => new FormCobro(1234.50m), null);
            Form1("20 buscar", () => (Form)Nuevo(typeof(FormBuscar), "", false), null);
            Form1("20 consultar precio", () => (Form)Nuevo(typeof(FormBuscar), "", true), null);
            Form1("20 peso", () => (Form)Nuevo(typeof(FormPeso), p), null);
            Form1("20 descuento", () => new FormDescuento(l, 1234), null);
            Form1("20 articulo varios", () => new FormArticuloComun(1), null);
            Form1("20 espera", () => new FormEspera(true), null);
            Form1("20 movimiento", () => new FormMovimiento(), null);
            Form1("20 cortes", () => new FormCortes(), null);
            Form1("20 pedir texto", () => new FormPedir("Contraseña del enlace", "Contraseña (6 o más caracteres):", "", true, false), null);
            Form1("20 pedir numero", () => new FormPedir("Corte Z - Declaración de efectivo", "Cuente el dinero del cajón. ¿Cuánto efectivo hay? (incluya el fondo)", "", false, true), null);
            Form1("21 producto nuevo", () => new FormProducto(null), null);
            Form1("21 producto modificar", () => new FormProducto(p), null);
            Form1("21 elegir producto", () => new FormElegirProducto(""), null);
            Form1("21 usuario nuevo", () => new FormUsuario(null), null);
            Form1("21 usuario admin", () => new FormUsuario(UsuarioRepo.Validar("admin", "admin")), null);
            Form1("22 impresoras", () => (Form)Nuevo(typeof(FormImpresoras), Config.Actual.Copia(), false), null);
            Form1("22 teclas", () => new FormAyudaTeclas(), null);
            Form1("22 licencia", () => (Form)Nuevo(typeof(FormLicencia), Licencia.Evaluar(), true), null);
            Form1("23 detalle ticket", () => new FormDetalleTicket(2), null);
            Form1("23 detalle corte", () => new FormDetalleCorte(1), null);
            Form1("24 importar zafirah", () => new FormImportarZafirah(), f => Pestanas(f, f, "24 importar zafirah"));
            Form1("24 comandos bk", () => new FormComandosBk("RESTORE DATABASE [vf_import_tmp] FROM DISK = 'C:\\x.bk'"), null);
            Form1("24 reparar", () => (Form)Nuevo(Type.GetType("VentaFacil.Forms.Reparar.FormReparar")), null);
        }

        static void Mensajes()
        {
            string largo = "No se pudo imprimir el ticket 1234.\n\nLa impresora «EPSON TM-T20II» no responde. Revise que esté encendida, que tenga papel y que el cable esté bien conectado.\n\nLa venta SÍ quedó guardada.";
            foreach (Tono t in Enum.GetValues(typeof(Tono)))
            {
                Tono tt = t;
                Form1("30 mensaje " + t, () => (Form)Nuevo(typeof(FormMensaje), largo, "VentaFácil", tt, false), null);
            }
            Form1("30 mensaje corto", () => (Form)Nuevo(typeof(FormMensaje), "Escriba el nombre del negocio.", "VentaFácil", Tono.Cuidado, false), null);
            Form1("30 mensaje pregunta", () => (Form)Nuevo(typeof(FormMensaje), "¿Dejar la configuración para después?\n\nLo que ya pasó con «Siguiente» quedó guardado.", "VentaFácil", Tono.Info, true), null);
            Form1("30 mensaje enorme", () => (Form)Nuevo(typeof(FormMensaje), "Hubo problemas.\n\n" + string.Join("\n", Enumerable.Range(1, 60).Select(i => "Renglón " + i + ": el producto no existe").ToArray()), "VentaFácil", Tono.Error, false), null);
            Form1("30 tostada", () => (Form)Nuevo(typeof(Tostada), "Configuración guardada", Tono.Bien), null);
            Form1("30 licencia vencida", () => (Form)Nuevo(typeof(FormLicencia), new InfoLicencia { Estado = EstadoLicencia.Vencida, Motivo = "El período de prueba terminó. Escriba su clave para seguir usando VentaFácil." }, false), null);
        }

        /// <summary>Quien no tiene permiso no ve el módulo, ni lo abre con su tecla, ni con un clic.</summary>
        static void SinPermiso()
        {
            const string n = "35 cajera";
            var antes = Sesion.Usuario;
            FormVenta f = null;
            try
            {
                foreach (var perfil in new[] { "=", "=turno", "=turno,almacen", "*" })
                {
                    Sesion.Usuario = new Usuario { Id = antes.Id, Login = "x", Nombre = "María Fernanda", Rol = "CAJERO", Permisos = perfil, Activo = true };
                    f = new FormVenta { StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
                    f.Show(); f.WindowState = FormWindowState.Normal; f.ClientSize = new Size(1366, 705); Esperar(700);
                    var b = (BarraModulos)Campo(f, "_barraMod");
                    Foto(f, n + " " + perfil.Replace("*", "encargado").Replace("=", "permisos "));
                    string[] claves = { null, "turno", "catalogo", "almacen", "resultados", "ajustes" };
                    for (int i = 1; i < 6; i++)
                    {
                        bool debe = perfil == "*" || perfil.Contains(claves[i]);
                        if (b.Visto(i) != debe) Falla(n, perfil + ": F" + (i + 1) + (debe ? " no aparece" : " sí aparece"));
                        Llamar(f, "IrAModulo", i); Esperar(60);
                        if (((int)Campo(f, "_modActivo") == i) != debe) Falla(n, perfil + ": F" + (i + 1) + (debe ? " no abre" : " abre sin permiso"));
                    }
                    Llamar(f, "IrAModulo", 0);
                    // el clic cae en el módulo que se ve, no en el escondido
                    var elegidos = new HashSet<int>();
                    for (int x = 0; x < b.Width; x += 20) { int c = (int)Llamar(b, "Cual", new Point(x, 20)); if (c >= 0) elegidos.Add(c); }
                    foreach (int c in elegidos) if (!b.Visto(c)) Falla(n, "el clic alcanza un módulo escondido: " + c);
                    Llamar(f, "Mensaje", "Artículo no encontrado: 7501234567890", true); Foto(f, n + " mensaje " + perfil.Length);
                    PonerCampo(f, "_forzarSalida", true); f.Close(); f.Dispose(); f = null;
                }
            }
            catch (Exception ex) { Falla(n, ex.ToString()); }
            finally { if (f != null) { PonerCampo(f, "_forzarSalida", true); f.Close(); f.Dispose(); } Sesion.Usuario = antes; }
        }

        static void Principal(string n, int ancho = 1366)
        {
            FormVenta f = null;
            try
            {
                f = new FormVenta { WindowState = FormWindowState.Normal, StartPosition = FormStartPosition.Manual, Location = new Point(0, 0), ClientSize = new Size(ancho, 705) };
                f.Show();
                f.WindowState = FormWindowState.Normal;
                f.ClientSize = new Size(ancho, 705);       // 1366x768 menos la barra de tareas y el título
                Esperar(1500);
                Foto(f, n + " F1");
                if (n.EndsWith("caja") || n.EndsWith("1024"))
                {
                    var ls = (List<LineaVenta>)Campo(f, "_lineas");
                    for (int i = 1; i <= 4; i++) ls.Add(new LineaVenta { ProductoId = i, Codigo = i.ToString(), Descripcion = "PRODUCTO DE PRUEBA CON NOMBRE LARGO " + i, Cantidad = 25, Precio = 1234.5m, PrecioLista = 1234.5m });
                    Llamar(f, "Refrescar", 0);
                    Foto(f, n + " F1 con venta");
                    ls.Clear(); Llamar(f, "Refrescar", -1);
                }
                Llamar(f, "AvisarUsb", new HallazgoUsb { Ruta = "x", Texto = "La USB trae el envío 12 de la oficina (50 cambios)" });
                Foto(f, n + " F1 aviso usb");
                Llamar(f, "OcultarAviso");
                for (int i = 1; i <= 5; i++)
                {
                    Llamar(f, "IrAModulo", i);
                    Esperar(300);
                    var m = (ModuloVista)Campo(f, "_modulo");
                    if (m == null) { Falla(n, "no abrió F" + (i + 1)); continue; }
                    var items = (System.Collections.IList)Campo(m, "_items");
                    for (int k = 0; k < items.Count; k++)
                    {
                        if (Campo(items[k], "Vista") == null) continue;
                        m.Mostrar(k);
                        Esperar(350);
                        string nombre = n + " F" + (i + 1) + " " + Campo(items[k], "Nombre");
                        Foto(f, nombre);
                        Pestanas(f, m, nombre);
                        if (m.Actual is SeccionCodigos && n == "10 caja") ProbarCodigos((SeccionCodigos)m.Actual, nombre);
                    }
                }
            }
            catch (Exception ex) { Falla(n, ex.ToString()); }
            finally { if (f != null) { try { PonerCampo(f, "_forzarSalida", true); f.Close(); f.Dispose(); } catch (Exception) { } } }
        }

        /// <summary>La vista de códigos de verdad: escanear guarda y pasa al siguiente.</summary>
        static void ProbarCodigos(SeccionCodigos v, string n)
        {
            var p = (Producto)Llamar(v, "Actual");
            ((TextBox)Campo(v, "_escaner")).Text = "7509999000017";
            Llamar(v, "Asignar");
            Esperar(200);
            var q = ProductoRepo.PorCodigo("7509999000017");
            if (p == null || q == null || q.Id != p.Id) Falla(n, "escanear no guardó el código");
            var sig = (Producto)Llamar(v, "Actual");
            if (sig == null || sig.Id == p.Id) Falla(n, "después de escanear no pasó al siguiente");
            Llamar(v, "SinBarras");
            if (!ProductoRepo.Obtener(sig.Id).SinBarras) Falla(n, "«sin código» no se marcó");
        }

        static void Pestanas(Form f, Control raiz, string n)
        {
            foreach (var tc in Todos(raiz).OfType<TabControl>().ToList())
                for (int i = 1; i < tc.TabCount; i++) { tc.SelectedIndex = i; Esperar(200); Foto(f, n + " [" + tc.TabPages[i].Text + "]"); }
        }

        static void Form1(string n, Func<Form> crear, Action<Form> recorrer)
        {
            Form f = null;
            try
            {
                f = crear();
                f.StartPosition = FormStartPosition.Manual; f.Location = new Point(0, 0);
                f.Show();
                Esperar(350);
                Foto(f, n);
                if (f.Width > 1366 || f.Height > 728) Falla(n, "no cabe en 1366x768: " + f.Width + "x" + f.Height);
                if (recorrer != null) recorrer(f);
            }
            catch (Exception ex) { Falla(n, ex.ToString()); }
            finally { if (f != null) try { f.Close(); f.Dispose(); } catch (Exception) { } }
        }

        // ------------------------------------------------------------------ captura y reglas
        static IEnumerable<Control> Todos(Control c) { foreach (Control h in c.Controls) { yield return h; foreach (var x in Todos(h)) yield return x; } }

        static void Foto(Form f, string n)
        {
            _pantallas++;
            Esperar(150);
            f.Refresh();
            foreach (var ch in Path.GetInvalidFileNameChars()) n = n.Replace(ch, '-');
            try { using (var b = new Bitmap(f.Width, f.Height)) { if (!(f is FormCerrando)) f.Activate(); Esperar(120); using (var g = Graphics.FromImage(b)) g.CopyFromScreen(f.Location, Point.Empty, f.Size); b.Save(Path.Combine(_salida, n + ".png"), ImageFormat.Png); } }
            catch (Exception ex) { Falla(n, "captura: " + ex.Message); }
            Revisar(f, n);
        }

        static string Q(Control c)
        {
            string t = (c.Text ?? "").Replace("\r", "").Replace("\n", "/");
            return c.GetType().Name + " «" + (t.Length > 42 ? t.Substring(0, 42) + "…" : t) + "» " + c.Bounds.X + "," + c.Bounds.Y + " " + c.Width + "x" + c.Height;
        }
        static bool Visible(Control c) { return c.Visible && c.Width > 0 && c.Height > 0; }
        static bool EsCampo(Control c) { return c is TextBox || c is ComboBox || c is NumericUpDown || c is DateTimePicker; }

        /// <summary>El rectángulo que de verdad ocupa: el de un Label es su texto, no su caja.</summary>
        static Rectangle Ocupa(Control c)
        {
            var l = c as Label;
            if (l == null || l.Text == "") return c.Bounds;
            var t = TextRenderer.MeasureText(l.Text, l.Font, l.AutoSize ? Size.Empty : new Size(l.Width, int.MaxValue), l.AutoSize ? TextFormatFlags.Default : TextFormatFlags.WordBreak);
            if (l.AutoSize) return l.Bounds;
            int y = l.Top, x = l.Left;
            var a = l.TextAlign;
            if (a == ContentAlignment.MiddleCenter || a == ContentAlignment.MiddleLeft || a == ContentAlignment.MiddleRight) y += (l.Height - t.Height) / 2;
            if (a == ContentAlignment.MiddleRight || a == ContentAlignment.TopRight) x += l.Width - t.Width;
            return new Rectangle(x, y, Math.Min(t.Width, l.Width), Math.Min(t.Height, l.Height));
        }

        static readonly HashSet<string> _ya = new HashSet<string>();
        static void Mal(string n, string regla, string texto) { if (_ya.Add(regla + texto)) Falla(n, regla + ": " + texto); }

        static void Revisar(Form f, string n)
        {
            foreach (var padre in new[] { (Control)f }.Concat(Todos(f)))
            {
                if (!Visible(padre) || padre is DataGridView || padre is NumericUpDown || padre is DateTimePicker || padre is TabControl) continue;
                var hijos = padre.Controls.Cast<Control>().Where(Visible).ToList();
                var area = padre.ClientRectangle;
                bool desliza = padre is ScrollableControl && ((ScrollableControl)padre).AutoScroll;
                for (int i = 0; i < hijos.Count; i++)
                {
                    var a = hijos[i]; var ra = Ocupa(a);
                    // 1) nada fuera de su contenedor
                    if (!desliza && a.Dock == DockStyle.None && (ra.Right > area.Right + 1 || ra.Bottom > area.Bottom + 1 || ra.Left < -1 || ra.Top < -1)) Mal(n, "SE SALE de " + padre.GetType().Name + " " + area.Width + "x" + area.Height, Q(a));
                    // 2) nada encimado
                    for (int j = i + 1; j < hijos.Count; j++)
                    {
                        var b = hijos[j];
                        if (a.Dock != DockStyle.None && b.Dock != DockStyle.None) continue;
                        var x = Rectangle.Intersect(ra, Ocupa(b));
                        if (x.Width > 1 && x.Height > 1) Mal(n, "ENCIMADO " + x.Width + "x" + x.Height, Q(a) + "  con  " + Q(b));
                    }
                    // 3) el texto cabe
                    if (a is Button && FundidoBoton.Buscar(a) == null) Mal(n, "BOTÓN SIN ESTILO", Q(a));
                    if (a is BotonF) { int w = TextRenderer.MeasureText(((BotonF)a).Nombre, a.Font, Size.Empty, TextFormatFlags.NoPadding).Width; if (w > a.Width - 4) Mal(n, "TEXTO NO CABE en botón F (" + w + ")", Q(a)); }
                    else if (a is Button && a.Text != "")
                    {
                        var t = TextRenderer.MeasureText(a.Text, a.Font, new Size(a.Width - 6, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                        if (t.Width > a.Width - 4 || t.Height > a.Height - 2) Mal(n, "TEXTO NO CABE en botón (" + t.Width + "x" + t.Height + ")", Q(a));
                        var sola = TextRenderer.MeasureText(a.Text, a.Font, Size.Empty, TextFormatFlags.NoPrefix);
                        if (a.Text.IndexOf('\n') < 0 && sola.Width > a.Width - 6) Mal(n, "TEXTO PARTIDO en botón (pide " + (sola.Width + 8) + " de ancho)", Q(a));
                        if (((Button)a).TextAlign != ContentAlignment.MiddleCenter && ((Button)a).Image == null) Mal(n, "TEXTO SIN CENTRAR", Q(a));
                    }
                    var lb = a as Label;
                    if (lb != null && !lb.AutoSize && !lb.AutoEllipsis && lb.Text != "")
                    {
                        var t = TextRenderer.MeasureText(lb.Text, lb.Font, new Size(lb.ClientSize.Width - lb.Padding.Horizontal, int.MaxValue), TextFormatFlags.WordBreak);
                        if (t.Height > lb.ClientSize.Height + 2 || t.Width > lb.ClientSize.Width + 4) Mal(n, "TEXTO CORTADO en etiqueta (" + t.Width + "x" + t.Height + ")", Q(a));
                    }
                    if ((a is CheckBox || a is RadioButton) && !a.AutoSize) { var t = TextRenderer.MeasureText(a.Text, a.Font); if (t.Width + 20 > a.Width) Mal(n, "TEXTO CORTADO en casilla", Q(a)); }
                    // 4) etiqueta y campo en el mismo renglón: centrados entre sí
                    if (a is Label && a.Text.TrimEnd().EndsWith(":"))
                    {
                        var campo = hijos.Where(EsCampo).Where(c => c.Left >= a.Left && c.Top < a.Bottom && c.Bottom > a.Top).OrderBy(c => c.Left).FirstOrDefault();
                        if (campo != null && Math.Abs((ra.Top + ra.Bottom) / 2 - (campo.Top + campo.Bottom) / 2) > 3) Mal(n, "NO CENTRADO con su campo (" + ((ra.Top + ra.Bottom) / 2 - (campo.Top + campo.Bottom) / 2) + " px)", Q(a) + "  con  " + Q(campo));
                    }
                }
                // 5) botones del mismo renglón: parejos
                var bot = hijos.OfType<Button>().Where(b => b.Dock == DockStyle.None || padre is FlowLayoutPanel).ToList();
                for (int i = 0; i < bot.Count; i++)
                    for (int j = i + 1; j < bot.Count; j++)
                    {
                        var a = bot[i]; var b = bot[j];
                        if (a.Top >= b.Bottom || b.Top >= a.Bottom) continue;
                        if (a.Top != b.Top || a.Height != b.Height) Mal(n, "BOTONES DISPAREJOS", Q(a) + "  y  " + Q(b));
                    }
            }
            // 6) encabezados de tabla completos
            foreach (var g in Todos(f).OfType<DataGridView>().Where(Visible))
                foreach (DataGridViewColumn c in g.Columns)
                    if (c.Visible && c.HeaderText != "" && TextRenderer.MeasureText(c.HeaderText, g.ColumnHeadersDefaultCellStyle.Font ?? g.Font).Width > c.Width - 4) Mal(n, "ENCABEZADO CORTADO", c.HeaderText + " (" + c.Width + " px)");
            foreach (var tc in Todos(f).OfType<TabControl>().Where(Visible))
            { var u = tc.TabCount > 0 ? tc.GetTabRect(tc.TabCount - 1) : Rectangle.Empty; if (u.Right > tc.Width) Mal(n, "PESTAÑAS NO CABEN", tc.TabPages[tc.TabCount - 1].Text); }
        }
    }
}
