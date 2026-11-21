using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text.RegularExpressions;
using VentaFacil.Core;
using VentaFacil.Data;

namespace VentaFacil.Printing
{
    /// <summary>Arma el contenido de los tickets (venta, cortes, movimientos).</summary>
    public static class Tickets
    {
        static readonly CultureInfo Mx = new CultureInfo("es-MX");

        static Ticket Encabezado(Config c)
        {
            var t = new Ticket(c.Columnas);
            t.Centro(c.NegocioNombre, Ticket.Estilo.Negrita);
            if (!string.IsNullOrWhiteSpace(c.NegocioDireccion)) t.Centro(c.NegocioDireccion);
            if (!string.IsNullOrWhiteSpace(c.NegocioTelefono)) t.Centro("Tel. " + c.NegocioTelefono);
            if (!string.IsNullOrWhiteSpace(c.NegocioRfc)) t.Centro("RFC: " + c.NegocioRfc);
            if (!string.IsNullOrWhiteSpace(c.TicketEncabezado)) t.Centro(c.TicketEncabezado);
            return t;
        }

        static string FechaLarga(DateTime d) { return d.ToString("ddd dd/MMM/yyyy  hh:mm tt", Mx); }

        public static Ticket Venta(Venta v, Config c, bool reimpresion = false)
        {
            var t = Encabezado(c);
            t.Separador();
            if (reimpresion) t.Centro("*** REIMPRESION ***", Ticket.Estilo.Negrita);
            if (v.Estado == "CANCELADA") t.Centro("*** TICKET CANCELADO ***", Ticket.Estilo.Negrita);
            t.Par("Ticket: " + v.Id, "Caja: " + v.Caja);
            t.Linea(FechaLarga(v.Fecha));
            t.Linea("Le atendió: " + v.Cajero);
            t.Separador();
            int anchoImporte = 11;
            foreach (var l in v.Lineas)
            {
                string desc = l.Descripcion;
                if (l.Cantidad == 1 && !l.Granel && l.Descuento == 0)
                {
                    t.Par(Recortar(desc, c.Columnas - anchoImporte - 1), Fmt.Dinero(l.Importe));
                }
                else
                {
                    t.Linea(desc);
                    string detalle = "  " + Fmt.Cant(l.Cantidad, l.Granel) + (l.Granel ? " kg" : "") + " x " + Fmt.Dinero(l.Precio);
                    t.Par(detalle, Fmt.Dinero(Fmt.R2(l.Cantidad * l.Precio)));
                    if (l.Descuento != 0) t.Par("  Descuento", "-" + Fmt.Dinero(l.Descuento));
                }
            }
            t.Separador();
            if (v.Descuento > 0)
            {
                t.Par("Subtotal:", Fmt.Dinero(v.Subtotal));
                t.Par("Descuento:", "-" + Fmt.Dinero(v.Descuento));
            }
            t.Par("TOTAL:", Fmt.Dinero(v.Total), Ticket.Estilo.Negrita);
            t.Par(NombrePago(v.FormaPago) + ":", Fmt.Dinero(v.PagoCon > 0 ? v.PagoCon : v.Total));
            if (v.Cambio > 0) t.Par("Su cambio:", Fmt.Dinero(v.Cambio), Ticket.Estilo.Negrita);
            if (!string.IsNullOrWhiteSpace(v.Referencia)) t.Linea("Ref: " + v.Referencia);
            if (c.ImporteConLetra) t.Linea(NumeroALetras.Pesos(v.Total));
            t.Linea("Artículos: " + Fmt.Dec(v.Articulos));
            t.Separador();
            if (!string.IsNullOrWhiteSpace(c.TicketPie)) t.Centro(c.TicketPie);
            return t;
        }

        public static string NombrePago(string forma)
        {
            switch (forma)
            {
                case "EFECTIVO": return "Efectivo";
                case "TARJETA": return "Tarjeta";
                case "TRANSFERENCIA": return "Transferencia";
                case "VALES": return "Vales";
                default: return forma;
            }
        }

        static string Recortar(string s, int n) { return s.Length <= n ? s : s.Substring(0, n); }

