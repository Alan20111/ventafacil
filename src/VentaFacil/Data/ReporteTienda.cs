using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.Linq;
using VentaFacil.Core;

namespace VentaFacil.Data
{
    /// <summary>Algo que no coincide entre el catálogo de la oficina y el de la tienda.</summary>
    public class DiferenciaTienda { public string Tipo = "", Uid = "", Codigo = "", Producto = "", Campo = "", Oficina = "", Tienda = ""; }

    /// <summary>
    /// Lo que la TIENDA le cuenta a la oficina: existencias, ventas por día y producto, cortes y su catálogo. Viaja dentro del
    /// acuse (cifrado). Es una foto: leerla dos veces no duplica nada, y en la oficina los días viejos se conservan.
    /// </summary>
    public static class ReporteTienda
    {
        public const int Dias = 92;   // cada reporte trae los últimos 3 meses: si una USB se pierde, el siguiente lo cubre
        public const string Distinto = "Distinto", SoloTienda = "Solo en la tienda", SoloOficina = "Solo en la oficina";

        static string N(decimal d) { return Envios.Num(d); }

        // ================================================================== TIENDA
        public static Dictionary<string, object> Armar()
        {
            string desde = DateTime.Today.AddDays(-Dias).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var r = new Dictionary<string, object> { { "desde", desde } };
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())   // todo del mismo instante
            {
                r["productos"] = Db.Lista(cn, tx, Envios.Sel + "ORDER BY p.id", x => { var f = Envios.Ficha(x); f["existencia"] = N(Db.D(x, "existencia")); return (object)f; });
                r["dias"] = Db.Lista(cn, tx, @"SELECT substr(v.fecha,1,10) dia, SUM(v.estado='PAGADA') n, ROUND(SUM(CASE WHEN v.estado='PAGADA' THEN v.total ELSE 0 END),2) t,
                        ROUND(SUM(CASE WHEN v.estado='PAGADA' AND v.forma_pago='EFECTIVO' THEN v.total ELSE 0 END),2) e, ROUND(SUM(CASE WHEN v.estado='PAGADA' THEN v.descuento ELSE 0 END),2) d, SUM(v.estado='CANCELADA') c
                        FROM ventas v WHERE v.fecha>=@p0 GROUP BY dia ORDER BY dia",
                    x => (object)new List<object> { Db.S(x, "dia"), Db.L(x, "n").ToString(), N(Db.D(x, "t")), N(Db.D(x, "e")), N(Db.D(x, "d")), Db.L(x, "c").ToString() }, desde);
                r["ventas"] = Db.Lista(cn, tx, @"SELECT substr(v.fecha,1,10) dia, IFNULL(p.uid,'') uid, d.codigo cod, d.descripcion des, ROUND(SUM(d.cantidad),3) q, ROUND(SUM(d.importe),2) i, ROUND(SUM(d.cantidad*d.costo),2) c
                        FROM venta_detalle d JOIN ventas v ON v.id=d.venta_id LEFT JOIN productos p ON p.id=d.producto_id
                        WHERE v.estado='PAGADA' AND v.fecha>=@p0 GROUP BY dia, p.uid, d.codigo, d.descripcion ORDER BY dia",
                    x => (object)new List<object> { Db.S(x, "dia"), Db.S(x, "uid"), Db.S(x, "cod"), Db.S(x, "des"), N(Db.D(x, "q")), N(Db.D(x, "i")), N(Db.D(x, "c")) }, desde);
                var turnos = Db.Lista(cn, tx, @"SELECT t.*, IFNULL(a.nombre,'') abrio, IFNULL(c.nombre,'') cerro FROM turnos t LEFT JOIN usuarios a ON a.id=t.abierto_por LEFT JOIN usuarios c ON c.id=t.cerrado_por
                        WHERE t.estado='CERRADO' AND t.cierre>=@p0 ORDER BY t.id",
                    x => new List<object> { Db.L(x, "id").ToString(), Db.L(x, "caja").ToString(), Db.S(x, "apertura"), Db.S(x, "cierre"), Db.S(x, "abrio"), Db.S(x, "cerro"), x["efectivo_declarado"] is DBNull ? "" : N(Db.D(x, "efectivo_declarado")) }, desde);
                foreach (var t in turnos)
                {
                    var c = TurnoRepo.Resumen(long.Parse((string)t[0], CultureInfo.InvariantCulture));
                    t.Add(c.NumVentas.ToString()); t.Add(N(c.VentasNetas)); t.Add(N(c.EfectivoEsperado));
                }
                r["cortes"] = turnos.Cast<object>().ToList();
                tx.Commit();
            }
            return r;
        }

