using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>
    /// Núcleo de las licencias. No usa nada de Windows ni de formularios: el mismo archivo se compila
    /// en VentaFácil y en la herramienta de licencias (que corre en Mac), así la verificación y la
    /// generación nunca se separan.
    ///
    /// Esquema: la clave es una FIRMA RSA-2048 del código del equipo. El programa solo lleva la
    /// clave PÚBLICA, que no sirve para fabricar claves. La clave privada se queda únicamente en la
    /// computadora de quien vende las licencias (~/.ventafacil-licencias/privada.pem). Por eso es
    /// seguro que el programa y su código sean públicos.
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

        /// <summary>XXXX-XXXX-XXXX</summary>
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

        // ---- CLAVE PÚBLICA (la privada NO está aquí). La genera la herramienta: herramientas/licencias ----
        public static readonly byte[] Modulo = Convert.FromBase64String("uPLjAcTPoQogAv1jsskXayj3McO4hGFNCvPYMhgtGjVAujqIvgMEZ42m2nLfslaUgkD2HPoOV+B8oF0QjDFz8vnBzYuXdNJ9NltLVujPSaT2Ba/1IJVgXQXBdHrwr8bnfCdmPpI+1SbJxClIb8OKN/JCCt6BdWR8M7wNijlcjN+5dwich3EYxEMt9zUU0Lrlv4qMp8l2ALHY416zmQQ3iTTWZ9umIQU6osVBIswf0KqacXR64BDy8mJYfRWw99kzXMk9tBhFLY5Rz8/ZnyEkLLSiIPoUzV4Ug/0O3Ft0lQyO1vNus9nmf+TvCyLjq2eLCsm/mjfZTBHQJ9Uz950lBQ==");
        public static readonly byte[] Exponente = Convert.FromBase64String("AQAB");

        // Prefijo DigestInfo de SHA-256 (PKCS#1 v1.5)
        static readonly byte[] PrefijoSha256 = {
            0x30, 0x31, 0x30, 0x0d, 0x06, 0x09, 0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01, 0x05, 0x00, 0x04, 0x20 };

        /// <summary>Código del equipo a partir de la huella (hex): XXXX-XXXX-XXXX.</summary>
        public static string CodigoInstalacion(string huellaHex)
        {
            var bytes = new byte[huellaHex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(huellaHex.Substring(i * 2, 2), 16);
            return Base32.Agrupar(Base32.Codificar(bytes).Substring(0, 12));
        }

        /// <summary>Lo que se firma: versión, equipo y vencimiento (yyyyMMdd o "0" = sin vencimiento).</summary>
        public static byte[] Datos(string codigoInstalacion, string vence)
        {
            return Encoding.UTF8.GetBytes(Version + "|" + Base32.Normalizar(codigoInstalacion) + "|" + vence);
        }

        /// <summary>Clave final: VF1.vence.firma(base64url). Se puede pegar por WhatsApp.</summary>
        public static string ArmarClave(string vence, byte[] firma)
        {
            return Version + "." + vence + "." + Convert.ToBase64String(firma).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>Verifica la clave contra el equipo. vence = null si no vence.</summary>
        public static bool Verificar(string codigoInstalacion, string clave, out DateTime? vence, out string motivo)
        {
            return Verificar(codigoInstalacion, clave, Modulo, Exponente, out vence, out motivo);
        }

        public static bool Verificar(string codigoInstalacion, string clave, byte[] modulo, byte[] exponente, out DateTime? vence, out string motivo)
        {
            vence = null;
            motivo = "";
            try
            {
                var sb = new StringBuilder();
                foreach (char c in clave ?? "") if (!char.IsWhiteSpace(c)) sb.Append(c);
                string[] partes = sb.ToString().Split('.');
                if (partes.Length != 3 || partes[0] != Version) { motivo = "La clave no tiene el formato correcto. Cópiela completa."; return false; }

                string b64 = partes[2].Replace('-', '+').Replace('_', '/');
                while (b64.Length % 4 != 0) b64 += "=";
                byte[] firma = Convert.FromBase64String(b64);

                if (!VerificarFirma(Datos(codigoInstalacion, partes[1]), firma, modulo, exponente))
                {
                    motivo = "La clave no corresponde a esta computadora. Revise que la copió completa y que su proveedor la generó con el código de este equipo.";
                    return false;
                }
                if (partes[1] != "0")
                {
                    DateTime d;
                    if (!DateTime.TryParseExact(partes[1], "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d))
                    { motivo = "La clave tiene una fecha inválida."; return false; }
                    vence = d;
                }
                return true;
            }
            catch (FormatException) { motivo = "La clave no tiene el formato correcto. Cópiela completa."; return false; }
        }

        /// <summary>Verificación RSA PKCS#1 v1.5 con SHA-256 hecha a mano (BigInteger): no depende de proveedores de Windows.</summary>
        public static bool VerificarFirma(byte[] datos, byte[] firma, byte[] modulo, byte[] exponente)
        {
            int k = modulo.Length;
            if (k < 64 || firma.Length != k) return false;
            BigInteger n = ABig(modulo), e = ABig(exponente), s = ABig(firma);
            if (s >= n) return false;
            byte[] em = DeBig(BigInteger.ModPow(s, e, n), k);

            byte[] hash;
            using (var sha = SHA256.Create()) hash = sha.ComputeHash(datos);

            var esperado = new byte[k];
            esperado[1] = 1;
            int t = PrefijoSha256.Length + hash.Length;
            for (int i = 2; i < k - t - 1; i++) esperado[i] = 0xFF;
            esperado[k - t - 1] = 0;
            Array.Copy(PrefijoSha256, 0, esperado, k - t, PrefijoSha256.Length);
            Array.Copy(hash, 0, esperado, k - hash.Length, hash.Length);

            int dif = 0;
            for (int i = 0; i < k; i++) dif |= em[i] ^ esperado[i];
            return dif == 0;
        }

        static BigInteger ABig(byte[] grandeAlFrente)
        {
            var r = new byte[grandeAlFrente.Length + 1]; // el 0 final evita que sea negativo
            for (int i = 0; i < grandeAlFrente.Length; i++) r[i] = grandeAlFrente[grandeAlFrente.Length - 1 - i];
            return new BigInteger(r);
        }

        static byte[] DeBig(BigInteger v, int k)
        {
            byte[] chico = v.ToByteArray();
            var r = new byte[k];
            for (int i = 0; i < chico.Length && i < k; i++) r[k - 1 - i] = chico[i];
            return r;
        }
    }
}
