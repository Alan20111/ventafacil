using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace VentaFacil.Data
{
    /// <summary>
    /// Revisión de la base de datos y restauración desde un respaldo bueno.
    /// Nunca borra la base dañada: la copia aparte (respaldos/danada_*.db) antes de reemplazarla.
    /// </summary>
    public static class Salud
    {
        /// <summary>"ok" si la base se puede leer completa; si no, la descripción del problema.</summary>
        public static string Revisar(string archivo)
        {
            if (!File.Exists(archivo)) return "No existe el archivo de datos.";
            try
            {
                using (var cn = new SQLiteConnection("Data Source=" + archivo + ";Version=3;"))
                {
                    cn.Open();
                    using (var cmd = new SQLiteCommand("PRAGMA quick_check", cn))
                    {
                        string r = Convert.ToString(cmd.ExecuteScalar());
                        return string.Equals(r, "ok", StringComparison.OrdinalIgnoreCase) ? "ok" : r;
                    }
                }
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Respaldos que se pueden leer, del más reciente al más antiguo.</summary>
        public static List<string> RespaldosBuenos()
        {
            var lista = new List<string>();
            string carpeta = Path.Combine(Db.Carpeta, "respaldos");
            if (!Directory.Exists(carpeta)) return lista;
            var archivos = new List<string>(Directory.GetFiles(carpeta, "*.db"));
            archivos.Sort((a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
            foreach (string a in archivos)
            {
                if (Path.GetFileName(a).StartsWith("danada_", StringComparison.OrdinalIgnoreCase)) continue;
                if (Revisar(a) == "ok") lista.Add(a);
            }
            return lista;
        }

        /// <summary>
        /// Reemplaza la base dañada por el respaldo. La base dañada (y su -wal) se guardan
        /// en respaldos/danada_AAAAMMDD_HHMMSS.db antes de tocar nada. Devuelve esa ruta.
        /// </summary>
        public static string Restaurar(string respaldo)
        {
            SQLiteConnection.ClearAllPools();
            string carpeta = Path.Combine(Db.Carpeta, "respaldos");
            Directory.CreateDirectory(carpeta);
            string guardada = Path.Combine(carpeta, "danada_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db");
            if (File.Exists(Db.Archivo)) File.Copy(Db.Archivo, guardada, true);
            if (File.Exists(Db.Archivo + "-wal")) File.Copy(Db.Archivo + "-wal", guardada + "-wal", true);

            foreach (string sufijo in new[] { "", "-wal", "-shm" })
                if (File.Exists(Db.Archivo + sufijo)) File.Delete(Db.Archivo + sufijo);

            File.Copy(respaldo, Db.Archivo, true);
            string r = Revisar(Db.Archivo);
            if (r != "ok") throw new IOException("El respaldo tampoco se pudo leer: " + r);
            return guardada;
        }
    }
}
