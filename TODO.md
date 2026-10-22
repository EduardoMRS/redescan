# 📋 RedeScan - Planejamento & Checklist de Restruturação

Este documento detalha o plano de modernização e reestruturação completa do **RedeScan** (antigo *WEBTester*), migrando da base legada em Windows Forms (.NET Framework 4.6.1) para **Avalonia UI** (.NET 8/9), com arquitetura limpa, multiplataforma e novos módulos de diagnóstico e monitoramento.

---

## 🧭 Visão Geral & Arquitetura Alvo

```
RedeScan/
├── src/
│   ├── RedeScan.Core/                  # Biblioteca de Classes (.NET Standard / .NET 8+)
│   │   ├── Models/                     # DTOs, Entidades de Rede, Configuração de Câmeras
│   │   ├── Services/                   # Lógica assíncrona pura (Ping, Scan, PortScan, DDoS, RTSP, Tracert)
│   │   ├── Interfaces/                 # Abstrações de SO (ex: IWifiScanner, IPlatformService)
│   │   └── ViewModels/                 # MVVM ViewModels (CommunityToolkit.Mvvm)
│   │
│   ├── RedeScan.UI/                    # Componentes visuais compartilhados (XAML)
│   │   ├── Views/                      # Telas (Dashboard, Câmeras RTSP, Scanners, DDoS, Navegador)
│   │   ├── Controls/                   # Controles customizados (Grid de Câmeras, Gráficos de Sinal)
│   │   ├── Styles/                     # Temas Dark/Light e estilos visuais
│   │   └── App.axaml                   # Recursos globais da aplicação
│   │
│   └── RedeScan.Desktop/               # Executável para Linux e Windows
│       ├── Platforms/                  # Implementações específicas (ex: LinuxWifiScanner, WindowsWifiScanner)
│       └── Program.cs                  # Ponto de entrada Desktop
```

---

## 🚀 Fases de Implementação

### 📌 Fase 0: Preparação do Ambiente e Inicialização do Projeto
- [x] Instalar o SDK do **.NET 8** no ambiente de usuário (`~/.dotnet`).
- [x] Instalar templates do Avalonia UI (`dotnet new install Avalonia.Templates`).
- [x] Criar a nova estrutura de solução e projeto Core (`RedeScan.sln`, `src/RedeScan.Core`, `tests/RedeScan.Core.Tests`).
- [x] Criar projetos de UI e Runner (`RedeScan.UI`, `RedeScan.Desktop`).
- [ ] Configurar pacotes essenciais:
  - [x] `Avalonia` e `Avalonia.Themes.Fluent` (v11.2.3)
  - [x] `CommunityToolkit.Mvvm` (padrão MVVM com Source Generators)
  - [x] `Avalonia.Controls.DataGrid` + `Avalonia.Fonts.Inter`
  - [x] Navegador integrado via CEF (`WebViewControl-Avalonia`) atrás da flag `EnableNativeMedia`; traz os runtimes do CEF (win/linux/macOS) por NuGet.
  - [x] `LibVLCSharp.Avalonia` + `VideoLAN.LibVLC.Windows/Mac` (reprodução de stream RTSP) atrás da flag `EnableNativeMedia`; no Linux o `libvlc` é dependência do SO.
  - [x] `Microsoft.Extensions.DependencyInjection` (injeção de dependência)
  - [x] `System.Text.Json` (gerenciamento de perfis e configurações)

---

### 📌 Fase 1: Camada Core & Serviços de Rede (Back-End)
- [x] **Configurações & Segurança (Fim do Hardcode!):**
  - [x] Implementar serviço de configuração em JSON (`ConfigurationService` em `~/.config/redescan/config.json`).
  - [x] Modelar lista de câmeras RTSP (`RtspCameraConfig` com injeção segura de credenciais).
  - [x] Modelar lista dinâmica de atalhos e roteadores comuns (`NetworkShortcut`, `AppSettings.CreateDefault()`).
