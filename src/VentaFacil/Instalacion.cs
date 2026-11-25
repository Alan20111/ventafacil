using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VentaFacil
{
    /// <summary>
    /// Instalación desde un solo .exe: al abrirlo se copia a C:\VentaFacil, crea los accesos
    /// del escritorio y arranca desde ahí. Los datos quedan en C:\VentaFacil\datos.
    /// </summary>
    static class Instalacion
    {
        public const string Carpeta = @"C:\VentaFacil";
        public const string NombreReparar = "VentaFácil - Reparar";

        /// <summary>
        /// Devuelve true si el programa debe seguir en este proceso.
        /// Devuelve false si ya se abrió la copia instalada (este proceso termina).
        /// La copia se compara por contenido (no solo por tamaño), así se repone si quedó dañada.
        /// </summary>
        public static bool Asegurar(string archivo = null)
        {
            try
            {
                string actual = Path.GetFullPath(Application.ExecutablePath);
                string destino = Path.GetFullPath(Path.Combine(Carpeta, "VentaFacil.exe"));

                if (string.Equals(actual, destino, StringComparison.OrdinalIgnoreCase))
                {
                    CrearAccesos();
                    Asociar(destino);
                    return true;
                }

                Directory.CreateDirectory(Carpeta);
                if (!File.Exists(destino) || Hash(actual) != Hash(destino))
                {
                    try { File.Copy(actual, destino, true); }
                    catch (IOException) { /* está abierta: se usa la copia que ya existe */ }
                }
                if (!File.Exists(destino)) return true; // no se pudo instalar: se queda corriendo aquí

                CrearAccesos();
                if (archivo == null) Process.Start(destino); else Process.Start(destino, "\"" + archivo + "\"");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Instalación");
                return true;
            }
        }

        /// <summary>Usado por "Reparar VentaFácil": repone la copia instalada si falta o está dañada.</summary>
        public static string Reinstalar()
        {
            try
            {
                string actual = Path.GetFullPath(Application.ExecutablePath);
                string destino = Path.Combine(Carpeta, "VentaFacil.exe");
                Directory.CreateDirectory(Carpeta);
                if (string.Equals(actual, Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase))
                {
                    CrearAccesos();
                    return "[OK] El programa corre desde " + Carpeta;
                }
                if (File.Exists(destino) && Hash(destino) == Hash(actual))
                {
                    CrearAccesos();
                    return "[OK] La copia instalada es correcta.";
                }
                File.Copy(actual, destino, true);
                CrearAccesos();
                return "[REPARADO] Se instaló una copia nueva en " + Carpeta + ". Use el acceso del escritorio.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Reinstalar");
                return "[ERROR] No se pudo reinstalar: " + ex.Message + ". Cierre VentaFácil e intente de nuevo.";
            }
        }

        [DllImport("shell32.dll")] static extern void SHChangeNotify(int evento, int banderas, IntPtr a, IntPtr b);

        /// <summary>Doble clic en un envío (.vfenvio) o acuse (.vfacuse) abre VentaFácil. Solo para este usuario de Windows; no pide permisos.</summary>
        static void Asociar(string exe)
        {
            try
            {
                string comando = "\"" + exe + "\" \"%1\"";
                bool cambio = false;
                using (var clases = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
                {
                    cambio |= Poner(clases, "VentaFacil.Archivo", "Archivo de VentaFácil");
                    cambio |= Poner(clases, @"VentaFacil.Archivo\DefaultIcon", exe + ",0");
                    cambio |= Poner(clases, @"VentaFacil.Archivo\shell\open\command", comando);
                    foreach (var ext in new[] { ".vfenvio", ".vfacuse" }) cambio |= Poner(clases, ext, "VentaFacil.Archivo");
                }
                if (cambio) SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);   // que el Explorador se entere
            }
            catch (Exception ex) { Log.Error(ex, "Asociar archivos de envío"); }
        }

        static bool Poner(RegistryKey clases, string clave, string valor)
        {
            using (var k = clases.CreateSubKey(clave))
            {
                if (Convert.ToString(k.GetValue("")) == valor) return false;
                k.SetValue("", valor);
                return true;
            }
        }

        /// <summary>Accesos del escritorio: abrir el programa y "Reparar VentaFácil".</summary>
        public static void CrearAccesos()
        {
            string destino = Path.Combine(Carpeta, "VentaFacil.exe");
            if (!File.Exists(destino)) return;
            Acceso("VentaFacil.lnk", destino, "", "VentaFácil - Punto de venta");
            Acceso(NombreReparar + ".lnk", destino, "--reparar", "Revisar y reparar VentaFácil sin perder datos");
        }

        /// <summary>Acceso directo por WScript.Shell (COM), sin dependencias extra.</summary>
        static void Acceso(string nombre, string destino, string argumentos, string descripcion)
        {
            try
            {
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), nombre);
                Type tipo = Type.GetTypeFromProgID("WScript.Shell");
                if (tipo == null) return;
                object shell = Activator.CreateInstance(tipo);
                object acceso = tipo.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type t = acceso.GetType();
                t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { destino });
                t.InvokeMember("Arguments", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { argumentos });
                t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { Carpeta });
                t.InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { destino + ",0" });
                t.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { descripcion });
                t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, acceso, null);
            }
            catch (Exception ex) { Log.Error(ex, "Acceso directo " + nombre); }
        }

        public static string Hash(string ruta)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(ruta))
                return Convert.ToBase64String(sha.ComputeHash(f));
        }

        public static string HashBytes(byte[] datos)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(datos));
        }
    }
}
