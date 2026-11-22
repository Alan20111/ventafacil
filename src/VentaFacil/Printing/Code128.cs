using System;
using System.Text;

namespace VentaFacil.Printing
{
    /// <summary>
    /// Código de barras Code 128, juego B (caracteres ASCII 32 a 126: letras, números y signos).
    /// Lo leen todos los lectores comunes. Sin dependencias de Windows ni de pantalla: se prueba aparte
    /// (herramientas/pruebas-barras) comparándolo contra una librería independiente.
    ///
    /// Un símbolo = 11 módulos (barra, espacio, barra, espacio, barra, espacio); el de paro = 13.
    /// Estructura: inicio B (104) + caracteres + dígito de control + paro (106).
    /// </summary>
    public static class Code128
    {
        // Anchos de barra/espacio de cada valor 0..106 (los seis primeros dígitos; el 106 lleva siete).
        static readonly string[] Patrones = {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
            "114131", "311141", "411131", "211412", "211214", "211232", "2331112" };

        const int InicioB = 104;
        const int Paro = 106;
        public const int LongitudMaxima = 40;

        /// <summary>¿Se puede codificar? (1 a 40 caracteres ASCII imprimibles).</summary>
        public static bool Soporta(string texto)
        {
            if (string.IsNullOrEmpty(texto) || texto.Length > LongitudMaxima) return false;
            foreach (char c in texto) if (c < 32 || c > 126) return false;
            return true;
        }

        /// <summary>Valores del código: inicio, caracteres, dígito de control y paro.</summary>
        public static int[] Valores(string texto)
        {
            if (!Soporta(texto)) throw new ArgumentException("El texto no se puede codificar en Code 128 (1 a " + LongitudMaxima + " caracteres ASCII).");
            var v = new int[texto.Length + 3];
            v[0] = InicioB;
            long suma = InicioB;
            for (int i = 0; i < texto.Length; i++)
            {
                v[i + 1] = texto[i] - 32;
                suma += (long)v[i + 1] * (i + 1);
            }
            v[texto.Length + 1] = (int)(suma % 103);
            v[texto.Length + 2] = Paro;
            return v;
        }

        /// <summary>Barras y espacios, módulo por módulo (true = barra negra). Sin zonas en blanco a los lados.</summary>
        public static bool[] Modulos(string texto)
        {
            var lista = new System.Collections.Generic.List<bool>();
            foreach (int valor in Valores(texto))
            {
                bool barra = true;
                foreach (char ancho in Patrones[valor])
                {
                    for (int k = 0; k < ancho - '0'; k++) lista.Add(barra);
                    barra = !barra;
                }
            }
            return lista.ToArray();
        }

        /// <summary>"1101001..." (para pruebas y depuración).</summary>
        public static string ComoTexto(string texto)
        {
            var sb = new StringBuilder();
            foreach (bool b in Modulos(texto)) sb.Append(b ? '1' : '0');
            return sb.ToString();
        }
    }
}