        public static Ticket Corte(ResumenCorte r, Config c, decimal? declarado)
        {
            var t = Encabezado(c);
            t.Separador('=');
            t.Centro(r.Titulo, Ticket.Estilo.Negrita);
            t.Separador('=');
            t.Linea("Caja: " + c.Caja + "   Turno: " + r.TurnoId);
            if (!string.IsNullOrEmpty(r.Cajero)) t.Linea("Cajero: " + r.Cajero);
            t.Linea("Desde: " + r.Desde.ToString("dd/MM/yyyy HH:mm", Mx));
            t.Linea("Hasta: " + r.Hasta.ToString("dd/MM/yyyy HH:mm", Mx));
            if (r.TicketInicial > 0) t.Linea("Tickets: " + r.TicketInicial + " al " + r.TicketFinal);
            t.Separador();
            t.Par("Ventas (" + r.NumVentas + "):", Fmt.Dinero(r.VentasBrutas));
            t.Par("Cancelaciones (" + r.NumCanceladas + "):", "-" + Fmt.Dinero(r.Canceladas));
            t.Par("VENTAS NETAS:", Fmt.Dinero(r.VentasNetas), Ticket.Estilo.Negrita);
            if (r.Descuentos > 0) t.Par("Descuentos otorgados:", Fmt.Dinero(r.Descuentos));
            t.Separador();
            t.Linea("POR FORMA DE PAGO", Ticket.Estilo.Negrita);
            foreach (var kv in r.PorFormaPago.OrderBy(x => x.Key))
                t.Par("  " + NombrePago(kv.Key) + ":", Fmt.Dinero(kv.Value));
            t.Separador();
            t.Linea("DINERO EN CAJA (EFECTIVO)", Ticket.Estilo.Negrita);
            t.Par("  Fondo inicial:", Fmt.Dinero(r.Fondo));
            t.Par("  + Ventas en efectivo:", Fmt.Dinero(r.VentasEfectivo));
            t.Par("  + Entradas:", Fmt.Dinero(r.Entradas));
            t.Par("  - Salidas/retiros:", Fmt.Dinero(r.Salidas));
            t.Par("DEBE HABER:", Fmt.Dinero(r.EfectivoEsperado), Ticket.Estilo.Negrita);
            if (declarado.HasValue)
            {
                t.Par("  Efectivo contado:", Fmt.Dinero(declarado.Value));
                decimal dif = declarado.Value - r.EfectivoEsperado;
                t.Par(dif == 0 ? "  Diferencia:" : dif > 0 ? "  SOBRANTE:" : "  FALTANTE:", Fmt.Dinero(Math.Abs(dif)), Ticket.Estilo.Negrita);
            }
            if (r.PorDepartamento.Count > 0)
            {
                t.Separador();
                t.Linea("VENTAS POR DEPARTAMENTO", Ticket.Estilo.Negrita);
                foreach (var kv in r.PorDepartamento) t.Par("  " + kv.Key, Fmt.Dinero(kv.Value));
            }
            if (string.IsNullOrEmpty(r.Cajero))
            {
                t.Separador();
                t.Par("Costo de lo vendido:", Fmt.Dinero(r.Costo));
                t.Par("Ganancia estimada:", Fmt.Dinero(r.Ganancia));
            }
            t.Separador();
            t.Linea("Impreso: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm", Mx));
            t.Linea("");
            t.Linea("");
            t.Centro("______________________");
            t.Centro("Firma");
            return t;
        }

        public static Ticket Movimiento(string tipo, decimal monto, string concepto, Config c)
        {
            var t = Encabezado(c);
            t.Separador();
            t.Centro(tipo == "ENTRADA" ? "ENTRADA DE EFECTIVO" : "SALIDA DE EFECTIVO", Ticket.Estilo.Negrita);
            t.Linea(FechaLarga(DateTime.Now));
            t.Linea("Caja: " + c.Caja + "  Usuario: " + Sesion.Usuario.Nombre);
            t.Par("Importe:", Fmt.Dinero(monto), Ticket.Estilo.Negrita);
            t.Linea("Concepto: " + concepto);
            t.Linea("");
            t.Linea("");
            t.Centro("______________________");
            t.Centro("Firma");
            return t;
        }

        public static Ticket Cancelacion(Venta v, Config c)
        {
            var t = Encabezado(c);
            t.Separador();
            t.Centro("CANCELACION DE TICKET", Ticket.Estilo.Negrita);
            t.Linea(FechaLarga(DateTime.Now));
            t.Par("Ticket cancelado:", v.Id.ToString());
            t.Par("Fecha del ticket:", v.Fecha.ToString("dd/MM/yy HH:mm", Mx));
            t.Par("Importe devuelto:", Fmt.Dinero(v.Total), Ticket.Estilo.Negrita);
            t.Par("Forma de pago:", NombrePago(v.FormaPago));
            t.Linea("Autorizó: " + Sesion.Usuario.Nombre);
            return t;
        }

        public static Ticket Suspendida(long folio, decimal total, Config c)
        {
            var t = new Ticket(c.Columnas);
            t.Centro("VENTA EN ESPERA", Ticket.Estilo.Negrita);
            t.Centro("No. " + folio, Ticket.Estilo.Grande);
            t.Par("Total:", Fmt.Dinero(total));
            t.Linea(FechaLarga(DateTime.Now));
            return t;
        }
    }

    /// <summary>Lectura de peso desde básculas seriales (Torrey, Rhino, Noval, etc.).</summary>
    public static class Bascula
    {
        public static decimal LeerPeso(Config c)
        {
            using (var sp = new SerialPort(c.BasculaPuerto, c.BasculaBaudios, Parity.None, 8, StopBits.One))
            {
                sp.ReadTimeout = 1500;
                sp.WriteTimeout = 1000;
                sp.Open();
                sp.DiscardInBuffer();
                if (!string.IsNullOrEmpty(c.BasculaComando)) sp.Write(c.BasculaComando.Replace("\\r", "\r").Replace("\\n", "\n"));
                System.Threading.Thread.Sleep(350);
                string resp = "";
                var inicio = DateTime.Now;
                while ((DateTime.Now - inicio).TotalMilliseconds < 1500)
                {
                    resp += sp.ReadExisting();
                    var m = Regex.Match(resp, @"(\d+[\.,]\d+)");
                    if (m.Success && resp.Length > m.Index + m.Length) return Fmt.ParseDec(m.Groups[1].Value);
                    System.Threading.Thread.Sleep(100);
                }
                var fin = Regex.Match(resp, @"(\d+[\.,]?\d*)");
                if (fin.Success) return Fmt.ParseDec(fin.Groups[1].Value);
                throw new InvalidOperationException("La báscula no respondió. Revise el puerto " + c.BasculaPuerto + ".");
            }
        }
    }
}
