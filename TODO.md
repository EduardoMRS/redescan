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
- [x] **Full em todas as plataformas:** Windows, Linux Ubuntu (apt) e Linux Fedora (container dnf) com CEF+LibVLC; portátil (AppImage/exe) + APK Android.
- [x] **Android — navegador embutido:** WebView nativo do Android (`android.webkit.WebView`) embutido via `NativeControlHost`/`AndroidViewControlHandle`, com seam `INativeWebView` + `App.NativeWebViewFactory` (registrada no `MainActivity`). O desktop usa CEF; o Android usa o WebView do sistema — ambos atrás da mesma abstração.
- [x] **RTSP compatível com o CentralMonitor:** `RtspCameraConfig` alinhado (Name/Url/Location/Group); `RtspListImporter` importa/exporta `rtspList.json` (`{ "Streams": [...] }`); grade agrupada por grupo (host); botões Importar/Exportar.
- [x] **Acesso remoto (SSH/Telnet/Winbox):** `RemoteAccessService` abre o cliente do SO — SSH/Telnet num terminal detectado por plataforma; Winbox por IP e por MAC (nativo no Windows, via Wine no Linux). Menu de contexto "Acesso remoto" no scanner. Degradação graciosa quando o cliente não existe.
- [x] **Terminal SSH embutido (todas as plataformas, incl. Android):** módulo `Terminal SSH` com `SshTerminalSession`/`ShellStream` (SSH.NET 2026.x, sem as CVEs das 2024.x). Shell de linha/stream (não é VT100 completo) para CLI de roteadores/switches. No scanner, "SSH (terminal embutido)" abre o terminal já apontando para o host. É a forma de ter terminal no Android, onde não há cliente de sistema para lançar.
- [x] **Atualização automática:** versão central em `Directory.Build.props`; `UpdateService` checa `latest.json` remoto (`https://deveduardomrs.pro/d/redescan/release/latest.json`), compara versões e escolhe o download da plataforma; banner de aviso no Painel. O `release.yml` gera o `latest.json` automaticamente (versão da tag + links dos assets) e o anexa à Release / artefato `latest-json`.

> **Honestidade sobre limites:** SO é *estimativa* (TTL/banner), não fingerprinting real (exigiria root/libpcap). Nome depende de o dispositivo responder (DNS/NetBIOS). "Full para Android" usará o WebView nativo do Android (o CEF é desktop-only).

---
### 🩹 v1.0.1 — correções de runtime (Linux .deb)
- [x] **Ping sem privilégio:** `PingService` não envia mais buffer de payload custom no Linux/macOS (evita o socket ICMP RAW / `cap_net_raw`). Usa o overload sem buffer; Windows mantém o buffer. Corrige também o traceroute.
- [x] **Scanner de rede vazio:** resolvido junto com o ping (o sweep usa o `PingService`).
- [x] **App não fechava / "travou":** fechar a janela encerra o app (`ShutdownMode.OnMainWindowClose`); o recolhimento para a bandeja passou a ser no **minimizar** — some o risco de processo invisível em Linux sem tray.
- [x] **Navegador travava sem erro:** CEF embutido só no Windows; no Linux usa o fallback "abrir no sistema".

---
### 🔎📷 Scanner reaproveitando o scan rápido + grade de câmeras reformulada
- [x] **Scanner usa o mesmo mecanismo do painel, com mais dados:** o `NetworkScannerService` ganhou sonda de portas web rápida (mesmas portas do painel, em paralelo, sem banner grabbing), ativada por `probeWebPorts`. O `NetworkHost` agora carrega `WebPorts`/`IsLikelyRouter`/`HasWeb`/`PreferredWebUrl`; o scanner liga `probeWebPorts: true` e mostra a coluna "Web" ao vivo. A inspeção pesada (SO/banners) continua sob demanda.
- [x] **Câmeras — tabela à esquerda com busca e filtro:** `ListBox` de câmeras (nome + local + miniatura de prévia), com `SearchText` (nome/local) e `GroupFilter` (combo de grupos). `FilteredCameras` aplica os dois.
- [x] **Câmeras — grade de vídeo 1 a 6 em tempo real:** seletor de layout (1–6); `ActiveSlots` + `CameraSlot` por posição; cada slot ganha seu próprio `MediaPlayer`/`VideoView` LibVLC (sob `ENABLE_NATIVE_MEDIA`), com parar por slot e parar tudo. `GridColumns` ajusta as colunas (1→1, 2→2, 3→3, 4→2, 5/6→3).
- [x] **Câmeras — editor/criador em modal:** `CameraEditorWindow` + `CameraEditorViewModel`, abertos via `ICameraEditorDialog.EditAsync` (ShowDialog sobre a janela principal; no Android devolve a câmera sem modal). Substitui o editor inline.

---
### 🔌 Scan de portas completo + 🧬 VLAN na config de rede
- [x] **Scan de portas — modo completo:** além das ~28 portas conhecidas (rápido), a aba "Portas" do scanner ganhou a opção "Varrer todas as portas (1–65535)". No modo completo usa timeout curto (300ms) e concorrência alta (600) para compensar o volume; mantém o nome do serviço nas portas conhecidas.
- [x] **VLAN 802.1Q na configuração de rede:** nova seção "VLAN" com interface física, ID (1–4094), nome opcional (padrão interface.ID), e IP DHCP/estático (CIDR/gateway/DNS). No Linux cria via `nmcli con add type vlan` + `con mod` + `con up`; no Windows mostra orientação (netsh não cria VLAN). Mantém o padrão preview/apply (nunca aplica sem confirmação). Model `VlanRequest`, método `ConfigureVlanAsync`.