- [x] **Informações de Rede Local:**
  - [x] Leitor de IP local, máscara e Gateway padrão multiplataforma (`NetworkInterfaceService`).
- [x] **Scanner de IPs na Sub-rede (Ping Sweep):**
  - [x] Varredura assíncrona e concorrente (`Parallel.ForEachAsync`) do range `.1` a `.254` (`NetworkScannerService`).
  - [x] Resolução reversa de Hostname e tempo de resposta.
- [x] **Scanner de Portas TCP:**
  - [x] Verificador de portas usando `TcpClient.ConnectAsync` com timeout configurável (`PortScannerService`).
  - [x] Dicionário de portas conhecidas (21 FTP, 22 SSH, 80 HTTP, 443 HTTPS, 554 RTSP, 8080, etc.).
- [x] **Diagnóstico: Ping Contínuo & Traceroute:**
  - [x] Serviço de Ping assíncrono com histórico de latência e perda de pacotes (`PingService`).
  - [x] Traceroute via incremento progressivo de `PingOptions.Ttl`.
- [x] **Simulador de Teste de Carga / Estresse (DDoS Simulator):**
  - [x] Motor assíncrono de rajadas configuráveis com UDP, HTTP e TCP (`LoadTestService`).
  - [x] Parâmetros de segurança: limite de taxa, tempo máximo de execução e botão de cancelamento imediato (`CancellationToken`).
  - [x] Métricas em tempo real: pacotes/s, Mbps, requisições com sucesso/erro, vazão estimada.
- [x] **Suíte de Testes Automatizados (xUnit):**
  - [x] 7 testes unitários cobrindo configuração, scanner de portas, simulação de estresse, cancelamento e interfaces de rede aprovados com 100% de sucesso.

---

### 📌 Fase 2: Camada Específica de Plataforma (Wi-Fi Scanner)
- [x] Definir modelo `WifiNetworkInfo` (SSID, BSSID, RSSI dBm, Percentual, Canais, Frequência, Bandas 2.4/5/6 GHz e Segurança).
- [x] Definir a interface `IWifiScanner` (`IsSupportedAsync`, `GetAvailableNetworksAsync`).
- [x] **Implementação Linux (`LinuxWifiScanner` & `LinuxNmcliWifiParser`):**
  - [x] Parser robusto com suporte a caracteres escapados (`\:` no BSSID) e detecção de status conectado.
  - [x] Consulta via `nmcli dev wifi list` com suporte a rescan.
- [x] **Implementação Windows (`WindowsWifiScanner` & `WindowsNetshWifiParser`):**
  - [x] Parser multilíngue (PT/EN) para saída do comando nativo `netsh wlan show networks mode=bssid`.
- [x] **Provedor Dinâmico (`PlatformWifiScanner`):**
  - [x] Detecção em runtime do sistema operacional e fallback seguro (`NullWifiScanner`) para máquinas sem adaptador Wi-Fi.
- [x] Registro via Injeção de Dependência no container (`ServiceCollectionExtensions`).
- [x] **Suíte de Testes da Fase 2:**
  - [x] 4 novos testes unitários adicionados (totalizando 11 testes aprovados com 100% de sucesso).

---

### 📌 Fase 3: Camada de Apresentação (Avalonia UI & MVVM) — 🔄 Em andamento
- [x] **Layout Principal & Navegação:**
  - [x] Menu lateral (Sidebar Navigation) com ícones, dirigido por `MainWindowViewModel` + `ViewLocator`.
  - [x] Suporte a Tema Escuro (Dark) e Claro (Light) com alternância em runtime.
- [x] **Módulo 1: Visualizador em Grid de Câmeras RTSP:**
  - [ ] Integração com `LibVLCSharp.Avalonia` (render do stream) — *pendente; grade e placeholder prontos.*
  - [x] Layout com `UniformGrid` dinâmico (colunas calculadas conforme a contagem de câmeras ativas).
  - [x] Adicionar, editar e remover câmeras salvas no JSON (credenciais via `RtspCameraConfig`, sem hardcode).
