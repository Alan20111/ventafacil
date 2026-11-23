using System.Collections;
using System.Data.Common;
using Microsoft.Data.Sqlite;

// ======================================================================
// ADAPTADOR: el programa usa System.Data.SQLite (cuya parte nativa solo existe para Windows). Aquí se ofrece la
// MISMA API mínima que usa el programa, pero apoyada en Microsoft.Data.Sqlite (que sí corre en Mac y Linux). Así
// se ejecuta el código real de la capa de datos contra un SQLite de verdad. Es solo para pruebas.
// ======================================================================
namespace System.Data.SQLite
{
    public class SQLiteException : Exception { public SQLiteException(string mensaje, Exception interna = null) : base(mensaje, interna) { } }

    public class SQLiteTransaction : IDisposable
    {
        internal readonly SqliteTransaction Tx;
        readonly SQLiteConnection _cn;
        internal SQLiteTransaction(SqliteTransaction tx, SQLiteConnection cn) { Tx = tx; _cn = cn; }
        public void Commit() { Tx.Commit(); _cn.Actual = null; }
        public void Rollback() { Tx.Rollback(); _cn.Actual = null; }
        public void Dispose() { if (_cn.Actual == this) _cn.Actual = null; Tx.Dispose(); }
    }

    public class SQLiteConnection : IDisposable
    {
        internal readonly SqliteConnection Cn;
        internal SQLiteTransaction Actual;
        readonly string _pragmas;

        public SQLiteConnection(string cadena)
        {
            string ruta = null; var pragmas = new List<string>();
            foreach (var par in cadena.Split(';'))
            {
                var kv = par.Split(new[] { '=' }, 2);
                if (kv.Length != 2) continue;
                string k = kv[0].Trim().ToLowerInvariant(), v = kv[1].Trim();
                if (k == "data source") ruta = v;
                else if (k == "journal mode" && v.ToUpperInvariant() == "WAL") pragmas.Add("PRAGMA journal_mode=WAL");
                else if (k == "foreign keys" && v.ToLowerInvariant() == "true") pragmas.Add("PRAGMA foreign_keys=ON");
                else if (k == "synchronous") pragmas.Add("PRAGMA synchronous=" + v);
                else if (k == "busytimeout") pragmas.Add("PRAGMA busy_timeout=" + v);
            }
            Cn = new SqliteConnection("Data Source=" + ruta + ";Pooling=False");
            _pragmas = string.Join(";", pragmas);
        }

        public void Open()
        {
            Cn.Open();
            if (_pragmas != "") using (var c = Cn.CreateCommand()) { c.CommandText = _pragmas; c.ExecuteNonQuery(); }
        }

        public SQLiteTransaction BeginTransaction() { var t = new SQLiteTransaction(Cn.BeginTransaction(), this); Actual = t; return t; }
        public void BackupDatabase(SQLiteConnection destino, string nombreDestino, string nombreOrigen, int paginas, object retorno, int reintentos) { Cn.BackupDatabase(destino.Cn); }
        public static void ClearAllPools() { SqliteConnection.ClearAllPools(); }
        public void Dispose() { Cn.Dispose(); }
    }

    public class SQLiteParameterCollection
    {
        internal readonly List<KeyValuePair<string, object>> L = new List<KeyValuePair<string, object>>();
        public object AddWithValue(string nombre, object valor) { L.Add(new KeyValuePair<string, object>(nombre, valor)); return null; }
    }

