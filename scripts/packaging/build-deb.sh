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

# Metadados do pacote. As dependências são baixadas pelo apt na instalação:
#  - WebKitGTK + GTK3: navegador embutido (motor web do sistema)
#  - ffmpeg: vídeo das câmeras RTSP e prévias
#  - iproute2 (ss): monitor de acessos
# Instale com "sudo apt install ./RedeScan-Full-Linux-x64.deb" (o apt resolve tudo).
# As alternativas cobrem Ubuntu 22.04 (nomes antigos) e 24.04+ (transição t64).
INSTALLED_KB="$(du -sk "${INSTALL_DIR}" | cut -f1)"
cat > "${PKGROOT}/DEBIAN/control" <<EOF
Package: redescan
Version: ${VERSION}
Section: net
Priority: optional
Architecture: amd64
Maintainer: RedeScan <deveduardomrs.pro>
Installed-Size: ${INSTALLED_KB}
Depends: libwebkit2gtk-4.1-0 | libwebkit2gtk-4.0-37, libgtk-3-0t64 | libgtk-3-0, ffmpeg, iproute2, libicu74 | libicu72 | libicu70 | libicu76
Recommends: network-manager, openssh-client, telnet
Description: RedeScan - análise e teste de redes
 Ferramenta multiplataforma de diagnóstico de redes: scanner, Wi-Fi,
 câmeras RTSP, navegador integrado e terminal SSH.
EOF

# Pós-instalação: bit de execução, pasta de logs gravável e dica de uso.
printf '%s\n' \
  '#!/bin/sh' \
  'set -e' \
  'chmod +x /opt/redescan/RedeScan || true' \
  'mkdir -p /opt/redescan/logs && chmod 1777 /opt/redescan/logs || true' \
  'echo "RedeScan instalado. Para iniciar com log no terminal: redescan --verbose"' \
  'exit 0' > "${PKGROOT}/DEBIAN/postinst"
chmod 0755 "${PKGROOT}/DEBIAN/postinst"

echo ">> Gerando ${OUT}"
dpkg-deb --build --root-owner-group "${PKGROOT}" "${OUT}"
echo ">> Pacote .deb pronto:"
ls -lh "${OUT}"
