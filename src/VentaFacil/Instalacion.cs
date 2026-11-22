using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace VentaFacil
{
    /// <summary>
    /// Instalación desde un solo .exe: al abrirlo se copia a C:\VentaFacil, crea el acceso
    /// directo en el escritorio y arranca desde ahí. Los datos quedan en C:\VentaFacil\datos.
    /// </summary>
    static class Instalacion
    {
        public const string Carpeta = @"C:\VentaFacil";

        /// <summary>
        /// Devuelve true si el programa debe seguir en este proceso.
        /// Devuelve false si ya se abrió la copia instalada (este proceso termina).
        /// </summary>
        public static bool Asegurar()
        {
            try
            {
                string actual = Path.GetFullPath(Application.ExecutablePath);
                string destino = Path.Combine(Carpeta, "VentaFacil.exe");

                if (string.Equals(actual, Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase))
                {
                    CrearAcceso(destino);
                    return true;
                }

                Directory.CreateDirectory(Carpeta);
                bool distinta = !File.Exists(destino) || new FileInfo(actual).Length != new FileInfo(destino).Length;
                if (distinta)
                {
                    try { File.Copy(actual, destino, true); }
                    catch (IOException) { /* está abierta: se usa la copia que ya existe */ }
                }
                if (!File.Exists(destino)) return true; // no se pudo instalar: se queda corriendo aquí

                CrearAcceso(destino);
                Process.Start(destino);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Instalación");
                return true;
            }
        }

        /// <summary>Acceso directo en el escritorio (WScript.Shell por COM, sin dependencias).</summary>
        static void CrearAcceso(string destino)
        {
            try
            {
                string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "VentaFacil.lnk");
                if (File.Exists(lnk)) return;
                Type tipo = Type.GetTypeFromProgID("WScript.Shell");
                if (tipo == null) return;
                object shell = Activator.CreateInstance(tipo);
                object acceso = tipo.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type t = acceso.GetType();
                t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { destino });
                t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { Carpeta });
                t.InvokeMember("Description", System.Reflection.BindingFlags.SetProperty, null, acceso, new object[] { "VentaFácil - Punto de venta" });
                t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, acceso, null);
            }
            catch (Exception ex) { Log.Error(ex, "Acceso directo"); }
        }
    }
}