        // ================================================================== OFICINA
        /// <summary>Reemplaza lo que se sabía de la tienda por lo de este reporte (los días anteriores a él se conservan).</summary>
        internal static void Guardar(SQLiteConnection cn, SQLiteTransaction tx, IDictionary<string, object> r, string fecha)
        {
            string desde = Envios.T(r, "desde");
            Db.Exec(cn, tx, "DELETE FROM tienda_productos");
            foreach (var o in (List<object>)r["productos"])
            {
                var f = (IDictionary<string, object>)o;
                Db.Exec(cn, tx, "INSERT OR REPLACE INTO tienda_productos(uid,codigo,descripcion,precio,costo,existencia,minimo,usa_inventario,activo,ficha) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)",
                    Envios.T(f, "uid"), Envios.T(f, "codigo"), Envios.T(f, "descripcion"), L(f, "precio"), L(f, "costo"), L(f, "existencia"), L(f, "minimo"), Envios.T(f, "usa_inventario") == "1" ? 1 : 0, Envios.T(f, "activo") == "1" ? 1 : 0, JsonMini.Escribir(f));
            }
            Db.Exec(cn, tx, "DELETE FROM tienda_dias WHERE dia>=@p0", desde);
            Db.Exec(cn, tx, "DELETE FROM tienda_ventas WHERE dia>=@p0", desde);
            Db.Exec(cn, tx, "DELETE FROM tienda_cortes WHERE cierre>=@p0", desde);
            foreach (List<object> d in (List<object>)r["dias"])
                Db.Exec(cn, tx, "INSERT OR REPLACE INTO tienda_dias(dia,tickets,total,efectivo,descuento,canceladas) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)", d[0], Entero(d[1]), Envios.Leer((string)d[2]), Envios.Leer((string)d[3]), Envios.Leer((string)d[4]), Entero(d[5]));
            foreach (List<object> d in (List<object>)r["ventas"])
                Db.Exec(cn, tx, "INSERT INTO tienda_ventas(dia,uid,codigo,descripcion,cantidad,importe,costo) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)", d[0], d[1], d[2], d[3], Envios.Leer((string)d[4]), Envios.Leer((string)d[5]), Envios.Leer((string)d[6]));
            foreach (List<object> d in (List<object>)r["cortes"])
                Db.Exec(cn, tx, "INSERT OR REPLACE INTO tienda_cortes(id,caja,apertura,cierre,abrio,cerro,declarado,tickets,ventas,esperado) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)",
                    Entero(d[0]), Entero(d[1]), d[2], d[3], d[4], d[5], (string)d[6] == "" ? null : (object)Envios.Leer((string)d[6]), Entero(d[7]), Envios.Leer((string)d[8]), Envios.Leer((string)d[9]));
            Envios.Poner(cn, tx, "reporte_fecha", fecha);
        }

