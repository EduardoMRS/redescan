#!/usr/bin/env bash
#
# Empacota o executável self-contained do RedeScan como um AppImage — o formato
# "instalador"/portátil universal do Linux (um único arquivo clicável que roda
# em qualquer distribuição, sem instalar nada).
#
# Uso:
#   scripts/make-appimage.sh <caminho-do-binario-RedeScan> [saida.AppImage]
#
# Ex.: scripts/make-appimage.sh ./publish/linux-x64/RedeScan RedeScan-x86_64.AppImage
#
set -euo pipefail

BIN="${1:?informe o caminho do binário RedeScan}"
OUT="${2:-RedeScan-x86_64.AppImage}"
APPDIR="$(mktemp -d)/RedeScan.AppDir"

if [ ! -f "${BIN}" ]; then
  echo "!! Binário não encontrado: ${BIN}" >&2
  exit 1
fi

echo ">> Montando AppDir em ${APPDIR}"
mkdir -p "${APPDIR}/usr/bin"
cp "${BIN}" "${APPDIR}/usr/bin/RedeScan"
chmod +x "${APPDIR}/usr/bin/RedeScan"

# Ponto de entrada do AppImage.
cat > "${APPDIR}/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "${HERE}/usr/bin/RedeScan" "$@"
EOF
chmod +x "${APPDIR}/AppRun"

# Arquivo .desktop (metadados exibidos pelo sistema).
cat > "${APPDIR}/RedeScan.desktop" <<'EOF'
[Desktop Entry]
Type=Application
Name=RedeScan
Comment=Análise e teste de redes
Exec=RedeScan
Icon=redescan
Categories=Network;Utility;
Terminal=false
EOF

# Ícone: usa o do projeto Android se existir; senão gera um placeholder 1x1.
ICON_SRC="src/RedeScan.Android/Resources/drawable/icon.png"
if [ -f "${ICON_SRC}" ]; then
  cp "${ICON_SRC}" "${APPDIR}/redescan.png"
else
  printf '\x89PNG\r\n\x1a\n' > "${APPDIR}/redescan.png"
fi

# Baixa o appimagetool (empacotador oficial) se não estiver no PATH.
TOOL="$(command -v appimagetool || true)"
if [ -z "${TOOL}" ]; then
  echo ">> Baixando appimagetool…"
  TOOL="$(mktemp -d)/appimagetool"
  curl -fsSL -o "${TOOL}" \
    "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
  chmod +x "${TOOL}"
fi

echo ">> Gerando ${OUT}"
# ARCH é exigido pelo appimagetool. --appimage-extract-and-run evita precisar de FUSE no CI.
ARCH=x86_64 "${TOOL}" --appimage-extract-and-run "${APPDIR}" "${OUT}"

echo ">> AppImage pronto: ${OUT}"
ls -lh "${OUT}"
