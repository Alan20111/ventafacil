using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace VentaFacil.Data
{
    /// <summary>
    /// Acceso a la base de datos SQLite local. Un solo archivo, sin servidor.
    /// </summary>
    public class BaseDanadaException : Exception
    {
        public BaseDanadaException(string mensaje) : base("La base de datos está dañada: " + mensaje) { }
    }

    public static class Db
    {
        public static string Carpeta { get; private set; }
        public static string Archivo { get; private set; }
        static string _cadena;

        /// <summary>Define carpeta y archivo de datos sin abrir nada. Lo usan el arranque y la reparación.</summary>
        public static void PrepararRutas()
        {
            Carpeta = ElegirCarpetaDatos();
            Archivo = Path.Combine(Carpeta, "ventafacil.db");
            // Synchronous=Full: al cortarse la luz no se pierde ninguna operación ya confirmada
            _cadena = "Data Source=" + Archivo + ";Version=3;Foreign Keys=True;Journal Mode=WAL;Synchronous=Full;BusyTimeout=5000";
        }

        public static void Inicializar()
        {
            PrepararRutas();
            Directory.CreateDirectory(Carpeta);
            bool nueva = !File.Exists(Archivo);
            if (!nueva)
            {
                // Antes de usar la base: si no se puede leer completa, no se abre
                string revision = Salud.Revisar(Archivo);
                if (revision != "ok") throw new BaseDanadaException(revision);
            }
            using (var cn = Abrir())
            {
                Esquema.Crear(cn);
                if (nueva) Esquema.DatosIniciales(cn);
                Esquema.Migrar(cn);
            }
        }

        /// <summary>
        /// Usa la carpeta "datos" junto al .exe si se puede escribir ahí (modo portátil);
        /// si no (p. ej. instalado en Archivos de programa), usa ProgramData.
        /// </summary>
        static string ElegirCarpetaDatos()
        {
            string junto = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "datos");
            try
            {
                Directory.CreateDirectory(junto);
                string prueba = Path.Combine(junto, ".prueba");
                File.WriteAllText(prueba, "ok");
                File.Delete(prueba);
                return junto;
            }
            catch
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VentaFacil");
            }
        }

        public static SQLiteConnection Abrir()
        {
            var cn = new SQLiteConnection(_cadena);
            cn.Open();
            return cn;
        }

        static void Parametros(SQLiteCommand cmd, object[] args)
        {
            if (args == null) return;
            for (int i = 0; i < args.Length; i++)
                cmd.Parameters.AddWithValue("@p" + i, args[i] ?? DBNull.Value);
        }

        public static int Exec(string sql, params object[] args)
        {
            using (var cn = Abrir()) return Exec(cn, null, sql, args);
        }

        public static int Exec(SQLiteConnection cn, SQLiteTransaction tx, string sql, params object[] args)
        {
            using (var cmd = new SQLiteCommand(sql, cn, tx))
            {
                Parametros(cmd, args);
                return cmd.ExecuteNonQuery();
            }
        }

        public static object Scalar(string sql, params object[] args)
        {
            using (var cn = Abrir()) return Scalar(cn, null, sql, args);
        }

        public static object Scalar(SQLiteConnection cn, SQLiteTransaction tx, string sql, params object[] args)
        {
            using (var cmd = new SQLiteCommand(sql, cn, tx))
            {
                Parametros(cmd, args);
                var r = cmd.ExecuteScalar();
                return r == DBNull.Value ? null : r;
            }
        }

        public static long InsertId(SQLiteConnection cn, SQLiteTransaction tx, string sql, params object[] args)
        {
            Exec(cn, tx, sql, args);
            return Convert.ToInt64(Scalar(cn, tx, "SELECT last_insert_rowid()"));
        }

        public static DataTable Tabla(string sql, params object[] args)
        {
            using (var cn = Abrir())
            using (var cmd = new SQLiteCommand(sql, cn))
            {
                Parametros(cmd, args);
                var dt = new DataTable();
                using (var da = new SQLiteDataAdapter(cmd)) da.Fill(dt);
                return dt;
            }
        }

        public static List<T> Lista<T>(string sql, Func<IDataRecord, T> mapear, params object[] args)
        {
            using (var cn = Abrir()) return Lista(cn, null, sql, mapear, args);
        }

        public static List<T> Lista<T>(SQLiteConnection cn, SQLiteTransaction tx, string sql, Func<IDataRecord, T> mapear, params object[] args)
        {
            var lista = new List<T>();
            using (var cmd = new SQLiteCommand(sql, cn, tx))
            {
                Parametros(cmd, args);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) lista.Add(mapear(rd));
            }
            return lista;
        }

        // ---------- lectura segura de columnas ----------
        public static string S(IDataRecord r, string col) { var v = r[col]; return v == DBNull.Value ? "" : Convert.ToString(v); }
        public static long L(IDataRecord r, string col) { var v = r[col]; return v == DBNull.Value ? 0 : Convert.ToInt64(v); }
        public static decimal D(IDataRecord r, string col) { return Core.Dinero.Leer(r[col]); }
        /// <summary>Convierte un valor leído de la base (REAL) a decimal sin ruido de decimales.</summary>
        public static decimal Dec(object v) { return Core.Dinero.Leer(v); }
        public static bool B(IDataRecord r, string col) { var v = r[col]; return v != DBNull.Value && Convert.ToInt64(v) != 0; }
        public static DateTime F(IDataRecord r, string col) { var v = r[col]; return v == DBNull.Value ? DateTime.MinValue : Fecha(Convert.ToString(v)); }

        public const string FormatoFecha = "yyyy-MM-dd HH:mm:ss";
        public static string Fecha(DateTime d) { return d.ToString(FormatoFecha, System.Globalization.CultureInfo.InvariantCulture); }
        public static DateTime Fecha(string s)
        {
            DateTime d;
            if (DateTime.TryParseExact(s, FormatoFecha, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d)) return d;
            return DateTime.MinValue;
        }
        public static string Ahora() { return Fecha(DateTime.Now); }

        /// <summary>Copia de seguridad del archivo de datos. Conserva las últimas 30 y, además, la última de cada uno de los 30 días más recientes.</summary>
        public static string Respaldar(string destino = null)
        {
            string carpeta = destino ?? Path.Combine(Carpeta, "respaldos");
            Directory.CreateDirectory(carpeta);
            string archivo = Path.Combine(carpeta, "ventafacil_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db");
            using (var origen = Abrir())
            using (var dest = new SQLiteConnection("Data Source=" + archivo + ";Version=3;"))
            {
                dest.Open();
                origen.BackupDatabase(dest, "main", "main", -1, null, 0);
            }
            if (destino == null)
            {
                var archivos = new List<string>(Directory.GetFiles(carpeta, "ventafacil_*.db"));
                foreach (var viejo in Sobrantes(archivos)) try { File.Delete(viejo); } catch { }
            }
            return archivo;
        }

        /// <summary>
        /// Cuáles respaldos ya se pueden borrar. Como hay uno en cada cierre, quedarse solo con «los últimos 30» podría
        /// cubrir unos pocos días: por eso también se guarda el último de cada día.
        /// </summary>
        public static List<string> Sobrantes(IEnumerable<string> archivos)
        {
            var l = new List<string>(archivos);
            l.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(b), Path.GetFileName(a)));   // del más nuevo al más viejo
            var borrar = new List<string>();
            var dias = new HashSet<string>();
            for (int i = 0; i < l.Count; i++)
            {
                string n = Path.GetFileName(l[i]);
                string dia = n.Length >= 19 ? n.Substring(0, 19) : n;      // ventafacil_AAAAMMDD
                bool ultimoDelDia = dias.Count < 30 && dias.Add(dia);
                if (i >= 30 && !ultimoDelDia) borrar.Add(l[i]);
            }
            return borrar;
        }
    }
}
