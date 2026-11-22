using System;
using System.Data.SQLite;
using System.IO;
using System.Threading;

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
            }
        }
    }
}
