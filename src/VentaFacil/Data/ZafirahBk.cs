using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace VentaFacil.Data
{
    public class ArchivoRespaldo
    {
        public string Logico = "", Fisico = "", Tipo = "";   // Tipo: D = datos, L = registro (log)
        public long Tamano;
    }

    public class InfoRespaldo
    {
        public string Base = "", Servidor = "", Fecha = "";
        public int Posicion = 1;                              // cuál de los respaldos del archivo (RESTORE … WITH FILE = n)
        public int VersionMayor, VersionMenor, VersionBase, Compatibilidad;
        public long TamanoBytes;
        public List<ArchivoRespaldo> Archivos = new List<ArchivoRespaldo>();
        public string Aviso = "";                             // problema de versión detectado de antemano ("" = nada)
        public bool Imposible;                                // el aviso es definitivo: no se podrá restaurar en este servidor

        public string Resumen()
        {
            var sb = new StringBuilder();
            sb.Append("Base «").Append(Base == "" ? "?" : Base).Append("»");
            if (VersionMayor > 0) sb.Append("  ·  hecho con ").Append(ZafirahBk.NombreVersion(VersionMayor, VersionMenor));
            if (TamanoBytes > 0) sb.Append("  ·  ").Append(TamanoBytes >= 1073741824L ? (TamanoBytes / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB" : (TamanoBytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB");
            if (Fecha != "") sb.Append("  ·  ").Append(Fecha);
            sb.Append("  ·  ").Append(Archivos.Count).Append(Archivos.Count == 1 ? " archivo" : " archivos");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Leer un respaldo (.BK / .BAK) de SQL Server SIN tocar la base de Zafirah: se RESTAURA en una base TEMPORAL
    /// (prefijo vf_import_tmp_), se leen los productos con el mismo asistente y la temporal se borra. Un respaldo
    /// nativo de SQL Server no se puede leer de otra forma: hace falta una instancia de SQL Server (Express sirve).
    ///
    /// SEGURIDAD: lo único que se puede borrar son bases cuyo nombre empiece con vf_import_tmp_ (EsTemporal);
    /// nunca la base real de Zafirah. Los archivos de la base temporal se crean en la misma carpeta de datos que
    /// usa SQL Server para master, que es donde el servicio seguro puede escribir.
    /// </summary>
    public static class ZafirahBk
    {
        public const string Prefijo = "vf_import_tmp_";
        static readonly Regex NombreTemporalValido = new Regex("^vf_import_tmp_[A-Za-z0-9_]+$");

        public static string NombreTemporal() { return Prefijo + Guid.NewGuid().ToString("N").Substring(0, 8); }
        public static bool EsTemporal(string nombre) { return nombre != null && NombreTemporalValido.IsMatch(nombre); }

        /// <summary>Texto como literal de T-SQL (N'…') con las comillas duplicadas.</summary>
        public static string Literal(string s) { return "N'" + (s ?? "").Replace("'", "''") + "'"; }

        // ------------------------------------------------------------------ versiones
        public static string NombreVersion(int mayor, int menor)
        {
            switch (mayor)
            {
                case 8: return "SQL Server 2000";
                case 9: return "SQL Server 2005";
                case 10: return menor >= 50 ? "SQL Server 2008 R2" : "SQL Server 2008";
                case 11: return "SQL Server 2012";
                case 12: return "SQL Server 2014";
                case 13: return "SQL Server 2016";
                case 14: return "SQL Server 2017";
                case 15: return "SQL Server 2019";
                case 16: return "SQL Server 2022";
            }
            return "SQL Server " + mayor + "." + menor;
        }

        /// <summary>
        /// Reglas conocidas de qué versión puede restaurar a cuál. Si SQL Server dice otra cosa al restaurar, manda SQL Server.
        /// </summary>
        public static bool Compatible(int mayorRespaldo, int mayorServidor, out string aviso, out bool imposible)
        {
            aviso = ""; imposible = false;
            if (mayorRespaldo <= 0 || mayorServidor <= 0) return true;
            if (mayorRespaldo > mayorServidor)
            {
                aviso = "El respaldo fue hecho con una versión MÁS NUEVA de SQL Server (" + NombreVersion(mayorRespaldo, 0) + ") que la de este servidor (" + NombreVersion(mayorServidor, 0) + "). No se puede restaurar aquí: use un SQL Server igual o más nuevo.";
                imposible = true; return false;
            }
            if (mayorRespaldo <= 8 && mayorServidor >= 11)
            {
                aviso = "El respaldo es de " + NombreVersion(mayorRespaldo, 0) + " y este servidor es " + NombreVersion(mayorServidor, 0) + ", que ya no acepta respaldos de SQL Server 2000. Hay que restaurarlo en un SQL Server 2005, 2008 o 2008 R2 (Express sirve).";
                imposible = true; return false;
            }
            if (mayorRespaldo <= 9 && mayorServidor >= 15)
            {
                aviso = "El respaldo es de " + NombreVersion(mayorRespaldo, 0) + " y este servidor es " + NombreVersion(mayorServidor, 0) + ". Puede que no lo acepte; se puede intentar, y si falla use una versión de SQL Server más antigua.";
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ comandos
        static string RutaDe(string carpeta, string archivo)
        {
            carpeta = carpeta ?? "";
            return carpeta.TrimEnd('\\', '/') + "\\" + archivo;
        }

        /// <summary>El RESTORE a una base temporal, con cada archivo movido a la carpeta de datos del servidor.</summary>
        public static string ComandoRestaurar(string nombreTmp, string rutaBk, int posicion, IList<ArchivoRespaldo> archivos, string carpetaDatos)
        {
            if (!EsTemporal(nombreTmp)) throw new ArgumentException("El nombre de la base temporal debe empezar con " + Prefijo + ".");
            var sb = new StringBuilder();
            sb.Append("RESTORE DATABASE ").Append(ZafirahSql.Cita(nombreTmp)).Append("\nFROM DISK = ").Append(Literal(rutaBk)).Append("\nWITH FILE = ").Append(Math.Max(1, posicion));
            int datos = 0, registros = 0;
            foreach (var a in archivos)
            {
                string ext = a.Tipo == "L" ? (registros++ == 0 ? ".ldf" : "_" + registros + ".ldf") : (datos++ == 0 ? ".mdf" : "_" + datos + ".ndf");
                sb.Append(",\n     MOVE ").Append(Literal(a.Logico)).Append(" TO ").Append(Literal(RutaDe(carpetaDatos, nombreTmp + ext)));
            }
            sb.Append(",\n     REPLACE, STATS = 5");
            return sb.ToString();
        }

        /// <summary>Los comandos para revisar un respaldo a mano (osql / Management Studio).</summary>
        public static string ComandosRevisar(string rutaBk)
        {
            return "RESTORE HEADERONLY FROM DISK = " + Literal(rutaBk) + "\nGO\nRESTORE FILELISTONLY FROM DISK = " + Literal(rutaBk) + "\nGO";
        }

        static readonly Regex Porciento = new Regex(@"(\d+)\s*(percent|por ciento|%)", RegexOptions.IgnoreCase);

        /// <summary>"10 percent processed." → 10 (lo que informa STATS); otro mensaje → null.</summary>
        public static int? Porcentaje(string mensaje)
        {
            var m = Porciento.Match(mensaje ?? "");
            int n;
            return m.Success && int.TryParse(m.Groups[1].Value, out n) && n >= 0 && n <= 100 ? (int?)n : null;
        }

        /// <summary>Mensaje claro para un error de SQL Server al revisar o restaurar un respaldo.</summary>
        public static string ExplicarNumero(int numero, string mensaje)
        {
            switch (numero)
            {
                case 3201:
                    return "SQL Server no pudo abrir el archivo del respaldo. Casi siempre es porque el SERVICIO de SQL Server no tiene permiso para leer esa carpeta (el Escritorio, Documentos o una USB). Copie el archivo a una carpeta como C:\\Users\\Public\\ y pruebe otra vez.";
                case 3241: case 3242: case 3243: case 3013: case 3033: case 3183:
                    return "El archivo no parece un respaldo de SQL Server válido, o está dañado o incompleto.";
                case 3169: case 3205:
                    return "Este SQL Server no puede restaurar ese respaldo (es de una versión incompatible). Use un SQL Server de la misma versión (o 2005 a 2008 R2 si el respaldo es de SQL Server 2000).";
                case 3156: case 5133: case 5123: case 3634:
                    return "SQL Server no pudo crear los archivos de la base temporal en su carpeta de datos (permiso o carpeta inexistente).";
                case 112: case 3202: case 1105: case 9002:
                    return "No hay espacio suficiente en el disco para restaurar. Se necesita, al menos, el tamaño del respaldo (la base temporal se borra al terminar).";
                case 262: case 229: case 15247: case 916:
                    return "Este usuario no tiene permiso para restaurar bases de datos. Use un usuario con permiso (por ejemplo, el administrador de SQL Server).";
            }
            return ZafirahSql.ExplicarNumero(numero, mensaje);
        }

        public static string Explicar(Exception ex)
        {
            var sql = ex as SqlException;
            if (sql != null) return ExplicarNumero(sql.Number, sql.Message) ?? "SQL Server dijo: " + sql.Message;
            return ex.Message;
        }

        // ------------------------------------------------------------------ con SQL Server
        static DataTable Consultar(SqlConnection cn, string sql)
        {
            var dt = new DataTable();
            using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 300 })
            using (var da = new SqlDataAdapter(cmd)) da.Fill(dt);
            return dt;
        }

        static object Col(DataRow r, string nombre) { return r.Table.Columns.Contains(nombre) && r[nombre] != DBNull.Value ? r[nombre] : null; }
        static int Entero(object o) { int n; return o != null && int.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), out n) ? n : 0; }
        static string Texto(object o) { return o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture).Trim(); }

        /// <summary>Versión del servidor: mayor y menor (p. ej. 10 y 50 = 2008 R2).</summary>
        public static void VersionServidor(string cadena, out int mayor, out int menor)
        {
            mayor = menor = 0;
            using (var cn = new SqlConnection(cadena))
            {
                cn.Open();
                using (var cmd = new SqlCommand("SELECT CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(50))", cn))
                {
                    var partes = Convert.ToString(cmd.ExecuteScalar()).Split('.');
                    if (partes.Length > 0) int.TryParse(partes[0], out mayor);
                    if (partes.Length > 1) int.TryParse(partes[1], out menor);
                }
            }
        }

        /// <summary>La carpeta de datos del servidor (donde está master.mdf): ahí el servicio sí puede escribir.</summary>
        public static string CarpetaDeDatos(string cadena)
        {
            using (var cn = new SqlConnection(cadena))
            {
                cn.Open();
                using (var cmd = new SqlCommand("SELECT TOP 1 RTRIM(filename) FROM master.dbo.sysfiles ORDER BY fileid", cn))
                {
                    string ruta = Convert.ToString(cmd.ExecuteScalar());
                    int i = ruta.LastIndexOf('\\');
                    if (i <= 0) throw new InvalidOperationException("No se pudo saber la carpeta de datos de SQL Server.");
                    return ruta.Substring(0, i);
                }
            }
        }

        /// <summary>Lee el respaldo SIN restaurarlo: qué base es, de qué versión, qué tan grande y cuáles archivos trae.</summary>
        public static InfoRespaldo Inspeccionar(string cadena, string rutaBk)
        {
            var info = new InfoRespaldo();
            using (var cn = new SqlConnection(cadena))
            {
                cn.Open();
                var cabecera = Consultar(cn, "RESTORE HEADERONLY FROM DISK = " + Literal(rutaBk));
                DataRow ultima = null;
                foreach (DataRow r in cabecera.Rows) if (Entero(Col(r, "BackupType")) == 1) ultima = r;   // 1 = respaldo completo de la base
                if (ultima == null) throw new InvalidOperationException("El archivo no trae un respaldo completo de una base de datos (solo registros de transacciones o diferenciales).");
                info.Base = Texto(Col(ultima, "DatabaseName"));
                info.Servidor = Texto(Col(ultima, "ServerName"));
                info.Posicion = Math.Max(1, Entero(Col(ultima, "Position")));
                info.VersionMayor = Entero(Col(ultima, "SoftwareVersionMajor"));
                info.VersionMenor = Entero(Col(ultima, "SoftwareVersionMinor"));
                info.VersionBase = Entero(Col(ultima, "DatabaseVersion"));
                info.Compatibilidad = Entero(Col(ultima, "CompatibilityLevel"));
                object tam = Col(ultima, "BackupSize");
                if (tam != null) info.TamanoBytes = Convert.ToInt64(Convert.ToDecimal(tam, CultureInfo.InvariantCulture));
                object fecha = Col(ultima, "BackupFinishDate");
                if (fecha is DateTime) info.Fecha = ((DateTime)fecha).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

                var archivos = Consultar(cn, "RESTORE FILELISTONLY FROM DISK = " + Literal(rutaBk) + " WITH FILE = " + info.Posicion);
                foreach (DataRow r in archivos.Rows)
                {
                    var a = new ArchivoRespaldo { Logico = Texto(Col(r, "LogicalName")), Fisico = Texto(Col(r, "PhysicalName")), Tipo = Texto(Col(r, "Type")) };
                    object t = Col(r, "Size");
                    if (t != null) a.Tamano = Convert.ToInt64(Convert.ToDecimal(t, CultureInfo.InvariantCulture));
                    if (a.Tipo == "D" || a.Tipo == "L") info.Archivos.Add(a);   // los catálogos de texto completo (F) no se necesitan
                }
                if (info.Archivos.Count == 0) throw new InvalidOperationException("El respaldo no trae archivos de datos.");
            }

            int mayorServidor, menorServidor;
            VersionServidor(cadena, out mayorServidor, out menorServidor);
            bool imposible; string aviso;
            Compatible(info.VersionMayor, mayorServidor, out aviso, out imposible);
            info.Aviso = aviso; info.Imposible = imposible;
            return info;
        }

        /// <summary>Restaura el respaldo en la base temporal. Puede tardar minutos; avisa el avance (porcentaje, mensaje).</summary>
        public static void Restaurar(string cadena, string rutaBk, InfoRespaldo info, string nombreTmp, Action<int, string> progreso)
        {
            if (!EsTemporal(nombreTmp)) throw new ArgumentException("Solo se puede restaurar a una base temporal (" + Prefijo + "…).");
            string sql = ComandoRestaurar(nombreTmp, rutaBk, info.Posicion, info.Archivos, CarpetaDeDatos(cadena));
            using (var cn = new SqlConnection(cadena))
            {
                cn.InfoMessage += (s, e) => { if (progreso != null) { int? p = Porcentaje(e.Message); progreso(p ?? -1, e.Message); } };
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 0 }) cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Borra una base TEMPORAL. Se niega a borrar cualquier otra.</summary>
        public static void Eliminar(string cadena, string nombreTmp)
        {
            if (!EsTemporal(nombreTmp)) throw new ArgumentException("Solo se pueden borrar las bases temporales de la importación (" + Prefijo + "…), nunca otra base.");
            string q = ZafirahSql.Cita(nombreTmp);
            string sql = "USE master;\nIF EXISTS (SELECT 1 FROM master.dbo.sysdatabases WHERE name = " + Literal(nombreTmp) + ")\nBEGIN\n  ALTER DATABASE " + q + " SET SINGLE_USER WITH ROLLBACK IMMEDIATE;\n  DROP DATABASE " + q + ";\nEND";
            using (var cn = new SqlConnection(cadena))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 300 }) cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Bases temporales que quedaron de una importación anterior (por ejemplo, si el programa se cerró a la mala).</summary>
        public static List<string> Huerfanas(string cadena)
        {
            var lista = new List<string>();
            using (var cn = new SqlConnection(cadena))
            {
                cn.Open();
                using (var cmd = new SqlCommand("SELECT name FROM master.dbo.sysdatabases WHERE name LIKE 'vf[_]import[_]tmp[_]%'", cn))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) { string n = Convert.ToString(rd[0]); if (EsTemporal(n)) lista.Add(n); }
            }
            return lista;
        }
    }
}
