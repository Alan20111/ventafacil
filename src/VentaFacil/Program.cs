using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Forms.Pos;
using VentaFacil.UI;

namespace VentaFacil
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Primero: que se encuentre SQLite aunque se ejecute el .exe suelto
            Bibliotecas.Preparar();
            // Antes del mutex: si se abre la copia instalada, esta termina y no choca con ella
            if (!Instalacion.Asegurar()) return;
            Arrancar();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        static void Arrancar()
        {
            bool unica;
            using (var mutex = new Mutex(true, "VentaFacil_PDV_Unico", out unica))
            {
                if (!unica)
                {
                    MessageBox.Show("VentaFácil ya está abierto.", "VentaFácil", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => Ui.Error(e.Exception, "Ocurrió un error inesperado. La venta en curso no se perdió.");
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Error(e.ExceptionObject as Exception, "No controlado");

                try
                {
                    Db.Inicializar();
                    Config.Cargar();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Inicio");
                    MessageBox.Show("No se pudo abrir la base de datos:\n\n" + ex.Message, "VentaFácil", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Respaldo automático al iniciar (uno por día como mínimo)
                try
                {
                    string marca = Path.Combine(Db.Carpeta, "respaldos", "ultimo.txt");
                    string hoy = DateTime.Today.ToString("yyyyMMdd");
                    if (!File.Exists(marca) || File.ReadAllText(marca) != hoy)
                    {
                        Db.Respaldar();
                        File.WriteAllText(marca, hoy);
                    }
                }
                catch (Exception ex) { Log.Error(ex, "Respaldo automático"); }

                using (var login = new FormLogin(false, null))
                {
                    if (login.ShowDialog() != DialogResult.OK) return;
                    Sesion.Usuario = login.UsuarioValido;
                }
                // Primer arranque: detectar la impresora y probarla
                if (!Config.Actual.ImpresoraRevisada && Sesion.Usuario.EsAdmin)
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
    }

    /// <summary>
    /// Si System.Data.SQLite.dll no está junto al .exe (se descargó solo el .exe),
    /// la saca de los recursos internos a una carpeta y la carga desde ahí.
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
            using (var s = typeof(Program).Assembly.GetManifestResourceStream(recurso))
            {
                if (s == null) throw new IOException("Falta el recurso " + recurso);
                if (File.Exists(destino) && new FileInfo(destino).Length == s.Length) return;
                Directory.CreateDirectory(Path.GetDirectoryName(destino));
                using (var f = File.Create(destino)) s.CopyTo(f);
            }
        }
    }

    public static class Log
    {
        public static void Error(Exception ex, string contexto)
        {
            try
            {
                string carpeta = Db.Carpeta ?? AppDomain.CurrentDomain.BaseDirectory;
                File.AppendAllText(Path.Combine(carpeta, "errores.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + contexto + "] " + (ex == null ? "" : ex.ToString()) + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }
}
