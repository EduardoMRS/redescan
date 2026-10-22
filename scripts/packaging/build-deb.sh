#!/usr/bin/env bash
#
# Monta um pacote .deb (Debian/Ubuntu) a partir da pasta publicada do RedeScan Full.
# Instala o app em /opt/redescan e cria um atalho no menu + link em /usr/bin.
#
# Uso:
#   scripts/packaging/build-deb.sh <pasta-publicada> <versao> [saida.deb]
# Ex.:
#   scripts/packaging/build-deb.sh ./full/linux-x64 1.0.0 RedeScan-Full-Linux-x64.deb
#
set -euo pipefail

SRC="${1:?informe a pasta publicada}"
VERSION="${2:?informe a versao (ex.: 1.0.0)}"
OUT="${3:-RedeScan-${VERSION}-amd64.deb}"

PKGROOT="$(mktemp -d)/redescan-deb"
INSTALL_DIR="${PKGROOT}/opt/redescan"

echo ">> Montando árvore do pacote em ${PKGROOT}"
mkdir -p "${INSTALL_DIR}" "${PKGROOT}/DEBIAN" \
         "${PKGROOT}/usr/bin" "${PKGROOT}/usr/share/applications" \
         "${PKGROOT}/usr/share/icons/hicolor/256x256/apps"

cp -r "${SRC}/." "${INSTALL_DIR}/"
chmod +x "${INSTALL_DIR}/RedeScan"

# Ícone oficial do RedeScan.
if [ -f "src/Assets/icons/redescan-256.png" ]; then
  cp "src/Assets/icons/redescan-256.png" \
     "${PKGROOT}/usr/share/icons/hicolor/256x256/apps/redescan.png"
fi

# Link no PATH.
ln -sf /opt/redescan/RedeScan "${PKGROOT}/usr/bin/redescan"

# Atalho no menu.
cat > "${PKGROOT}/usr/share/applications/redescan.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=RedeScan
Comment=Análise e teste de redes
Exec=/opt/redescan/RedeScan
Icon=redescan
Categories=Network;Utility;
Terminal=false
EOF

# Metadados do pacote. libvlc5 é recomendado para o vídeo RTSP funcionar.
INSTALLED_KB="$(du -sk "${INSTALL_DIR}" | cut -f1)"
cat > "${PKGROOT}/DEBIAN/control" <<EOF
Package: redescan
Version: ${VERSION}
Section: net
Priority: optional
Architecture: amd64
Maintainer: RedeScan <deveduardomrs.pro>
Installed-Size: ${INSTALLED_KB}
Recommends: vlc, libvlc5
Description: RedeScan - análise e teste de redes
 Ferramenta multiplataforma de diagnóstico de redes: scanner, Wi-Fi,
 câmeras RTSP, navegador integrado e terminal SSH.
EOF

echo ">> Gerando ${OUT}"
dpkg-deb --build --root-owner-group "${PKGROOT}" "${OUT}"
echo ">> Pacote .deb pronto:"
ls -lh "${OUT}"
