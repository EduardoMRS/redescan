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
- [ ] Instalar o SDK do **.NET 8 ou .NET 9** e templates do Avalonia UI (`dotnet new install Avalonia.Templates`).
- [ ] Criar a nova estrutura de solução e projetos (`RedeScan.Core`, `RedeScan.UI`, `RedeScan.Desktop`).
- [ ] Configurar pacotes essenciais:
  - `Avalonia` e `Avalonia.Themes.Fluent`
  - `CommunityToolkit.Mvvm` (padrão MVVM com Source Generators)
  - `Avalonia.WebView` (navegador multiplataforma integrado)
  - `LibVLCSharp.Avalonia` + `VideoLAN.LibVLC.*` (reprodução de stream RTSP)
  - `Microsoft.Extensions.DependencyInjection` (injeção de dependência)
  - `System.Text.Json` (gerenciamento de perfis e configurações)

---

### 📌 Fase 1: Camada Core & Serviços de Rede (Back-End)
- [ ] **Configurações & Segurança (Fim do Hardcode!):**
  - [ ] Implementar serviço de configuração em JSON (`appsettings.json` ou pasta do usuário `~/.config/redescan/`).
  - [ ] Modelar lista de câmeras RTSP (nome, grupo, URL rtsp, credenciais desacopladas).
  - [ ] Modelar lista dinâmica de atalhos e roteadores comuns (IPs, portas, rótulos).
- [ ] **Informações de Rede Local:**
  - [ ] Leitor de IP local e Gateway padrão multiplataforma usando `System.Net.NetworkInformation`.
- [ ] **Scanner de IPs na Sub-rede (Ping Sweep):**
  - [ ] Varredura assíncrona e concorrente (`Task.WhenAll` ou `Parallel.ForEachAsync`) do range `.1` a `.254`.
  - [ ] Resolução reversa de Hostname e tempo de resposta.
- [ ] **Scanner de Portas TCP:**
  - [ ] Verificador de portas usando `TcpClient.ConnectAsync` com timeout configurável.
  - [ ] Dicionário de portas conhecidas (21 FTP, 22 SSH, 80 HTTP, 443 HTTPS, 554 RTSP, 8080, etc.).
- [ ] **Diagnóstico: Ping Contínuo & Traceroute:**
  - [ ] Serviço de Ping assíncrono com histórico de latência e perda de pacotes.
  - [ ] Traceroute via incremento progressivo de `PingOptions.Ttl`.
- [ ] **Simulador de Teste de Carga / Estresse (DDoS Simulator):**
  - [ ] Motor assíncrono de rajadas configuráveis (UDP Flooder ou HTTP flood com `HttpClient`).
  - [ ] Parâmetros de segurança: limite de taxa, tempo máximo de execução e botão de cancelamento imediato (`CancellationToken`).
  - [ ] Métricas em tempo real: pacotes/s, requisições com sucesso/erro, vazão estimada.

---

### 📌 Fase 2: Camada Específica de Plataforma (Wi-Fi Scanner)
- [ ] Definir a interface `IWifiScanner`:
  ```csharp
  public interface IWifiScanner
  {
      Task<IReadOnlyList<WifiNetworkInfo>> GetAvailableNetworksAsync(CancellationToken ct = default);
  }
  ```
- [ ] **Implementação Linux (`LinuxWifiScanner`):**
  - [ ] Consulta via `nmcli -t -f SSID,BSSID,CHAN,FREQ,SIGNAL,SECURITY dev wifi` ou leitura de `/proc/net/wireless`.
- [ ] **Implementação Windows (`WindowsWifiScanner`):**
  - [ ] Consulta via API `wlanapi.dll` (Native Wifi API).
- [ ] Registro via Injeção de Dependência condicional à plataforma em tempo de execução.

---

### 📌 Fase 3: Camada de Apresentação (Avalonia UI & MVVM)
- [ ] **Layout Principal & Navegação:**
  - [ ] Menu lateral expansível (Sidebar Navigation) com ícones e transições fluidas.
  - [ ] Suporte nativo a Tema Escuro (Dark) e Claro (Light).
- [ ] **Módulo 1: Visualizador em Grid de Câmeras RTSP:**
  - [ ] Integração com `LibVLCSharp.Avalonia`.
  - [ ] Layout com `UniformGrid` dinâmico (1x1, 2x2, 3x3 ou automático conforme a contagem de câmeras ativas).
  - [ ] Diálogo/Modal para adicionar, editar e remover câmeras salvas no JSON.
- [ ] **Módulo 2: Scanner de Rede Local & Portas:**
  - [ ] Tabela interativa com filtros, status (Online/Offline), latência e fabricante (OUI lookup opcional).
  - [ ] Ação rápida: clicar com o botão direito para iniciar Scan de Portas ou abrir no navegador interno.
- [ ] **Módulo 3: Ferramentas de Diagnóstico (Ping, Tracert, Estresse):**
  - [ ] Exibição gráfica da variação de latência ao longo do tempo.
  - [ ] Painel do Simulador de Carga com controles de segurança e contadores de pacotes em tempo real.
- [ ] **Módulo 4: Scanner de Espectro / Canais Wi-Fi:**
  - [ ] Visualização em gráfico de colunas ou curvas dos canais 2.4 GHz e 5 GHz para identificar sobreposição e interferência.
- [ ] **Módulo 5: Navegador Web Interno:**
  - [ ] Integração do `Avalonia.WebView` para carregar interfaces web dos roteadores ou ferramentas como o Fast.com.

---

### 📌 Fase 4: Limpeza, Testes e Polimento
- [ ] Validação multiplataforma (execução e testes em Linux e Windows).
- [ ] Garantir que nenhum arquivo temporário, senha ou token seja persistido no repositório.
- [ ] Documentação completa no `README.md` com instruções de execução e configuração.
