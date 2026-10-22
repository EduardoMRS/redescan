# Como publicar uma Release

Este projeto usa GitHub Actions para publicar os binários automaticamente. Você
nunca precisa subir arquivos à mão — tudo acontece a partir de uma **tag**.

## Entendendo os dois modos do CI

| Gatilho | O que acontece | Onde ficam os binários |
|---|---|---|
| `push` na `main` ou `pull_request` | compila, roda testes e sobe os binários como *artifacts* da execução | aba **Actions** → a execução → seção *Artifacts* (temporários) |
| `push` de uma **tag `v*`** (ex.: `v1.0.0`) | tudo acima **+ cria uma GitHub Release** | aba **Releases** do repositório (permanente, versionado) |

- **Artifacts** servem para baixar e testar builds do dia a dia. Expiram.
- **Release** é a publicação de verdade: página versionada, com notas de versão e os
  arquivos para download público.

## Publicando a v1.0.0

Com tudo já na `main`:

```bash
# 1. Garanta que está na main atualizada
git checkout main
git pull

# 2. Crie a tag anotada
git tag -a v1.0.0 -m "RedeScan v1.0.0"

# 3. Envie a tag (isto dispara o job de release)
git push origin v1.0.0
```

Em alguns minutos, a aba **Releases** terá a `v1.0.0` com **duas variantes**:

### Portátil (leve) — recomendada para a maioria
Um único arquivo, sem navegador embutido (usa "abrir no sistema" para web):

- `RedeScan-Linux-x86_64.AppImage` — portátil universal do Linux
  (`chmod +x *.AppImage && ./RedeScan-Linux-x86_64.AppImage`)
- `RedeScan-Windows-x64.exe` — executável único para Windows (não precisa instalar)
- `RedeScan-Android.apk` — APK de teste (debug-signed)

### Full (com motor web) — maior
Pacote em `.zip` com o **navegador integrado (CEF)** e o **vídeo RTSP (LibVLC)**
embutidos. É um pacote de vários arquivos (o CEF exige isso), ~400 MB:

- `RedeScan-Full-Linux-x64.zip`
- `RedeScan-Full-Windows-x64.zip`

Descompacte e rode o executável `RedeScan`/`RedeScan.exe` de dentro da pasta.

As notas de versão são geradas automaticamente a partir dos commits/PRs.

> **Por que duas variantes?** O motor web (Chromium/CEF) pesa centenas de MB e
> precisa de arquivos soltos — não cabe no formato "um arquivo só". Quem quer um
> download pequeno e portátil usa a variante leve; quem precisa do navegador
> embutido usa a Full. (Os *artifacts* por execução do CI sempre chegam em `.zip`
> porque o GitHub Actions embrulha todo artifact; os assets da Release são os
> arquivos diretos.)

## Corrigindo uma tag errada

```bash
git tag -d v1.0.0                # apaga localmente
git push origin :refs/tags/v1.0.0 # apaga no remoto
# recrie e reenvie conforme acima
```

## Observações

- O APK é assinado com a **chave de debug** (adequado para testes). Para publicar na
  Play Store seria necessária uma *keystore* própria (mantida fora do repositório, em
  GitHub Secrets).
- A build de **mídia nativa** (CEF + LibVLC) não entra na Release automática por ser
  grande e opcional; gere-a sob demanda com `scripts/package-native.sh`.
