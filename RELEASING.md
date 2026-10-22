# Como publicar uma Release

O projeto tem **dois workflows** separados no GitHub Actions:

| Workflow | Quando roda | O que faz |
|---|---|---|
| **CI (validação)** — `ci.yml` | automático em `push`/`pull_request` na `main` | só valida: restaura dependências, compila (avisos como erro) e roda os testes. Não gera binários. |
| **Release (deploy manual)** — `release.yml` | **manual** (botão *Run workflow*) ou ao enviar uma **tag `v*`** | builda todas as variantes e publica a **GitHub Release**. |

Ou seja: o dia a dia (push/PR) é rápido e barato — só checa se o código compila e
passa nos testes. O build pesado (binários, pacotes Full ~400 MB) só acontece quando
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
Um único arquivo, sem navegador embutido (usa "abrir no sistema" para web):

- `RedeScan-Linux-x86_64.AppImage` — portátil universal do Linux
  (`chmod +x *.AppImage && ./RedeScan-Linux-x86_64.AppImage`)
- `RedeScan-Windows-x64.exe` — executável único para Windows (não precisa instalar)
- `RedeScan-Android.apk` — APK (debug-signed)

### Full (com motor web + vídeo) — maior (~400 MB)
Pacote `.zip` com **navegador CEF** e **vídeo LibVLC** embutidos (vários arquivos):

- `RedeScan-Full-Windows-x64.zip`
- `RedeScan-Full-Linux-Ubuntu-x64.zip`  (base Debian/Ubuntu)
- `RedeScan-Full-Linux-Fedora-x64.zip`  (base Fedora/RHEL)

Descompacte e rode o `RedeScan`/`RedeScan.exe` de dentro da pasta.

> **Por que duas variantes de Linux Full?** O libvlc é dependência do sistema e difere
> entre famílias (apt no Ubuntu, dnf no Fedora). Geramos um pacote em cada base para
> maximizar a compatibilidade do vídeo RTSP.

## Corrigindo uma tag errada

```bash
git tag -d v1.0.0
git push origin :refs/tags/v1.0.0
```

## Observações honestas

- O **APK** é assinado com a chave de **debug** (para testes). Publicar na Play Store
  exigiria uma *keystore* própria (guardada em GitHub Secrets, fora do repo).
- No **Android**, o navegador integrado agora é **embutido** usando o WebView nativo do
  sistema (`android.webkit.WebView`) — não o CEF (que é desktop-only). O APK já traz isso.
- A variante **Full** não é single-file/AppImage porque o CEF precisa de arquivos
  soltos (libcef, locales, processo auxiliar).
