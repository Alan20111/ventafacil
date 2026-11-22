using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using VentaFacil.Core;

// ======================================================================
// Herramienta de licencias de VentaFácil. Corre en la computadora de quien VENDE el programa
// y se abre en el navegador. Solo escucha en 127.0.0.1.
//
// Guarda, FUERA del repositorio, en ~/.ventafacil-licencias:
//   privada.pem   -> la llave privada con la que se firman las claves. RESPÁLDELA (USB, otra nube).
//                    Si se pierde no se pueden generar más claves para los programas ya vendidos.
//   registro.json -> quién compró, en qué equipo y cuándo.
// ======================================================================

var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ventafacil-licencias");
var rutaPrivada = Path.Combine(carpeta, "privada.pem");
var rutaRegistro = Path.Combine(carpeta, "registro.json");
Directory.CreateDirectory(carpeta);

if (args.Contains("--probar")) return Probar();

RSA rsa = CargarOCrearClave();

if (args.Contains("--publica"))
{
    var p = rsa.ExportParameters(false);
    Console.WriteLine("Pegue estas dos líneas en src/VentaFacil/Core/LicenciaNucleo.cs:");
    Console.WriteLine();
    Console.WriteLine($"        public static readonly byte[] Modulo = Convert.FromBase64String(\"{Convert.ToBase64String(p.Modulus!)}\");");
    Console.WriteLine($"        public static readonly byte[] Exponente = Convert.FromBase64String(\"{Convert.ToBase64String(p.Exponent!)}\");");
    return 0;
}

const string Direccion = "http://127.0.0.1:5098";
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Direccion);
builder.Logging.ClearProviders();
var app = builder.Build();
var cerrojo = new object();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/registro", () => { lock (cerrojo) return Results.Json(Leer()); });

app.MapPost("/api/generar", (Solicitud s) =>
{
    string codigo = Base32.Normalizar(s.CodigoInstalacion);
    if (codigo.Length != 12) return Results.BadRequest(new { error = "El código de instalación debe tener 12 caracteres (XXXX-XXXX-XXXX)." });
    if (string.IsNullOrWhiteSpace(s.Cliente)) return Results.BadRequest(new { error = "Escriba el nombre del cliente." });

    string vence = "0";
    if (!string.IsNullOrWhiteSpace(s.Vence))
    {
        if (!DateTime.TryParse(s.Vence, out var d)) return Results.BadRequest(new { error = "La fecha de vencimiento no es válida." });
        vence = d.ToString("yyyyMMdd");
    }

    byte[] firma = rsa.SignData(LicenciaNucleo.Datos(codigo, vence), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    string clave = LicenciaNucleo.ArmarClave(vence, firma);

    // Comprobación con el MISMO código que usa el programa: si la clave pública del programa
    // no corresponde a esta llave privada, se avisa aquí y no en la tienda del cliente.
    if (!LicenciaNucleo.Verificar(codigo, clave, out _, out _))
        return Results.BadRequest(new { error = "La clave pública del programa no coincide con esta llave privada. Ejecute 'dotnet run -- --publica' y actualice LicenciaNucleo.cs." });

    lock (cerrojo)
    {
        var lista = Leer();
        var entrada = new Entrada
        {
            Id = lista.Count == 0 ? 1 : lista.Max(x => x.Id) + 1,
            Cliente = s.Cliente.Trim(), Telefono = (s.Telefono ?? "").Trim(), Notas = (s.Notas ?? "").Trim(),
            CodigoInstalacion = Base32.Agrupar(codigo), Vence = vence == "0" ? "" : vence, Clave = clave, Fecha = DateTime.Now
        };
        lista.Add(entrada);
        Guardar(lista);
        return Results.Json(entrada);
    }
});

app.MapDelete("/api/registro/{id:int}", (int id) =>
{
    lock (cerrojo)
    {
        var lista = Leer();
        if (lista.RemoveAll(x => x.Id == id) == 0) return Results.NotFound();
        Guardar(lista);
        return Results.Ok();
    }
});

Console.WriteLine();
Console.WriteLine("  VentaFácil - Licencias");
Console.WriteLine("  ----------------------");
Console.WriteLine($"  Abra en el navegador:  {Direccion}");
Console.WriteLine($"  Llave privada:         {rutaPrivada}   (¡respáldela!)");
Console.WriteLine($"  Registro de clientes:  {rutaRegistro}");
Console.WriteLine("  Para cerrar: Ctrl+C");
Console.WriteLine();
try { if (OperatingSystem.IsMacOS()) Process.Start("open", Direccion); else if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(Direccion) { UseShellExecute = true }); } catch { }
app.Run();
return 0;

