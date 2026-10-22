# RedeScan

Uma ferramenta de rede feita para quem vive mexendo em rede: escanear dispositivos,
diagnosticar conexão, ver câmeras, abrir o roteador no navegador e cair num terminal
SSH — tudo num lugar só. Roda em **Windows, Linux e Android**.

É open source e de coração aberto: contribuições, ideias e correções são muito bem-vindas.

---

## O que dá pra fazer

- **Painel** — vê num relance seu IP, gateway, sub-rede e os dispositivos com página web
  na rede (com atalho pra abrir direto).
- **Scanner de rede** — descobre quem está na rede, com nome, fabricante, latência e as
  portas abertas. Clique com o botão direito num dispositivo para ações rápidas.
- **Inspeção de host** — identifica o serviço rodando em cada porta, estima o sistema
  operacional e pega o MAC. Dá pra mandar **Wake-on-LAN** pra ligar a máquina.
- **Diagnóstico** — ping contínuo com gráfico, traceroute e um simulador de carga pra
  teste de estresse.
- **Wi-Fi** — lista as redes ao redor com sinal, canal e banda, e mostra quais canais
  estão congestionados.
- **Câmeras RTSP** — organiza suas câmeras em grade por grupo. Importa e exporta a mesma
  lista do CentralMonitor (`rtspList.json`).
- **Navegador integrado** — abre a interface web de roteadores e ONUs sem sair do app.
- **Acesso remoto** — SSH, Telnet e **Winbox** (por IP ou MAC) direto do scanner, além de
  um terminal SSH embutido que funciona até no celular.

---

## Baixar e instalar

Pegue a versão mais recente na página de **[Releases](https://github.com/EduardoMRS/redescan/releases)**.
Tem duas opções:

### Portátil (leve) — é só abrir, não instala nada
- **Windows:** `RedeScan-Windows-x64.exe` — clica e usa.
- **Linux:** `RedeScan-Linux-x86_64.AppImage` — dá permissão e abre:
  ```bash
  chmod +x RedeScan-Linux-x86_64.AppImage
  ./RedeScan-Linux-x86_64.AppImage
  ```
- **Android:** `RedeScan-Android.apk` — instale no aparelho (precisa permitir "apps de
  fontes desconhecidas").

### Completa (com navegador e vídeo embutidos) — instalador de verdade
- **Windows:** `RedeScan-Full-Windows-x64.msi` — duplo clique, segue o assistente.
- **Ubuntu/Debian:** `RedeScan-Full-Linux-x64.deb`
  ```bash
  sudo apt install ./RedeScan-Full-Linux-x64.deb
  ```
- **Fedora/RHEL:** `RedeScan-Full-Linux-x86_64.rpm`
  ```bash
  sudo dnf install ./RedeScan-Full-Linux-x86_64.rpm
  ```

> A versão **Completa** traz o navegador e o vídeo das câmeras embutidos (por isso é
> maior). A **Portátil** é menor e usa o navegador/player do seu sistema. Escolha a que
> preferir — as duas têm as mesmas ferramentas de rede.

O app avisa sozinho quando sai uma versão nova. 🙂

---

## Suas configurações ficam só com você

Câmeras e atalhos são salvos no seu perfil, nunca no app nem em nenhum servidor:

- **Linux/macOS:** `~/.config/redescan/config.json`
- **Windows:** `%APPDATA%\redescan\config.json`

As senhas das câmeras ficam apenas nesse arquivo local e são usadas só na hora de abrir o
stream. Nada de credencial escondida no código.

---

## Quer contribuir? 🧑‍💻

Que bom ter você por aqui! O RedeScan é .NET 8 com [Avalonia UI](https://avaloniaui.net/)
(interface) no padrão MVVM.

### Rodar o projeto

Você vai precisar do [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Então:

```bash
git clone https://github.com/EduardoMRS/redescan.git
cd redescan
dotnet run --project src/RedeScan.Desktop/RedeScan.Desktop.csproj
```

Pronto, o app abre. Para mexer no app Android, você também precisa do workload
(`dotnet workload install android`), do JDK 17+ e do Android SDK.

### Como o código é organizado

| Pasta | O que é |
|---|---|
| `src/RedeScan.Core` | o coração: toda a lógica de rede (scanner, ping, Wi-Fi, SSH…), sem interface |
| `src/RedeScan.UI` | as telas e a interface (Avalonia/MVVM) |
| `src/RedeScan.Desktop` | o app de Windows e Linux |
| `src/RedeScan.Android` | o app de Android |

A ideia é simples: a lógica mora no **Core** e não conhece a tela; a **UI** só consome
essa lógica. Isso deixa fácil adicionar uma ferramenta nova sem bagunçar o resto.

Abra uma issue contando sua ideia ou mande um pull request — toda ajuda conta.

---

## Licença

Open source sob a licença [MIT](LICENSE). Use, modifique e compartilhe à vontade.
