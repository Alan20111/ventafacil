using System.Drawing;
using System.Windows.Forms;

namespace VentaFacil.UI
{
    /// <summary>Colores del menú superior: azul oscuro la barra, azul al seleccionar, crema los desplegables.</summary>
    public class MenuColores : ProfessionalColorTable
    {
        public MenuColores() { UseSystemColors = false; }
        public override Color MenuStripGradientBegin { get { return Tema.AzulOscuro; } }
        public override Color MenuStripGradientEnd { get { return Tema.AzulOscuro; } }
        public override Color MenuItemSelected { get { return Tema.Azul; } }
        public override Color MenuItemSelectedGradientBegin { get { return Tema.Azul; } }
        public override Color MenuItemSelectedGradientEnd { get { return Tema.Azul; } }
        public override Color MenuItemPressedGradientBegin { get { return Tema.Azul; } }
        public override Color MenuItemPressedGradientMiddle { get { return Tema.Azul; } }
        public override Color MenuItemPressedGradientEnd { get { return Tema.Azul; } }
        public override Color MenuItemBorder { get { return Tema.Azul; } }
        public override Color MenuBorder { get { return Tema.Azul; } }
        public override Color ToolStripDropDownBackground { get { return Tema.Crema; } }
        public override Color ImageMarginGradientBegin { get { return Tema.Crema; } }
        public override Color ImageMarginGradientMiddle { get { return Tema.Crema; } }
        public override Color ImageMarginGradientEnd { get { return Tema.Crema; } }
        public override Color SeparatorDark { get { return Tema.Piedra; } }
        public override Color SeparatorLight { get { return Tema.Crema; } }
    }

    /// <summary>Que el texto de la opción seleccionada se lea (blanco sobre azul).</summary>
    public class MenuRenderer : ToolStripProfessionalRenderer
    {
        public MenuRenderer() : base(new MenuColores()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item.Enabled && (e.Item.Selected || e.Item.Pressed)) e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item.Selected || e.Item.Pressed ? Color.White : Tema.Azul;
            base.OnRenderArrow(e);
        }
    }

    /// <summary>Lista de teclas rápidas del punto de venta.</summary>
    public class FormAyudaTeclas : Dialogo
    {
        public FormAyudaTeclas() : base("Teclas rápidas", 720, 640)
        {
            Controls.Add(Ui.Etiqueta("Teclas rápidas del punto de venta", 16, 12, Tema.Mediana, Tema.Azul));
            var texto = new TextBox
            {
                Left = 16, Top = 54, Width = 688, Height = 520, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = Tema.Mono, BackColor = Tema.Crema, Text = string.Join("\r\n", new[] {
                    "Código + Enter ... agregar artículo",
                    "3*  + código ..... cantidad 3 de un artículo",
                    "+ o F1 ........... COBRAR",
                    "F2 ............... buscar artículo",
                    "F3 o Supr ........ quitar artículo",
                    "F4 ............... consultar precio",
                    "F5 ............... venta en espera",
                    "F6 ............... descuento / precio",
                    "F7 ............... entradas / salidas de efectivo",
                    "F8 ............... artículo varios",
                    "F9 ............... cortes de caja (X y Z)",
                    "F10 .............. cambiar usuario",
                    "F11 .............. administración",
                    "F12 .............. cancelar ticket",
                    "Ctrl+P ........... reimprimir último ticket",
                    "Ctrl+D ........... abrir cajón",
                    "Ctrl+I ........... elegir / detectar impresora",
                    "",
                    "Alt ............... abre el menú de arriba",
                    "",
                    "Todo se puede hacer con el menú de arriba o con la barra de",
                    "botones; las teclas son solo para ir más rápido." })
            };
            Controls.Add(texto);
            Controls.Add(Ui.Boton("Cerrar (Esc)", 556, 586, 148, 44, (s, e) => Close()));
        }
    }
}
