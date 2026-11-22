using System;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.Data;
using VentaFacil.Printing;
using VentaFacil.UI;

namespace VentaFacil.Forms.Admin
{
    /// <summary>Ver un ticket ya emitido: reimprimir o cancelar.</summary>
    public class FormDetalleTicket : Dialogo
    {
        readonly Venta _v;

        public FormDetalleTicket(long folio) : base("Ticket " + folio, 480, 600)
        {
            _v = VentaRepo.Obtener(folio);
            if (_v == null) throw new InvalidOperationException("No existe el ticket " + folio + ".");
            var texto = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = Tema.Mono, BackColor = Tema.Crema,
                Left = 12, Top = 12, Width = 456, Height = 520, Text = Tickets.Venta(_v, Config.Actual).ComoTexto()
            };
            Controls.Add(texto);
            Controls.Add(Ui.Boton("Reimprimir", 12, 546, 130, 40, (s, e) => Reimprimir()));
            if (_v.Estado == "PAGADA")
            {
                var cancelar = Ui.Boton("Cancelar ticket", 150, 546, 150, 40, (s, e) => Cancelar());
                cancelar.ForeColor = Tema.Rojo;
                Controls.Add(cancelar);
            }
            var cerrar = Ui.Boton("Cerrar (Esc)", 338, 546, 130, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(cerrar);
            CancelButton = cerrar;
            Shown += (s, e) => { cerrar.Focus(); texto.SelectionLength = 0; };
        }

        void Reimprimir()
        {
            try
            {
                var c = Config.Actual.Copia(); c.Copias = 1;
                Impresora.Imprimir(Tickets.Venta(_v, c, true), c);
            }
            catch (Exception ex) { Ui.Error(ex, "Reimprimir"); }
        }

        void Cancelar()
        {
            var turno = Sesion.Turno ?? TurnoRepo.Abierto(Config.Actual.Caja);
            if (turno == null || turno.Estado != "ABIERTO")
            {
                Ui.Aviso("No hay un turno de caja abierto.\nAbra la caja en la pantalla de ventas para poder cancelar (el dinero devuelto sale del turno actual).");
                return;
            }
            if (!Ui.Confirmar("¿Cancelar el ticket " + _v.Id + " por " + Fmt.Dinero(_v.Total) + "?\n\nSe regresa la mercancía al inventario y el importe se descuenta del corte actual.\nEsta acción no se puede deshacer.")) return;
            try
            {
                VentaRepo.Cancelar(_v.Id, turno.Id, Sesion.Usuario.Id);
                try
                {
                    var c = Config.Actual.Copia(); c.Copias = 1;
                    Impresora.Imprimir(Tickets.Cancelacion(_v, c), c);
                }
                catch (Exception ex) { Ui.Error(ex, "El ticket se canceló, pero no se pudo imprimir el comprobante"); }
                Ui.Info("Ticket " + _v.Id + " cancelado.");
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { Ui.Error(ex, "Cancelar ticket"); }
        }
    }

    /// <summary>Ver un corte (turno) anterior y reimprimirlo.</summary>
    public class FormDetalleCorte : Dialogo
    {
        readonly Ticket _ticket;

        public FormDetalleCorte(long turnoId) : base("Corte del turno " + turnoId, 480, 640)
        {
            var t = TurnoRepo.Obtener(turnoId);
            if (t == null) throw new InvalidOperationException("No existe el turno " + turnoId + ".");
            var r = TurnoRepo.Resumen(turnoId);
            r.Titulo = t.Estado == "CERRADO" ? "CORTE DE CAJA (Z)" : "CORTE PARCIAL (X) - TURNO ABIERTO";
            _ticket = Tickets.Corte(r, Config.Actual, t.EfectivoDeclarado);
            var texto = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = Tema.Mono, BackColor = Tema.Crema,
                Left = 12, Top = 12, Width = 456, Height = 560, Text = _ticket.ComoTexto()
            };
            Controls.Add(texto);
            Controls.Add(Ui.Boton("Reimprimir", 12, 586, 130, 40, (s, e) =>
            {
                try { var c = Config.Actual.Copia(); c.Copias = 1; Impresora.Imprimir(_ticket, c); }
                catch (Exception ex) { Ui.Error(ex, "Reimprimir corte"); }
            }));
            var cerrar = Ui.Boton("Cerrar (Esc)", 338, 586, 130, 40, (s, e) => { DialogResult = DialogResult.Cancel; });
            Controls.Add(cerrar);
            CancelButton = cerrar;
            Shown += (s, e) => cerrar.Focus();
        }
    }
}
