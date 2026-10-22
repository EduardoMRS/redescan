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

Em alguns minutos, a aba **Releases** terá a `v1.0.0` com:

- `RedeScan-Linux-x86_64.AppImage` — instalador/portátil universal do Linux
  (dê permissão de execução e clique duas vezes: `chmod +x *.AppImage && ./RedeScan-Linux-x86_64.AppImage`)
- `RedeScan-Windows-x64.exe` — executável único para Windows (não precisa instalar)
- `RedeScan-Android.apk` — APK de teste (debug-signed)

As notas de versão são geradas automaticamente a partir dos commits/PRs.

> **Nota sobre os formatos:** o executável self-contained é publicado **sem os
> `.pdb`** (símbolos de debug), então cada download é um único arquivo. No Linux,
> o binário é empacotado como **AppImage** — o formato universal que roda em
> qualquer distribuição sem instalação. (Os *artifacts* por execução do CI ainda
> chegam dentro de um `.zip` porque o próprio GitHub Actions embrulha todo
> artifact; os assets da Release, porém, são os arquivos diretos.)

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