---
### ⚙️ v1.0.2 — Configurações, tela inicial e atalhos do navegador
- [x] **Definir tela inicial pelo menu:** botão direito em qualquer item do menu lateral → "Definir como tela inicial" (ex.: abrir direto nas Câmeras). Persiste em `StartupPageKey`.
- [x] **Menu de Configurações:** novo módulo com tela inicial, tema (Dark/Light), módulos visíveis (ocultar recursos não usados — "Configurações" nunca some), página inicial do navegador embutido ("router" = gateway da rede, ou uma URL leve) e range padrão do scan de portas. Persiste em `AppSettings`.
- [x] **Módulos habilitáveis:** `EnabledModules` filtra o menu lateral; aplicado ao salvar e no startup.
- [x] **Range de portas configurável:** o scanner usa `PortScanStart/End` das preferências como range padrão (ou "todas as portas"); a aba Portas mostra os campos De/até.
- [x] **Navegador — seletor de roteadores:** a barra de endereço ganhou um combo com os roteadores/dispositivos web descobertos (via `IWebDeviceRegistry`, alimentado pelo painel); escolher um navega direto para ele. Útil para alternar entre roteadores.
- [x] **Chave estável por módulo (`ViewModelBase.Key`)** para persistir preferências sem depender do título.

---
### 🐛 Correções na v1.0.2 (pós-publicação)
- [x] **App demorava demais / parecia não abrir (.deb e AppImage):** o `MainWindowViewModel` carregava as preferências no construtor com `LoadSettingsAsync().GetAwaiter().GetResult()` na UI thread durante o startup, prendendo a inicialização até o continuation async destravar. Adicionado `IConfigurationService.LoadSettings()` 100% síncrono (sem I/O async); o shell e o scanner passam a usá-lo. `OnFrameworkInitializationCompleted` agora tem try/catch que nunca deixa o processo vivo sem janela.
- [x] **Stream RTSP não exibia nada (sem erro):** a `Media` era descartada (`using var`) logo após `Play()`, antes do stream abrir — tela preta silenciosa. Agora a `Media` vive até o `StopNative`. Adicionadas opções `:rtsp-tcp` (força TCP; Wowza/NAT) e `:network-caching=1000`, e handler `EncounteredError` para dar feedback real de falha.
- [x] Testes de regressão para `LoadSettings()` síncrono (13 testes no total).

---
### 🛠️ v1.0.3 — correções do teste em Windows/Linux + monitor de acessos
- [x] **Câmeras RTSP:** player trocado do LibVLC (no Ubuntu/Mint o libvlc não traz RTSP/live555; no Windows a superfície nativa não aparecia dentro da grade) pelo **ffmpeg** como processo separado, renderizado num `WriteableBitmap` (`VideoSurface`). Reconecta sozinho alternando TCP/UDP, mostra erros claros (401/403/404/recusado/tempo esgotado) e nunca trava a UI. Miniaturas reais na lista. Botão "Instalar ffmpeg" baixa a versão oficial se faltar.
- [x] **Navegador no Linux:** o WebKitGTK reporta `SupportedScenarios = NativeDialog` (não desenha embutido — ficava preto). O app detecta isso e abre a página numa janela própria (`NativeWebDialog`) encaixada sobre a área do navegador, acompanhando mover/redimensionar/minimizar, controlada pela barra de endereço do app. Windows segue embutido (WebView2). Validado pelo usuário no Linux Mint.
- [x] **Navegador embutido:** CEF substituído pelo `Avalonia.Controls.WebView` (MIT) — WebView2 no Windows, WebKitGTK no Linux. Voltar/avançar/recarregar, links de nova janela abrem na mesma aba, detecção de motor ausente com instrução, e opção (desligada por padrão) de aceitar certificados autoassinados de roteadores.
- [x] **Terminal SSH:** comandos não chegavam ao servidor (o `ShellStream` só envia no `Flush`). Corrigido e validado contra um servidor SSH real. Enter capturado por handler tunnel, histórico (↑/↓), Tab, Ctrl+C, auto-rolagem e remoção de códigos ANSI.
- [x] **WinBox:** se não estiver instalado, baixa o WinBox 4 oficial (Windows/Linux, SHA-256) e abre já conectando no IP/MAC.
- [x] **Botão "Escanear portas":** ficava desabilitado (o comando não era notificado da seleção). Corrigido.
- [x] **Menu lateral:** removido "Alternar tema" (fica em Configurações); Configurações fixo no fim do menu.
- [x] **Monitor de acessos:** novo módulo — conexões e portas em escuta desta máquina com processo, sentido (entrada/saída), histórico de conexões novas, resumo "quem acessou" e exportação CSV (`ss` no Linux, `netstat -ano` no Windows, fallback .NET).
- [x] **Botões fora da tela:** barras de ferramentas viraram `WrapPanel.toolbar` (quebram linha em janelas estreitas).
- [x] **Instaladores Full baixam dependências:** .deb (`Depends`: WebKitGTK, GTK3, ffmpeg, iproute2), .rpm (`Requires`), setup.exe (WebView2 + ffmpeg via página de download do Inno). Validados em Ubuntu 22.04/24.04 e Fedora 40.
- [x] **Log de falhas:** `AppLog` grava em `logs/` na pasta do app (fallback no perfil), arquivo por usuário, `crash-*.log` em exceções não tratadas, espelhado no console (`redescan --verbose`, `sudo redescan`; no Windows anexa ao console do terminal).
- [ ] **Repaginação do layout (ícones/botões):** em andamento na branch `redesign/ui`.
