# RedeScan

Ferramenta multiplataforma de **análise, diagnóstico e teste de redes**, construída em
[Avalonia UI](https://avaloniaui.net/) sobre **.NET 8**. É a evolução do antigo
*WEBTester* (Windows Forms) para uma arquitetura limpa, MVVM e multiplataforma
(Windows, Linux e Android).

> Projeto open source. Sem credenciais em código: toda configuração sensível
> (câmeras, atalhos) fica no perfil do usuário, nunca no repositório.

---

## ✨ Recursos

- **Dashboard** — IP local, gateway, sub-rede e interfaces ativas.
- **Scanner de rede** — varredura da sub-rede (ping sweep) com hostname, latência e
  fabricante por OUI; scan de portas TCP conhecidas. Menu de contexto com ações rápidas.
- **Diagnóstico** — ping contínuo (histórico, média, % de perda) com gráfico de latência,
  traceroute e simulador de carga/estresse (UDP/HTTP/TCP) com métricas em tempo real.
- **Wi-Fi** — lista de redes (sinal, RSSI, canal, banda, segurança) e gráfico de ocupação
  de canais 2.4/5 GHz.
- **Câmeras RTSP** — cadastro em grade (`UniformGrid`), credenciais protegidas; render
  inline com LibVLC (opcional) e abertura em player externo.
- **Navegador integrado** — acesso rápido a interfaces web de roteadores/ferramentas
  (CEF opcional; fallback para o navegador do sistema).
- **Atalhos** — roteadores, ONUs e links úteis, abertos no navegador interno ou do sistema.

---

## 🏗️ Arquitetura

```
RedeScan/
├── src/
│   ├── RedeScan.Core/      # Biblioteca .NET 8 — serviços de rede, modelos, DI (sem UI)
│   ├── RedeScan.UI/        # Avalonia — Views + ViewModels (MVVM), composição de DI
│   ├── RedeScan.Desktop/   # Executável Windows/Linux (entry point desktop)
│   └── RedeScan.Android/   # App Android (net8.0-android) — gera o APK
├── tests/
│   └── RedeScan.Core.Tests # Testes unitários (xUnit)
├── scripts/                # Scripts de empacotamento (mídia nativa, Android)
└── RedeScan.sln            # Solução (Core + UI + Desktop + Tests; Android fica à parte)
```

- **Core** não conhece a UI: expõe serviços (`INetworkScannerService`, `IPingService`,
  `IPortScannerService`, `ILoadTestService`, `IWifiScanner`, `IConfigurationService`, …)
  registrados via `AddRedeScanCore()`.
- **UI** reúne os ViewModels e Views, compartilhados por Desktop e Android através de
  `Composition.BuildServiceProvider()` e de um `MainView` reutilizável.
- Padrão MVVM com [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet).

---

## 🚀 Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Para **Android**: workload `android` (`dotnet workload install android`),
  **JDK 17+** e o **Android SDK** (API 34).
- Para **mídia nativa** (navegador CEF + vídeo LibVLC): veja a seção abaixo.

---

## ▶️ Executar no desktop (Windows/Linux)

```bash
# Restaurar, compilar e testar
dotnet build RedeScan.sln -c Release
dotnet test RedeScan.sln -c Release

# Rodar o app desktop
dotnet run --project src/RedeScan.Desktop/RedeScan.Desktop.csproj
```

### Gerar o binário autocontido (single-file)

```bash
dotnet publish src/RedeScan.Desktop/RedeScan.Desktop.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish/linux-x64
```

Troque `linux-x64` por `win-x64` para Windows. O resultado é um único executável
`RedeScan` (não exige .NET instalado na máquina alvo).

---

## 🎥🌐 Mídia nativa opcional (navegador embutido + vídeo RTSP)

O navegador embutido (CEF) e o vídeo RTSP inline (LibVLC) são **desligados por padrão**
— isso mantém o build/publish padrão leve e sem dependências nativas. Habilite com a
propriedade MSBuild `EnableNativeMedia`:

```bash
# Build de distribuição com mídia nativa (NÃO single-file — o CEF exige arquivos soltos)
scripts/package-native.sh linux-x64     # ou win-x64
```

- O runtime do CEF é empacotado via NuGet em todas as plataformas.
- O `libvlc` vem via NuGet no Windows/macOS. **No Linux é dependência do sistema**:
  `sudo apt-get install -y vlc libvlc-dev`.
- Sem mídia nativa, o navegador oferece "abrir no sistema" e as câmeras usam "player externo".

---

## 🤖 Build Android (APK)

O projeto Android fica **fora da `RedeScan.sln`** de propósito, para que o build/CI padrão
não exija o workload `android`.

```bash
# Pré-requisitos: workload android, JDK 17+, Android SDK (API 34) em $ANDROID_HOME
ANDROID_HOME=~/Android/Sdk scripts/build-android.sh
```

Gera `dist/android/com.redescan.app-Signed.apk` (debug-signed, para testes). Instalar:

```bash
$ANDROID_HOME/platform-tools/adb install -r dist/android/com.redescan.app-Signed.apk
```

> O APK atual é assinado com a chave de debug (teste). Uma release de produção na loja
> exigiria uma keystore própria (não versionada).

---

## ⚙️ Configuração

Na primeira execução, o RedeScan cria um arquivo de configuração no perfil do usuário:

- **Linux/macOS:** `~/.config/redescan/config.json`
- **Windows:** `%APPDATA%\redescan\config.json`

Lá ficam as câmeras RTSP e os atalhos. **As credenciais das câmeras são gravadas apenas
nesse arquivo local** e injetadas na URL só em tempo de execução
(`RtspCameraConfig.GetAuthenticatedUrl()`) — nunca ficam em código nem no repositório.

---

## 🔁 Integração contínua e releases

O workflow `.github/workflows/desktop.yml`:

- **Em push na `main`/PR:** compila, roda os testes e publica os binários como *artifacts*
  da execução.
- **Ao publicar uma tag `v*` (ex.: `v1.0.0`):** cria automaticamente uma **GitHub Release**
  com os binários anexados: `RedeScan-Linux-x86_64.AppImage` (instalador universal Linux),
  `RedeScan-Windows-x64.exe` (executável único) e `RedeScan-Android.apk`. Veja [RELEASING.md](RELEASING.md).

---

## 🧪 Testes

```bash
dotnet test RedeScan.sln -c Release
```

Cobrem configuração, scanner de portas, simulador de carga, cancelamento, interfaces de
rede e os parsers de Wi-Fi.

---

## 📄 Licença

Open source. Consulte o arquivo [LICENSE](LICENSE).