    /// <summary>System.Data.SQLite lanza IndexOutOfRangeException si la columna no existe; Microsoft.Data.Sqlite, otra. Se traduce.</summary>
    public class LectorAdaptado : DbDataReader
    {
        readonly SqliteDataReader R;
        public LectorAdaptado(SqliteDataReader r) { R = r; }
        public override int Depth { get { return R.Depth; } }
        public override int FieldCount { get { return R.FieldCount; } }
        public override bool HasRows { get { return R.HasRows; } }
        public override bool IsClosed { get { return R.IsClosed; } }
        public override int RecordsAffected { get { return R.RecordsAffected; } }
        public override object this[int i] { get { return R[i]; } }
        public override object this[string nombre] { get { return R.GetValue(GetOrdinal(nombre)); } }
        public override bool GetBoolean(int i) { return R.GetBoolean(i); }
        public override byte GetByte(int i) { return R.GetByte(i); }
        public override long GetBytes(int i, long a, byte[] b, int c, int d) { return R.GetBytes(i, a, b, c, d); }
        public override char GetChar(int i) { return R.GetChar(i); }
        public override long GetChars(int i, long a, char[] b, int c, int d) { return R.GetChars(i, a, b, c, d); }
        public override string GetDataTypeName(int i) { return R.GetDataTypeName(i); }
        public override DateTime GetDateTime(int i) { return R.GetDateTime(i); }
        public override decimal GetDecimal(int i) { return R.GetDecimal(i); }
        public override double GetDouble(int i) { return R.GetDouble(i); }
        public override IEnumerator GetEnumerator() { return ((IEnumerable)R).GetEnumerator(); }
        public override Type GetFieldType(int i) { return R.GetFieldType(i); }
        public override float GetFloat(int i) { return R.GetFloat(i); }
        public override Guid GetGuid(int i) { return R.GetGuid(i); }
        public override short GetInt16(int i) { return R.GetInt16(i); }
        public override int GetInt32(int i) { return R.GetInt32(i); }
        public override long GetInt64(int i) { return R.GetInt64(i); }
        public override string GetName(int i) { return R.GetName(i); }
        public override int GetOrdinal(string nombre)
        {
            for (int i = 0; i < R.FieldCount; i++) if (string.Equals(R.GetName(i), nombre, StringComparison.OrdinalIgnoreCase)) return i;
            throw new IndexOutOfRangeException(nombre);
        }
        public override string GetString(int i) { return R.GetString(i); }
        public override object GetValue(int i) { return R.GetValue(i); }
        public override int GetValues(object[] v) { return R.GetValues(v); }
        public override bool IsDBNull(int i) { return R.IsDBNull(i); }
        public override bool NextResult() { return R.NextResult(); }
        public override bool Read() { return R.Read(); }
        public override void Close() { R.Close(); }
    }

    public class SQLiteCommand : IDisposable
    {
        readonly SQLiteConnection _cn;
        readonly SQLiteTransaction _tx;
        readonly string _sql;
        public SQLiteParameterCollection Parameters { get; private set; }

        public SQLiteCommand(string sql, SQLiteConnection cn) : this(sql, cn, null) { }
        public SQLiteCommand(string sql, SQLiteConnection cn, SQLiteTransaction tx)
        {
            _sql = sql; _cn = cn; _tx = tx ?? cn.Actual;   // System.Data.SQLite usa la transacción abierta de la conexión
            Parameters = new SQLiteParameterCollection();
        }

        SqliteCommand Armar()
        {
            var c = _cn.Cn.CreateCommand();
            c.CommandText = _sql;
            if (_tx != null) c.Transaction = _tx.Tx;
            foreach (var p in Parameters.L) c.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
            return c;
        }

        public int ExecuteNonQuery() { try { using (var c = Armar()) return c.ExecuteNonQuery(); } catch (SqliteException e) { throw new SQLiteException(e.Message, e); } }
        public object ExecuteScalar() { try { using (var c = Armar()) return c.ExecuteScalar(); } catch (SqliteException e) { throw new SQLiteException(e.Message, e); } }
        public DbDataReader ExecuteReader() { try { return new LectorAdaptado(Armar().ExecuteReader()); } catch (SqliteException e) { throw new SQLiteException(e.Message, e); } }
        public void Dispose() { }
    }

    public class SQLiteDataAdapter : IDisposable
    {
        readonly SQLiteCommand _c;
        public SQLiteDataAdapter(SQLiteCommand c) { _c = c; }
        public int Fill(DataTable dt) { using (var rd = _c.ExecuteReader()) dt.Load(rd); return dt.Rows.Count; }
        public void Dispose() { }
    }
}

namespace VentaFacil
{
    /// <summary>En el programa, Log vive en Program.cs (que usa pantallas). Aquí solo se anota en consola.</summary>
    public static class Log
    {
        public static List<string> Anotados = new List<string>();
        public static void Error(Exception ex, string contexto) { Anotados.Add(contexto + ": " + (ex == null ? "" : ex.Message)); }
    }
}
