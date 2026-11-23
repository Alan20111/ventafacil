using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace VentaFacil.Data
{
    public class TablaSql
    {
        public string Esquema = "dbo", Nombre = "", Tipo = "Tabla";
        public long Filas;
        public string Completo { get { return ZafirahSql.Cita(Esquema) + "." + ZafirahSql.Cita(Nombre); } }
        public override string ToString() { return Nombre + (Tipo == "Vista" ? "  (vista)" : Filas > 0 ? "  (" + Filas + " filas)" : ""); }
    }

    /// <summary>
    /// Lectura de la base de datos de Zafirah (SQL Server). SOLO LEE: únicamente se ejecutan SELECT, nunca se
    /// escribe ni se cambia nada en la base de Zafirah. Usa el proveedor System.Data.SqlClient, que ya viene en
    /// .NET Framework (no hay que instalar nada). Funciona con SQL Server 2000 en adelante.
    /// </summary>
    public static class ZafirahSql
    {
        /// <summary>Entre corchetes y con los corchetes del nombre duplicados: un nombre nunca se interpreta como SQL.</summary>
        public static string Cita(string identificador) { return "[" + (identificador ?? "").Replace("]", "]]") + "]"; }

        public static string Cadena(string servidor, string baseDatos, string usuario, string clave)
        {
            var b = new SqlConnectionStringBuilder { DataSource = servidor, ConnectTimeout = 6, ApplicationName = "VentaFacil (importar)", Pooling = false };
            if (!string.IsNullOrEmpty(baseDatos)) b.InitialCatalog = baseDatos;
            if (string.IsNullOrEmpty(usuario)) b.IntegratedSecurity = true;
            else { b.UserID = usuario; b.Password = clave ?? ""; }
            return b.ConnectionString;
        }

        /// <summary>Nombres donde suele estar el SQL Server de un programa instalado en la misma computadora.</summary>
        public static string[] ServidoresProbables()
        {
            return new[] { "(local)", @".\SQLEXPRESS", "localhost", Environment.MachineName, Environment.MachineName + @"\SQLEXPRESS" }
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        /// <summary>Bases de datos del usuario (sin las del sistema). Sirve también para comprobar que se puede entrar.</summary>
        public static List<string> Bases(string servidor, string usuario, string clave)
        {
            var lista = new List<string>();
            using (var cn = new SqlConnection(Cadena(servidor, null, usuario, clave)))
            {
                cn.Open();
                using (var cmd = new SqlCommand("SELECT name FROM sys.databases WHERE database_id > 4 AND HAS_DBACCESS(name) = 1 ORDER BY name", cn) { CommandTimeout = 20 })
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) lista.Add(Convert.ToString(rd[0]));
            }
            return lista;
        }

        public static List<TablaSql> Tablas(string cadena)
        {
            var lista = new List<TablaSql>();
            using (var cn = new SqlConnection(cadena))
            {
                cn.Open();
                try
                {
                    const string sql = @"SELECT s.name AS esquema, o.name AS nombre, CASE o.type WHEN 'U' THEN 'Tabla' ELSE 'Vista' END AS tipo,
                        ISNULL((SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = o.object_id AND p.index_id IN (0, 1)), 0) AS filas
                        FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
                        WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0 ORDER BY o.name";
                    using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 30 })
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read()) lista.Add(new TablaSql { Esquema = Convert.ToString(rd[0]), Nombre = Convert.ToString(rd[1]), Tipo = Convert.ToString(rd[2]), Filas = Convert.ToInt64(rd[3]) });
                }
                catch (SqlException)
                {
                    // SQL Server muy viejo o sin permiso sobre sys.*: se usa la vista estándar
                    lista.Clear();
                    using (var cmd = new SqlCommand("SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_NAME", cn) { CommandTimeout = 30 })
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read()) lista.Add(new TablaSql { Esquema = Convert.ToString(rd[0]), Nombre = Convert.ToString(rd[1]), Tipo = Convert.ToString(rd[2]) == "VIEW" ? "Vista" : "Tabla" });
                }
            }
            return lista;
        }

        /// <summary>Lee la tabla completa (hasta el límite). READ UNCOMMITTED: no estorba a Zafirah si está abierto.</summary>
        public static DataTable Leer(string cadena, TablaSql tabla, int limite = 200000)
        {
            var dt = new DataTable();
            using (var cn = new SqlConnection(cadena))
            {
                cn.Open();
                string sql = "SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED; SELECT TOP " + Math.Max(1, limite) + " * FROM " + tabla.Completo;
                using (var cmd = new SqlCommand(sql, cn) { CommandTimeout = 120 })
                using (var da = new SqlDataAdapter(cmd)) da.Fill(dt);
            }
            return dt;
        }

        /// <summary>Mensaje claro en español para los errores de conexión más comunes.</summary>
        public static string Explicar(Exception ex)
        {
            var sql = ex as SqlException;
            if (sql != null)
            {
                switch (sql.Number)
                {
                    case 18456: return "SQL Server rechazó el usuario o la contraseña. Si Zafirah entra con el usuario de Windows, deje «Usuario de Windows»; si no, escriba el usuario y la contraseña de SQL (la persona que instaló Zafirah los sabe).";
                    case 4060: return "Ese usuario no tiene permiso para abrir esa base de datos.";
                    case 229: case 230: case 262: return "Ese usuario no tiene permiso para leer esa tabla. Pida a quien administra SQL Server un usuario con permiso de lectura.";
                    case -1: case 2: case 53: case 10061: case 10060: case 11001:
                        return "No se encontró el servidor de SQL Server. Revise que esté encendido y que el nombre sea correcto (si Zafirah está en otra computadora, escriba el nombre de esa computadora; si es SQL Express, suele ser «.\\SQLEXPRESS»).";
                }
                return "SQL Server dijo: " + sql.Message;
            }
            return ex.Message;
        }
    }
}
