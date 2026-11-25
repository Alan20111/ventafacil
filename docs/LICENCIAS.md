# Licencias de VentaFácil

Licencia **permanente por computadora**. 30 días de prueba sin clave; después se pide la clave de activación (16 letras y números, `XXXX-XXXX-XXXX-XXXX`).

## Para el cliente
1. Abre VentaFácil. Cuando le queden 7 días de prueba (o ya haya terminado) aparece la pantalla **Licencia**. También está en *F6 Ajustes → Licencia*
2. Te manda el **código del equipo** (`XXXX-XXXX-XXXX`) con "Copiar código".
3. Tú le generas la clave (abajo) y se la mandas por WhatsApp.
4. La escribe en la pantalla y pulsa **Activar**. Una sola vez. Si reinstala el programa, escribe la misma clave (siempre es la misma para esa computadora).

## Para ti (quien vende)

### Lo normal: el portal web
**https://licencias.alanmendez.dev** (acceso directo "Licencias" en el escritorio de la Mac). Entras con tu contraseña, eliges **VentaFácil**, pegas el código del equipo y copias la clave. Sirve desde el teléfono. Es un portal general para todos tus productos; su código está en `~/Documents/Proyectos/PortalLicencias` (ver su `LEEME.md`).
- El secreto de VentaFácil está ahí como variable de entorno de Vercel, igual al de tu Mac. Las claves de una y otra herramienta son **idénticas** (hay pruebas que lo comprueban).
- Los registros del portal web y de la herramienta local (abajo) son **separados**: elige uno para trabajar.

### Alternativa local (sin internet)
La primera vez, crea el secreto (una sola vez en la vida):
```bash
cd herramientas/licencias
dotnet run -- --crear-secreto
```
Después, cada vez que vendas:
```bash
cd herramientas/licencias
dotnet run
```
Se abre una página en el navegador (`http://127.0.0.1:5098`, solo en tu computadora): escribes el cliente, pegas su código del equipo y pulsas **Generar clave**. Queda registrada (quién, qué equipo, cuándo). Si el equipo ya tenía clave, te devuelve la misma.

### El secreto — importante
- Está en `~/.ventafacil-licencias/LicenciaSecreto.cs`. **No está en el repositorio ni debe estarlo** (ni en su historial).
- Al compilar (`scripts/publicar_release.sh`), si ese archivo existe se incrusta en el `.exe`; si no existe, se usa uno de desarrollo y **ese `.exe` no acepta claves reales**. Publica siempre desde tu Mac.
- **Respáldalo** (USB y otra nube). Si se pierde o se cambia, **las claves ya entregadas dejan de servir** y habría que reactivar a todos.
- El registro de clientes está en `~/.ventafacil-licencias/registro.json`. Respáldalo también.

## Cómo funciona
- El código del equipo sale del `MachineGuid` de Windows: no cambia al cambiar disco o memoria; cambia si se reinstala Windows (entonces se emite una clave nueva).
- La clave es `HMAC-SHA256(secreto, "VF1|" + código del equipo)` recortada a 16 caracteres. Es la misma idea que MediConsulta, con un secreto distinto.

## Límites asumidos (léelos)
- **El secreto viaja dentro del `.exe`.** Como los `.exe` de VentaFácil están en GitHub Releases (público), cualquier persona con conocimientos puede sacarlo y fabricar claves para cualquier equipo. Esto frena copiar el programa a otra computadora, no a un atacante decidido.
- Si algún día importa más, hay dos salidas: dejar de publicar el `.exe` en un lugar público (mandarlo por WhatsApp o un enlace privado), o cambiar a una firma RSA, donde el programa solo lleva una llave pública que no sirve para fabricar claves (la clave sería más larga, ~350 caracteres, y se pegaría en lugar de teclearse).
- Quien borre a la vez el archivo `datos\licencia.dat` y la clave de registro `HKCU\Software\VentaFacil` reinicia la prueba de 30 días.
- Si se atrasa la fecha de la computadora más de un día, se pide corregirla.

## Verificación
```bash
cd herramientas/licencias && dotnet run -- --probar
```
Prueba que la clave válida se acepta (con minúsculas, sin guiones o con espacios) y que se rechazan la de otro equipo, la alterada, la corta, la de otro secreto y el texto basura.