- [x] **Módulo 2: Scanner de Rede Local & Portas:**
  - [x] `DataGrid` interativo com status online, latência e seleção de host; resultados ao vivo via `IProgress`.
  - [x] Scan de portas conhecidas do host selecionado, cancelável.
  - [x] Lookup de fabricante (OUI) via tabela ARP do SO (`ArpService` + `OuiVendorLookup`), exibido na grade.
  - [x] Menu de contexto (botão direito) com ações rápidas: escanear portas, ping, abrir HTTP/HTTPS (interno/sistema), copiar IP; duplo-clique escaneia portas.
- [x] **Módulo 3: Ferramentas de Diagnóstico (Ping, Tracert, Estresse):**
  - [x] Ping contínuo com histórico, média de latência e % de perda; traceroute ao vivo.
  - [x] Painel do Simulador de Carga com controles de segurança e contadores em tempo real.
  - [x] Gráfico de variação de latência ao longo do tempo (`SparklineControl`, sem dependências externas).
- [x] **Módulo 4: Scanner de Espectro / Canais Wi-Fi:**
  - [x] Listagem das redes com sinal %, RSSI dBm, canal, banda e segurança.
  - [x] Gráfico de colunas de ocupação por canal (2.4/5 GHz) para visualizar sobreposição.
- [x] **Módulo 5: Navegador / Atalhos de Rede:**
  - [x] Atalhos de roteadores e ferramentas web persistidos em JSON, abertos no navegador do sistema (multiplataforma).
  - [x] Navegador embutido (CEF via `WebViewControl-Avalonia`) atrás da flag `EnableNativeMedia`, com barra de endereço e fallback "abrir no sistema".
- [x] **Menu de contexto (botão direito):**
  - [x] Scanner de rede: escanear portas, abrir HTTP no navegador interno, abrir HTTPS no sistema, copiar IP.
  - [x] Atalhos: abrir no navegador interno ou no sistema.
  - [x] Câmeras: abrir no player externo (VLC/SO) e editar.
- [x] **Render RTSP na grade (opcional):**
  - [x] Pré-visualização inline com `LibVLCSharp.Avalonia` (`VideoView` + `MediaPlayer`, play/stop) atrás da flag `EnableNativeMedia`; placeholder + "player externo" sempre disponíveis.
  - [x] Liberação de recursos nativos (`Dispose` do `MediaPlayer`/engine) ao sair da tela.

> **Nota:** a solução inteira compila com 0 erros, os 11 testes continuam passando e o `RedeScan.Desktop` publica como executável self-contained em `linux-x64` e `win-x64`.
>
> **Mídia nativa opcional:** navegador CEF e vídeo LibVLC ficam atrás da propriedade MSBuild `EnableNativeMedia` (desligada por padrão). Build normal (CI) permanece leve e sem dependências nativas.
>
> **Empacotamento da build nativa:** use `scripts/package-native.sh <rid>` (ex.: `linux-x64`, `win-x64`). A build nativa NÃO é single-file (o CEF exige arquivos soltos). O runtime do CEF (`libcef`, `.pak`, locales, processo auxiliar) é empacotado via NuGet em todas as plataformas. O `libvlc` vem por NuGet no Windows/macOS; no Linux é dependência do SO (`apt-get install vlc libvlc-dev`). O job de CI `package-native-media` gera esses pacotes por plataforma. O render inline degrada com elegância (mensagem de status) quando o `libvlc` está ausente, mantendo o "player externo" sempre disponível.
>
> **Open source / segurança:** removida a assinatura de assembly/manifest e o `.pfx` do projeto legado; nenhum atributo proprietário de copyright/trademark permanece. Não há strings de conexão de banco de dados em nenhum projeto. O `.gitignore` agora ignora `*.pfx`/`*.snk`/`*.key`, `.env`, `secrets.json`, `config.json` e o diretório interno `.dd/`.

---

