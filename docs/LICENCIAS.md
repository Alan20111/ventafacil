# Licencias de VentaFácil

Licencia **permanente por computadora**. 30 días de prueba sin clave; después se pide la clave de activación.

## Para el cliente
1. Abre VentaFácil. Cuando le queden 7 días de prueba (o ya haya terminado) aparece la pantalla **Licencia**. También está en Administración → Configuración → **Licencia…**
2. Te manda el **código del equipo** (`XXXX-XXXX-XXXX`) con "Copiar código".
3. Tú le generas la clave (abajo) y se la mandas por WhatsApp.
4. La pega en la pantalla y pulsa **Activar**. Una sola vez. Si reinstala el programa, pega la misma clave.

## Para ti (quien vende)
```bash
cd herramientas/licencias
dotnet run
```
Se abre una página en el navegador (`http://127.0.0.1:5098`, solo en tu computadora):
escribes el cliente, pegas su código del equipo y pulsas **Generar clave**. Queda registrada (quién, qué equipo, cuándo). Puedes poner una fecha de vencimiento si algún día vendes algo por suscripción (por ejemplo, el panel web).

### La llave privada — importante
- Se crea sola la primera vez en `~/.ventafacil-licencias/privada.pem`. **No está en el repositorio ni debe estarlo.**
- **Respáldala** (USB y otra nube). Si se pierde no puedes generar claves para los programas ya vendidos; habría que cambiar la llave pública del programa y reactivar a todos.
- El registro de clientes está en `~/.ventafacil-licencias/registro.json`. Respáldalo también.

## Cómo funciona (por qué es seguro que el código sea público)
- La clave es una **firma RSA-2048** del código del equipo. El programa solo lleva la parte **pública**, que sirve para comprobar firmas pero no para fabricarlas.
- Con un esquema de "secreto compartido" (como el de MediConsulta) cualquiera que descargue el `.exe` público podría sacar el secreto y fabricar claves para todos. Aquí no.
- La clave es larga (~350 caracteres) porque una firma RSA es larga: se pega, no se teclea.
- El código del equipo sale del `MachineGuid` de Windows: no cambia al cambiar disco o memoria; cambia si se reinstala Windows (se emite una clave nueva).

## Límites asumidos
- Frena copiar el programa a otra computadora. No frena a quien sepa modificar el `.exe`.
- Quien borre a la vez el archivo `datos\licencia.dat` y la clave de registro `HKCU\Software\VentaFacil` reinicia la prueba de 30 días.
- Si se atrasa la fecha de la computadora más de un día, se pide corregirla.

## Verificación
```bash
cd herramientas/licencias && dotnet run -- --probar
```
Prueba que una clave válida se acepta y que se rechazan las alteradas, las de otro equipo, las de otra llave, las con fecha cambiada y el texto basura.
