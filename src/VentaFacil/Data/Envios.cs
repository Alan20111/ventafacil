using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using VentaFacil.Core;

namespace VentaFacil.Data
{
    /// <summary>Un renglón de lo que trae (o hizo) un envío.</summary>
    public class LineaEnvio
    {
        public string Tipo = "";        // Precio | Costo | Dato | Nuevo | Baja | Reactivado | Entrada | Salida | Problema
        public long ProductoId;
        public string Codigo = "", Producto = "", Campo = "", Antes = "", Despues = "", Nota = "";
        public decimal Cantidad;
    }

    public class ResultadoEnvio
    {
        public string EnvioId = "", Oficina = "", Usuario = "", Fecha = "", Respaldo = "";
        public long Num;
        public bool YaAplicado;                       // todo lo que trae ya estaba
        public bool PrimerEnvio;                      // aún no hay oficina reconocida: un administrador la acepta
        public List<LineaEnvio> Lineas = new List<LineaEnvio>();
        public List<string> Alertas = new List<string>();

        int Productos(string tipo) { return Lineas.Where(l => l.Tipo == tipo).Select(l => l.Codigo + "|" + l.Producto).Distinct().Count(); }
        static string N(int n, string uno, string varios) { return n == 0 ? null : n + " " + (n == 1 ? uno : varios); }
        public int Problemas { get { return Lineas.Count(l => l.Tipo == "Problema"); } }

        /// <summary>«5 precios · 42 entradas de mercancía · 3 nuevos». Solo lo que sí hay.</summary>
        public string Resumen
        {
            get
            {
                var p = new[] { N(Productos("Precio"), "precio", "precios"), N(Productos("Costo"), "costo", "costos"), N(Productos("Entrada"), "entrada de mercancía", "entradas de mercancía"),
                    N(Productos("Salida"), "salida de mercancía", "salidas de mercancía"), N(Productos("Nuevo"), "nuevo", "nuevos"), N(Productos("Dato"), "con otros datos", "con otros datos"),
                    N(Productos("Reactivado"), "reactivado", "reactivados"), N(Productos("Baja"), "dado de baja", "dados de baja"), N(Problemas, "PROBLEMA", "PROBLEMAS") };
                string s = string.Join("  ·  ", p.Where(x => x != null).ToArray());
                return s == "" ? "Sin cambios: la tienda ya estaba igual" : s;
            }
        }
    }

    public class EnvioHecho { public string Id, Fecha, Usuario, Resumen, Estado, Archivo, Aplicado, Problemas; public long Num, Desde, Hasta; }
    public class EnvioRecibido { public string Id, FechaEnvio, Fecha, Usuario, Resumen, Estado, OficinaId; public long Orden, Num; }
    public class NovedadEnvio { public long Num, Hasta; public int Cambios; }
    public class RenglonMercancia { public long ProductoId; public decimal Cantidad; public decimal? Costo; }

    /// <summary>
    /// Envíos de la OFICINA a la TIENDA por USB. La oficina numera cada cambio (tabla cambios, llenada por disparadores) y
    /// manda siempre TODO lo que no está confirmado; la tienda recuerda hasta qué número aplicó. Por eso un envío repetido no
    /// hace nada y uno perdido lo recupera el siguiente. Los datos viajan con su valor final; la mercancía, como cantidad
    /// que se SUMA (así no importa lo que se haya vendido entretanto). Sin pantallas: se prueba con un SQLite de verdad.
    /// </summary>
    public static class Envios
    {
        public class Campo { public string Nombre, Columna, Titulo, Tipo; public char Clase; }   // Clase: T texto, N número, B sí/no, D departamento
        static Campo C(string nombre, string titulo, char clase, string tipo = "Dato", string columna = null) { return new Campo { Nombre = nombre, Columna = columna ?? nombre, Titulo = titulo, Clase = clase, Tipo = tipo }; }

        public static readonly Campo[] Campos =
        {
            C("codigo", "Código", 'T'), C("codigo2", "Código de barras", 'T'), C("descripcion", "Nombre", 'T'), C("departamento", "Grupo", 'D', "Dato", "departamento_id"),
            C("precio", "Precio", 'N', "Precio"), C("precio_medio", "Precio medio mayoreo", 'N', "Precio"), C("medio_desde", "Medio mayoreo desde", 'N', "Precio"),
            C("precio_mayoreo", "Precio mayoreo", 'N', "Precio"), C("mayoreo_desde", "Mayoreo desde", 'N', "Precio"), C("costo", "Costo", 'N', "Costo"),
            C("costo_bulto", "Costo del bulto", 'N', "Costo"), C("cant_bulto", "Trae el bulto", 'N', "Costo"), C("granel", "A granel", 'B'), C("usa_inventario", "Controla existencia", 'B'),
            C("minimo", "Mínimo", 'N'), C("proveedor", "Proveedor", 'T'), C("orden", "Orden en la lista", 'N'), C("sin_barras", "Sin código de barras", 'B'), C("activo", "Activo", 'B', "Baja")
        };
        internal static Campo BuscarCampo(string nombre) { return Campos.FirstOrDefault(c => c.Nombre == nombre); }
        static Campo PorNombreOTitulo(string n) { return BuscarCampo(n) ?? Campos.FirstOrDefault(c => c.Titulo == n); }

        // ================================================================== enlace
        public static string Dato(string clave) { return Convert.ToString(Db.Scalar("SELECT valor FROM enlace WHERE clave=@p0", clave)) ?? ""; }
        static string Dato(SQLiteConnection cn, SQLiteTransaction tx, string clave) { return Convert.ToString(Db.Scalar(cn, tx, "SELECT valor FROM enlace WHERE clave=@p0", clave)) ?? ""; }
        internal static void Poner(SQLiteConnection cn, SQLiteTransaction tx, string clave, string valor) { Db.Exec(cn, tx, "INSERT OR REPLACE INTO enlace(clave,valor) VALUES(@p0,@p1)", clave, valor); }

        public static bool EsOficina { get { return Dato("rol") == "OFICINA"; } }
        /// <summary>La tienda ya sacó su archivo de enlace (aunque todavía no llegue el primer envío).</summary>
        public static bool EsTienda { get { return Dato("rol") == "TIENDA"; } }
        static byte[] Clave(string b64) { return Convert.FromBase64String(b64); }

