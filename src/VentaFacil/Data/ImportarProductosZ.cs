using System;
using System.Collections.Generic;
using System.Linq;
using VentaFacil.Core;

namespace VentaFacil.Data
{
    public class OpcionesAplicarZ
    {
        public bool ActualizarExistentes = true;     // si el producto ya está: actualizar sus precios y costos
        public bool ActualizarExistencias = false;   // si el producto ya está: ajustar su existencia a la de Zafirah
        public bool SobrescribirConflictos = false;  // si el CÓDIGO ya es de OTRO producto (otro nombre): pisarlo
    }

    public class ResultadoAplicarZ
    {
        public int Nuevos, Actualizados, Omitidos, BarrasRepetidos;
        public List<string> Conflictos = new List<string>();
        public List<string> Errores = new List<string>();
        public string Respaldo = "";
    }

    /// <summary>
    /// Guarda en VentaFácil lo que salió de ZafirahMapeo.Transformar. Todo en UNA transacción (si algo falla no
    /// se cambia nada) y con un respaldo automático antes. Solo toca los datos que la persona eligió importar.
    /// </summary>
    public static class ImportarProductosZ
    {
        static bool MismoNombre(string a, string b) { return ZafirahMapeo.Norm(a) == ZafirahMapeo.Norm(b); }

        public static ResultadoAplicarZ Aplicar(ResultadoZ datos, OpcionesAplicarZ op, long usuarioId)
        {
            var r = new ResultadoAplicarZ();
            r.Respaldo = Db.Respaldar();   // por si algo no sale como se esperaba: se puede volver a este punto
            var M = datos.Mapeados;
            var deptos = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

            using (var cn = Db.Abrir())
            using (var tx = cn.BeginTransaction())
            {
                foreach (var x in datos.Productos)
                {
                    try
                    {
                        Producto p = Db.Lista(cn, tx, "SELECT p.*, '' AS departamento FROM productos p WHERE (p.codigo=@p0 OR (p.codigo2<>'' AND p.codigo2=@p0)) LIMIT 1", Producto.De, x.Codigo).FirstOrDefault();
                        bool pisarNombre = false;
                        if (p != null)
                        {
                            if (!MismoNombre(p.Descripcion, x.Descripcion))
                            {
                                if (!op.SobrescribirConflictos)
                                {
                                    r.Conflictos.Add(x.Codigo + ":  en VentaFácil «" + p.Descripcion + "»  ·  en Zafirah «" + x.Descripcion + "»");
                                    continue;
                                }
                                pisarNombre = true;
                            }
                            if (!op.ActualizarExistentes) { r.Omitidos++; continue; }
                        }
                        else
                        {
                            // Mismo nombre con otro código (p. ej. importado antes de la lista de la semillera): es el mismo producto
                            p = Db.Lista(cn, tx, "SELECT p.*, '' AS departamento FROM productos p WHERE p.descripcion=@p0 COLLATE NOCASE LIMIT 1", Producto.De, x.Descripcion).FirstOrDefault();
                            if (p != null && !op.ActualizarExistentes) { r.Omitidos++; continue; }
                        }

                        bool esNuevo = p == null;
                        if (esNuevo) p = new Producto { Codigo = x.Codigo, Descripcion = x.Descripcion, UsaInventario = true, Activo = true };
                        else if (pisarNombre) p.Descripcion = x.Descripcion;

                        p.Precio = x.Menudeo;
                        if (M.Contains(CampoZ.Costo)) p.Costo = x.Costo;
                        if (M.Contains(CampoZ.Medio) && M.Contains(CampoZ.DesdeMedio)) { p.PrecioMedio = x.Medio; p.MedioDesde = x.DesdeMedio; }
                        if (M.Contains(CampoZ.Mayoreo) && M.Contains(CampoZ.DesdeMayoreo)) { p.PrecioMayoreo = x.Mayoreo; p.MayoreoDesde = x.DesdeMayoreo; }
                        if (M.Contains(CampoZ.Unidad)) p.Granel = x.Granel;
                        if (M.Contains(CampoZ.Proveedor) && x.Proveedor != "") p.Proveedor = x.Proveedor;
                        if (M.Contains(CampoZ.Minimo) && x.Minimo.HasValue) p.Minimo = x.Minimo.Value;
                        if (M.Contains(CampoZ.Grupo) && x.Grupo != "")
                        {
                            long dep;
                            if (!deptos.TryGetValue(x.Grupo, out dep)) { dep = DepartamentoRepo.ObtenerOCrear(cn, tx, x.Grupo); deptos[x.Grupo] = dep; }
                            p.DepartamentoId = dep;
                        }
                        if (M.Contains(CampoZ.Barras) && x.Barras != "" && p.Codigo2 != x.Barras)
                        {
                            long usos = Convert.ToInt64(Db.Scalar(cn, tx, "SELECT COUNT(*) FROM productos WHERE id<>@p1 AND (codigo=@p0 OR (codigo2<>'' AND codigo2=@p0))", x.Barras, p.Id));
                            if (usos > 0) r.BarrasRepetidos++;
                            else { p.Codigo2 = x.Barras; p.SinBarras = false; }
                        }
                        if (esNuevo && M.Contains(CampoZ.Existencia)) p.Existencia = x.Existencia ?? 0m;

                        ProductoRepo.Guardar(cn, tx, p, usuarioId);
                        if (p.SinBarras == false && p.Codigo2 != "") Db.Exec(cn, tx, "UPDATE productos SET sin_barras=0 WHERE id=@p0", p.Id);

                        if (!esNuevo && op.ActualizarExistencias && M.Contains(CampoZ.Existencia) && x.Existencia.HasValue)
                        {
                            decimal actual = Db.Dec(Db.Scalar(cn, tx, "SELECT existencia FROM productos WHERE id=@p0", p.Id));
                            if (actual != x.Existencia.Value)
                                ProductoRepo.MoverExistencia(cn, tx, p.Id, x.Existencia.Value - actual, "AJUSTE", "Importación de Zafirah", usuarioId);
                        }
                        if (esNuevo) r.Nuevos++; else r.Actualizados++;
                    }
                    catch (Exception ex) { r.Errores.Add(x.Codigo + " " + x.Descripcion + ": " + ex.Message); }
                }
                tx.Commit();
            }
            Respaldo.Pedir();
            return r;
        }
    }
}
