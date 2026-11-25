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
            Controls.Add(Ui.Etiqueta("Teclas rápidas", 16, 12, Tema.Mediana, Tema.Azul));
            var texto = new TextBox
            {
                Left = 16, Top = 54, Width = 688, Height = 520, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Font = Tema.Mono, BackColor = Tema.Crema, Text = string.Join("\r\n", new[] {
                    "MÓDULOS (en cualquier pantalla)",
                    "F1 Caja     F2 Turno     F3 Catálogo",
                    "F4 Almacén  F5 Resultados  F6 Ajustes",
                    "Esc ............ regresar a Caja",
                    "F10 ............ cambiar usuario",
                    "",
                    "CAJA",
                    "Código + Enter ... agregar artículo",
                    "3*  + código ..... cantidad 3 de un artículo",
                    "+ o Ctrl+F1 ...... COBRAR",
                    "Ctrl+F2 .......... buscar artículo",
                    "Ctrl+F3 o Supr ... quitar artículo",
                    "Ctrl+F4 .......... consultar precio",
                    "Ctrl+F5 .......... venta en espera",
                    "Ctrl+F6 .......... descuento / precio",
                    "Ctrl+F7 .......... artículo varios",
                    "Ctrl+F8 .......... cancelar ticket",
                    "Ctrl+P ........... reimprimir último ticket",
                    "Ctrl+D ........... abrir cajón",
                    "Ctrl+I ........... elegir / detectar impresora",
                    "",
                    "CATÁLOGO, ALMACÉN, RESULTADOS Y AJUSTES",
                    "Ctrl+N nuevo   Ctrl+M modificar   Ctrl+E eliminar",
                    "Ctrl+B buscar (Almacén)   Ctrl+R consultar (Resultados)",
                    "",
                    "Los demás módulos: Ctrl+F1, Ctrl+F2… son los botones",
                    "que se ven arriba de cada uno (cambian de vista o hacen algo)." })
            };
            Controls.Add(texto);
            Controls.Add(Ui.Boton("Cerrar (Esc)", 556, 586, 148, 44, (s, e) => Close()));
        }
    }
}
