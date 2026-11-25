using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace VentaFacil.Core
{
    /// <summary>El archivo no es válido, no es para este equipo o fue modificado. El mensaje es para la persona.</summary>
    public class EnvioInvalidoException : Exception { public EnvioInvalidoException(string mensaje) : base(mensaje) { } }

    /// <summary>
    /// Formato de los archivos que viajan en la USB. El contenido va comprimido y cifrado (AES-256), con un sello
    /// (HMAC-SHA256) que detecta cualquier cambio. El ENVÍO lleva además la firma RSA de la oficina: la llave que firma
    /// nunca sale de la oficina, así que la PC de la caja puede comprobar un envío pero no fabricarlo.
    ///   marca(4) · id de la tienda(32) · sal/IV(16) · largo(4) · cifrado · sello(32) · [largo(4) · firma]
    /// </summary>
    public static class EnvioArchivo
    {
        public const string Envio = "VFE1", Acuse = "VFA1", Enlace = "VFL1";
        const int Cabecera = 4 + 32 + 16 + 4, MaximoDescomprimido = 64 * 1024 * 1024;

        public static string NuevoId() { return Guid.NewGuid().ToString("N"); }

        public static byte[] NuevaClave()
        {
            var b = new byte[32];
            using (var r = new RNGCryptoServiceProvider()) r.GetBytes(b);
            return b;
        }

        /// <summary>Par de llaves de la oficina. Devuelve la privada (XML); la pública sale de ella.</summary>
        public static string NuevaLlavePrivada() { using (var rsa = new RSACryptoServiceProvider(2048)) { rsa.PersistKeyInCsp = false; return rsa.ToXmlString(true); } }
        public static string Publica(string privada) { using (var rsa = Rsa(privada)) return rsa.ToXmlString(false); }
        static RSACryptoServiceProvider Rsa(string xml) { var r = new RSACryptoServiceProvider(); r.PersistKeyInCsp = false; r.FromXmlString(xml); return r; }

        static byte[] Derivar(byte[] clave, string para) { using (var h = new HMACSHA256(clave)) return h.ComputeHash(Encoding.ASCII.GetBytes(para)); }

        /// <summary>La clave que sale de la contraseña del archivo de enlace.</summary>
        static byte[] DeContrasena(string contrasena, byte[] sal) { using (var d = new Rfc2898DeriveBytes(contrasena, sal, 50000)) return d.GetBytes(32); }

        // ------------------------------------------------------------------ escribir
        public static byte[] Cerrar(string marca, string tiendaId, string texto, byte[] clave, string privada = null, string contrasena = null)
        {
            var iv = new byte[16];
            using (var r = new RNGCryptoServiceProvider()) r.GetBytes(iv);
            if (contrasena != null) clave = DeContrasena(contrasena, iv);
            byte[] cifrado;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256; aes.Key = Derivar(clave, "cifrar"); aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                byte[] comprimido;
                using (var ms = new MemoryStream())
                {
                    using (var gz = new GZipStream(ms, CompressionMode.Compress, true)) { var b = Encoding.UTF8.GetBytes(texto); gz.Write(b, 0, b.Length); }
                    comprimido = ms.ToArray();
                }
                using (var enc = aes.CreateEncryptor()) cifrado = enc.TransformFinalBlock(comprimido, 0, comprimido.Length);
            }
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(Encoding.ASCII.GetBytes(marca)); w.Write(Encoding.ASCII.GetBytes(tiendaId)); w.Write(iv); w.Write(cifrado.Length); w.Write(cifrado);
                w.Flush();
                using (var h = new HMACSHA256(Derivar(clave, "sello"))) w.Write(h.ComputeHash(ms.ToArray()));
                w.Flush();
                if (privada != null)
                    using (var rsa = Rsa(privada))
                    {
                        var firma = rsa.SignHash(Resumen(ms.ToArray()), CryptoConfig.MapNameToOID("SHA256"));
                        w.Write(firma.Length); w.Write(firma);
                    }
                w.Flush();
                return ms.ToArray();
            }
        }

        static byte[] Resumen(byte[] b) { using (var s = SHA256.Create()) return s.ComputeHash(b); }

        // ------------------------------------------------------------------ leer
        /// <summary>Para qué tienda es, sin abrirlo. null si no es un archivo de este tipo.</summary>
        public static string TiendaDe(byte[] a, string marca)
        {
            if (a == null || a.Length < Cabecera + 32 || Encoding.ASCII.GetString(a, 0, 4) != marca) return null;
            return Encoding.ASCII.GetString(a, 4, 32);
        }

        /// <summary>
        /// Comprueba el sello y abre el contenido. Con <paramref name="publica"/> comprueba también la firma; si es null y
        /// el archivo es un envío, la firma se comprueba después con FirmaValida (primer envío: la llave viene adentro).
        /// </summary>
        public static string Abrir(byte[] a, string marca, byte[] clave, string publica = null, string contrasena = null)
        {
            if (TiendaDe(a, marca) == null) throw new EnvioInvalidoException("El archivo no es de VentaFácil o está incompleto.");
            int largo = BitConverter.ToInt32(a, 52);
            if (largo <= 0 || largo > a.Length - Cabecera - 32) throw new EnvioInvalidoException("El archivo está incompleto. Vuelva a copiarlo a la USB.");
            var iv = Parte(a, 36, 16);
            if (contrasena != null) clave = DeContrasena(contrasena, iv);
            int fin = Cabecera + largo;
            byte[] sello;
            using (var h = new HMACSHA256(Derivar(clave, "sello"))) sello = h.ComputeHash(a, 0, fin);
            if (!Iguales(sello, Parte(a, fin, 32)))
                throw new EnvioInvalidoException(contrasena != null ? "La contraseña no es correcta o el archivo está dañado." : "El archivo fue modificado o está dañado. No se usó.");
            if (marca != Envio && a.Length != fin + 32) throw new EnvioInvalidoException("El archivo tiene datos de más. No se usó.");
            if (publica != null && !FirmaValida(a, publica)) throw new EnvioInvalidoException("El archivo no viene firmado por la oficina de esta tienda. No se usó.");
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256; aes.Key = Derivar(clave, "cifrar"); aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
                    // Se descifra completo antes de descomprimir: en .NET Framework, cerrar un CryptoStream sin leerlo hasta el final puede fallar
                    byte[] comprimido;
                    using (var dec = aes.CreateDecryptor()) comprimido = dec.TransformFinalBlock(a, Cabecera, largo);
                    using (var gz = new GZipStream(new MemoryStream(comprimido), CompressionMode.Decompress))
                    using (var ms = new MemoryStream())
                    {
                        var buf = new byte[8192]; int n;
                        while ((n = gz.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, n);
                            if (ms.Length > MaximoDescomprimido) throw new EnvioInvalidoException("El archivo es demasiado grande.");
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
            }
            catch (EnvioInvalidoException) { throw; }
            catch (Exception) { throw new EnvioInvalidoException("No se pudo leer el contenido del archivo."); }
        }

        public static bool FirmaValida(byte[] a, string publica)
        {
            try
            {
                int largo = BitConverter.ToInt32(a, 52);
                long fin = (long)Cabecera + largo + 32;
                if (largo <= 0 || a.Length < fin + 4) return false;
                int n = BitConverter.ToInt32(a, (int)fin);
                if (n <= 0 || n != a.Length - fin - 4) return false;
                using (var rsa = Rsa(publica)) return rsa.VerifyHash(Resumen(Parte(a, 0, (int)fin)), CryptoConfig.MapNameToOID("SHA256"), Parte(a, (int)fin + 4, n));
            }
            catch (Exception) { return false; }
        }

        static byte[] Parte(byte[] a, int desde, int n) { var b = new byte[n]; Buffer.BlockCopy(a, desde, b, 0, n); return b; }
        static bool Iguales(byte[] a, byte[] b) { int d = a.Length ^ b.Length; for (int i = 0; i < a.Length && i < b.Length; i++) d |= a[i] ^ b[i]; return d == 0; }

        // ------------------------------------------------------------------ disco
        /// <summary>Escribe completo en un temporal, lo baja al disco, lo relee y solo entonces le pone su nombre. Así nunca queda uno a medias.</summary>
        public static void Guardar(string ruta, byte[] datos)
        {
            string tmp = ruta + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write)) { fs.Write(datos, 0, datos.Length); fs.Flush(true); }
            if (!Iguales(File.ReadAllBytes(tmp), datos)) { File.Delete(tmp); throw new IOException("Lo escrito en la USB no coincide. Pruebe con otra memoria."); }
            if (File.Exists(ruta)) File.Delete(ruta);
            File.Move(tmp, ruta);
        }

        public static byte[] Cargar(string ruta)
        {
            var i = new FileInfo(ruta);
            if (i.Length > 32 * 1024 * 1024) throw new EnvioInvalidoException("El archivo es demasiado grande para ser un envío.");
            return File.ReadAllBytes(ruta);
        }
    }
}
