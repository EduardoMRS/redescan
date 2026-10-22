#!/usr/bin/env bash
#
# Empacota a build de mídia nativa do RedeScan (navegador CEF + vídeo LibVLC).
#
# Diferente do publish padrão (single-file, leve, usado pelo CI), esta build
# NÃO pode ser single-file: o CEF precisa de arquivos soltos (libcef, .pak,
# locales, processo auxiliar). O resultado é uma pasta autocontida.
#
# Uso:
#   scripts/package-native.sh <rid>
# Exemplos:
#   scripts/package-native.sh linux-x64
#   scripts/package-native.sh win-x64
#
# Dependências nativas por plataforma:
#   - Windows/macOS: libvlc vem via NuGet (VideoLAN.LibVLC.*). Nada a instalar.
#   - Linux: libvlc é dependência do sistema. Instale antes de executar o app:
#       Debian/Ubuntu:  sudo apt-get install -y vlc libvlc-dev
#       Fedora:         sudo dnf install -y vlc vlc-devel
#
set -euo pipefail

RID="${1:-linux-x64}"
PROJECT="src/RedeScan.Desktop/RedeScan.Desktop.csproj"
OUT="dist/native/${RID}"

echo ">> Publicando RedeScan (mídia nativa) para ${RID} em ${OUT}"

# PublishSingleFile=false é obrigatório por causa do CEF.
dotnet publish "${PROJECT}" \
  --configuration Release \
  --runtime "${RID}" \
  --self-contained true \
  -p:EnableNativeMedia=true \
  -p:PublishSingleFile=false \
  --output "${OUT}"

echo ">> Publish concluído."

# Verificação básica dos artefatos do CEF.
if find "${OUT}" -iname 'libcef*' -o -iname 'icudtl.dat' | grep -q .; then
  echo ">> OK: runtime do CEF presente na saída."
else
  echo "!! Aviso: não encontrei os binários do CEF na saída (${OUT})." >&2
fi

case "${RID}" in
  linux-*)
    echo ">> Lembrete (Linux): instale o libvlc do sistema para o vídeo RTSP funcionar:"
    echo "     sudo apt-get install -y vlc libvlc-dev"
    ;;
esac

echo ">> Pronto: ${OUT}"
