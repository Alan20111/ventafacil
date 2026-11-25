using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using VentaFacil.Core;

namespace VentaFacil.Data
{
    /// <summary>
    /// Copia de seguridad continua: después de cada venta, cobro, movimiento o cambio de producto
    /// se actualiza respaldos/ultimo.db. Se hace en segundo plano (no frena la caja) y varios
    /// cambios seguidos se juntan en una sola copia. Llamar Pedir() DESPUÉS de confirmar la transacción.
    /// </summary>
    public static class Respaldo
    {
        static readonly object _cerrojo = new object();
        static readonly AutoResetEvent _senal = new AutoResetEvent(false);
        static volatile bool _pendiente;
        static Thread _hilo;

        public const string NombreUltimo = "ultimo.db";

        public static void Iniciar()
        {
            if (_hilo != null) return;
            _hilo = new Thread(Bucle) { IsBackground = true, Name = "VentaFacil-respaldo" };
            _hilo.Start();
        }

        public static void Pedir()
        {
            _pendiente = true;
            _senal.Set();
        }

        /// <summary>Al cerrar el programa: si hay un cambio sin copiar, se copia ya.</summary>
        public static void Final()
        {
            try { if (_pendiente) { _pendiente = false; Escribir(); } }
            catch (Exception ex) { Log.Error(ex, "Respaldo al cerrar"); }
        }

        static void Bucle()
        {
            while (true)
            {
                _senal.WaitOne();
                Thread.Sleep(250); // junta cambios seguidos en una sola copia
                if (!_pendiente) continue;
                _pendiente = false;
                try { Escribir(); }
                catch (Exception ex) { Log.Error(ex, "Respaldo automático"); }
            }
        }

        static void Escribir()
        {
            lock (_cerrojo)
            {
                string carpeta = Path.Combine(Db.Carpeta, "respaldos");
                Directory.CreateDirectory(carpeta);
                string tmp = Path.Combine(carpeta, "ultimo.tmp");
                string fin = Path.Combine(carpeta, NombreUltimo);
                if (File.Exists(tmp)) File.Delete(tmp);
                using (var origen = Db.Abrir())
                using (var dest = new SQLiteConnection("Data Source=" + tmp + ";Version=3;"))
                {
                    dest.Open();
                    origen.BackupDatabase(dest, "main", "main", -1, null, 0);
                }
                // Primero se escribe completo en un temporal; solo después reemplaza al bueno.
                if (File.Exists(fin)) File.Delete(fin);
                File.Move(tmp, fin);
                Exportar(fin, "VentaFacil_ultimo.db");
            }
        }

        /// <summary>
        /// Copia el respaldo a la carpeta elegida en Configuración (p. ej. escritorio o USB).
        /// Si la carpeta no está disponible (USB quitada), se anota en el registro y se sigue.
        /// </summary>
        public static bool Exportar(string archivo, string nombreDestino)
        {
            string carpeta = Config.Actual == null ? "" : Config.Actual.CarpetaRespaldo;
            if (string.IsNullOrEmpty(carpeta)) return true;
            try
            {
                Directory.CreateDirectory(carpeta);
                File.Copy(archivo, Path.Combine(carpeta, nombreDestino), true);
                return true;
            }
            catch (Exception ex) { Log.Error(ex, "Copia a carpeta de respaldo " + carpeta); return false; }
        }

        /// <summary>Uno por día en la carpeta elegida (el último del día reemplaza a los anteriores). Se conservan 60.</summary>
        public static string NombreDelDia { get { return "VentaFacil_" + DateTime.Now.ToString("yyyy-MM-dd") + ".db"; } }

        public class Cierre { public string Archivo = ""; public bool CopiaBien = true; public string Carpeta = ""; }

        /// <summary>true cuando ya se respaldó en este cierre (para no repetirlo).</summary>
        public static bool CierreHecho { get; private set; }

        /// <summary>
        /// Respaldo obligatorio al cerrar: copia completa, COMPROBADA (se vuelve a leer), la copia continua y la carpeta elegida.
        /// Lanza un error si el respaldo de esta computadora no quedó bien; si solo falló la carpeta elegida, lo dice en CopiaBien.
        /// </summary>
        public static Cierre AlCerrar(Action<string> paso)
        {
            if (paso == null) paso = t => { };
            _pendiente = false;
            var r = new Cierre { Carpeta = Config.Actual == null ? "" : Config.Actual.CarpetaRespaldo };
            paso("Haciendo respaldo…");
            r.Archivo = Db.Respaldar();
            paso("Comprobando el respaldo…");
            string mal = Salud.Revisar(r.Archivo);
            if (mal != "ok")
            {
                try { File.Delete(r.Archivo); } catch (IOException) { }
                throw new IOException("El respaldo no se pudo comprobar: " + mal);
            }
            lock (_cerrojo)
            {
                string fin = Path.Combine(Db.Carpeta, "respaldos", NombreUltimo);
                File.Copy(r.Archivo, fin, true);
            }
            if (!string.IsNullOrEmpty(r.Carpeta))
            {
                paso("Copiando a la carpeta de respaldos…");
                r.CopiaBien = Exportar(r.Archivo, "VentaFacil_ultimo.db") && Exportar(r.Archivo, NombreDelDia);
                if (r.CopiaBien)
                    try
                    {
                        var l = new System.Collections.Generic.List<string>(Directory.GetFiles(r.Carpeta, "VentaFacil_????-??-??.db"));
                        l.Sort(StringComparer.Ordinal);
                        for (int i = 0; i < l.Count - 60; i++) File.Delete(l[i]);
                    }
                    catch (Exception ex) { Log.Error(ex, "Limpiar respaldos viejos"); }
            }
            CierreHecho = true;
            return r;
        }
    }
}
