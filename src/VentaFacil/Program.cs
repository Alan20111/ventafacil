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
                Application.Run(new FormVenta());
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
