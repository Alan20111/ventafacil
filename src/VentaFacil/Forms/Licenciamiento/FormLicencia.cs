using System;
using System.Drawing;
using System.Windows.Forms;
using VentaFacil.Core;
using VentaFacil.UI;

namespace VentaFacil.Forms.Licenciamiento
{
    /// <summary>
    /// Pantalla de licencia: muestra el código del equipo y recibe la clave de activación.
    /// Se abre sola cuando quedan 7 días o menos de prueba, o cuando la prueba terminó.
    /// </summary>
    public class FormLicencia : Dialogo
    {
        readonly TextBox _clave;
        public bool Continuar { get; private set; }

        FormLicencia(InfoLicencia info, bool puedeContinuar) : base("Licencia de VentaFácil", 720, 470)
        {
            Continuar = puedeContinuar;
            Controls.Add(Ui.Etiqueta("Licencia de VentaFácil", 16, 12, Tema.Grande, Tema.Azul));

            string estado;
            if (info.Estado == EstadoLicencia.Activa)
                estado = "Activada en este equipo." + (info.Vence.HasValue ? " Vence el " + info.Vence.Value.ToString("dd/MM/yyyy") + "." : " No vence.");
            else if (info.Estado == EstadoLicencia.Prueba)
                estado = "Período de prueba: le quedan " + info.DiasRestantes + (info.DiasRestantes == 1 ? " día." : " días.");
            else estado = info.Motivo;
            Controls.Add(new Banda(info.Estado == EstadoLicencia.Activa ? Tono.Bien : info.Estado == EstadoLicencia.Prueba ? (info.DiasRestantes <= 7 ? Tono.Cuidado : Tono.Info) : Tono.Error, estado, 16, 64, 688));

            Controls.Add(Ui.Etiqueta("Código de este equipo (envíeselo a su proveedor):", 16, 126));
            var codigo = new TextBox { Left = 16, Top = 156, Width = 380, ReadOnly = true, Font = Tema.Mono, Text = Licencia.CodigoInstalacion(), BackColor = Tema.Crema };
            Controls.Add(codigo);
            Controls.Add(Ui.Boton("Copiar código", 408, 152, 200, 40, (s, e) => { try { Clipboard.SetText(codigo.Text); } catch (Exception) { } }));

            Controls.Add(Ui.Etiqueta("Escriba la clave de activación que le dio su proveedor (16 letras y números):", 16, 214));
            _clave = new TextBox { Left = 16, Top = 244, Width = 688, Font = new Font("Consolas", 22f, FontStyle.Bold), BackColor = Color.White, CharacterCasing = CharacterCasing.Upper, MaxLength = 30 };
            Controls.Add(_clave);

            var activar = Ui.Boton("Activar", 16, 316, 200, 52, (s, e) => Activar());
            Tema.Pastilla(activar, true);
            Controls.Add(activar);
            if (puedeContinuar && info.Estado != EstadoLicencia.Activa)
                Controls.Add(Ui.Boton("Continuar con la prueba", 232, 316, 280, 52, (s, e) => { Continuar = true; Close(); }));
            Controls.Add(Ui.Boton(info.Estado == EstadoLicencia.Activa ? "Cerrar" : "Salir", 528, 316, 176, 52, (s, e) => { Continuar = info.Estado == EstadoLicencia.Activa; Close(); }));

            Controls.Add(Ui.Etiqueta("La licencia es permanente y sirve solo en esta computadora. Si reinstala VentaFácil,\nvuelva a escribir la misma clave.", 16, 392, null, Tema.Humo));
        }

        void Activar()
        {
            string error = Licencia.Activar(_clave.Text);
            if (error != null) { Ui.Aviso(error, "Licencia"); return; }
            Ui.Info("VentaFácil quedó activado en esta computadora. Ya no necesita volver a hacerlo.", "Licencia");
            Continuar = true;
            Close();
        }

        /// <summary>Muestra la pantalla. Devuelve true si se puede seguir trabajando.</summary>
        public static bool Mostrar(InfoLicencia info, bool puedeContinuar)
        {
            using (var f = new FormLicencia(info, puedeContinuar)) { f.ShowDialog(); return f.Continuar; }
        }

        /// <summary>Al arrancar: si está activa o la prueba va holgada no estorba.</summary>
        public static bool Comprobar()
        {
            var info = Licencia.Evaluar();
            if (info.Estado == EstadoLicencia.Activa) return true;
            if (info.Estado == EstadoLicencia.Prueba && info.DiasRestantes > 7) return true;
            return Mostrar(info, info.Estado == EstadoLicencia.Prueba);
        }
    }
}