// ---------------------------------------------------------------- funciones

RSA CargarOCrearClave()
{
    var r = RSA.Create();
    if (File.Exists(rutaPrivada)) { r.ImportFromPem(File.ReadAllText(rutaPrivada)); return r; }
    r = RSA.Create(2048);
    File.WriteAllText(rutaPrivada, r.ExportPkcs8PrivateKeyPem());
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(rutaPrivada, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    Console.WriteLine("Se creó una llave privada nueva en " + rutaPrivada);
    return r;
}

List<Entrada> Leer()
{
    if (!File.Exists(rutaRegistro)) return new List<Entrada>();
    return JsonSerializer.Deserialize<List<Entrada>>(File.ReadAllText(rutaRegistro)) ?? new List<Entrada>();
}

void Guardar(List<Entrada> lista)
{
    string tmp = rutaRegistro + ".tmp";
    File.WriteAllText(tmp, JsonSerializer.Serialize(lista, new JsonSerializerOptions { WriteIndented = true }));
    File.Move(tmp, rutaRegistro, true);
}

// Prueba de punta a punta con el MISMO código del programa
int Probar()
{
    int fallas = 0;
    void Revisar(string nombre, bool ok) { Console.WriteLine((ok ? "[OK]    " : "[FALLA] ") + nombre); if (!ok) fallas++; }

    using var par = RSA.Create(2048);
    var pub = par.ExportParameters(false);
    string equipo = "ABCD-EFGH-JKLM";
    string otro = "ZZZZ-YYYY-XXXX";

    foreach (string vence in new[] { "0", "20991231" })
    {
        byte[] firma = par.SignData(LicenciaNucleo.Datos(equipo, vence), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        string clave = LicenciaNucleo.ArmarClave(vence, firma);

        Revisar($"clave válida (vence={vence}) acepta", LicenciaNucleo.Verificar(equipo, clave, pub.Modulus!, pub.Exponent!, out var v, out _) && (vence == "0" ? v == null : v == new DateTime(2099, 12, 31)));
        Revisar("el código se acepta con minúsculas y sin guiones", LicenciaNucleo.Verificar("abcdefghjklm", clave, pub.Modulus!, pub.Exponent!, out _, out _));
        Revisar("la clave con saltos de línea (pegada de WhatsApp) acepta", LicenciaNucleo.Verificar(equipo, clave.Insert(30, "\r\n").Insert(90, " "), pub.Modulus!, pub.Exponent!, out _, out _));
        Revisar("en otro equipo se rechaza", !LicenciaNucleo.Verificar(otro, clave, pub.Modulus!, pub.Exponent!, out _, out _));

        string alterada = clave.Substring(0, clave.Length - 6) + (clave.EndsWith("AAAAAA") ? "BBBBBB" : "AAAAAA");
        Revisar("clave alterada se rechaza", !LicenciaNucleo.Verificar(equipo, alterada, pub.Modulus!, pub.Exponent!, out _, out _));
    }

    // Quitar el vencimiento de una clave con vencimiento no debe funcionar
    byte[] f2 = par.SignData(LicenciaNucleo.Datos(equipo, "20260101"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    string conVence = LicenciaNucleo.ArmarClave("20260101", f2);
    string sinVence = "VF1.0." + conVence.Split('.')[2];
    Revisar("cambiar la fecha de vencimiento invalida la firma", !LicenciaNucleo.Verificar(equipo, sinVence, pub.Modulus!, pub.Exponent!, out _, out _));

    using var otraLlave = RSA.Create(2048);
    var pub2 = otraLlave.ExportParameters(false);
    byte[] f3 = par.SignData(LicenciaNucleo.Datos(equipo, "0"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    Revisar("firmada por otra llave se rechaza", !LicenciaNucleo.Verificar(equipo, LicenciaNucleo.ArmarClave("0", f3), pub2.Modulus!, pub2.Exponent!, out _, out _));
    Revisar("texto basura se rechaza", !LicenciaNucleo.Verificar(equipo, "hola", pub.Modulus!, pub.Exponent!, out _, out _));

    Console.WriteLine(fallas == 0 ? "\nTodas las pruebas pasaron." : $"\n{fallas} prueba(s) fallaron.");
    return fallas == 0 ? 0 : 1;
}

record Solicitud(string Cliente, string Telefono, string Notas, string CodigoInstalacion, string Vence);

class Entrada
{
    public int Id { get; set; }
    public string Cliente { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Notas { get; set; } = "";
    public string CodigoInstalacion { get; set; } = "";
    public string Vence { get; set; } = "";
    public string Clave { get; set; } = "";
    public DateTime Fecha { get; set; }
}
