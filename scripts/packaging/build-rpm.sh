#!/usr/bin/env bash
#
# Monta um pacote .rpm (Fedora/RHEL) a partir da pasta publicada do RedeScan Full.
# Instala em /opt/redescan + atalho no menu + link em /usr/bin.
#
# Uso:
#   scripts/packaging/build-rpm.sh <pasta-publicada> <versao> [saida.rpm]
# Requer: rpmbuild (pacote rpm-build no Fedora).
#
set -euo pipefail

SRC="${1:?informe a pasta publicada}"
VERSION="${2:?informe a versao (ex.: 1.0.0)}"
OUT="${3:-RedeScan-${VERSION}-x86_64.rpm}"

TOP="$(mktemp -d)/rpmbuild"
BUILDROOT="${TOP}/BUILDROOT/redescan-${VERSION}"
mkdir -p "${TOP}"/{SPECS,RPMS} \
         "${BUILDROOT}/opt/redescan" \
         "${BUILDROOT}/usr/bin" \
         "${BUILDROOT}/usr/share/applications" \
         "${BUILDROOT}/usr/share/icons/hicolor/256x256/apps"

cp -r "${SRC}/." "${BUILDROOT}/opt/redescan/"
chmod +x "${BUILDROOT}/opt/redescan/RedeScan"
ln -sf /opt/redescan/RedeScan "${BUILDROOT}/usr/bin/redescan"

if [ -f "src/Assets/icons/redescan-256.png" ]; then
  cp "src/Assets/icons/redescan-256.png" \
     "${BUILDROOT}/usr/share/icons/hicolor/256x256/apps/redescan.png"
fi

cat > "${BUILDROOT}/usr/share/applications/redescan.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=RedeScan
Comment=Análise e teste de redes
Exec=/opt/redescan/RedeScan
Icon=redescan
Categories=Network;Utility;
Terminal=false
EOF

cat > "${TOP}/SPECS/redescan.spec" <<EOF
Name:           redescan
Version:        ${VERSION}
Release:        1
Summary:        RedeScan - análise e teste de redes
License:        MIT
BuildArch:      x86_64
Recommends:     vlc
AutoReqProv:    no

%description
Ferramenta multiplataforma de diagnóstico de redes: scanner, Wi-Fi,
câmeras RTSP, navegador integrado e terminal SSH.

%files
/opt/redescan
/usr/bin/redescan
/usr/share/applications/redescan.desktop
/usr/share/icons/hicolor/256x256/apps/redescan.png

%post
# Garante o bit de execução do binário principal.
chmod +x /opt/redescan/RedeScan || true
EOF

echo ">> Gerando RPM"
rpmbuild --define "_topdir ${TOP}" \
         --buildroot "${BUILDROOT}" \
         -bb "${TOP}/SPECS/redescan.spec"

FOUND="$(find "${TOP}/RPMS" -name '*.rpm' | head -n1)"
cp "${FOUND}" "${OUT}"
echo ">> Pacote .rpm pronto:"
ls -lh "${OUT}"
