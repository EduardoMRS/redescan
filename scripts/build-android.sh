#!/usr/bin/env bash
#
# Gera o APK de teste do RedeScan para Android.
#
# Pré-requisitos (uma vez):
#   - .NET 8 SDK + workload android:   dotnet workload install android
#   - JDK 17+ (ex.: OpenJDK 21)
#   - Android SDK (cmdline-tools + platform-tools + platforms;android-34 + build-tools;34.0.0)
#     Defina ANDROID_HOME apontando para a raiz do SDK.
#
# Uso:
#   ANDROID_HOME=~/Android/Sdk scripts/build-android.sh
#
set -euo pipefail

ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
PROJECT="src/RedeScan.Android/RedeScan.Android.csproj"
OUT="dist/android"

if [ ! -d "${ANDROID_HOME}" ]; then
  echo "!! Android SDK não encontrado em ${ANDROID_HOME}. Defina ANDROID_HOME." >&2
  exit 1
fi

echo ">> Publicando APK (Release) com ANDROID_HOME=${ANDROID_HOME}"
dotnet publish "${PROJECT}" \
  --configuration Release \
  -p:AndroidSdkDirectory="${ANDROID_HOME}" \
  --output "${OUT}"

echo ">> APKs gerados:"
find "${OUT}" -name '*.apk' -exec ls -lh {} \;

echo ">> Para instalar em um dispositivo/emulador conectado:"
echo "     ${ANDROID_HOME}/platform-tools/adb install -r ${OUT}/com.redescan.app-Signed.apk"
