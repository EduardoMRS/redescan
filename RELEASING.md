# Como publicar uma Release

O projeto tem **dois workflows** separados no GitHub Actions:

| Workflow | Quando roda | O que faz |
|---|---|---|
| **CI (validação)** — `ci.yml` | automático em `push`/`pull_request` na `main` | só valida: restaura dependências, compila (avisos como erro) e roda os testes. Não gera binários. |
| **Release (deploy manual)** — `release.yml` | **manual** (botão *Run workflow*) ou ao enviar uma **tag `v*`** | builda todas as variantes e publica a **GitHub Release**. |

Ou seja: o dia a dia (push/PR) é rápido e barato — só checa se o código compila e
passa nos testes. O build dos binários e instaladores só acontece quando
**você** decide publicar.

## Publicar — opção 1: por tag

```bash
git checkout main && git pull
git tag -a v1.0.0 -m "RedeScan v1.0.0"
git push origin v1.0.0          # dispara o deploy e cria a Release v1.0.0
```

## Publicar — opção 2: manual (sem tag)

Na aba **Actions** → workflow **Release (deploy manual)** → **Run workflow**. Você pode
informar a versão (ex.: `v1.0.0`); se deixar em branco, sai uma **pré-release de teste**
com nome automático.

## O que entra na Release

### Portátil (leve) — recomendada para a maioria
Um único arquivo. Usa o motor web e o ffmpeg que já existirem no sistema (se faltar o
ffmpeg, o app oferece baixar com um clique):

- `RedeScan-Linux-x86_64.AppImage` — portátil universal do Linux
  (`chmod +x *.AppImage && ./RedeScan-Linux-x86_64.AppImage`)
- `RedeScan-Windows-x64.exe` — executável único para Windows (não precisa instalar)
- `RedeScan-Android.apk` — APK (debug-signed)

### Full — instaladores que baixam as dependências na instalação
- `RedeScan-Full-Windows-x64.exe` — instalador (Inno Setup). Durante a instalação baixa o
  **WebView2 Runtime** (se faltar) e o **ffmpeg** (verificado por SHA-256) para `{app}\ffmpeg`.
- `RedeScan-Full-Linux-x64.deb` — `sudo apt install ./RedeScan-Full-Linux-x64.deb`; o apt
  instala `libwebkit2gtk-4.1-0`, GTK3, `ffmpeg` e `iproute2`.
- `RedeScan-Full-Linux-x86_64.rpm` — `sudo dnf install ./RedeScan-Full-Linux-x86_64.rpm`;
  o dnf instala `webkit2gtk4.1`, `gtk3`, `ffmpeg-free` (ou `ffmpeg`) e `iproute`.

> Validado em containers limpos: Ubuntu 22.04, Ubuntu 24.04 e Fedora 40 instalam o pacote
> e todas as dependências. Use `apt install`/`dnf install` (não `dpkg -i`/`rpm -i`, que
> não baixam dependências).

## Corrigindo uma tag errada

```bash
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

## Observações honestas

- O **APK** é assinado com a chave de **debug** (para testes). Publicar na Play Store
  exigiria uma *keystore* própria (guardada em GitHub Secrets, fora do repo).
- O navegador embutido usa o motor web do próprio sistema em todas as plataformas
  (WebView2, WebKitGTK, `android.webkit.WebView`) — nada de Chromium empacotado.
- O vídeo das câmeras usa o **ffmpeg** como processo separado. No Fedora, o `ffmpeg-free`
  do repositório oficial pode não decodificar H.264 de algumas câmeras; nesse caso instale
  o `ffmpeg` do RPM Fusion.
- O instalador Windows é validado no CI (job `validate-windows-installer`) compilando o
  `.iss` num container Wine — o que confere sintaxe e script, mas não executa a instalação.

---

## Atualização automática (manifesto `latest.json`)

O app verifica atualizações no início comparando a versão instalada com um
manifesto remoto:

```
https://deveduardomrs.pro/d/redescan/release/latest.json
```

Quando há versão nova, o Painel mostra um aviso com botão **Baixar** (abre o link
da plataforma no navegador).

### Como o manifesto é gerado
O workflow de release **gera o `latest.json` automaticamente** com a versão da tag
e os links dos assets da GitHub Release, e o anexa à release. Você não escreve o
JSON à mão.

### Mantendo o seu site atualizado
Para o app apontar para o seu domínio, publique o `latest.json` em
`https://deveduardomrs.pro/d/redescan/release/`. Duas formas:

1. **Simples:** após cada release, baixe o artefato `latest-json` da execução do
   workflow (ou o `latest.json` anexado à Release) e suba para a pasta do seu site.
2. **Automático (futuro):** adicionar um passo no `release.yml` que faz upload do
   `latest.json` (e, se quiser, dos binários) para o seu servidor via SCP/FTP,
   usando credenciais guardadas em **GitHub Secrets**. Hoje os links do manifesto
   apontam para os assets da própria GitHub Release, então basta hospedar o JSON.

### A versão vem de um lugar só
A versão fica em `Directory.Build.props` (`<RedeScanVersion>`). Atualize-a ao
publicar e crie a tag correspondente (ex.: `1.1.0` + tag `v1.1.0`). O app lê essa
mesma versão em runtime para a comparação.
