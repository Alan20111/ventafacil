using System.Text.Json;
using VentaFacil.Printing;

// Pruebas del código de barras Code 128. Uso:  dotnet run
// Las referencias (referencias-jsbarcode.json) las generó la librería independiente JsBarcode (formato CODE128B):
// si nuestro codificador da EXACTAMENTE las mismas barras, un lector las leerá igual.
int fallas = 0;
void Revisar(string nombre, bool ok, string detalle = "") { Console.WriteLine((ok ? "[OK]    " : "[FALLA] ") + nombre + (ok || detalle == "" ? "" : "  -> " + detalle)); if (!ok) fallas++; }

var referencias = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "referencias-jsbarcode.json")));
foreach (var par in referencias)
    Revisar($"«{par.Key}» da las mismas barras que JsBarcode ({par.Value.Length} módulos)", Code128.ComoTexto(par.Key) == par.Value,
        Code128.ComoTexto(par.Key) == par.Value ? "" : $"\n     nuestro: {Code128.ComoTexto(par.Key)}\n     jsbarcode: {par.Value}");

// Un decodificador propio, hecho aparte con las reglas del estándar, para comprobar el ida y vuelta con textos al azar
bool EstructuraValida(bool[] m)
{
    // 11 módulos por símbolo (13 el paro), alternando barra/espacio, empezando por barra y terminando el paro en barra de 2
    if ((m.Length - 13) % 11 != 0) return false;
    if (!m[0] || !(m[^1] && m[^2]) || m[^3]) return false;   // empieza con barra; el paro termina en barra de dos módulos
    int simbolos = (m.Length - 13) / 11;
    for (int s = 0; s < simbolos; s++)
    {
        int ini = s * 11;
        if (!m[ini] || m[ini + 10]) return false;             // cada símbolo comienza en barra y termina en espacio
        int barras = 0; for (int k = 1; k < 11; k++) if (m[ini + k] && !m[ini + k - 1]) barras++;
        if (barras != 2) return false;                         // 3 barras = 1 al inicio + 2 transiciones nuevas
    }
    return true;
}
var azar = new Random(7);
int malos = 0, total = 0;
for (int n = 0; n < 5000; n++)
{
    int largo = azar.Next(1, 41);
    var texto = new string(Enumerable.Range(0, largo).Select(_ => (char)azar.Next(32, 127)).ToArray());
    total++;
    var m = Code128.Modulos(texto);
    var valores = Code128.Valores(texto);
    // dígito de control recalculado a mano
    long suma = 104; for (int i = 0; i < texto.Length; i++) suma += (long)(texto[i] - 32) * (i + 1);
    if (!EstructuraValida(m) || valores[^2] != (int)(suma % 103) || valores[0] != 104 || valores[^1] != 106 || m.Length != 11 * (texto.Length + 2) + 13) malos++;
}
Revisar($"{total} textos al azar (1 a 40 caracteres): estructura, dígito de control y largo correctos", malos == 0, $"{malos} con problema");

Revisar("los números cortos que usa la tienda (1, 12, 109, 00123) se pueden codificar", new[] { "1", "12", "109", "00123", "7501234567890" }.All(Code128.Soporta));
Revisar("se rechaza vacío, más de 40 caracteres, acentos y saltos de línea", !Code128.Soporta("") && !Code128.Soporta(null) && !Code128.Soporta(new string('1', 41)) && !Code128.Soporta("TAPIOCA Ñ") && !Code128.Soporta("a\nb"));
bool lanzo = false; try { Code128.Modulos("ñ"); } catch (ArgumentException) { lanzo = true; }
Revisar("codificar algo no soportado lanza un error claro (no genera barras falsas)", lanzo);
Revisar("el mismo texto siempre da las mismas barras", Code128.ComoTexto("109") == Code128.ComoTexto("109") && Code128.ComoTexto("109") != Code128.ComoTexto("110"));

// ---------- trama ESC/POS para imprimir las etiquetas ----------
{
    // 10 puntos de ancho = 2 bytes por fila. Puntos negros en x = 0, 7, 8 y 9 de la fila 1.
    var negros = new HashSet<(int, int)> { (0, 1), (7, 1), (8, 1), (9, 1) };
    var d = RasterEscPos.Empaquetar((x, y) => negros.Contains((x, y)), 10, 3);
    Revisar("empaquetar: 10 puntos de ancho = 2 bytes por fila, 3 filas", d.Length == 6);
    Revisar("empaquetar: el punto de más a la izquierda es el bit más alto (0x80) y el 8 abre el segundo byte", d[2] == 0x81 && d[3] == 0xC0 && d[0] == 0 && d[1] == 0 && d[4] == 0 && d[5] == 0, BitConverter.ToString(d));

    var c = RasterEscPos.Comandos(d, 10, 3);
    Revisar("comando GS v 0: encabezado 1D 76 30 00, ancho 2 bytes, alto 3, y los datos", c.Length == 8 + 6 && c[0] == 0x1D && c[1] == 0x76 && c[2] == 0x30 && c[3] == 0 && c[4] == 2 && c[5] == 0 && c[6] == 3 && c[7] == 0 && c.Skip(8).SequenceEqual(d));

    int ancho = 384, alto = 300, bpf = RasterEscPos.BytesPorFila(ancho);
    var grande = RasterEscPos.Empaquetar((x, y) => x == y, ancho, alto);
    var cg = RasterEscPos.Comandos(grande, ancho, alto);
    Revisar("384 puntos = 48 bytes por fila; 300 filas se mandan en 3 bandas (128 + 128 + 44)", bpf == 48 && cg.Length == 3 * 8 + 48 * 300 && cg[6] == 128 && cg[8 + 48 * 128 + 6] == 128 && cg[2 * (8 + 48 * 128) + 6] == 44);
    // los datos de cada banda son los de sus filas, sin saltar ni repetir ninguna
    var rearmado = new List<byte>(); int pos = 0;
    while (pos < cg.Length) { int h = cg[pos + 6] | (cg[pos + 7] << 8); pos += 8; rearmado.AddRange(cg.Skip(pos).Take(48 * h)); pos += 48 * h; }
    Revisar("al juntar las bandas se recupera la imagen completa, sin cambios", rearmado.SequenceEqual(grande));
    Revisar("576 puntos (papel de 80 mm) = 72 bytes por fila", RasterEscPos.BytesPorFila(576) == 72);
    bool lanzoRaster = false; try { RasterEscPos.Comandos(new byte[5], 10, 3); } catch (ArgumentException) { lanzoRaster = true; }
    Revisar("datos que no corresponden a la imagen se rechazan (no se manda basura a la impresora)", lanzoRaster);
}

Console.WriteLine(fallas == 0 ? "\nTodas las pruebas del código de barras pasaron." : $"\n{fallas} prueba(s) fallaron.");
return fallas == 0 ? 0 : 1;
