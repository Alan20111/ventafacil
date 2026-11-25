using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Forms.Licenciamiento;
using VentaFacil.Forms.Pos;
using VentaFacil.Forms.Reparar;
using VentaFacil.UI;

namespace VentaFacil
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Primero: que se encuentre SQLite aunque se ejecute el .exe suelto
            Bibliotecas.Preparar();
            // Acceso "Reparar VentaFácil": no abre el punto de venta
            if (args.Length > 0 && args[0] == "--reparar") { FormReparar.Ejecutar(); return; }
            // Antes del mutex: si se abre la copia instalada, esta termina y no choca con ella
            string archivo = AbrirArchivo.De(args);   // doble clic en un envío o un acuse
            if (!Instalacion.Asegurar(archivo)) return;
            Arrancar(archivo);
        }

        /// <summary>
        /// Primer arranque: pregunta dónde guardar la copia de seguridad.
        /// Por defecto, el escritorio (carpeta VentaFacil_Respaldos).
        /// </summary>
        static void ElegirCarpetaRespaldo()
        {
            if (Config.Actual.CarpetaRespaldoRevisada) return;
            string escritorio = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VentaFacil_Respaldos");
            string elegida = escritorio;
            if (!Ui.Confirmar("Primera vez: los respaldos del programa se guardarán en:\n\n" + escritorio +
                "\n\n¿Usar esta carpeta?\n(Si elige No, podrá escoger otra, por ejemplo una memoria USB.)"))
            {
                using (var d = new FolderBrowserDialog { Description = "Elija la carpeta para los respaldos", SelectedPath = escritorio })
                    if (d.ShowDialog() == DialogResult.OK) elegida = d.SelectedPath;
            }
            var c = Config.Actual.Copia();
            c.CarpetaRespaldo = elegida;
            c.CarpetaRespaldoRevisada = true;
            c.Guardar();
            Config.Cargar();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void Arrancar(string porAbrir)
        {
            bool unica;
            using (var mutex = new Mutex(true, "VentaFacil_PDV_Unico", out unica))
            {
                if (!unica)
                {
                    if (porAbrir != null && AbrirArchivo.Mandar(porAbrir)) return;
                    MessageBox.Show("VentaFácil ya está abierto.", "VentaFácil", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                AbrirArchivo.Llego(porAbrir);
                AbrirArchivo.Escuchar();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Ui.Error(e.Exception, "Ocurrió un error inesperado. Sus ventas guardadas no se pierden. Si vuelve a pasar, cierre y abra el programa.");
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error(e.ExceptionObject as Exception, "No controlado");

                bool nueva = false;
                try
                {
                    try { Db.Inicializar(); }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Inicio");
                        // Base dañada: ofrece restaurar el último respaldo bueno (sin borrar nada)
                        if (!FormReparar.RecuperarBase(ex.Message)) return;
                        Db.Inicializar();
                    }
                    Config.Cargar();
                    nueva = PrimeraVez.EsInstalacionNueva();
                    if (nueva)
                    {   // el asistente pregunta la carpeta; mientras, que ya haya respaldos
                        if (!Config.Actual.CarpetaRespaldoRevisada) { var c = Config.Actual.Copia(); c.CarpetaRespaldo = Forms.Admin.FormAsistente.PorDefecto; c.Guardar(); }
                    }
                    else ElegirCarpetaRespaldo();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Inicio tras restaurar");
                    MessageBox.Show("No se pudo abrir la base de datos:\n\n" + ex.Message + "\n\nAbra 'Reparar VentaFácil' en el escritorio.", "VentaFácil", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Licencia: 30 días de prueba, después clave de activación atada al equipo
                if (!FormLicencia.Comprobar()) return;

                // Copia continua después de cada cambio (venta, cobro, movimiento, producto)
                Respaldo.Iniciar();

                // Desde aquí, salga por donde salga, SIEMPRE se respalda al cerrar
                try
                {
                    Trabajar(nueva);
                }
                finally { FormCerrando.Ejecutar(); }
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void Trabajar(bool nueva)
        {
                // Respaldo diario (se conservan los últimos 30 y uno por día)
                try
                {
                    string marca = Path.Combine(Db.Carpeta, "respaldos", "ultimo_diario.txt");
                    string hoy = DateTime.Today.ToString("yyyyMMdd");
                    if (!File.Exists(marca) || File.ReadAllText(marca) != hoy)
                    {
                        string archivo = Db.Respaldar();
                        Respaldo.Exportar(archivo, Respaldo.NombreDelDia);
                        File.WriteAllText(marca, hoy);
                    }
                }
                catch (Exception ex) { Log.Error(ex, "Respaldo automático"); }

                var vv = typeof(Program).Assembly.GetName().Version;
                string version = vv.Major + "." + vv.Minor;
                bool asistente = false;
                if (nueva)
                {   // instalación nueva: se configura todo antes de entrar, empezando por la cuenta del administrador
                    Sesion.Usuario = UsuarioRepo.Validar("admin", "admin");
                    asistente = true;
                    try
                    {
                        using (var f = new Forms.Admin.FormAsistente(true) { StartPosition = FormStartPosition.CenterScreen, ShowInTaskbar = true })
                        {
                            f.ShowDialog();
                            if (f.Restaurado) return;
                            if (f.CuentaLista) PrimeraVez.MarcarVista(version);
                            else Sesion.Usuario = null;      // no creó su cuenta: entra con la de fábrica
                        }
                    }
                    catch (Exception ex) { Log.Error(ex, "Asistente"); Sesion.Usuario = null; }
                }
                if (Sesion.Usuario == null)
                    using (var login = new FormLogin(false, null))
                    {
                        if (login.ShowDialog() != DialogResult.OK) return;
                        Sesion.Usuario = login.UsuarioValido;
                    }
                // Después de actualizar: configurar o seguir igual. Solo a un administrador; si entra un cajero, se queda pendiente.
                if (!asistente && PrimeraVez.HayQuePreguntar(version, Sesion.Usuario))
                {
                    try
                    {
                        DialogResult r;
                        using (var f = new Forms.Admin.FormActualizado(version)) r = f.ShowDialog();
                        if (r == DialogResult.Yes)
                        {
                            asistente = true;
                            using (var f = new Forms.Admin.FormAsistente(false) { StartPosition = FormStartPosition.CenterScreen, ShowInTaskbar = true }) { f.ShowDialog(); if (f.Restaurado) return; }
                        }
                        PrimeraVez.MarcarVista(version);
                        Config.Cargar();
                    }
                    catch (Exception ex) { Log.Error(ex, "Asistente de actualización"); }
                }
                // Primer arranque: detectar la impresora y probarla
                if (!asistente && !Config.Actual.ImpresoraRevisada && Sesion.Usuario.EsAdmin && !Envios.EsOficina)
                {
                    try
                    {
                        FormImpresoras.Configurar(null, true);
                        if (!Config.Actual.ImpresoraRevisada) { var c = Config.Actual.Copia(); c.ImpresoraRevisada = true; c.Guardar(); }
                        Config.Cargar();
                    }
                    catch (Exception ex) { Log.Error(ex, "Detección de impresora"); }
                }
                Application.Run(new FormVenta());
        }
    }

    /// <summary>
    /// Si System.Data.SQLite.dll no está junto al .exe (se descargó solo el .exe),
    /// la saca de los recursos internos a una carpeta y la carga desde ahí.
    /// Si el archivo extraído está dañado (distinto del recurso), lo repone.
    /// </summary>
    static class Bibliotecas
    {
        static string _carpeta;

        public static void Preparar()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(baseDir, "System.Data.SQLite.dll")) && File.Exists(Path.Combine(baseDir, "x86", "SQLite.Interop.dll"))) return;
            try
            {
                string version = typeof(Program).Assembly.GetName().Version.ToString();
                foreach (var candidata in new[] {
                    Path.Combine(baseDir, "lib"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VentaFacil", "lib-" + version) })
                {
                    try
                    {
                        Extraer("lib.System.Data.SQLite.dll", Path.Combine(candidata, "System.Data.SQLite.dll"));
                        Extraer("lib.x86.SQLite.Interop.dll", Path.Combine(candidata, "x86", "SQLite.Interop.dll"));
                        _carpeta = candidata;
                        break;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                if (_carpeta != null)
                    AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
                        e.Name.StartsWith("System.Data.SQLite", StringComparison.OrdinalIgnoreCase)
                            ? System.Reflection.Assembly.LoadFrom(Path.Combine(_carpeta, "System.Data.SQLite.dll"))
                            : null;
            }
            catch (Exception ex) { Log.Error(ex, "Preparar bibliotecas"); }
        }

        static void Extraer(string recurso, string destino)
        {
            byte[] datos;
            using (var s = typeof(Program).Assembly.GetManifestResourceStream(recurso))
            {
                if (s == null) throw new IOException("Falta el recurso " + recurso);
                using (var ms = new MemoryStream()) { s.CopyTo(ms); datos = ms.ToArray(); }
            }
            if (File.Exists(destino) && Instalacion.HashBytes(File.ReadAllBytes(destino)) == Instalacion.HashBytes(datos)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destino));
            File.WriteAllBytes(destino, datos);
        }
    }

    public static class Log
    {
        public static void Error(Exception ex, string contexto)
        {
            try
            {
                string carpeta = Db.Carpeta ?? Instalacion.Carpeta;
                Directory.CreateDirectory(carpeta);
                File.AppendAllText(Path.Combine(carpeta, "errores.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + contexto + "] " + (ex == null ? "" : ex.ToString()) + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }
}