### 📌 Fase 3.5: Build Android (APK de teste) — ✅ Concluída
- [x] Refatorar o shell em `MainView` (UserControl) reutilizável; `MainWindow` apenas o hospeda (suporte a single-view).
- [x] Raiz de composição compartilhada (`Composition.BuildServiceProvider`) usada por Desktop e Android.
- [x] `App` trata tanto `IClassicDesktopStyleApplicationLifetime` quanto `ISingleViewApplicationLifetime`.
- [x] Projeto `src/RedeScan.Android` (`net8.0-android`, Avalonia.Android 11.2.3, `MainActivity`, manifest com permissões de rede/Wi-Fi, ícone e tema).
- [x] APK de teste gerado e validado (`com.redescan.app-Signed.apk`, minSdk 21, ~45 MB após restringir RIDs a arm64/x64).
- [x] Script `scripts/build-android.sh` e job de CI `build-android-apk` (fora da `RedeScan.sln` para não exigir o workload android no build principal).

> **Nota Android:** projeto deliberadamente fora da `RedeScan.sln` — `net8.0-android` exige o workload `android`, que o build/CI padrão não tem. Build via `scripts/build-android.sh` (requer JDK 17+, workload android e Android SDK API 34).

---

### 📌 Fase 4: Limpeza, Testes e Polimento — ✅ Concluída
- [x] Validação multiplataforma: workflow compila/testa e publica Desktop (win/linux) + APK Android; build local verde (0 erros, 11/11 testes).
- [x] Garantir que nenhum arquivo temporário, senha ou token seja persistido no repositório (varredura confirmou: sem credenciais/segredos; `config.json` fica no perfil do usuário).
- [x] Documentação completa no `README.md` (visão geral, arquitetura, build/execução por plataforma, configuração) + `LICENSE` (MIT) + `RELEASING.md`.
- [x] **Deploy real:** workflow publica GitHub Release ao enviar uma tag `v*` (binários desktop zipados + APK anexados).

> **Correção de CI (Disk quota exceeded no Android):** o runner (Ubuntu 26.04) já traz o .NET 10; `dotnet workload install android` pegava o manifesto do .NET 10 e baixava packs net8+net9+net10 até estourar o disco. Resolvido com `global.json` fixando o SDK na banda 8.0.4xx (`rollForward: latestFeature`) + `--skip-manifest-update`, e restringindo os RIDs do Android a `android-arm64;android-x64`.

---

### 📌 Fase 5: Rede avançada e scan aprofundado — 🔄 Em andamento
- [x] **Painel — atalhos de dispositivos web:** `WebDeviceScanner` acha hosts com interface web em qualquer porta; auto-descoberta periódica (Wi-Fi/cabo) e busca de roteadores no 1º acesso.
- [x] **Configuração de rede:** IP estático/DHCP, DNS e PPPoE via `nmcli`/`netsh`, com pré-visualização do comando antes de aplicar (operações sensíveis, exigem privilégio).
- [x] **Scan aprofundado (`HostInspector`):** banner grabbing para identificar o serviço real (não só a porta), estimativa de SO por TTL+banners, nome (DNS), MAC e fabricante (OUI).
- [x] **Wake-on-LAN (`WakeOnLanService`):** envia magic packet UDP para ligar dispositivos pelo MAC.
- [ ] **Uptime remoto:** inviável sem acesso ao dispositivo (SNMP/SSH com credencial) — deixado de fora para não exibir dado incorreto.
- [x] **Deploy manual + validação automática:** dois workflows — `ci.yml` (push/PR: deps + compilação com `-warnaserror` + testes) e `release.yml` (manual/tag: builda tudo e publica a Release).
- [x] **Full em todas as plataformas:** Windows, Linux Ubuntu (apt) e Linux Fedora (container dnf) com CEF+LibVLC; portátil (AppImage/exe) + APK Android. *Android usa "abrir no sistema" para web (WebView nativo embutido = item futuro).*

> **Honestidade sobre limites:** SO é *estimativa* (TTL/banner), não fingerprinting real (exigiria root/libpcap). Nome depende de o dispositivo responder (DNS/NetBIOS). "Full para Android" usará o WebView nativo do Android (o CEF é desktop-only).
