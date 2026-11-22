using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using VentaFacil.Core;

// ======================================================================
// Herramienta de licencias de VentaFácil (código corto). Corre en la computadora de quien VENDE
// el programa y se abre en el navegador. Solo escucha en 127.0.0.1.
//
// Guarda, FUERA del repositorio, en ~/.ventafacil-licencias:
//   LicenciaSecreto.cs -> el secreto con el que se calculan las claves. RESPÁLDELO (USB, otra nube).
//                         Si se pierde o se cambia, las claves ya entregadas dejan de servir.
//   registro.json      -> quién compró, en qué equipo y cuándo.
// ======================================================================

var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ventafacil-licencias");
var rutaSecreto = Path.Combine(carpeta, "LicenciaSecreto.cs");
var rutaRegistro = Path.Combine(carpeta, "registro.json");
Directory.CreateDirectory(carpeta);

if (args.Contains("--probar")) return Probar();

if (args.Contains("--crear-secreto"))
{
    if (File.Exists(rutaSecreto)) { Console.WriteLine("Ya existe un secreto en " + rutaSecreto + ". No se cambia (cambiarlo invalida las claves ya entregadas)."); return 0; }
    var bytes = RandomNumberGenerator.GetBytes(36);
    string secreto = "VentaFacil::MX::v1::" + Convert.ToBase64String(bytes).Replace('+', 'a').Replace('/', 'b').Replace("=", "");
    File.WriteAllText(rutaSecreto,
        "namespace VentaFacil.Core\n{\n    // SECRETO DE LICENCIAS. No subir a ningún repositorio. Respáldelo.\n    internal static class LicenciaSecreto\n    {\n" +
        $"        public const string Valor = \"{secreto}\";\n        public const bool Desarrollo = false;\n    }}\n}}\n");
    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(rutaSecreto, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    Console.WriteLine("Secreto creado en " + rutaSecreto);
    Console.WriteLine("Vuelva a ejecutar el comando para que el programa lo use.");
    return 0;
}

bool sinSecreto = LicenciaSecreto.Desarrollo;
if (sinSecreto)
{
    Console.WriteLine("Todavía no hay un secreto de licencias. Ejecute primero:  dotnet run -- --crear-secreto");
    Console.WriteLine("(y después vuelva a ejecutar dotnet run).");
    return 1;
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
    if (codigo.Length != LicenciaNucleo.LongitudCodigo) return Results.BadRequest(new { error = "El código del equipo debe tener 12 caracteres (XXXX-XXXX-XXXX)." });
    if (string.IsNullOrWhiteSpace(s.Cliente)) return Results.BadRequest(new { error = "Escriba el nombre del cliente." });

    string clave = LicenciaNucleo.GenerarClave(codigo, LicenciaSecreto.Valor);
    // Comprobación con el MISMO código que usa el programa
    if (!LicenciaNucleo.Verificar(codigo, clave, out _)) return Results.BadRequest(new { error = "Error interno: la clave generada no se verifica." });

    lock (cerrojo)
    {
        var lista = Leer();
        var existente = lista.FirstOrDefault(x => Base32.Normalizar(x.CodigoInstalacion) == codigo);
        if (existente != null) return Results.Json(new { existente.Id, existente.Cliente, existente.CodigoInstalacion, existente.Clave, repetido = true });
        var entrada = new Entrada
        {
            Id = lista.Count == 0 ? 1 : lista.Max(x => x.Id) + 1,
            Cliente = s.Cliente.Trim(), Telefono = (s.Telefono ?? "").Trim(), Notas = (s.Notas ?? "").Trim(),
            CodigoInstalacion = Base32.Agrupar(codigo), Clave = clave, Fecha = DateTime.Now
        };
        lista.Add(entrada);
        Guardar(lista);
        return Results.Json(new { entrada.Id, entrada.Cliente, entrada.CodigoInstalacion, entrada.Clave, repetido = false });
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
Console.WriteLine($"  Secreto:               {rutaSecreto}   (¡respáldelo!)");
Console.WriteLine($"  Registro de clientes:  {rutaRegistro}");
Console.WriteLine("  Para cerrar: Ctrl+C");
Console.WriteLine();
try { if (OperatingSystem.IsMacOS()) Process.Start("open", Direccion); else if (OperatingSystem.IsWindows()) Process.Start(new ProcessStartInfo(Direccion) { UseShellExecute = true }); } catch { }
app.Run();
return 0;

// ---------------------------------------------------------------- funciones

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

// Prueba con el MISMO código del programa
int Probar()
{
    int fallas = 0;
    void Revisar(string nombre, bool ok) { Console.WriteLine((ok ? "[OK]    " : "[FALLA] ") + nombre); if (!ok) fallas++; }
    const string secreto = "secreto-de-prueba-123";
    string equipo = "ABCD-EFGH-JKLM", otro = "ZZZZ-YYYY-XXXX";

    string clave = LicenciaNucleo.GenerarClave(equipo, secreto);
    Revisar("la clave tiene 16 caracteres en 4 grupos (XXXX-XXXX-XXXX-XXXX)", clave.Length == 19 && clave.Count(c => c == '-') == 3);
    Revisar("la clave es siempre la misma para el mismo equipo (sirve al reinstalar)", LicenciaNucleo.GenerarClave("abcdefghjklm", secreto) == clave);
    Revisar("la clave válida se acepta", LicenciaNucleo.Verificar(equipo, clave, secreto, out _));
    Revisar("se acepta en minúsculas, sin guiones y con espacios", LicenciaNucleo.Verificar(equipo, " " + clave.Replace("-", "").ToLowerInvariant() + " ", secreto, out _));
    Revisar("en otro equipo se rechaza", !LicenciaNucleo.Verificar(otro, clave, secreto, out _));
    Revisar("equipos distintos dan claves distintas", LicenciaNucleo.GenerarClave(otro, secreto) != clave);

    char[] c = clave.ToCharArray();
    int i = clave.Length - 1; c[i] = c[i] == 'A' ? 'B' : 'A';
    Revisar("una clave con un carácter cambiado se rechaza", !LicenciaNucleo.Verificar(equipo, new string(c), secreto, out _));
    Revisar("una clave más corta se rechaza", !LicenciaNucleo.Verificar(equipo, clave.Substring(0, 15), secreto, out string m1) && m1.Contains("16"));
    Revisar("con otro secreto la clave se rechaza", !LicenciaNucleo.Verificar(equipo, clave, "otro-secreto", out _));
    Revisar("texto basura se rechaza", !LicenciaNucleo.Verificar(equipo, "hola mundo", secreto, out _));
    Revisar("código de equipo inválido se rechaza", !LicenciaNucleo.Verificar("ABC", clave, secreto, out _));
    Revisar("el alfabeto no usa caracteres que se confunden (0, 1, I, O)", !Base32.Codificar(RandomNumberGenerator.GetBytes(2000)).Any(ch => "01IO".Contains(ch)));

    Console.WriteLine(fallas == 0 ? "\nTodas las pruebas pasaron." : $"\n{fallas} prueba(s) fallaron.");
    return fallas == 0 ? 0 : 1;
}

record Solicitud(string Cliente, string Telefono, string Notas, string CodigoInstalacion);

class Entrada
{
    public int Id { get; set; }
    public string Cliente { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Notas { get; set; } = "";
    public string CodigoInstalacion { get; set; } = "";
    public string Clave { get; set; } = "";
    public DateTime Fecha { get; set; }
}
