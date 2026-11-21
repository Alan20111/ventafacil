using System;
using System.Collections.Generic;
using System.Data;

namespace VentaFacil.Data
{
    /// <summary>Consultas de reportes e inventario para el módulo de administración.</summary>
    public static class AdminConsultas
    {
        static string Ini(DateTime d) { return Db.Fecha(d.Date); }
        static string Fin(DateTime d) { return Db.Fecha(d.Date.AddDays(1)); }

        public static DataTable VentasPorDia(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT substr(v.fecha,1,10) AS Fecha, COUNT(*) AS Tickets, ROUND(SUM(v.total),2) AS Total,
                ROUND(IFNULL((SELECT SUM(d.cantidad*d.costo) FROM venta_detalle d JOIN ventas v2 ON v2.id=d.venta_id
                    WHERE v2.estado='PAGADA' AND substr(v2.fecha,1,10)=substr(v.fecha,1,10)),0),2) AS Costo
                FROM ventas v WHERE v.estado='PAGADA' AND v.fecha>=@p0 AND v.fecha<@p1
                GROUP BY substr(v.fecha,1,10) ORDER BY Fecha", Ini(desde), Fin(hasta));
        }

        public static DataTable Tickets(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT v.id AS Folio, v.fecha AS Fecha, IFNULL(u.nombre,'') AS Cajero, v.caja AS Caja, v.forma_pago AS FormaPago,
                ROUND(v.total,2) AS Total, v.estado AS Estado
                FROM ventas v LEFT JOIN usuarios u ON u.id=v.usuario_id
                WHERE v.fecha>=@p0 AND v.fecha<@p1 ORDER BY v.id DESC", Ini(desde), Fin(hasta));
        }

        public static DataTable ProductosMasVendidos(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT d.codigo AS Codigo, d.descripcion AS Descripcion, ROUND(SUM(d.cantidad),3) AS Cantidad,
                ROUND(SUM(d.importe),2) AS Importe, ROUND(SUM(d.importe - d.cantidad*d.costo),2) AS Ganancia
                FROM venta_detalle d JOIN ventas v ON v.id=d.venta_id
                WHERE v.estado='PAGADA' AND v.fecha>=@p0 AND v.fecha<@p1
                GROUP BY d.codigo, d.descripcion ORDER BY Importe DESC", Ini(desde), Fin(hasta));
        }

        public static DataTable VentasPorCajero(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT IFNULL(u.nombre,'(sin usuario)') AS Cajero, COUNT(*) AS Tickets, ROUND(SUM(v.total),2) AS Total
                FROM ventas v LEFT JOIN usuarios u ON u.id=v.usuario_id
                WHERE v.estado='PAGADA' AND v.fecha>=@p0 AND v.fecha<@p1 GROUP BY Cajero ORDER BY Total DESC", Ini(desde), Fin(hasta));
        }

        public static DataTable VentasPorFormaPago(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT v.forma_pago AS FormaPago, COUNT(*) AS Tickets, ROUND(SUM(v.total),2) AS Total
                FROM ventas v WHERE v.estado='PAGADA' AND v.fecha>=@p0 AND v.fecha<@p1 GROUP BY v.forma_pago ORDER BY Total DESC", Ini(desde), Fin(hasta));
        }

        public static DataTable VentasPorDepartamento(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT IFNULL(dp.nombre,'Varios') AS Departamento, ROUND(SUM(d.cantidad),3) AS Cantidad, ROUND(SUM(d.importe),2) AS Importe,
                ROUND(SUM(d.importe - d.cantidad*d.costo),2) AS Ganancia
                FROM venta_detalle d JOIN ventas v ON v.id=d.venta_id
                LEFT JOIN productos p ON p.id=d.producto_id LEFT JOIN departamentos dp ON dp.id=p.departamento_id
                WHERE v.estado='PAGADA' AND v.fecha>=@p0 AND v.fecha<@p1 GROUP BY Departamento ORDER BY Importe DESC", Ini(desde), Fin(hasta));
        }

        public static DataTable Turnos(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT t.id AS Turno, t.caja AS Caja, t.apertura AS Apertura, IFNULL(t.cierre,'') AS Cierre,
                ROUND(t.fondo,2) AS Fondo, t.efectivo_declarado AS Declarado, t.estado AS Estado, IFNULL(u.nombre,'') AS AbrioPor
                FROM turnos t LEFT JOIN usuarios u ON u.id=t.abierto_por
                WHERE t.apertura<@p1 AND (t.cierre IS NULL OR t.cierre>=@p0) ORDER BY t.id DESC", Ini(desde), Fin(hasta));
        }

        public static DataTable MovimientosCaja(DateTime desde, DateTime hasta)
        {
            return Db.Tabla(@"SELECT m.fecha AS Fecha, m.tipo AS Tipo, ROUND(m.monto,2) AS Monto, IFNULL(m.concepto,'') AS Concepto,
                IFNULL(u.nombre,'') AS Usuario, m.turno_id AS Turno
                FROM movimientos_caja m LEFT JOIN usuarios u ON u.id=m.usuario_id
                WHERE m.fecha>=@p0 AND m.fecha<@p1 ORDER BY m.fecha", Ini(desde), Fin(hasta));
        }

        public static DataTable BajosDeExistencia()
        {
            return Db.Tabla(@"SELECT p.codigo AS Codigo, p.descripcion AS Descripcion, IFNULL(d.nombre,'') AS Departamento,
                p.existencia AS Existencia, p.minimo AS Minimo
                FROM productos p LEFT JOIN departamentos d ON d.id=p.departamento_id
                WHERE p.activo=1 AND p.usa_inventario=1 AND p.existencia<=p.minimo ORDER BY p.descripcion");
        }

        public static DataTable Kardex(long productoId)
        {
            return Db.Tabla(@"SELECT m.fecha AS Fecha, m.tipo AS Tipo, m.cantidad AS Cantidad, m.existencia AS Existencia,
                IFNULL(m.referencia,'') AS Referencia, IFNULL(u.nombre,'') AS Usuario
                FROM movimientos_inv m LEFT JOIN usuarios u ON u.id=m.usuario_id WHERE m.producto_id=@p0 ORDER BY m.id DESC", productoId);
        }

        public static DataTable ValorInventario()
        {
            return Db.Tabla(@"SELECT IFNULL(d.nombre,'Sin departamento') AS Departamento, COUNT(*) AS Productos,
                ROUND(SUM(p.existencia),3) AS Existencia, ROUND(SUM(p.existencia*p.costo),2) AS ValorCosto, ROUND(SUM(p.existencia*p.precio),2) AS ValorVenta
                FROM productos p LEFT JOIN departamentos d ON d.id=p.departamento_id
                WHERE p.activo=1 AND p.usa_inventario=1 GROUP BY Departamento ORDER BY Departamento");
        }

        public static void ActualizarCosto(long productoId, decimal costo)
        {
            Db.Exec("UPDATE productos SET costo=@p0, actualizado=@p1 WHERE id=@p2", costo, Db.Ahora(), productoId);
        }

        public static List<Producto> TodosLosProductos()
        {
            return ProductoRepo.Buscar("", true, 1000000);
        }
    }
}
