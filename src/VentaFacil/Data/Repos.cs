using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;

namespace VentaFacil.Data
{
    public static class ConfigRepo
    {
        public static Dictionary<string, string> Todo()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in Db.Lista("SELECT clave, valor FROM config", r => new KeyValuePair<string, string>(Db.S(r, "clave"), Db.S(r, "valor"))))
                d[kv.Key] = kv.Value;
            return d;
        }

        public static void Guardar(Dictionary<string, string> valores)
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                foreach (var kv in valores)
                    Db.Exec(cn, tx, "INSERT OR REPLACE INTO config(clave, valor) VALUES(@p0, @p1)", kv.Key, kv.Value);
                tx.Commit();
            }
            Respaldo.Pedir();
        }
    }

    public static class UsuarioRepo
    {
        public static Usuario Validar(string login, string clave)
        {
            var u = Db.Lista("SELECT * FROM usuarios WHERE usuario=@p0 AND clave_hash=@p1 AND activo=1", Usuario.De, login.Trim(), Core.Seguridad.Hash(clave));
            return u.Count > 0 ? u[0] : null;
        }

        public static List<Usuario> Todos() { return Db.Lista("SELECT * FROM usuarios ORDER BY nombre", Usuario.De); }
        public static Usuario Obtener(long id) { return Db.Lista("SELECT * FROM usuarios WHERE id=@p0", Usuario.De, id).FirstOrDefault(); }

        public static void Guardar(Usuario u, string claveNueva)
        {
            if (u.Id == 0)
                u.Id = Convert.ToInt64(Db.Scalar("INSERT INTO usuarios(usuario,nombre,clave_hash,rol,activo) VALUES(@p0,@p1,@p2,@p3,@p4); SELECT last_insert_rowid();",
                    u.Login.Trim(), u.Nombre.Trim(), Core.Seguridad.Hash(claveNueva ?? ""), u.Rol, u.Activo ? 1 : 0));
            else
            {
                Db.Exec("UPDATE usuarios SET usuario=@p0, nombre=@p1, rol=@p2, activo=@p3 WHERE id=@p4", u.Login.Trim(), u.Nombre.Trim(), u.Rol, u.Activo ? 1 : 0, u.Id);
                if (!string.IsNullOrEmpty(claveNueva))
                    Db.Exec("UPDATE usuarios SET clave_hash=@p0 WHERE id=@p1", Core.Seguridad.Hash(claveNueva), u.Id);
            }
            Respaldo.Pedir();
        }

        public static int AdminsActivos() { return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM usuarios WHERE rol='ADMIN' AND activo=1")); }
    }

    public static class DepartamentoRepo
    {
        public static List<Departamento> Todos()
        {
            return Db.Lista("SELECT id, nombre FROM departamentos ORDER BY nombre", r => new Departamento { Id = Db.L(r, "id"), Nombre = Db.S(r, "nombre") });
        }

        public static long ObtenerOCrear(SQLiteConnection cn, SQLiteTransaction tx, string nombre)
        {
            nombre = (nombre ?? "").Trim();
            if (nombre == "") nombre = "General";
            var id = Db.Scalar(cn, tx, "SELECT id FROM departamentos WHERE nombre=@p0", nombre);
            if (id != null) return Convert.ToInt64(id);
            return Db.InsertId(cn, tx, "INSERT INTO departamentos(nombre) VALUES(@p0)", nombre);
        }

        public static void Guardar(Departamento d)
        {
            if (d.Id == 0) Db.Exec("INSERT INTO departamentos(nombre) VALUES(@p0)", d.Nombre.Trim());
            else Db.Exec("UPDATE departamentos SET nombre=@p0 WHERE id=@p1", d.Nombre.Trim(), d.Id);
        }

        public static bool Eliminar(long id)
        {
            if (Convert.ToInt64(Db.Scalar("SELECT COUNT(*) FROM productos WHERE departamento_id=@p0", id)) > 0) return false;
            Db.Exec("DELETE FROM departamentos WHERE id=@p0", id);
            return true;
        }
    }

    public static class ProductoRepo
    {
        const string Base = "SELECT p.*, IFNULL(d.nombre,'') AS departamento FROM productos p LEFT JOIN departamentos d ON d.id=p.departamento_id ";

        /// <summary>Busca por código de barras o código alterno exacto.</summary>
        public static Producto PorCodigo(string codigo)
        {
            codigo = (codigo ?? "").Trim();
            if (codigo == "") return null;
            return Db.Lista(Base + "WHERE p.activo=1 AND (p.codigo=@p0 OR (p.codigo2<>'' AND p.codigo2=@p0)) LIMIT 1", Producto.De, codigo).FirstOrDefault();
        }

        public static Producto Obtener(long id) { return Db.Lista(Base + "WHERE p.id=@p0", Producto.De, id).FirstOrDefault(); }

        /// <summary>Búsqueda por texto: todas las palabras deben aparecer en la descripción o código.</summary>
        public static List<Producto> Buscar(string texto, bool incluirInactivos = false, int limite = 500)
        {
            var palabras = (texto ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var sql = new StringBuilder(Base + "WHERE 1=1 ");
            if (!incluirInactivos) sql.Append("AND p.activo=1 ");
            var args = new List<object>();
            foreach (var w in palabras)
            {
                sql.AppendFormat("AND (p.descripcion LIKE @p{0} OR p.codigo LIKE @p{0} OR p.codigo2 LIKE @p{0} OR p.proveedor LIKE @p{0}) ", args.Count);
                args.Add("%" + w + "%");
            }
            sql.Append("ORDER BY p.descripcion LIMIT " + limite);
            return Db.Lista(sql.ToString(), Producto.De, args.ToArray());
        }

        public static List<string> Proveedores()
        {
            return Db.Lista("SELECT DISTINCT proveedor FROM productos WHERE proveedor<>'' ORDER BY proveedor", r => Db.S(r, "proveedor"));
        }

        // ---------- códigos de barras (se guardan en el código alterno; el código corto no se toca) ----------

        /// <summary>Quién tiene ese código (como código corto o como código de barras), aunque esté desactivado.</summary>
        public static Producto QuienTieneCodigo(string codigo, long excluirId)
        {
            codigo = (codigo ?? "").Trim();
            if (codigo == "") return null;
            return Db.Lista(Base + "WHERE p.id<>@p1 AND (p.codigo=@p0 OR (p.codigo2<>'' AND p.codigo2=@p0)) LIMIT 1", Producto.De, codigo, excluirId).FirstOrDefault();
        }

        /// <summary>
        /// Pone el código de barras (código alterno) de un producto. Devuelve null si quedó, o el producto que ya
        /// tenía ese código (no se asigna: un código no puede ser de dos productos).
        /// </summary>
        public static Producto AsignarCodigoBarras(long productoId, string codigo)
        {
            codigo = (codigo ?? "").Trim();
            if (codigo == "" || codigo.Length > 40 || codigo.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) >= 0)
                throw new ArgumentException("El código de barras no puede estar vacío, tener espacios ni pasar de 40 caracteres.");
            var otro = QuienTieneCodigo(codigo, productoId);
            if (otro != null) return otro;
            Db.Exec("UPDATE productos SET codigo2=@p0, sin_barras=0, actualizado=@p1 WHERE id=@p2", codigo, Db.Ahora(), productoId);
            Respaldo.Pedir();
            return null;
        }

        /// <summary>Quita el código de barras del producto (vuelve a quedar solo con su código corto).</summary>
        public static void QuitarCodigoBarras(long productoId)
        {
            Db.Exec("UPDATE productos SET codigo2='', actualizado=@p0 WHERE id=@p1", Db.Ahora(), productoId);
            Respaldo.Pedir();
        }

        /// <summary>Marca (o desmarca) que el producto no lleva código de barras: deja de salir en la lista de pendientes.</summary>
        public static void MarcarSinBarras(long productoId, bool sinBarras)
        {
            Db.Exec("UPDATE productos SET sin_barras=@p0, actualizado=@p1 WHERE id=@p2", sinBarras ? 1 : 0, Db.Ahora(), productoId);
            Respaldo.Pedir();
        }

        /// <summary>Cuántos productos activos hay con código de barras, sin él pero marcados, y pendientes.</summary>
        public static void ResumenBarras(out int conBarras, out int sinMarcar, out int marcados)
        {
            conBarras = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM productos WHERE activo=1 AND codigo2<>''"));
            marcados = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM productos WHERE activo=1 AND codigo2='' AND sin_barras=1"));
            sinMarcar = Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM productos WHERE activo=1 AND codigo2='' AND sin_barras=0"));
        }

        public static bool CodigoExiste(string codigo, long excluirId)
        {
            return Convert.ToInt64(Db.Scalar("SELECT COUNT(*) FROM productos WHERE (codigo=@p0 OR (codigo2<>'' AND codigo2=@p0)) AND id<>@p1", codigo.Trim(), excluirId)) > 0;
        }

        public static void Guardar(Producto p, long usuarioId)
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                Guardar(cn, tx, p, usuarioId);
                tx.Commit();
            }
            Respaldo.Pedir();
        }

        public static void Guardar(SQLiteConnection cn, SQLiteTransaction tx, Producto p, long usuarioId)
        {
            object dep = p.DepartamentoId > 0 ? (object)p.DepartamentoId : null;
            if (p.Id == 0)
            {
                if (p.Orden == 0) p.Orden = Convert.ToInt64(Db.Scalar(cn, tx, "SELECT IFNULL(MAX(orden),0)+1 FROM productos"));
                p.Id = Db.InsertId(cn, tx, @"INSERT INTO productos(codigo,codigo2,descripcion,departamento_id,precio,costo,precio_mayoreo,mayoreo_desde,granel,usa_inventario,existencia,minimo,activo,actualizado,
                        precio_medio,medio_desde,proveedor,costo_bulto,cant_bulto,orden)
                    VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15,@p16,@p17,@p18,@p19)",
                    p.Codigo.Trim(), (p.Codigo2 ?? "").Trim(), p.Descripcion.Trim(), dep, p.Precio, p.Costo, p.PrecioMayoreo, p.MayoreoDesde,
                    p.Granel ? 1 : 0, p.UsaInventario ? 1 : 0, p.Existencia, p.Minimo, p.Activo ? 1 : 0, Db.Ahora(),
                    p.PrecioMedio, p.MedioDesde, (p.Proveedor ?? "").Trim(), p.CostoBulto, p.CantBulto, p.Orden);
                if (p.Existencia != 0)
                    Db.Exec(cn, tx, "INSERT INTO movimientos_inv(producto_id,fecha,tipo,cantidad,existencia,referencia,usuario_id) VALUES(@p0,@p1,'ENTRADA',@p2,@p2,'Alta de producto',@p3)",
                        p.Id, Db.Ahora(), p.Existencia, usuarioId);
            }
            else
            {
                // La existencia NO se modifica aquí: se cambia con movimientos de inventario.
                Db.Exec(cn, tx, @"UPDATE productos SET codigo=@p0,codigo2=@p1,descripcion=@p2,departamento_id=@p3,precio=@p4,costo=@p5,precio_mayoreo=@p6,
                    mayoreo_desde=@p7,granel=@p8,usa_inventario=@p9,minimo=@p10,activo=@p11,actualizado=@p12,
                    precio_medio=@p14,medio_desde=@p15,proveedor=@p16,costo_bulto=@p17,cant_bulto=@p18,orden=CASE WHEN @p19>0 THEN @p19 ELSE orden END WHERE id=@p13",
                    p.Codigo.Trim(), (p.Codigo2 ?? "").Trim(), p.Descripcion.Trim(), dep, p.Precio, p.Costo, p.PrecioMayoreo, p.MayoreoDesde,
                    p.Granel ? 1 : 0, p.UsaInventario ? 1 : 0, p.Minimo, p.Activo ? 1 : 0, Db.Ahora(), p.Id,
                    p.PrecioMedio, p.MedioDesde, (p.Proveedor ?? "").Trim(), p.CostoBulto, p.CantBulto, p.Orden);
            }
        }

        /// <summary>Si el producto tiene ventas solo se desactiva; si no, se borra.</summary>
        public static void Eliminar(long id)
        {
            long usos = Convert.ToInt64(Db.Scalar("SELECT COUNT(*) FROM venta_detalle WHERE producto_id=@p0", id));
            if (usos > 0) Db.Exec("UPDATE productos SET activo=0 WHERE id=@p0", id);
            else
            {
                Db.Exec("DELETE FROM movimientos_inv WHERE producto_id=@p0", id);
                Db.Exec("DELETE FROM productos WHERE id=@p0", id);
            }
            Respaldo.Pedir();
        }

        /// <summary>Suma (o resta) existencia y deja registro en el kardex.</summary>
        public static decimal MoverExistencia(SQLiteConnection cn, SQLiteTransaction tx, long productoId, decimal cantidad, string tipo, string referencia, long usuarioId)
        {
            Db.Exec(cn, tx, "UPDATE productos SET existencia = ROUND(existencia + @p0, 3) WHERE id=@p1 AND usa_inventario=1", cantidad, productoId);
            var ex = Db.Scalar(cn, tx, "SELECT existencia FROM productos WHERE id=@p0", productoId);
            decimal existencia = Db.Dec(ex);
            Db.Exec(cn, tx, "INSERT INTO movimientos_inv(producto_id,fecha,tipo,cantidad,existencia,referencia,usuario_id) VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                productoId, Db.Ahora(), tipo, cantidad, existencia, referencia, usuarioId);
            return existencia;
        }

        public static void MoverExistencia(long productoId, decimal cantidad, string tipo, string referencia, long usuarioId)
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                MoverExistencia(cn, tx, productoId, cantidad, tipo, referencia, usuarioId);
                tx.Commit();
            }
            Respaldo.Pedir();
        }

        /// <summary>Fija la existencia a un valor contado físicamente.</summary>
        public static void AjustarExistencia(long productoId, decimal nuevaExistencia, string referencia, long usuarioId)
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                var ex = Db.Scalar(cn, tx, "SELECT existencia FROM productos WHERE id=@p0", productoId);
                decimal actual = Db.Dec(ex);
                decimal dif = nuevaExistencia - actual;
                if (dif != 0) MoverExistencia(cn, tx, productoId, dif, "AJUSTE", referencia, usuarioId);
                tx.Commit();
            }
            Respaldo.Pedir();
        }

        /// <summary>
        /// Fija la existencia de varios productos en UNA sola operación (conteo físico por Excel):
        /// si algo falla no se cambia ninguno. Devuelve cuántos cambiaron.
        /// </summary>
        public static int AjustarVarios(List<KeyValuePair<long, decimal>> nuevas, string referencia, long usuarioId)
        {
            int n = 0;
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                foreach (var kv in nuevas)
                {
                    decimal actual = Db.Dec(Db.Scalar(cn, tx, "SELECT existencia FROM productos WHERE id=@p0", kv.Key));
                    decimal dif = kv.Value - actual;
                    if (dif != 0) { MoverExistencia(cn, tx, kv.Key, dif, "AJUSTE", referencia, usuarioId); n++; }
                }
                tx.Commit();
            }
            Respaldo.Pedir();
            return n;
        }
    }

    public static class TurnoRepo
    {
        public static Turno Abierto(int caja)
        {
            return Db.Lista("SELECT * FROM turnos WHERE caja=@p0 AND estado='ABIERTO' ORDER BY id DESC LIMIT 1", Turno.De, caja).FirstOrDefault();
        }

        public static Turno Obtener(long id) { return Db.Lista("SELECT * FROM turnos WHERE id=@p0", Turno.De, id).FirstOrDefault(); }

        public static Turno Abrir(int caja, long usuarioId, decimal fondo)
        {
            using (var cn = Db.Abrir())
            {
                long id = Db.InsertId(cn, null, "INSERT INTO turnos(caja,apertura,abierto_por,fondo,estado) VALUES(@p0,@p1,@p2,@p3,'ABIERTO')", caja, Db.Ahora(), usuarioId, fondo);
                var turno = Db.Lista(cn, null, "SELECT * FROM turnos WHERE id=@p0", Turno.De, id).First();
                Respaldo.Pedir();
                return turno;
            }
        }

        public static void Cerrar(long turnoId, long usuarioId, decimal? efectivoDeclarado)
        {
            Db.Exec("UPDATE turnos SET estado='CERRADO', cierre=@p0, cerrado_por=@p1, efectivo_declarado=@p2 WHERE id=@p3",
                Db.Ahora(), usuarioId, efectivoDeclarado.HasValue ? (object)efectivoDeclarado.Value : null, turnoId);
            Respaldo.Pedir();
        }

        public static void Movimiento(long turnoId, long usuarioId, string tipo, decimal monto, string concepto)
        {
            Db.Exec("INSERT INTO movimientos_caja(turno_id,usuario_id,fecha,tipo,monto,concepto) VALUES(@p0,@p1,@p2,@p3,@p4,@p5)",
                turnoId, usuarioId, Db.Ahora(), tipo, monto, concepto);
            Respaldo.Pedir();
        }

        /// <summary>
        /// Resumen del turno. Si usuarioId &gt; 0 solo cuenta lo de ese cajero (corte de cajero).
        /// Las cancelaciones se restan en el turno en el que se hicieron.
        /// </summary>
        public static ResumenCorte Resumen(long turnoId, long usuarioId = 0)
        {
            var t = Obtener(turnoId);
            var r = new ResumenCorte { TurnoId = turnoId, Desde = t.Apertura, Hasta = t.Cierre ?? DateTime.Now, Fondo = usuarioId > 0 ? 0 : t.Fondo };
            string fU = usuarioId > 0 ? " AND usuario_id=" + usuarioId : "";
            string fUc = usuarioId > 0 ? " AND cancelada_por=" + usuarioId : "";
            using (var cn = Db.Abrir())
            {
                foreach (var row in Db.Lista(cn, null, "SELECT forma_pago, COUNT(*) n, ROUND(SUM(total),2) t, ROUND(SUM(descuento),2) d FROM ventas WHERE turno_id=@p0" + fU + " GROUP BY forma_pago",
                    x => new { F = Db.S(x, "forma_pago"), N = Db.L(x, "n"), T = Db.D(x, "t"), D = Db.D(x, "d") }, turnoId))
                {
                    r.NumVentas += (int)row.N;
                    r.VentasBrutas += row.T;
                    r.Descuentos += row.D;
                    r.PorFormaPago[row.F] = row.T;
                }
                foreach (var row in Db.Lista(cn, null, "SELECT forma_pago, COUNT(*) n, ROUND(SUM(total),2) t FROM ventas WHERE estado='CANCELADA' AND cancelada_turno=@p0" + fUc + " GROUP BY forma_pago",
                    x => new { F = Db.S(x, "forma_pago"), N = Db.L(x, "n"), T = Db.D(x, "t") }, turnoId))
                {
                    r.NumCanceladas += (int)row.N;
                    r.Canceladas += row.T;
                    decimal v; r.PorFormaPago.TryGetValue(row.F, out v);
                    r.PorFormaPago[row.F] = v - row.T;
                }
                r.VentasNetas = r.VentasBrutas - r.Canceladas;

                r.Entradas = Db.Dec(Db.Scalar(cn, null, "SELECT IFNULL(ROUND(SUM(monto),2),0) FROM movimientos_caja WHERE tipo='ENTRADA' AND turno_id=@p0" + fU, turnoId));
                r.Salidas = Db.Dec(Db.Scalar(cn, null, "SELECT IFNULL(ROUND(SUM(monto),2),0) FROM movimientos_caja WHERE tipo='SALIDA' AND turno_id=@p0" + fU, turnoId));

                r.Costo = Db.Dec(Db.Scalar(cn, null, "SELECT IFNULL(ROUND(SUM(d.cantidad*d.costo),2),0) FROM venta_detalle d JOIN ventas v ON v.id=d.venta_id WHERE v.estado='PAGADA' AND v.turno_id=@p0" + fU.Replace("usuario_id", "v.usuario_id"), turnoId));

                var min = Db.Scalar(cn, null, "SELECT MIN(id) FROM ventas WHERE turno_id=@p0" + fU, turnoId);
                var max = Db.Scalar(cn, null, "SELECT MAX(id) FROM ventas WHERE turno_id=@p0" + fU, turnoId);
                r.TicketInicial = min == null ? 0 : Convert.ToInt64(min);
                r.TicketFinal = max == null ? 0 : Convert.ToInt64(max);

                r.PorDepartamento = Db.Lista(cn, null,
                    @"SELECT IFNULL(dp.nombre, 'Varios') dep, ROUND(SUM(d.importe),2) t FROM venta_detalle d JOIN ventas v ON v.id=d.venta_id
                      LEFT JOIN productos p ON p.id=d.producto_id LEFT JOIN departamentos dp ON dp.id=p.departamento_id
                      WHERE v.estado='PAGADA' AND v.turno_id=@p0" + fU.Replace("usuario_id", "v.usuario_id") + " GROUP BY dep ORDER BY t DESC",
                    x => new KeyValuePair<string, decimal>(Db.S(x, "dep"), Db.D(x, "t")), turnoId);
            }
            return r;
        }

        /// <summary>Cajeros que vendieron o movieron dinero en el turno.</summary>
        public static List<Usuario> CajerosDelTurno(long turnoId)
        {
            return Db.Lista(@"SELECT * FROM usuarios WHERE id IN (SELECT usuario_id FROM ventas WHERE turno_id=@p0
                UNION SELECT usuario_id FROM movimientos_caja WHERE turno_id=@p0 UNION SELECT cancelada_por FROM ventas WHERE cancelada_turno=@p0) ORDER BY nombre", Usuario.De, turnoId);
        }
    }

    public static class VentaRepo
    {
        /// <summary>Guarda la venta, descuenta inventario. Devuelve el número de ticket.</summary>
        public static long Guardar(Venta v, long usuarioId)
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                v.Fecha = DateTime.Now;
                v.Id = Db.InsertId(cn, tx, @"INSERT INTO ventas(turno_id,caja,usuario_id,fecha,articulos,subtotal,descuento,total,forma_pago,pago_con,cambio,referencia,estado)
                    VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,'PAGADA')",
                    v.TurnoId, v.Caja, usuarioId, Db.Fecha(v.Fecha), v.Articulos, v.Subtotal, v.Descuento, v.Total, v.FormaPago, v.PagoCon, v.Cambio, v.Referencia);
                foreach (var l in v.Lineas)
                {
                    Db.Exec(cn, tx, @"INSERT INTO venta_detalle(venta_id,producto_id,codigo,descripcion,cantidad,precio,descuento,importe,costo,granel)
                        VALUES(@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)",
                        v.Id, l.ProductoId > 0 ? (object)l.ProductoId : null, l.Codigo, l.Descripcion, l.Cantidad, l.Precio, l.Descuento, l.Importe, l.Costo, l.Granel ? 1 : 0);
                    if (l.ProductoId > 0)
                        ProductoRepo.MoverExistencia(cn, tx, l.ProductoId, -l.Cantidad, "VENTA", "Ticket " + v.Id, usuarioId);
                }
                tx.Commit();
            }
            Respaldo.Pedir();
            return v.Id;
        }

        public static Venta Obtener(long id)
        {
            using (var cn = Db.Abrir())
            {
                var v = Db.Lista(cn, null, "SELECT v.*, IFNULL(u.nombre,'') cajero FROM ventas v LEFT JOIN usuarios u ON u.id=v.usuario_id WHERE v.id=@p0", r => new Venta
                {
                    Id = Db.L(r, "id"), TurnoId = Db.L(r, "turno_id"), Caja = (int)Db.L(r, "caja"), UsuarioId = Db.L(r, "usuario_id"), Cajero = Db.S(r, "cajero"),
                    Fecha = Db.F(r, "fecha"), Articulos = Db.D(r, "articulos"), Subtotal = Db.D(r, "subtotal"), Descuento = Db.D(r, "descuento"), Total = Db.D(r, "total"),
                    FormaPago = Db.S(r, "forma_pago"), PagoCon = Db.D(r, "pago_con"), Cambio = Db.D(r, "cambio"), Referencia = Db.S(r, "referencia"), Estado = Db.S(r, "estado")
                }, id).FirstOrDefault();
                if (v == null) return null;
                v.Lineas = Db.Lista(cn, null, "SELECT * FROM venta_detalle WHERE venta_id=@p0 ORDER BY id", r => new LineaVenta
                {
                    ProductoId = Db.L(r, "producto_id"), Codigo = Db.S(r, "codigo"), Descripcion = Db.S(r, "descripcion"), Cantidad = Db.D(r, "cantidad"),
                    Precio = Db.D(r, "precio"), PrecioLista = Db.D(r, "precio"), Descuento = Db.D(r, "descuento"), Costo = Db.D(r, "costo"), Granel = Db.B(r, "granel")
                }, id);
                return v;
            }
        }

        public static long UltimoTicket(int caja)
        {
            var x = Db.Scalar("SELECT MAX(id) FROM ventas WHERE caja=@p0", caja);
            return x == null ? 0 : Convert.ToInt64(x);
        }

        /// <summary>Cancela un ticket completo: regresa inventario y lo resta del turno actual.</summary>
        public static void Cancelar(long ventaId, long turnoActual, long usuarioId)
        {
            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                int n = Db.Exec(cn, tx, "UPDATE ventas SET estado='CANCELADA', cancelada_por=@p0, cancelada_fecha=@p1, cancelada_turno=@p2 WHERE id=@p3 AND estado='PAGADA'",
                    usuarioId, Db.Ahora(), turnoActual, ventaId);
                if (n == 0) throw new InvalidOperationException("El ticket no existe o ya estaba cancelado.");
                var lineas = Db.Lista(cn, tx, "SELECT producto_id, cantidad FROM venta_detalle WHERE venta_id=@p0 AND producto_id IS NOT NULL",
                    r => new { P = Db.L(r, "producto_id"), C = Db.D(r, "cantidad") }, ventaId);
                foreach (var l in lineas)
                    ProductoRepo.MoverExistencia(cn, tx, l.P, l.C, "CANCELACION", "Cancela ticket " + ventaId, usuarioId);
                tx.Commit();
            }
            Respaldo.Pedir();
        }
    }

    /// <summary>Ventas en espera (F5 cuenta pendiente).</summary>
    public static class SuspendidaRepo
    {
        public class Item { public long Id; public DateTime Fecha; public string Nota = ""; public decimal Total; public string Usuario = ""; }

        public static long Guardar(List<LineaVenta> lineas, string nota, long usuarioId)
        {
            var sb = new StringBuilder();
            foreach (var l in lineas)
                sb.Append(string.Join("\t", new[] {
                    l.ProductoId.ToString(), Limpia(l.Codigo), Limpia(l.Descripcion), Core.Fmt.Dec(l.Cantidad), Core.Fmt.Dec(l.PrecioLista),
                    Core.Fmt.Dec(l.Precio), Core.Fmt.Dec(l.Descuento), Core.Fmt.Dec(l.Costo), l.Granel ? "1" : "0", l.PrecioManual ? "1" : "0" })).Append('\n');
            decimal total = lineas.Sum(x => x.Importe);
            long id;
            using (var cn = Db.Abrir())
                id = Db.InsertId(cn, null, "INSERT INTO suspendidas(fecha,usuario_id,nota,total,datos) VALUES(@p0,@p1,@p2,@p3,@p4)", Db.Ahora(), usuarioId, nota, total, sb.ToString());
            Respaldo.Pedir();
            return id;
        }

        static string Limpia(string s) { return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '); }

        public static List<Item> Todas()
        {
            return Db.Lista("SELECT s.id, s.fecha, s.nota, s.total, IFNULL(u.nombre,'') usuario FROM suspendidas s LEFT JOIN usuarios u ON u.id=s.usuario_id ORDER BY s.id",
                r => new Item { Id = Db.L(r, "id"), Fecha = Db.F(r, "fecha"), Nota = Db.S(r, "nota"), Total = Db.D(r, "total"), Usuario = Db.S(r, "usuario") });
        }

        /// <summary>Recupera y borra la venta en espera.</summary>
        public static List<LineaVenta> Recuperar(long id)
        {
            var datos = Convert.ToString(Db.Scalar("SELECT datos FROM suspendidas WHERE id=@p0", id) ?? "");
            Db.Exec("DELETE FROM suspendidas WHERE id=@p0", id);
            var lista = new List<LineaVenta>();
            foreach (var renglon in datos.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var c = renglon.Split('\t');
                if (c.Length < 9) continue;
                lista.Add(new LineaVenta
                {
                    ProductoId = long.Parse(c[0]), Codigo = c[1], Descripcion = c[2], Cantidad = Core.Fmt.ParseDec(c[3]), PrecioLista = Core.Fmt.ParseDec(c[4]),
                    Precio = Core.Fmt.ParseDec(c[5]), Descuento = Core.Fmt.ParseDec(c[6]), Costo = Core.Fmt.ParseDec(c[7]), Granel = c[8] == "1",
                    PrecioManual = c.Length > 9 && c[9] == "1"
                });
            }
            return lista;
        }
    }
}
