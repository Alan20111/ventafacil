using System;
using System.Security.Cryptography;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>
    /// Núcleo de las licencias. No usa nada de Windows ni de formularios: el mismo archivo se compila
    /// en VentaFácil y en la herramienta de licencias (que corre en Mac), así la verificación y la
    /// generación nunca se separan.
    ///
    /// Esquema (igual al de MediConsulta): el programa muestra un CÓDIGO DEL EQUIPO de 12 caracteres.
    /// La CLAVE es de 16 caracteres y se calcula con HMAC-SHA256 del código del equipo y un secreto.
    /// Es siempre la misma para ese equipo: no caduca y sirve al reinstalar.
    ///
    /// LÍMITE ASUMIDO: el secreto viaja dentro del .exe. Quien lo analice puede fabricar claves.
    /// Frena copiar el programa a otra computadora, no a un atacante decidido.
    /// El secreto NO está en el repositorio: vive en ~/.ventafacil-licencias/LicenciaSecreto.cs
    /// (ver LicenciaSecreto.cs y herramientas/licencias).
    /// </summary>
    public static class Base32
    {
        // Sin I, O, 0 ni 1 para que no se confundan al dictar un código.
        const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Codificar(byte[] datos)
        {
            var sb = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (byte b in datos)
            {
                buffer = (buffer << 8) | b;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Alfabeto[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0) sb.Append(Alfabeto[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        /// <summary>Mayúsculas y solo letras o números (quita guiones y espacios).</summary>
        public static string Normalizar(string texto)
        {
            var sb = new StringBuilder();
            foreach (char c in (texto ?? "").ToUpperInvariant())
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>XXXX-XXXX-XXXX-XXXX</summary>
        public static string Agrupar(string codigo)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < codigo.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append('-');
                sb.Append(codigo[i]);
            }
            return sb.ToString();
        }
    }

    public static class LicenciaNucleo
    {
        public const string Version = "VF1";
        public const int LongitudCodigo = 12;
        public const int LongitudClave = 16;

        /// <summary>Código del equipo a partir de la huella (hex): XXXX-XXXX-XXXX.</summary>
        public static string CodigoInstalacion(string huellaHex)
        {
            var bytes = new byte[huellaHex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(huellaHex.Substring(i * 2, 2), 16);
            return Base32.Agrupar(Base32.Codificar(bytes).Substring(0, LongitudCodigo));
        }

        /// <summary>La clave de activación de un equipo. Siempre es la misma para el mismo código y secreto.</summary>
        public static string GenerarClave(string codigoInstalacion, string secreto)
        {
            string equipo = Base32.Normalizar(codigoInstalacion);
            if (equipo.Length != LongitudCodigo)
                throw new ArgumentException("El código del equipo debe tener " + LongitudCodigo + " caracteres (XXXX-XXXX-XXXX).");
            byte[] firma;
            using (var h = new HMACSHA256(Encoding.UTF8.GetBytes(secreto)))
                firma = h.ComputeHash(Encoding.UTF8.GetBytes(Version + "|" + equipo));
            return Base32.Agrupar(Base32.Codificar(firma).Substring(0, LongitudClave));
        }

        public static bool Verificar(string codigoInstalacion, string clave, out string motivo)
        {
            return Verificar(codigoInstalacion, clave, LicenciaSecreto.Valor, out motivo);
        }

        public static bool Verificar(string codigoInstalacion, string clave, string secreto, out string motivo)
        {
            motivo = "";
            string recibida = Base32.Normalizar(clave);
            if (recibida.Length != LongitudClave)
            {
                motivo = "La clave debe tener " + LongitudClave + " letras y números. Revise que no falte ninguno.";
                return false;
            }
            string esperada;
            try { esperada = Base32.Normalizar(GenerarClave(codigoInstalacion, secreto)); }
            catch (ArgumentException) { motivo = "El código de este equipo no es válido."; return false; }

            // Comparación en tiempo constante
            int dif = 0;
            for (int i = 0; i < esperada.Length; i++) dif |= esperada[i] ^ recibida[i];
            if (dif != 0)
            {
                motivo = "La clave no corresponde a esta computadora. Revise que la copió completa y que su proveedor la generó con el código de este equipo.";
                return false;
            }
            return true;
        }
    }
}
