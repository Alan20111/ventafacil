using System;
using System.Collections.Generic;

namespace VentaFacil.Printing
{
    /// <summary>
    /// Imagen en blanco y negro → comandos ESC/POS de trama (GS v 0). Sin dependencias de Windows ni de pantalla:
    /// se prueba aparte (herramientas/pruebas-barras). Sirve para imprimir etiquetas con código de barras en
    /// CUALQUIER impresora térmica ESC/POS, sin depender de que soporte el comando nativo de códigos de barras.
    /// </summary>
    public static class RasterEscPos
    {
        /// <summary>Se manda por bandas de este alto: algunas impresoras tienen poco búfer y fallan con imágenes altas.</summary>
        public const int FilasPorBanda = 128;

        public static int BytesPorFila(int anchoPx) { return (anchoPx + 7) / 8; }

        /// <summary>Un bit por punto (1 = negro), 8 puntos por byte, el de más a la izquierda en el bit más alto.</summary>
        public static byte[] Empaquetar(Func<int, int, bool> oscuro, int ancho, int alto)
        {
            int bpf = BytesPorFila(ancho);
            var datos = new byte[bpf * alto];
            for (int y = 0; y < alto; y++)
                for (int x = 0; x < ancho; x++)
                    if (oscuro(x, y)) datos[y * bpf + (x >> 3)] |= (byte)(0x80 >> (x & 7));
            return datos;
        }

        /// <summary>GS v 0 m xL xH yL yH + datos, por bandas.</summary>
        public static byte[] Comandos(byte[] datos, int anchoPx, int alto)
        {
            int bpf = BytesPorFila(anchoPx);
            if (datos.Length != bpf * alto) throw new ArgumentException("Los datos no corresponden a la imagen.");
            var salida = new List<byte>();
            for (int y0 = 0; y0 < alto; y0 += FilasPorBanda)
            {
                int h = Math.Min(FilasPorBanda, alto - y0);
                salida.AddRange(new byte[] { 0x1D, 0x76, 0x30, 0x00, (byte)(bpf & 0xFF), (byte)(bpf >> 8), (byte)(h & 0xFF), (byte)(h >> 8) });
                var banda = new byte[bpf * h];
                Array.Copy(datos, y0 * bpf, banda, 0, banda.Length);
                salida.AddRange(banda);
            }
            return salida.ToArray();
        }
    }
}