        static decimal L(IDictionary<string, object> f, string k) { return Envios.Leer(Envios.T(f, k)); }
        static long Entero(object o) { return long.Parse((string)o, CultureInfo.InvariantCulture); }
        static string Dia(DateTime d) { return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        const string PorEnviar = "IFNULL((SELECT ROUND(SUM(c.cantidad),3) FROM cambios c WHERE c.producto_uid=t.uid AND c.campo='existencia' AND c.seq>CAST(IFNULL((SELECT valor FROM enlace WHERE clave='confirmado'),'0') AS INTEGER)),0.0)";

        /// <summary>De cuándo son los datos de la tienda ("" si nunca ha llegado un reporte).</summary>
        public static string Fecha { get { return Envios.Dato("reporte_fecha"); } }

        public static DataTable Existencias(bool soloFaltantes)
        {
            return Db.Tabla("SELECT t.codigo AS Codigo, t.descripcion AS Descripcion, t.existencia AS Existencia, " + PorEnviar + " AS PorEnviar, t.minimo AS Minimo, t.costo AS Costo, t.precio AS Precio, ROUND(t.existencia*t.costo,2) AS ValorCosto " +
                "FROM tienda_productos t WHERE t.activo=1 AND t.usa_inventario=1" + (soloFaltantes ? " AND t.existencia<=t.minimo" : "") + " ORDER BY t.descripcion");
        }

        /// <summary>Lo último que se supo que había en la tienda de ese producto, o null.</summary>
        public static decimal? Existencia(long productoId)
        {
            var v = Db.Scalar("SELECT t.existencia FROM tienda_productos t JOIN productos p ON p.uid=t.uid WHERE p.id=@p0", productoId);
            return v == null ? (decimal?)null : Db.Dec(v);
        }

        public static DataTable VentasPorDia(DateTime d, DateTime h)
        {
            return Db.Tabla(@"SELECT x.dia AS Fecha, x.tickets AS Tickets, x.total AS Total, IFNULL(c.costo,0.0) AS Costo, ROUND(x.total-IFNULL(c.costo,0.0),2) AS Ganancia, x.efectivo AS Efectivo, x.descuento AS Descuentos, x.canceladas AS Canceladas
                FROM tienda_dias x LEFT JOIN (SELECT dia, ROUND(SUM(costo),2) costo FROM tienda_ventas GROUP BY dia) c ON c.dia=x.dia WHERE x.dia>=@p0 AND x.dia<=@p1 ORDER BY x.dia", Dia(d), Dia(h));
        }

        public static DataTable ProductosVendidos(DateTime d, DateTime h)
        {
            return Db.Tabla(@"SELECT v.codigo AS Codigo, v.descripcion AS Descripcion, ROUND(SUM(v.cantidad),3) AS Cantidad, ROUND(SUM(v.importe),2) AS Importe, ROUND(SUM(v.importe-v.costo),2) AS Ganancia,
                (SELECT t.existencia FROM tienda_productos t WHERE t.uid=v.uid AND v.uid<>'') AS Existencia
                FROM tienda_ventas v WHERE v.dia>=@p0 AND v.dia<=@p1 GROUP BY v.uid, v.codigo, v.descripcion ORDER BY Importe DESC", Dia(d), Dia(h));
        }

        public static DataTable Cortes(DateTime d, DateTime h)
        {
            return Db.Tabla(@"SELECT id AS Turno, caja AS Caja, apertura AS Apertura, cierre AS Cierre, abrio AS AbrioPor, cerro AS Cerro, tickets AS Tickets, ventas AS Ventas, esperado AS Esperado, declarado AS Declarado,
                ROUND(declarado-esperado,2) AS Diferencia FROM tienda_cortes WHERE cierre>=@p0 AND cierre<@p1 ORDER BY id DESC", Dia(d), Dia(h.AddDays(1)));
        }

        // ------------------------------------------------------------------ catálogo distinto
        static readonly string[] NoComparar = { "orden", "minimo", "sin_barras" };

        /// <summary>Lo que no coincide, sin contar lo que ya está por enviarse.</summary>
        public static List<DiferenciaTienda> Diferencias()
        {
            var l = new List<DiferenciaTienda>();
            if (Fecha == "") return l;
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                long conf = Envios.Confirmado(cn, tx);
                var pendientes = new HashSet<string>(Db.Lista(cn, tx, "SELECT DISTINCT producto_uid || '|' || campo k FROM cambios WHERE seq>@p0", x => Db.S(x, "k"), conf));
                var oficina = Db.Lista(cn, tx, Envios.Sel + "ORDER BY p.descripcion", x => Envios.Ficha(x)).ToDictionary(f => Envios.T(f, "uid"));
                var tienda = Db.Lista(cn, tx, "SELECT ficha FROM tienda_productos ORDER BY descripcion", x => (IDictionary<string, object>)JsonMini.Leer(Db.S(x, "ficha"))).ToDictionary(f => Envios.T(f, "uid"));
                foreach (var t in tienda)
                {
                    Dictionary<string, object> o;
                    if (!oficina.TryGetValue(t.Key, out o))
                    {
                        if (Envios.T(t.Value, "activo") == "1" && !pendientes.Contains(t.Key + "|eliminado")) l.Add(new DiferenciaTienda { Tipo = SoloTienda, Uid = t.Key, Codigo = Envios.T(t.Value, "codigo"), Producto = Envios.T(t.Value, "descripcion"), Campo = "precio", Tienda = Envios.T(t.Value, "precio") });
                        continue;
                    }
                    if (pendientes.Contains(t.Key + "|alta")) continue;
                    if (Envios.T(o, "activo") == "0" && Envios.T(t.Value, "activo") == "0") continue;
                    foreach (var c in Envios.Campos)
                    {
                        if (NoComparar.Contains(c.Nombre) || pendientes.Contains(t.Key + "|" + c.Nombre)) continue;
                        string a = Envios.T(o, c.Nombre), b = Envios.T(t.Value, c.Nombre);
                        if (c.Clase == 'D' ? string.Equals(a, b, StringComparison.OrdinalIgnoreCase) : a == b) continue;
                        l.Add(new DiferenciaTienda { Tipo = Distinto, Uid = t.Key, Codigo = Envios.T(o, "codigo"), Producto = Envios.T(o, "descripcion"), Campo = c.Nombre, Oficina = a, Tienda = b });
                    }
                }
                foreach (var o in oficina)
                    if (!tienda.ContainsKey(o.Key) && Envios.T(o.Value, "activo") == "1" && !pendientes.Contains(o.Key + "|alta"))
                        l.Add(new DiferenciaTienda { Tipo = SoloOficina, Uid = o.Key, Codigo = Envios.T(o.Value, "codigo"), Producto = Envios.T(o.Value, "descripcion"), Campo = "precio", Oficina = Envios.T(o.Value, "precio") });
                tx.Commit();
            }
            return l;
        }

        /// <summary>La oficina se queda con lo de la tienda (o da de alta el producto que solo existe allá). null si quedó; si no, por qué no.</summary>
        public static string TomarDeTienda(DiferenciaTienda d)
        {
            if (d.Tipo == SoloOficina) return "«" + d.Producto + "» no existe en la tienda.";
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                var texto = Convert.ToString(Db.Scalar(cn, tx, "SELECT ficha FROM tienda_productos WHERE uid=@p0", d.Uid));
                if (texto == "") return "Ya no hay datos de «" + d.Producto + "» en el reporte.";
                var f = (IDictionary<string, object>)JsonMini.Leer(texto);
                object id = Db.Scalar(cn, tx, "SELECT id FROM productos WHERE uid=@p0", d.Uid);
                long propio = id == null ? 0 : Convert.ToInt64(id);
                foreach (var k in new[] { "codigo", "codigo2" })
                {
                    string v = Envios.T(f, k);
                    if (v == "" || (d.Tipo == Distinto && d.Campo != k)) continue;
                    var otro = Db.Scalar(cn, tx, "SELECT descripcion FROM productos WHERE id<>@p1 AND (codigo=@p0 OR (codigo2<>'' AND codigo2=@p0)) LIMIT 1", v, propio);
                    if (otro != null) return "En la oficina el código " + v + " ya es de «" + otro + "». Cambie uno de los dos.";
                }
                if (d.Tipo == SoloTienda) { if (id == null) Envios.Insertar(cn, tx, d.Uid, f); }
                else Envios.Escribir(cn, tx, propio, Envios.BuscarCampo(d.Campo), Envios.T(f, d.Campo));
                tx.Commit();
            }
            Respaldo.Pedir();
            return null;
        }

        /// <summary>El próximo envío vuelve a llevar lo de la oficina. null si quedó; si no, por qué no.</summary>
        public static string MandarDeOficina(DiferenciaTienda d)
        {
            if (d.Tipo == SoloTienda) return "«" + d.Producto + "» solo existe en la tienda: primero tráigalo a la oficina.";
            Db.Exec("INSERT INTO cambios(producto_uid,campo,antes,despues) VALUES(@p0,@p1,@p2,@p3)", d.Uid, d.Tipo == SoloOficina ? "alta" : d.Campo, d.Tienda, d.Oficina);
            Respaldo.Pedir();
            return null;
        }
    }
}
