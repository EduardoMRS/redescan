Novos recursos:
- reprodutor em grid de cameras rtsp que podem ser adicionadas via json a aplicação e acessiveis atraves do menu lateral possibilitando visualizar cameras
- simulador de DDoS para testes de carga em rede
- scan de interferencia wireless para dispositivos com atena
- substituição do windowsform para Avalonia UI com C# em .NET Standard ou .NET 8/9
- tirar todas credenciais e senhas do hardcode!!!

Novas organização técnica de cada um dos seus requisitos dentro do ecossistema do Avalonia UI funciona da seguinte forma:

🎨 1. Camada de Interface Visual (UI & Mídia)

Esta é a parte que o Avalonia UI gerencia diretamente na sua árvore de componentes usando XAML no VS Code.
• Webbrowser Interno:
	• Tecnologia: Pacote open-source Avalonia.WebView.
	• Como Organizar: Diferente do Windows Forms que usava bibliotecas amarradas ao Windows, o componente WebView do Avalonia abstrai o motor nativo de cada sistema (Edge WebView2 no Windows, WebKitGTK no Linux, e o Chrome Android WebView no Android). Você colocará a tag <WebView /> direto no seu arquivo XAML vinculando a URL do IP do roteador. [1] (https://avaloniaui.net/avalonia), [2] (https://docs.avaloniaui.net/docs/migration/maui/)
• Reprodutor em Grid de Câmeras RTSP:
	• Tecnologia: LibVLCSharp.Avalonia + Controle nativo UniformGrid do Avalonia.
	• Como Organizar: O VLC possui decodificação de hardware nativa para streams de vídeo de baixa latência em Linux, Windows e Android. Na interface, você usará uma UniformGrid (que organiza os filhos automaticamente em linhas e colunas iguais), e dentro dela instanciará dinamicamente múltiplos controles do tipo <vlc:VideoView /> conforme o usuário adicione câmeras na tela. [1] (https://www.nuget.org/packages/LibVLCSharp.Avalonia)

🌐 2. Camada de Baixo Nível e Diagnóstico de Rede (Back-End C#)

Esta lógica roda de forma "invisível" por trás da interface. O ideal é criar um projeto do tipo Class Library que as telas do Avalonia apenas chamam.
• Scan de IPs em uso na rede:
	• Tecnologia: Programação assíncrona (Task.WhenAll) disparando a classe nativa System.Net.NetworkInformation.Ping.
	• Como Organizar: Para mapear a rede local de forma multiplataforma (varrendo de .1 a .254), você dispara pings simultâneos em tarefas assíncronas para não travar o app. O resultado atualiza uma lista na tela (ObservableCollection) usando a arquitetura MVVM do Avalonia. [1] (https://en.wikipedia.org/wiki/Avalonia_(software_framework))
• Scan de Portas em IP específico:
	• Tecnologia: Classe nativa System.Net.Sockets.TcpClient.
	• Como Organizar: Uma rotina em C# simples que tenta abrir conexões assíncronas (ConnectAsync) em um laço de portas comuns (como 80, 443, 22, 23, 8080) contra o IP alvo. Se a conexão estabelecer com sucesso, a porta está aberta. Funciona perfeitamente em Linux, Windows e Android.
• Testes de Ping, Tracert e Simulador de DDoS (Carga):
	• Ping / Tracert: A classe nativa Ping resolve o ping básico. Para o Tracert (Traceroute), você pode incrementar o campo TTL (Time to Live) a cada pacote enviado usando PingOptions.Ttl, interceptando o IP que respondeu até chegar no destino final.
	• Simulador de DDoS (Teste de Estresse): Pode ser feito puramente criando rajadas de requisições assíncronas usando múltiplos Sockets UDP brutos ou disparando loops em massa com HttpClient direcionados ao IP de teste de carga.
	• Nota Importante sobre Android/Linux: No Linux e Android, para enviar pacotes de ping brutos de ICMP de forma nativa sem privilégios de Administrador (Root), o ecossistema .NET utiliza por baixo dos panos sockets não-privilegiados que funcionam perfeitamente na maioria das distribuições atuais.

📶 3. Camada Específica por Plataforma (Hardware e Sistema)

• Scan de interferência de sinal Wireless (Canais Wi-Fi):
	• Tecnologia: Interfaces nativas por plataforma acopladas via Injeção de Dependência (Dependency Injection).
	• Como Organizar: Esta é a única funcionalidade da sua lista que não possui uma biblioteca unificada universal no .NET, porque ler as redes Wi-Fi ao redor exige conversar com APIs muito diferentes de cada sistema operacional:
		• No Windows: Você precisará chamar a API nativa Native Wifi (wlanapi.dll).
		• No Linux: Você precisará executar comandos internos como o nmcli ou ler os dados do subsistema /proc/net/wireless através do seu código C#.
		• No Android: Você precisará usar o WifiManager nativo do Android.
	• Solução no Avalonia: Você criará uma interface no seu projeto chamada IWifiScanner com o método GetAvailableNetworks(). No projeto de cada plataforma (Windows, Linux, Android), você implementa o código específico que lê o Wi-Fi e injeta isso na sua tela principal do Avalonia.

🏗️ Estrutura Recomendada do Projeto no VS Code

Ao criar sua solução multiplataforma, organize as pastas do projeto dessa forma:
text
MeuAppRede/
│
├── MeuAppRede.Core/                <-- Código C# Puro (.NET Standard / .NET 9)
│   ├── Services/                   <-- Lógica do Ping, PortScan, DDoS e RTSP
│   └── ViewModels/                 <-- Lógica das telas (Padrão MVVM)
│
├── MeuAppRede.UI/                  <-- Telas e Estilos Gerais (.NET 9)
│   ├── Views/                      <-- Arquivos XAML (Grid RTSP, WebView, etc.)
│   └── App.axaml
│
└── MeuAppRede.Runner/              <-- Os executáveis finais gerados pelo Avalonia
    ├── MeuAppRede.Desktop/         <-- Roda no Windows e no Linux
    └── MeuAppRede.Android/         <-- Roda no Android (Contém as permissões do manifest)
Use o código com cuidado.