# 🤖 Ponto de Entrada para Agentes de IA (AGENTS.md)

Bem-vindo ao repositório **RedeScan**! Este arquivo serve como mapa inicial de navegação e diretrizes para qualquer agente autônomo ou assistente de IA trabalhando neste projeto.

---

## 📌 Guia Rápido de Contexto

Para entender o estado atual do projeto em profundidade, consulte os arquivos no diretório `./context/` (estes arquivos contêm o histórico operacional e são mantidos fora do controle de versão git):

- **[Estado Atual & O que foi feito](file:///home/eduardo/Projetos/redescan/context/CURRENT_STATE.md)**: Visão detalhada do que está pronto, arquitetura atual e próximos passos.
- **[Mapa de Arquitetura & Diretórios](file:///home/eduardo/Projetos/redescan/context/ARCHITECTURE.md)**: Como o código está organizado entre `Core`, `UI` e `Plataformas`.
- **[Comandos Úteis de Terminal](file:///home/eduardo/Projetos/redescan/context/COMMANDS.md)**: Como compilar, rodar testes e gerenciar o SDK do .NET 8.
- **[Histórico das Tarefas](file:///home/eduardo/Projetos/redescan/context/TASK_HISTORY.md)**: Log de decisões e implementações das fases anteriores.
- **[Roadmap & Checklist](file:///home/eduardo/Projetos/redescan/TODO.md)**: Checklist principal das fases da reestruturação.
- **[Especificação Original](file:///home/eduardo/Projetos/redescan/RESTRUTURACAO.md)**: Documento de requisitos enviado pelo usuário.

---

## 🛠️ Regras de Ouro para Agentes

1. **SDK do .NET**: O .NET 8 está instalado no usuário em `~/.dotnet`. Certifique-se de usar `export PATH="$HOME/.dotnet:$PATH"` antes de comandos dotnet se o comando não estiver no PATH da subshell.
2. **Solução Ativa**: Utilize sempre `RedeScan.sln` para novos desenvolvimentos e testes. O arquivo `WEBTester - Analise de Redes.sln` e sua pasta correspondente são legados mantidos apenas para histórico e referência.
3. **Segurança de Credenciais**: **NUNCA** deixe senhas, tokens ou IPs de câmeras e roteadores em código estático. Utilize sempre a abstração `IConfigurationService` e o modelo `RtspCameraConfig`.
4. **Testes Antes de Concluir**: Sempre execute `dotnet test RedeScan.sln` após alterar ou adicionar qualquer funcionalidade para garantir que nada foi quebrado.
5. **Atualização Contínua**: Ao concluir novas tarefas, atualize o `TODO.md` e o `context/CURRENT_STATE.md`.

---

## 🚀 Status Rápido do Projeto

- **Fase 0 (Setup)**: ✅ Concluída (SDK .NET 8 e solução estruturada).
- **Fase 1 (Core & Serviços de Rede)**: ✅ Concluída com 7 testes unitários aprovados.
- **Fase 2 (Wi-Fi Scanner)**: ⏳ Próxima etapa.
- **Fase 3 (Avalonia UI & MVVM)**: ⏳ A iniciar após/junto à Fase 2.