        /// <summary>TIENDA: crea el archivo con el que se enlaza la oficina (copia del catálogo y la clave compartida, protegidos con contraseña).</summary>
        public static int CrearEnlace(string ruta, string contrasena, string negocio)
        {
            if (EsOficina) throw new InvalidOperationException("Este equipo es la oficina.");
            if ((contrasena ?? "").Length < 6) throw new InvalidOperationException("La contraseña del enlace debe tener al menos 6 caracteres.");
            string tienda = Dato("tienda_id"), clave = Convert.ToBase64String(EnvioArchivo.NuevaClave());
            if (tienda == "") tienda = EnvioArchivo.NuevoId();
            List<object> productos;
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                productos = Db.Lista(cn, tx, Sel + "ORDER BY p.orden, p.id", r => (object)Ficha(r));
                tx.Commit();
            }
            var doc = new Dictionary<string, object> { { "version", "1" }, { "tiendaId", tienda }, { "clave", clave }, { "negocio", negocio ?? "" }, { "fecha", Db.Ahora() }, { "productos", productos } };
            EnvioArchivo.Guardar(ruta, EnvioArchivo.Cerrar(EnvioArchivo.Enlace, tienda, JsonMini.Escribir(doc), null, null, contrasena));
            // Solo después de que el archivo quedó bien escrito: si la USB falla, el enlace anterior sigue sirviendo
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                Poner(cn, tx, "rol", "TIENDA"); Poner(cn, tx, "tienda_id", tienda); Poner(cn, tx, "clave", clave);
                Poner(cn, tx, "oficina_id", ""); Poner(cn, tx, "oficina_pub", "");
                Db.Exec(cn, tx, "DELETE FROM recibidos");
                tx.Commit();
            }
            Respaldo.Pedir();
            return productos.Count;
        }

        /// <summary>
        /// OFICINA: este equipo toma el catálogo de la tienda y queda enlazado. Solo en una base sin ventas; lo que
        /// hubiera en el catálogo se reemplaza (antes se respalda).
        /// </summary>
        public static int ConvertirEnOficina(string ruta, string contrasena)
        {
            var datos = EnvioArchivo.Cargar(ruta);
            if (EnvioArchivo.TiendaDe(datos, EnvioArchivo.Enlace) == null) throw new EnvioInvalidoException("Ese no es un archivo de enlace de VentaFácil.");
            var doc = JsonMini.Leer(EnvioArchivo.Abrir(datos, EnvioArchivo.Enlace, null, null, contrasena)) as IDictionary<string, object>;
            if (doc == null || T(doc, "version") != "1") throw new EnvioInvalidoException("El archivo de enlace es de otra versión de VentaFácil.");
            if (Convert.ToInt64(Db.Scalar("SELECT COUNT(*) FROM ventas")) > 0)
                throw new InvalidOperationException("Esta base ya tiene ventas, así que es de una caja. La oficina debe ser una instalación nueva en otra computadora.");
            string privada = EnvioArchivo.NuevaLlavePrivada();
            Db.Respaldar();
            int n = 0;
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                Db.Exec(cn, tx, "DELETE FROM enlace");   // primero: así la copia del catálogo no se anota como cambios
                foreach (var t in new[] { "cambios", "envios", "envio_detalle", "envios_aplicados", "recibidos", "movimientos_inv", "suspendidas", "productos", "tienda_productos", "tienda_dias", "tienda_ventas", "tienda_cortes" }) Db.Exec(cn, tx, "DELETE FROM " + t);
                foreach (var o in (List<object>)doc["productos"])
                {
                    var f = (IDictionary<string, object>)o;
                    Insertar(cn, tx, T(f, "uid"), f);
                    n++;
                }
                Poner(cn, tx, "tienda_id", T(doc, "tiendaId")); Poner(cn, tx, "clave", T(doc, "clave")); Poner(cn, tx, "negocio", T(doc, "negocio"));
                Poner(cn, tx, "oficina_id", EnvioArchivo.NuevoId()); Poner(cn, tx, "privada", privada);
                Poner(cn, tx, "confirmado", "0"); Poner(cn, tx, "rol", "OFICINA");
                // El número de cambio empieza en 1 con cada enlace
                Db.Exec(cn, tx, "DELETE FROM sqlite_sequence WHERE name='cambios'");
                tx.Commit();
            }
            Respaldo.Pedir();
            return n;
        }

        /// <summary>Quita el enlace: el equipo vuelve a ser una caja normal. Productos y ventas no se tocan.</summary>
        public static void QuitarEnlace()
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                Db.Exec(cn, tx, "DELETE FROM enlace WHERE clave<>'tienda_id'");
                Db.Exec(cn, tx, "DELETE FROM recibidos");
                tx.Commit();
            }
            Respaldo.Pedir();
        }

        // ================================================================== OFICINA
        /// <summary>«Llegó mercancía»: anota lo que se sumará (o restará, si es negativo) en la tienda. No cambia la existencia de la oficina.</summary>
        public static void RegistrarMercancia(List<RenglonMercancia> renglones, string referencia)
        {
            if (!EsOficina) throw new InvalidOperationException("Este equipo no es la oficina.");
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                foreach (var r in renglones)
                {
                    if (r.Cantidad != 0)
                        Db.Exec(cn, tx, "INSERT INTO cambios(producto_uid,campo,cantidad,referencia) SELECT uid,'existencia',@p0,@p1 FROM productos WHERE id=@p2", r.Cantidad, referencia ?? "", r.ProductoId);
                    if (r.Costo.HasValue) Db.Exec(cn, tx, "UPDATE productos SET costo=@p0, actualizado=@p1 WHERE id=@p2", r.Costo.Value, Db.Ahora(), r.ProductoId);
                }
                tx.Commit();
            }
            Respaldo.Pedir();
        }

        class Cambio { public long Seq; public string Uid, Producto, CampoNombre, Antes, Ref, Fecha; public decimal Cantidad; }

        static List<Cambio> Pendientes(SQLiteConnection cn, SQLiteTransaction tx, long desdeSeq)
        {
            return Db.Lista(cn, tx, "SELECT * FROM cambios WHERE seq>@p0 ORDER BY seq", r => new Cambio
            {
                Seq = Db.L(r, "seq"), Uid = Db.S(r, "uid"), Producto = Db.S(r, "producto_uid"), CampoNombre = Db.S(r, "campo"), Antes = Valor(BuscarCampo(Db.S(r, "campo")), r["antes"]),
                Cantidad = Db.D(r, "cantidad"), Ref = Db.S(r, "referencia"), Fecha = Db.S(r, "fecha")
            }, desdeSeq);
        }

        internal static long Confirmado(SQLiteConnection cn, SQLiteTransaction tx) { long n; return long.TryParse(Dato(cn, tx, "confirmado"), out n) ? n : 0; }

        /// <summary>OFICINA: lo que se mandaría en el próximo envío, ya resumido por producto.</summary>
        public static ResultadoEnvio VistaPendientes()
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                var r = new ResultadoEnvio();
                long conf = Confirmado(cn, tx);
                foreach (var g in Pendientes(cn, tx, conf).GroupBy(c => c.Producto))
                {
                    var f = FichaDe(cn, tx, g.Key);
                    int desde = r.Lineas.Count;
                    string codigo = f == null ? g.Select(c => c.Ref).LastOrDefault(x => !string.IsNullOrEmpty(x)) ?? "" : T(f, "codigo");
                    string nombre = f == null ? g.Where(c => c.CampoNombre == "eliminado").Select(c => c.Antes).FirstOrDefault() ?? "" : T(f, "descripcion");
                    bool alta = g.Any(c => c.CampoNombre == "alta");
                    if (f == null) { if (!alta) r.Lineas.Add(new LineaEnvio { Tipo = "Baja", Codigo = codigo, Producto = nombre }); continue; }
                    if (alta) r.Lineas.Add(new LineaEnvio { Tipo = "Nuevo", Codigo = codigo, Producto = nombre, Campo = "Precio", Despues = T(f, "precio") });
                    else
                        foreach (var gc in g.Where(c => BuscarCampo(c.CampoNombre) != null).GroupBy(c => c.CampoNombre))
                        {
                            var campo = BuscarCampo(gc.Key);
                            string antes = gc.First().Antes, despues = T(f, gc.Key);
                            if (antes != despues) r.Lineas.Add(new LineaEnvio { Tipo = TipoDe(campo, despues), Codigo = codigo, Producto = nombre, Campo = campo.Titulo, Antes = antes, Despues = despues });
                        }
                    decimal q = g.Where(c => c.CampoNombre == "existencia").Sum(c => c.Cantidad);
                    if (q != 0) r.Lineas.Add(new LineaEnvio { Tipo = q > 0 ? "Entrada" : "Salida", Codigo = codigo, Producto = nombre, Cantidad = q, Nota = Refs(g) });
                    Alertar(r, f, desde);
                }
                AlertarBajas(r);
                return r;
            }
        }

        /// <summary>Cuántos cambios (sin resumir) hay sin confirmar y cuántos de esos ya se habían mandado.</summary>
        public static void ContarPendientes(out long sinConfirmar, out long yaEnviados)
        {
            using (var cn = Db.Abrir())
            {
                long conf = Confirmado(cn, null);
                sinConfirmar = Convert.ToInt64(Db.Scalar(cn, null, "SELECT COUNT(*) FROM cambios WHERE seq>@p0", conf));
                yaEnviados = Convert.ToInt64(Db.Scalar(cn, null, "SELECT COUNT(*) FROM cambios WHERE seq>@p0 AND seq<=(SELECT IFNULL(MAX(hasta),0) FROM envios)", conf));
            }
        }

        /// <summary>OFICINA: escribe en la carpeta elegida un archivo con todo lo no confirmado. Devuelve la ruta.</summary>
        public static string Crear(string carpeta, string usuario)
        {
            if (!EsOficina) throw new InvalidOperationException("Este equipo no es la oficina.");
            var vista = VistaPendientes();
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                long conf = Confirmado(cn, tx);
                var cambios = Pendientes(cn, tx, conf);
                if (cambios.Count == 0) throw new InvalidOperationException("No hay cambios por enviar.");
                long num = Convert.ToInt64(Db.Scalar(cn, tx, "SELECT IFNULL(MAX(num),0)+1 FROM envios"));
                string id = EnvioArchivo.NuevoId(), tienda = Dato(cn, tx, "tienda_id"), privada = Dato(cn, tx, "privada"), ahora = Db.Ahora();
                var fichas = new Dictionary<string, object>();
                foreach (var uid in cambios.Select(c => c.Producto).Distinct()) { var f = FichaDe(cn, tx, uid); if (f != null) fichas[uid] = f; }
                var doc = new Dictionary<string, object>
                {
                    { "version", "1" }, { "id", id }, { "num", num.ToString() }, { "fecha", ahora }, { "usuario", usuario ?? "" }, { "tiendaId", tienda }, { "oficinaId", Dato(cn, tx, "oficina_id") },
                    { "oficina", Dato(cn, tx, "negocio") }, { "publica", EnvioArchivo.Publica(privada) }, { "desde", cambios[0].Seq.ToString() }, { "hasta", cambios[cambios.Count - 1].Seq.ToString() },
                    { "cambios", cambios.Select(c => (object)new List<object> { c.Seq.ToString(), c.Uid, c.Producto, c.CampoNombre, c.Antes, Num(c.Cantidad), c.Ref, c.Fecha }).ToList() },
                    { "fichas", fichas }
                };
                string ruta = Path.Combine(carpeta, "ENVIO_" + Limpio(Dato(cn, tx, "negocio")) + "_" + num.ToString("0000") + "_" + DateTime.Now.ToString("yyyy-MM-dd") + ".vfenvio");
                EnvioArchivo.Guardar(ruta, EnvioArchivo.Cerrar(EnvioArchivo.Envio, tienda, JsonMini.Escribir(doc), Clave(Dato(cn, tx, "clave")), privada));
                Db.Exec(cn, tx, "INSERT INTO envios(id,num,fecha,usuario,desde,hasta,archivo,resumen) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",
                    id, num, ahora, usuario ?? "", cambios[0].Seq, cambios[cambios.Count - 1].Seq, Path.GetFileName(ruta), vista.Resumen);
                tx.Commit();
                Respaldo.Pedir();
                return ruta;
            }
        }

        static string Limpio(string s)
        {
            var b = new System.Text.StringBuilder();
            foreach (char ch in (s ?? "").Trim().Normalize(System.Text.NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) b.Append(ch < 128 && char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '-');
            string t = b.ToString().Trim('-');
            return t == "" ? "TIENDA" : (t.Length > 24 ? t.Substring(0, 24) : t);
        }

        public static List<EnvioHecho> Hechos()
        {
            return Db.Lista("SELECT * FROM envios ORDER BY num DESC LIMIT 200", r => new EnvioHecho { Id = Db.S(r, "id"), Num = Db.L(r, "num"), Fecha = Db.S(r, "fecha"), Usuario = Db.S(r, "usuario"), Resumen = Db.S(r, "resumen"),
                Estado = Db.S(r, "estado"), Archivo = Db.S(r, "archivo"), Aplicado = Db.S(r, "aplicado"), Problemas = Db.S(r, "problemas"), Desde = Db.L(r, "desde"), Hasta = Db.L(r, "hasta") });
        }

        /// <summary>OFICINA: vuelve a poner por enviar lo de ese envío y los siguientes. No duplica: la tienda salta lo que ya tiene.</summary>
        public static void Reenviar(string envioId)
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                long desde = Convert.ToInt64(Db.Scalar(cn, tx, "SELECT desde FROM envios WHERE id=@p0", envioId) ?? 1L);
                if (desde - 1 < Confirmado(cn, tx)) Poner(cn, tx, "confirmado", (desde - 1).ToString());
                tx.Commit();
            }
            Respaldo.Pedir();
        }

        /// <summary>OFICINA: lee el acuse que dejó la tienda. Devuelve un texto para la persona.</summary>
        public static string LeerAcuse(string ruta)
        {
            if (!EsOficina) throw new InvalidOperationException("Este equipo no es la oficina.");
            var datos = EnvioArchivo.Cargar(ruta);
            if (EnvioArchivo.TiendaDe(datos, EnvioArchivo.Acuse) != Dato("tienda_id")) throw new EnvioInvalidoException("El acuse es de otra tienda.");
            var doc = JsonMini.Leer(EnvioArchivo.Abrir(datos, EnvioArchivo.Acuse, Clave(Dato("clave")))) as IDictionary<string, object>;
            if (doc == null || T(doc, "oficinaId") != Dato("oficina_id")) throw new EnvioInvalidoException("El acuse es de otra oficina.");
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                if (string.CompareOrdinal(T(doc, "fecha"), Dato(cn, tx, "acuse_fecha")) < 0) return "Ese acuse es más viejo que el último que se leyó; no se usó.";
                long ultimo = long.Parse(T(doc, "ultimo"), CultureInfo.InvariantCulture);
                if (ultimo > 0 && Convert.ToString(Db.Scalar(cn, tx, "SELECT uid FROM cambios WHERE seq=@p0", ultimo)) != T(doc, "ultimoUid"))
                    throw new EnvioInvalidoException("El acuse no corresponde a esta base. Si la oficina se restauró de un respaldo, hay que volver a enlazarla con la tienda.");
                Poner(cn, tx, "confirmado", ultimo.ToString());
                Poner(cn, tx, "acuse_fecha", T(doc, "fecha"));
                int problemas = 0;
                foreach (var o in (List<object>)doc["envios"])
                {
                    var e = (IDictionary<string, object>)o;
                    var lista = ((List<object>)e["problemas"]).Select(Convert.ToString).ToArray();
                    if (T(e, "estado") == "APLICADO") problemas += lista.Length;
                    Db.Exec(cn, tx, "UPDATE envios SET estado=@p0, aplicado=@p1, problemas=@p2 WHERE id=@p3", T(e, "estado"), T(e, "fecha") + " · " + T(e, "usuario"), string.Join("\n", lista), T(e, "id"));
                }
                object rep;
                bool conReporte = doc.TryGetValue("reporte", out rep) && rep is IDictionary<string, object>;
                if (conReporte) ReporteTienda.Guardar(cn, tx, (IDictionary<string, object>)rep, T(doc, "fecha"));
                long faltan = Convert.ToInt64(Db.Scalar(cn, tx, "SELECT COUNT(*) FROM cambios WHERE seq>@p0", ultimo));
                tx.Commit();
                Respaldo.Pedir();
                return "Acuse leído. " + (faltan == 0 ? "La tienda está al día." : "Quedan " + faltan + " cambios por enviar.") + (conReporte ? "\nSe actualizaron las ventas y existencias de la tienda (F5 Resultados)." : "") + (problemas > 0 ? "\n\nLa tienda reportó " + problemas + " problema(s): véalos en el historial." : "");
            }
        }

        // ================================================================== ¿hay algo nuevo en la USB? (solo lee)
        /// <summary>TIENDA: si el archivo es un envío de esta tienda con algo que todavía no tiene, qué trae. null si no (o si no es válido).</summary>
        public static NovedadEnvio Novedad(string ruta)
        {
            try
            {
                string tienda = Dato("tienda_id"), clave = Dato("clave"), pub = Dato("oficina_pub");
                if (!EsTienda || clave == "") return null;
                var datos = EnvioArchivo.Cargar(ruta);
                if (EnvioArchivo.TiendaDe(datos, EnvioArchivo.Envio) != tienda) return null;
                var doc = JsonMini.Leer(EnvioArchivo.Abrir(datos, EnvioArchivo.Envio, Clave(clave), pub == "" ? null : pub)) as IDictionary<string, object>;
                if (doc == null) return null;
                long ultimo = Convert.ToInt64(Db.Scalar("SELECT IFNULL(MAX(seq),0) FROM recibidos"));
                int n = ((List<object>)doc["cambios"]).Count(o => long.Parse((string)((List<object>)o)[0], CultureInfo.InvariantCulture) > ultimo);
                return n == 0 ? null : new NovedadEnvio { Num = long.Parse(T(doc, "num"), CultureInfo.InvariantCulture), Hasta = long.Parse(T(doc, "hasta"), CultureInfo.InvariantCulture), Cambios = n };
            }
            catch (Exception) { return null; }   // dañado, de otra oficina o se sacó la USB: no se avisa (al abrirlo a mano sí dice por qué)
        }

        /// <summary>OFICINA: el acuse es de esta tienda y más nuevo que el último leído.</summary>
        public static bool AcuseNuevo(string ruta)
        {
            try
            {
                if (!EsOficina) return false;
                var datos = EnvioArchivo.Cargar(ruta);
                if (EnvioArchivo.TiendaDe(datos, EnvioArchivo.Acuse) != Dato("tienda_id")) return false;
                var doc = JsonMini.Leer(EnvioArchivo.Abrir(datos, EnvioArchivo.Acuse, Clave(Dato("clave")))) as IDictionary<string, object>;
                return doc != null && T(doc, "oficinaId") == Dato("oficina_id") && string.CompareOrdinal(T(doc, "fecha"), Dato("acuse_fecha")) > 0;
            }
            catch (Exception) { return false; }
        }

        // ================================================================== TIENDA
        /// <summary>TIENDA: qué haría el envío, sin cambiar nada (es la misma operación, que al final se descarta).</summary>
        public static ResultadoEnvio Revisar(string ruta) { return Procesar(ruta, 0, "", false); }

        /// <summary>TIENDA: aplica el envío. Todo en una sola operación: si algo falla a la mitad, no se cambia nada.</summary>
        public static ResultadoEnvio Aplicar(string ruta, long usuarioId, string usuario) { return Procesar(ruta, usuarioId, usuario, true); }

        static ResultadoEnvio Procesar(string ruta, long usuarioId, string usuario, bool deVeras)
        {
            string tienda = Dato("tienda_id"), clave = Dato("clave"), pub = Dato("oficina_pub");
            if (EsOficina) throw new InvalidOperationException("Este equipo es la oficina: los envíos se aplican en la caja.");
            if (!EsTienda || clave == "") throw new InvalidOperationException("Esta caja todavía no está enlazada con su oficina (Ajustes → Envíos).");
            var datos = EnvioArchivo.Cargar(ruta);
            string para = EnvioArchivo.TiendaDe(datos, EnvioArchivo.Envio);
            if (para == null) throw new EnvioInvalidoException("Ese archivo no es un envío de VentaFácil o está incompleto.");
            if (para != tienda) throw new EnvioInvalidoException("Este envío es para otra tienda. No se usó.");
            var doc = JsonMini.Leer(EnvioArchivo.Abrir(datos, EnvioArchivo.Envio, Clave(clave), pub == "" ? null : pub)) as IDictionary<string, object>;
            if (doc == null || T(doc, "version") != "1") throw new EnvioInvalidoException("El envío es de otra versión de VentaFácil. Actualice el programa en la caja.");
            if (T(doc, "tiendaId") != tienda) throw new EnvioInvalidoException("Este envío es para otra tienda. No se usó.");
            string oficina = T(doc, "oficinaId");
            bool primero = pub == "";
            if (primero) { pub = T(doc, "publica"); if (!EnvioArchivo.FirmaValida(datos, pub)) throw new EnvioInvalidoException("El archivo no está bien firmado. No se usó."); }
            else if (oficina != Dato("oficina_id")) throw new EnvioInvalidoException("Este envío es de otra oficina. No se usó.");

            var r = new ResultadoEnvio { EnvioId = T(doc, "id"), Num = long.Parse(T(doc, "num"), CultureInfo.InvariantCulture), Oficina = T(doc, "oficina"), Usuario = T(doc, "usuario"), Fecha = T(doc, "fecha"), PrimerEnvio = primero };
            var cambios = new List<Cambio>();
            foreach (var o in (List<object>)doc["cambios"])
            {
                var c = (List<object>)o;
                cambios.Add(new Cambio { Seq = long.Parse((string)c[0], CultureInfo.InvariantCulture), Uid = (string)c[1], Producto = (string)c[2], CampoNombre = (string)c[3], Antes = (string)c[4], Cantidad = Leer((string)c[5]), Ref = (string)c[6], Fecha = (string)c[7] });
            }
            var fichas = (IDictionary<string, object>)doc["fichas"];

            long yaHay = Convert.ToInt64(Db.Scalar("SELECT IFNULL(MAX(seq),0) FROM recibidos"));
            if (deVeras && cambios.Any(x => x.Seq > yaHay)) r.Respaldo = Db.Respaldar();
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                long ultimo = Convert.ToInt64(Db.Scalar(cn, tx, "SELECT IFNULL(MAX(seq),0) FROM recibidos"));
                // Lo que ya se tenía debe ser exactamente lo mismo; si no, la oficina se restauró de un respaldo y sus números ya no coinciden
                foreach (var c in cambios.Where(x => x.Seq <= ultimo))
                    if (Convert.ToString(Db.Scalar(cn, tx, "SELECT uid FROM recibidos WHERE seq=@p0", c.Seq)) != c.Uid)
                        throw new EnvioInvalidoException("El historial de la oficina no coincide con el de esta tienda (¿se restauró un respaldo en la oficina?). No se aplicó nada. Hay que volver a enlazarlas.");
                var nuevos = cambios.Where(x => x.Seq > ultimo).OrderBy(x => x.Seq).ToList();
                if (nuevos.Count == 0) { r.YaAplicado = true; return r; }
                if (nuevos[0].Seq != ultimo + 1)
                    throw new EnvioInvalidoException("Falta un envío anterior: este empieza en el cambio " + nuevos[0].Seq + " y la tienda va en el " + ultimo + ". No se aplicó nada.\n\nLleve a la oficina el acuse de esta tienda y vuelvan a crear el envío.");
                for (int i = 1; i < nuevos.Count; i++) if (nuevos[i].Seq != nuevos[i - 1].Seq + 1) throw new EnvioInvalidoException("El envío está incompleto. No se aplicó nada.");

                long orden = Db.InsertId(cn, tx, "INSERT INTO envios_aplicados(id,num,oficina_id,fecha_envio,fecha,usuario_id,usuario,respaldo) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",
                    r.EnvioId, r.Num, oficina, r.Fecha, Db.Ahora(), usuarioId, usuario ?? "", r.Respaldo);
                var ap = new Aplicador { Cn = cn, Tx = tx, R = r, UsuarioId = usuarioId, Referencia = "Envío " + r.Num };
                var grupos = nuevos.GroupBy(x => x.Producto).ToList();
                // Primero las bajas (liberan códigos), luego los datos y al final la mercancía
                foreach (var g in grupos.Where(x => !fichas.ContainsKey(x.Key))) ap.Baja(g.Key);
                foreach (var g in grupos.Where(x => fichas.ContainsKey(x.Key))) ap.Ficha(g.Key, (IDictionary<string, object>)fichas[g.Key], g.ToList());
                foreach (var g in grupos) ap.Mercancia(g.Key, fichas.ContainsKey(g.Key) ? (IDictionary<string, object>)fichas[g.Key] : null, g.ToList());
                AlertarBajas(r);

                foreach (var l in r.Lineas)
                    Db.Exec(cn, tx, "INSERT INTO envio_detalle(orden,producto_id,tipo,codigo,producto,campo,antes,despues,cantidad,nota) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)",
                        orden, l.ProductoId, l.Tipo, l.Codigo, l.Producto, l.Campo, l.Antes, l.Despues, l.Cantidad, l.Nota);
                foreach (var c in nuevos) Db.Exec(cn, tx, "INSERT INTO recibidos(seq,uid,envio_id) VALUES(@p0,@p1,@p2)", c.Seq, c.Uid, orden.ToString());
                Db.Exec(cn, tx, "UPDATE envios_aplicados SET resumen=@p0 WHERE orden=@p1", r.Resumen, orden);
                if (primero) { Poner(cn, tx, "oficina_id", oficina); Poner(cn, tx, "oficina_pub", pub); }
                if (!deVeras) { tx.Rollback(); return r; }
                tx.Commit();
            }
            Respaldo.Pedir();
            return r;
        }

        /// <summary>Hace en la tienda lo que pide cada producto del envío y anota cada cosa en R.Lineas (con el nombre interno del campo para poder deshacer).</summary>
        class Aplicador
        {
            public SQLiteConnection Cn; public SQLiteTransaction Tx; public ResultadoEnvio R; public long UsuarioId; public string Referencia;
            /// <summary>Productos que no se pudieron dar de alta: su mercancía también es problema.</summary>
            readonly Dictionary<string, string> _fallidos = new Dictionary<string, string>();

            Producto Buscar(string uid) { return Db.Lista(Cn, Tx, "SELECT p.*, '' AS departamento FROM productos p WHERE p.uid=@p0", Producto.De, uid).FirstOrDefault(); }
            void Linea(string tipo, Producto p, string campo = "", string antes = "", string despues = "", decimal cantidad = 0, string nota = "")
            { R.Lineas.Add(new LineaEnvio { Tipo = tipo, ProductoId = p == null ? 0 : p.Id, Codigo = p == null ? "" : p.Codigo, Producto = p == null ? "" : p.Descripcion, Campo = campo, Antes = antes, Despues = despues, Cantidad = cantidad, Nota = nota }); }

            public void Baja(string uid)
            {
                var p = Buscar(uid);
                if (p == null || !p.Activo) return;
                Db.Exec(Cn, Tx, "UPDATE productos SET activo=0, actualizado=@p0 WHERE id=@p1", Db.Ahora(), p.Id);
                Linea("Baja", p, "activo", "1", "0");
            }

            Producto Dueno(string codigo, long excluir)
            { return Db.Lista(Cn, Tx, "SELECT p.*, '' AS departamento FROM productos p WHERE p.id<>@p1 AND (p.codigo=@p0 OR (p.codigo2<>'' AND p.codigo2=@p0)) ORDER BY p.activo DESC LIMIT 1", Producto.De, codigo, excluir).FirstOrDefault(); }

            /// <summary>Si el código lo tiene un producto dado de baja, se libera. Devuelve el producto ACTIVO que lo tiene, o null si quedó libre.</summary>
            Producto Liberar(string codigo, long excluir)
            {
                for (int i = 0; i < 20; i++)
                {
                    var d = Dueno(codigo, excluir);
                    if (d == null || d.Activo) return d;
                    if (string.Equals(d.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
                    {
                        string libre = d.Codigo + "~" + d.Id;
                        Db.Exec(Cn, Tx, "UPDATE productos SET codigo=@p0 WHERE id=@p1", libre, d.Id);
                        Linea("Dato", d, "codigo", d.Codigo, libre, 0, "Estaba dado de baja; se liberó su código");
                    }
                    else
                    {
                        Db.Exec(Cn, Tx, "UPDATE productos SET codigo2='' WHERE id=@p0", d.Id);
                        Linea("Dato", d, "codigo2", d.Codigo2, "", 0, "Estaba dado de baja; se liberó su código");
                    }
                }
                return Dueno(codigo, excluir);
            }

            public void Ficha(string uid, IDictionary<string, object> f, List<Cambio> cambios)
            {
                var p = Buscar(uid);
                int desde = R.Lineas.Count;
                bool todo = cambios.Any(c => c.CampoNombre == "alta");
                if (p == null)
                {
                    if (!todo && cambios.All(c => c.CampoNombre == "existencia")) { _fallidos[uid] = "El producto no existe en la tienda"; return; }
                    var d = Dueno(T(f, "codigo"), 0);
                    if (d != null && string.Equals(d.Codigo, T(f, "codigo"), StringComparison.OrdinalIgnoreCase) && ZafirahMapeo.Norm(d.Descripcion) == ZafirahMapeo.Norm(T(f, "descripcion")))
                    {   // el mismo producto, dado de alta por separado en la tienda: se enlaza
                        Db.Exec(Cn, Tx, "UPDATE productos SET uid=@p0 WHERE id=@p1", uid, d.Id);
                        p = Buscar(uid); todo = true;
                    }
                    else
                    {
                        d = Liberar(T(f, "codigo"), 0);
                        if (d != null)
                        {
                            _fallidos[uid] = "El código " + T(f, "codigo") + " ya es de «" + d.Descripcion + "» en la tienda";
                            R.Lineas.Add(new LineaEnvio { Tipo = "Problema", Codigo = T(f, "codigo"), Producto = T(f, "descripcion"), Campo = "alta", Nota = _fallidos[uid] + ": no se agregó" });
                            return;
                        }
                        string nota = "", c2 = T(f, "codigo2");
                        if (c2 != "") { d = Liberar(c2, 0); if (d != null) { nota = "Sin código de barras: " + c2 + " ya es de «" + d.Descripcion + "»"; f = new Dictionary<string, object>(f); f["codigo2"] = ""; } }
                        Insertar(Cn, Tx, uid, f);
                        p = Buscar(uid);
                        Linea("Nuevo", p, "alta", "", T(f, "precio"), 0, nota);
                        Alertar(R, f, desde);
                        return;
                    }
                }
                bool toco = false;
                foreach (var campo in Campos)
                {
                    var suyos = cambios.Where(c => c.CampoNombre == campo.Nombre).ToList();
                    if (suyos.Count == 0 && !todo) continue;
                    string actual = Actual(p.Id, campo), nuevo = T(f, campo.Nombre);
                    if (actual == nuevo) continue;
                    if ((campo.Nombre == "codigo" || campo.Nombre == "codigo2") && nuevo != "")
                    {
                        var d = Liberar(nuevo, p.Id);
                        if (d != null) { Linea("Problema", p, campo.Nombre, actual, nuevo, 0, campo.Titulo + " " + nuevo + " ya es de «" + d.Descripcion + "»: se quedó " + (actual == "" ? "vacío" : actual)); continue; }
                    }
                    Escribir(Cn, Tx, p.Id, campo, nuevo);
                    string aviso = suyos.Count > 0 && suyos[0].Antes != actual ? "En la tienda se había cambiado; queda lo de la oficina" : "";
                    Linea(TipoDe(campo, nuevo), p, campo.Nombre, actual, nuevo, 0, aviso);
                    toco = true;
                }
                if (toco) { Db.Exec(Cn, Tx, "UPDATE productos SET actualizado=@p0 WHERE id=@p1", Db.Ahora(), p.Id); Alertar(R, f, desde); }
            }

            public void Mercancia(string uid, IDictionary<string, object> f, List<Cambio> cambios)
            {
                decimal q = cambios.Where(c => c.CampoNombre == "existencia").Sum(c => c.Cantidad);
                if (q == 0) return;
                var p = Buscar(uid);
                if (p == null && f == null && cambios.Any(c => c.CampoNombre == "alta")) return;   // se creó y se borró en la oficina antes de enviarse
                if (p == null || !p.UsaInventario)
                {
                    string porque;
                    if (p != null) porque = "El producto no controla existencia"; else if (!_fallidos.TryGetValue(uid, out porque)) porque = "El producto no existe en la tienda";
                    R.Lineas.Add(new LineaEnvio { Tipo = "Problema", ProductoId = p == null ? 0 : p.Id, Codigo = p != null ? p.Codigo : T(f, "codigo"), Producto = p != null ? p.Descripcion : T(f, "descripcion"), Campo = "existencia", Cantidad = q, Nota = porque + ": no se " + (q > 0 ? "sumaron " : "restaron ") + Fmt.Dec(Math.Abs(q)) });
                    return;
                }
                decimal antes = p.Existencia;
                string refs = Refs(cambios);
                decimal ahora = ProductoRepo.MoverExistencia(Cn, Tx, p.Id, q, q > 0 ? "ENTRADA" : "SALIDA", Referencia + (refs == "" ? "" : ": " + refs), UsuarioId);
                Linea(q > 0 ? "Entrada" : "Salida", p, "existencia", Num(antes), Num(ahora), q, refs + (ahora < 0 ? "  (queda negativa)" : ""));
            }

            string Actual(long id, Campo c)
            {
                return Valor(c, Db.Scalar(Cn, Tx, c.Clase == 'D' ? "SELECT d.nombre FROM productos p JOIN departamentos d ON d.id=p.departamento_id WHERE p.id=@p0" : "SELECT " + c.Columna + " FROM productos WHERE id=@p0", id));
            }
        }

        /// <summary>TIENDA: historial, el más nuevo primero.</summary>
        public static List<EnvioRecibido> Recibidos()
        {
            return Db.Lista("SELECT * FROM envios_aplicados ORDER BY orden DESC LIMIT 200", r => new EnvioRecibido { Orden = Db.L(r, "orden"), Id = Db.S(r, "id"), Num = Db.L(r, "num"), FechaEnvio = Db.S(r, "fecha_envio"), Fecha = Db.S(r, "fecha"),
                Usuario = Db.S(r, "usuario"), Resumen = Db.S(r, "resumen"), Estado = Db.S(r, "estado"), OficinaId = Db.S(r, "oficina_id") });
        }

        public static List<LineaEnvio> Detalle(long orden) { using (var cn = Db.Abrir()) return Detalle(cn, null, orden); }

        static List<LineaEnvio> Detalle(SQLiteConnection cn, SQLiteTransaction tx, long orden)
        {
            return Db.Lista(cn, tx, "SELECT * FROM envio_detalle WHERE orden=@p0 ORDER BY id", r => new LineaEnvio { Tipo = Db.S(r, "tipo"), ProductoId = Db.L(r, "producto_id"), Codigo = Db.S(r, "codigo"), Producto = Db.S(r, "producto"),
                Campo = Db.S(r, "campo"), Antes = Db.S(r, "antes"), Despues = Db.S(r, "despues"), Cantidad = Db.D(r, "cantidad"), Nota = Db.S(r, "nota") }, orden);
        }

        /// <summary>
        /// TIENDA: revierte el ÚLTIMO envío aplicado, sin tocar ventas. Lo que alguien haya cambiado después se respeta.
        /// Ese envío se puede volver a aplicar. Devuelve lo que no se pudo revertir.
        /// </summary>
        public static List<string> Deshacer(long orden, long usuarioId)
        {
            var noSe = new List<string>();
            Db.Respaldar();
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                long ultimo = Convert.ToInt64(Db.Scalar(cn, tx, "SELECT IFNULL(MAX(orden),0) FROM envios_aplicados WHERE estado='APLICADO' AND oficina_id=@p0", Dato(cn, tx, "oficina_id")));
                if (ultimo == 0 || ultimo != orden) throw new InvalidOperationException("Solo se puede deshacer el último envío aplicado.");
                string num = Convert.ToString(Db.Scalar(cn, tx, "SELECT num FROM envios_aplicados WHERE orden=@p0", orden));
                var lineas = Detalle(cn, tx, orden);
                lineas.Reverse();
                foreach (var l in lineas)
                {
                    if (l.Tipo == "Problema" || l.ProductoId == 0) continue;
                    if (Convert.ToInt64(Db.Scalar(cn, tx, "SELECT COUNT(*) FROM productos WHERE id=@p0", l.ProductoId)) == 0) { noSe.Add(l.Producto + ": ya no existe"); continue; }
                    if (l.Campo == "existencia") { ProductoRepo.MoverExistencia(cn, tx, l.ProductoId, -l.Cantidad, "AJUSTE", "Deshacer envío " + num, usuarioId); continue; }
                    if (l.Campo == "alta") { Db.Exec(cn, tx, "UPDATE productos SET activo=0, actualizado=@p0 WHERE id=@p1", Db.Ahora(), l.ProductoId); continue; }
                    var c = BuscarCampo(l.Campo);
                    if (c == null) continue;
                    string actual = Valor(c, Db.Scalar(cn, tx, c.Clase == 'D' ? "SELECT d.nombre FROM productos p JOIN departamentos d ON d.id=p.departamento_id WHERE p.id=@p0" : "SELECT " + c.Columna + " FROM productos WHERE id=@p0", l.ProductoId));
                    if (actual != l.Despues) { noSe.Add(l.Producto + ": " + c.Titulo + " se cambió después (" + actual + "); no se tocó"); continue; }
                    if ((c.Nombre == "codigo" || c.Nombre == "codigo2") && l.Antes != "" &&
                        Convert.ToInt64(Db.Scalar(cn, tx, "SELECT COUNT(*) FROM productos WHERE id<>@p1 AND (codigo=@p0 OR (codigo2<>'' AND codigo2=@p0))", l.Antes, l.ProductoId)) > 0)
                    { noSe.Add(l.Producto + ": el código " + l.Antes + " ya lo tiene otro producto"); continue; }
                    Escribir(cn, tx, l.ProductoId, c, l.Antes);
                }
                Db.Exec(cn, tx, "DELETE FROM recibidos WHERE envio_id=@p0", orden.ToString());
                Db.Exec(cn, tx, "UPDATE envios_aplicados SET estado='DESHECHO' WHERE orden=@p0", orden);
                tx.Commit();
            }
            Respaldo.Pedir();
            return noSe;
        }

        /// <summary>TIENDA: deja junto al envío el acuse que la oficina leerá para saber hasta dónde va la tienda.</summary>
        public static string EscribirAcuse(string carpeta)
        {
            string tienda = Dato("tienda_id");
            long ultimo = Convert.ToInt64(Db.Scalar("SELECT IFNULL(MAX(seq),0) FROM recibidos"));
            var envios = Recibidos().Where(e => e.OficinaId == Dato("oficina_id")).GroupBy(e => e.Id).Select(g => g.First()).Take(30).Select(e => (object)new Dictionary<string, object>
            {
                { "id", e.Id }, { "estado", e.Estado }, { "fecha", e.Fecha }, { "usuario", e.Usuario },
                { "problemas", Detalle(e.Orden).Where(l => l.Tipo == "Problema").Select(l => (object)((l.Codigo + " " + l.Producto).Trim() + ": " + l.Nota)).ToList() }
            }).ToList();
            var doc = new Dictionary<string, object> { { "version", "1" }, { "tiendaId", tienda }, { "oficinaId", Dato("oficina_id") }, { "fecha", Db.Ahora() }, { "ultimo", ultimo.ToString() },
                { "ultimoUid", Convert.ToString(Db.Scalar("SELECT uid FROM recibidos WHERE seq=@p0", ultimo)) ?? "" }, { "envios", envios } };
            // Ventas, existencias y cortes para la oficina. Si algo falla al armarlos, el acuse sale de todos modos
            try { doc["reporte"] = ReporteTienda.Armar(); } catch (Exception ex) { Log.Error(ex, "Reporte para la oficina"); }
            string ruta = Path.Combine(carpeta, "ACUSE_" + tienda.Substring(0, 6).ToUpperInvariant() + ".vfacuse");
            EnvioArchivo.Guardar(ruta, EnvioArchivo.Cerrar(EnvioArchivo.Acuse, tienda, JsonMini.Escribir(doc), Clave(Dato("clave"))));
            return ruta;
        }

        // ================================================================== alertas
        static void Alertar(ResultadoEnvio r, IDictionary<string, object> f, int desde)
        {
            decimal precio = Leer(T(f, "precio")), costo = Leer(T(f, "costo"));
            string nombre = T(f, "codigo") + " " + T(f, "descripcion");
            var suyas = r.Lineas.Skip(desde).ToList();   // las de este producto son las últimas
            bool tocaPrecio = suyas.Any(l => (l.Tipo == "Precio" || l.Tipo == "Costo" || l.Tipo == "Nuevo"));
            if (tocaPrecio && precio > 0 && costo > precio) r.Alertas.Add(nombre + ": el precio " + Fmt.Dinero(precio) + " queda por debajo del costo " + Fmt.Dinero(costo));
            foreach (var l in suyas.Where(x => (x.Campo == "precio" || x.Campo == "Precio") && x.Tipo == "Precio"))
            {
                decimal a = Leer(l.Antes), d = Leer(l.Despues);
                if (d <= 0) r.Alertas.Add(nombre + ": el precio queda en cero");
                else if (a > 0 && Math.Abs(d - a) / a > 0.30m) r.Alertas.Add(nombre + ": de " + Fmt.Dinero(a) + " a " + Fmt.Dinero(d) + " (" + (d > a ? "sube " : "baja ") + Math.Round(Math.Abs(d - a) / a * 100) + " %)");
            }
        }

        static void AlertarBajas(ResultadoEnvio r) { int n = r.Lineas.Count(l => l.Tipo == "Baja"); if (n > 10) r.Alertas.Add("Se dan de baja " + n + " productos"); }

        // ================================================================== valores
        internal const string Sel = "SELECT p.*, IFNULL(d.nombre,'') AS departamento FROM productos p LEFT JOIN departamentos d ON d.id=p.departamento_id ";

        internal static Dictionary<string, object> Ficha(System.Data.IDataRecord r)
        {
            var f = new Dictionary<string, object> { { "uid", Db.S(r, "uid") } };
            foreach (var c in Campos) f[c.Nombre] = Valor(c, r[c.Clase == 'D' ? "departamento" : c.Columna]);
            return f;
        }

        static Dictionary<string, object> FichaDe(SQLiteConnection cn, SQLiteTransaction tx, string uid) { return Db.Lista(cn, tx, Sel + "WHERE p.uid=@p0", Ficha, uid).FirstOrDefault(); }

        internal static string T(IDictionary<string, object> d, string clave) { object v; return d != null && d.TryGetValue(clave, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }
        internal static string Num(decimal v) { return v.ToString("0.######", CultureInfo.InvariantCulture); }
        internal static decimal Leer(string s) { decimal d; return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : 0m; }

        /// <summary>Texto único de un valor, para comparar sin ruido de decimales (92, 92.0 y 92.00 son iguales).</summary>
        static string Valor(Campo c, object v)
        {
            if (v == null || v is DBNull) return c != null && c.Clase != 'T' && c.Clase != 'D' ? "0" : "";
            if (c == null || c.Clase == 'T' || c.Clase == 'D') return Convert.ToString(v, CultureInfo.InvariantCulture).Trim();
            return c.Clase == 'B' ? (Convert.ToInt64(v) != 0 ? "1" : "0") : Num(Db.Dec(v));
        }

        /// <summary>Cómo se ve un valor en pantalla.</summary>
        public static string Bonito(string campo, string v)
        {
            var c = PorNombreOTitulo(campo == "alta" ? "precio" : campo);
            return c == null ? v : Bonito(c, v);
        }
        static string Bonito(Campo c, string v)
        {
            if (c.Clase == 'B') return v == "1" ? "Sí" : v == "" ? "" : "No";
            if (c.Nombre.StartsWith("precio") || c.Nombre.StartsWith("costo")) return v == "" ? "" : Fmt.Dinero(Leer(v));
            return v;
        }
        public static string TituloCampo(string campo) { var c = BuscarCampo(campo); return c != null ? c.Titulo : campo == "alta" ? "Precio" : campo == "existencia" ? "Existencia" : campo; }

        static string TipoDe(Campo c, string nuevo) { return c.Nombre == "activo" ? (nuevo == "1" ? "Reactivado" : "Baja") : c.Tipo; }
        static string Refs(IEnumerable<Cambio> cs) { return string.Join(", ", cs.Where(c => c.CampoNombre == "existencia" && !string.IsNullOrEmpty(c.Ref)).Select(c => c.Ref).Distinct().Take(3).ToArray()); }

        internal static void Escribir(SQLiteConnection cn, SQLiteTransaction tx, long id, Campo c, string valor)
        {
            Db.Exec(cn, tx, "UPDATE productos SET " + c.Columna + "=@p0 WHERE id=@p1", ParaGuardar(cn, tx, c, valor), id);
        }

        static object ParaGuardar(SQLiteConnection cn, SQLiteTransaction tx, Campo c, string valor)
        {
            if (c.Clase == 'D') return valor == "" ? (object)null : DepartamentoRepo.ObtenerOCrear(cn, tx, valor);
            if (c.Clase == 'N') return Leer(valor);
            if (c.Clase == 'B') return valor == "1" ? 1 : 0;
            return valor;
        }

        /// <summary>Da de alta el producto con su identificador y existencia en cero (la mercancía llega aparte).</summary>
        internal static void Insertar(SQLiteConnection cn, SQLiteTransaction tx, string uid, IDictionary<string, object> f)
        {
            var columnas = new List<string> { "uid", "actualizado" };
            var valores = new List<object> { uid, Db.Ahora() };
            foreach (var c in Campos) { columnas.Add(c.Columna); valores.Add(ParaGuardar(cn, tx, c, T(f, c.Nombre))); }
            Db.Exec(cn, tx, "INSERT INTO productos(" + string.Join(",", columnas.ToArray()) + ") VALUES(" + string.Join(",", valores.Select((v, i) => "@p" + i).ToArray()) + ")", valores.ToArray());
        }
    }
}
