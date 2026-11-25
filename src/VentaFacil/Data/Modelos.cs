using System;
using System.Collections.Generic;
using System.Data;

namespace VentaFacil.Data
{
    public class Usuario
    {
        public long Id;
        public string Login = "";
        public string Nombre = "";
        public string Rol = "CAJERO";
        public string Permisos = "";   // ver Core.Permisos
        public bool Activo = true;
        public bool EsAdmin { get { return Rol == "ADMIN"; } }

        internal static Usuario De(IDataRecord r)
        {
            return new Usuario { Id = Db.L(r, "id"), Login = Db.S(r, "usuario"), Nombre = Db.S(r, "nombre"), Rol = Db.S(r, "rol"), Permisos = Db.S(r, "permisos"), Activo = Db.B(r, "activo") };
        }
    }

    public class Departamento
    {
        public long Id;
        public string Nombre = "";
        public override string ToString() { return Nombre; }
    }

    public class Producto
    {
        public long Id;
        public string Codigo = "";
        public string Codigo2 = "";
        public string Descripcion = "";
        public long DepartamentoId;
        public string Departamento = "";
        public decimal Precio;
        public decimal Costo;
        public decimal PrecioMayoreo;
        public decimal MayoreoDesde;
        public decimal PrecioMedio;       // medio mayoreo
        public decimal MedioDesde;
        public string Proveedor = "";
        public decimal CostoBulto;        // lo que cuesta el bulto/caja
        public decimal CantBulto;         // kg o piezas que trae el bulto
        public long Orden;                // orden en la lista de precios
        public bool Granel;
        public bool UsaInventario = true;
        public decimal Existencia;
        public decimal Minimo;
        public bool Activo = true;
        public bool SinBarras;            // marcado como "no lleva código de barras" (granel, verdura…)

        /// <summary>Precio que corresponde a la cantidad (aplica mayoreo si procede).</summary>
        public decimal PrecioPara(decimal cantidad)
        {
            if (PrecioMayoreo > 0 && MayoreoDesde > 0 && cantidad >= MayoreoDesde) return PrecioMayoreo;
            if (PrecioMedio > 0 && MedioDesde > 0 && cantidad >= MedioDesde) return PrecioMedio;
            return Precio;
        }

        internal static Producto De(IDataRecord r)
        {
            var p = new Producto
            {
                Id = Db.L(r, "id"), Codigo = Db.S(r, "codigo"), Codigo2 = Db.S(r, "codigo2"), Descripcion = Db.S(r, "descripcion"),
                DepartamentoId = Db.L(r, "departamento_id"), Precio = Db.D(r, "precio"), Costo = Db.D(r, "costo"),
                PrecioMayoreo = Db.D(r, "precio_mayoreo"), MayoreoDesde = Db.D(r, "mayoreo_desde"), Granel = Db.B(r, "granel"),
                UsaInventario = Db.B(r, "usa_inventario"), Existencia = Db.D(r, "existencia"), Minimo = Db.D(r, "minimo"), Activo = Db.B(r, "activo"),
                PrecioMedio = Db.D(r, "precio_medio"), MedioDesde = Db.D(r, "medio_desde"), Proveedor = Db.S(r, "proveedor"),
                CostoBulto = Db.D(r, "costo_bulto"), CantBulto = Db.D(r, "cant_bulto"), Orden = Db.L(r, "orden")
            };
            try { p.Departamento = Db.S(r, "departamento"); } catch (IndexOutOfRangeException) { }
            try { p.SinBarras = Db.B(r, "sin_barras"); } catch (IndexOutOfRangeException) { }
            return p;
        }
    }

    /// <summary>Renglón de la venta en curso.</summary>
    public class LineaVenta
    {
        public long ProductoId;          // 0 = artículo común / venta departamental
        public string Codigo = "";
        public string Descripcion = "";
        public decimal Cantidad;
        public decimal PrecioLista;      // precio original
        public decimal Precio;           // precio unitario aplicado
        public decimal Descuento;        // descuento en importe de la línea
        public decimal Costo;
        public bool Granel;
        public bool PrecioManual;        // el precio se ajustó a mano (F6): no recalcular mayoreo

        public decimal Importe { get { return Math.Round(Cantidad * Precio - Descuento, 2, MidpointRounding.AwayFromZero); } }
    }

    public class Venta
    {
        public long Id;
        public long TurnoId;
        public int Caja;
        public long UsuarioId;
        public string Cajero = "";
        public DateTime Fecha;
        public decimal Articulos;
        public decimal Subtotal;
        public decimal Descuento;
        public decimal Total;
        public string FormaPago = "EFECTIVO";
        public decimal PagoCon;
        public decimal Cambio;
        public string Referencia = "";
        public string Estado = "PAGADA";
        public List<LineaVenta> Lineas = new List<LineaVenta>();
    }

    public class Turno
    {
        public long Id;
        public int Caja;
        public DateTime Apertura;
        public long AbiertoPor;
        public decimal Fondo;
        public DateTime? Cierre;
        public decimal? EfectivoDeclarado;
        public string Estado = "ABIERTO";

        internal static Turno De(IDataRecord r)
        {
            var t = new Turno { Id = Db.L(r, "id"), Caja = (int)Db.L(r, "caja"), Apertura = Db.F(r, "apertura"), AbiertoPor = Db.L(r, "abierto_por"), Fondo = Db.D(r, "fondo"), Estado = Db.S(r, "estado") };
            if (r["cierre"] != DBNull.Value) t.Cierre = Db.F(r, "cierre");
            if (r["efectivo_declarado"] != DBNull.Value) t.EfectivoDeclarado = Db.D(r, "efectivo_declarado");
            return t;
        }
    }

    /// <summary>Resumen de dinero para cortes X / Z.</summary>
    public class ResumenCorte
    {
        public string Titulo = "";
        public long TurnoId;
        public string Cajero = "";           // vacío = toda la caja
        public DateTime Desde;
        public DateTime Hasta;
        public decimal Fondo;
        public int NumVentas;
        public int NumCanceladas;
        public decimal VentasBrutas;         // pagadas + canceladas
        public decimal Canceladas;
        public decimal VentasNetas;
        public decimal Descuentos;
        public decimal Entradas;
        public decimal Salidas;
        public decimal Costo;
        public long TicketInicial;
        public long TicketFinal;
        public Dictionary<string, decimal> PorFormaPago = new Dictionary<string, decimal>();
        public List<KeyValuePair<string, decimal>> PorDepartamento = new List<KeyValuePair<string, decimal>>();

        public decimal VentasEfectivo { get { decimal v; return PorFormaPago.TryGetValue("EFECTIVO", out v) ? v : 0; } }
        /// <summary>Lo que debe haber en el cajón.</summary>
        public decimal EfectivoEsperado { get { return Fondo + VentasEfectivo + Entradas - Salidas; } }
        public decimal Ganancia { get { return VentasNetas - Costo; } }
    }
}
