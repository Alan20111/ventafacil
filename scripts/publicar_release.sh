#!/usr/bin/env bash
# Compila VentaFácil y lo publica como un release nuevo en GitHub
# (v1.0 -> v1.1 -> v1.2 ...) con:
#   - VentaFacil.exe            ejecutable suelto (trae SQLite adentro, funciona solo)
#   - VentaFacil-vX.Y-win.zip   carpeta completa: exe, DLLs, instalar.bat y LEEME.txt
#
#   scripts/publicar_release.sh "Qué cambió"         (siguiente versión)
#   scripts/publicar_release.sh "Qué cambió" v2.0    (versión explícita)
#
# Requiere: el commit ya subido a GitHub (push), gh con sesión iniciada y el SDK de .NET.
set -euo pipefail
cd "$(dirname "$0")/.."

REPO="Alan20111/ventafacil"
CAMBIOS="${1:?Indica qué cambió, por ejemplo: scripts/publicar_release.sh \"Nueva pantalla de X\"}"

if [ -n "$(git status --porcelain)" ]; then
  echo "Hay cambios sin commit; haz commit y push antes de publicar." >&2; exit 1
fi
git fetch -q origin
if [ "$(git rev-parse HEAD)" != "$(git rev-parse origin/main)" ]; then
  echo "El commit actual no está en origin/main; haz push antes de publicar." >&2; exit 1
fi

# Versión: la indicada, o la última + 0.1
if [ -n "${2:-}" ]; then
  VERSION="$2"
else
  ULTIMA=$(gh release list -R "$REPO" --limit 1 --json tagName --jq '.[0].tagName' 2>/dev/null || true)
  ULTIMA="${ULTIMA:-v0.9}"
  MAYOR=$(echo "${ULTIMA#v}" | cut -d. -f1); MENOR=$(echo "${ULTIMA#v}" | cut -d. -f2)
  VERSION="v${MAYOR}.$((MENOR + 1))"
fi
export VERSION_NUM="${VERSION#v}.0"

echo "== Compilando $VERSION"
./scripts/publicar.sh

SALIDA="$(mktemp -d)"
ZIP="VentaFacil-$VERSION-win.zip"
cp publish/VentaFacil.zip "$SALIDA/$ZIP"
cp publish/VentaFacil/VentaFacil.exe "$SALIDA/"

cat > "$SALIDA/notas.md" <<NOTAS
## Cambios

$CAMBIOS

## Descarga

- **VentaFacil.exe**: el programa en un solo archivo. Ábralo: se instala solo en \`C:\\VentaFacil\` y crea el acceso en el escritorio. Los datos quedan en \`C:\\VentaFacil\\datos\`.
- **$ZIP**: lo mismo en carpeta, por si prefiere copiarla a mano (\`LEEME.txt\` incluido).

## Antes de abrirlo

1. Windows 7: instalar **.NET Framework 4.8** (sin él no abre; Windows 8, 10 y 11 ya lo traen).
2. Entrar con usuario \`admin\`, contraseña \`admin\` (cámbiela en F11 → Usuarios).
3. La primera vez se abre la ventana de impresora: **Detectar automáticamente** → **F3 Imprimir prueba** → **Guardar**.
4. Para cargar los productos de la semillera: F11 → Productos → **Importar Excel semillera**.

> Compilado con el SDK de .NET desde macOS (commit $(git rev-parse --short HEAD)).
NOTAS

echo "== Publicando $VERSION"
gh release create "$VERSION" "$SALIDA/VentaFacil.exe" "$SALIDA/$ZIP" -R "$REPO" \
  --target "$(git rev-parse HEAD)" --title "VentaFácil $VERSION" --notes-file "$SALIDA/notas.md"
rm -rf "$SALIDA"
