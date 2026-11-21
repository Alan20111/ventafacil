#!/bin/bash
# Compila VentaFácil y deja la carpeta lista para copiar a la computadora de la tienda.
set -e
cd "$(dirname "$0")/.."
RAIZ=$(pwd)
rm -rf publish && mkdir -p publish/VentaFacil
dotnet build src/VentaFacil/VentaFacil.csproj -c Release -o publish/VentaFacil -nologo -v q ${VERSION_NUM:+-p:Version=$VERSION_NUM}
cd publish/VentaFacil
rm -f *.pdb
rm -rf x64            # el programa corre en 32 bits (x86): funciona igual en Windows de 32 y 64 bits
cp "$RAIZ/scripts/instalar.bat" .
sed "s/\$/$(printf "\\r")/" "$RAIZ/docs/LEEME.txt" > LEEME.txt
cd ..
rm -f VentaFacil.zip && zip -qr VentaFacil.zip VentaFacil
echo "Listo: $(pwd)/VentaFacil.zip"
du -sh VentaFacil VentaFacil.zip
