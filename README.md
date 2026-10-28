# Descanso

[![CI](https://github.com/brenoacp/descanso/actions/workflows/ci.yml/badge.svg)](https://github.com/brenoacp/descanso/actions/workflows/ci.yml)
[![Licença: MIT](https://img.shields.io/badge/licen%C3%A7a-MIT-blue.svg)](LICENSE)
[![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4.svg)](#requisitos)

Aplicativo de bandeja do Windows que lembra você de fazer pausas. A notificação é
nativa, usa o cenário *Reminder* (fica na tela até ser dispensada) e traz botões
para registrar a pausa, adiar ou pular.

![ícone](docs/icon.png)

## Baixar

**[Baixar Descanso.exe](https://github.com/brenoacp/descanso/releases/latest/download/Descanso.exe)** — v1.0.0

O download é um único `.exe`, sem instalador: coloque em qualquer pasta e execute.
O binário não tem assinatura digital, então o SmartScreen pode avisar na primeira
execução — clique em *Mais informações* → *Executar assim mesmo*.

## O que ele faz

- Conta o tempo de trabalho ativo e avisa a cada N minutos, mesmo com outras janelas em primeiro plano.
- Uma pausa longa a cada N pausas, com mensagem e duração próprias.
- **Para o cronômetro quando você sai.** Sem teclado ou mouse, o tempo não conta; uma ausência longa é tratada como descanso e reinicia o ciclo.
- Respeita **horário de trabalho e dias da semana**, inclusive turnos que atravessam a meia-noite.
- **Pausa manual** por 30 min, 1 h ou até amanhã, pelo menu da bandeja.
- **Adiamento** limitado por pausa (padrão: 5 min, até 3 vezes).
- **Inicia com o Windows** (opcional, sem administrador).
- **O ícone da bandeja é o próprio cronômetro**: o preenchimento desce conforme o
  tempo restante (cheio no início do ciclo, vazio na hora da pausa) e fica cinza
  quando a contagem está parada — pausa manual ou fora do expediente.
- Tooltip da bandeja mostra a contagem para a próxima pausa.

## Requisitos

- Windows 10 2004 (build 19041) ou Windows 11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (o build publicado é *framework-dependent*)

## Uso

Ao abrir pela primeira vez, o app cria `%APPDATA%\Descanso\settings.json` e mostra a
tela de configuração. Depois disso ele vive na bandeja.

| Ação | Como |
|---|---|
| Abrir configurações | Duplo clique no ícone da bandeja, ou menu → *Configurar...* |
| Fazer uma pausa agora | Menu → *Fazer uma pausa agora* (ou `Descanso.exe --break-now`) |
| Pausar os lembretes | Menu → *Pausar* → 30 min / 1 h / até amanhã |
| Sair | Menu → *Sair* |

O app nunca abre a janela ao iniciar com o Windows: o autostart usa `--minimized`.

## Configuração

O arquivo é JSON e pode ser editado com o app aberto — a mudança é aplicada em
menos de um segundo, sem reiniciar. Valores fora de faixa são corrigidos
automaticamente na leitura.

| Campo | Padrão | Efeito |
|---|---|---|
| `intervalMinutes` | `50` | Tempo de trabalho até a próxima pausa (1–480) |
| `breakSeconds` | `300` | Duração informada da pausa curta |
| `longBreakEvery` | `4` | Uma pausa longa a cada N pausas (`0` desativa) |
| `longBreakSeconds` | `900` | Duração informada da pausa longa |
| `soundEnabled` | `true` | Som na notificação (em loop no modo lembrete) |
| `idle.thresholdSeconds` | `120` | Após este tempo sem uso, o cronômetro congela |
| `idle.resetAfterSeconds` | `300` | Após este tempo ausente, conta como descanso |
| `workingHours.enabled` | `true` | Lembrar apenas no expediente |
| `workingHours.start` / `end` | `08:00` / `18:00` | Janela; se `end < start`, atravessa a meia-noite |
| `workingHours.days` | `[1,2,3,4,5]` | Dias ISO: 1 = segunda … 7 = domingo |
| `snooze.minutes` | `5` | Quanto tempo o adiamento empurra a pausa |
| `snooze.maxPerBreak` | `3` | Adiamentos permitidos por pausa |
| `startWithWindows` | `false` | Entrada em `HKCU\...\CurrentVersion\Run` |

## Como o ciclo funciona

O núcleo é uma máquina de estados pura (`BreakScheduler`) que recebe um *tick* a cada
5 segundos com três informações: hora local, tempo decorrido e tempo de inatividade.
Ela devolve no máximo um evento `BreakDue`.

- O tempo só acumula dentro do expediente, com o usuário ativo e sem pausa manual.
- Ao atingir o intervalo, o excedente do ciclo é preservado (`Pular` não zera o crédito).
- **Suspensão e hibernação não disparam lembrete falso**: intervalos maiores que 30 s
  são descartados como tempo de trabalho. A inatividade cobre o mesmo caso por outro ângulo.
- O progresso do ciclo **não** é persistido: reiniciar o app recomeça a contagem.

## Desenvolvimento

```powershell
dotnet build
dotnet test
dotnet run --project src\Descanso.App
```

Publicação (um único `.exe`):

```powershell
dotnet publish src\Descanso.App -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist
```

O ícone é gerado por script e versionado como binário:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1
```

O ícone **da bandeja** não vem do arquivo: `IconRenderer` o desenha a cada mudança
de nível (ICO DIB de um frame em memória, porque `GetHicon()` perde o alfa). Para
revisar o desenho sem esperar o ciclo, gere a folha de prévia:

```powershell
.\dist\Descanso.exe --export-icon-preview docs\tray-icons.png
```

### Estrutura

```
src/Descanso.Core/     agendador, configuração, textos — sem dependência de WPF
src/Descanso.App/      bandeja, toast, inatividade, autostart, configuração
tests/                 xUnit sobre o núcleo (relógio simulado, sem UI)
tools/make-icon.ps1    gera o .ico com frames DIB
```

A separação é proposital: a parte difícil (quando lembrar) é testável com relógio
falso, e a casca do Windows só reage a eventos.

## Solução de problemas

- Erros ficam em `%APPDATA%\Descanso\log.txt` (rotaciona em 512 KB, mantendo um
  `log.txt.old`). Anexe o conteúdo ao abrir uma issue.
- Se a notificação não aparecer, abra a configuração: ela detecta quando o app está
  desativado nas notificações do Windows e oferece um botão de teste.

## Limitações conhecidas

- **O Assistente de Foco pode suprimir a notificação.** Não há API para furar o
  bloqueio; o app avisa na tela de configuração quando detecta o app desativado no
  registro e oferece um botão de teste.
- **Um toast persistente pode ficar para trás** se você sair do app com um lembrete
  na tela; ele some sozinho no próximo ciclo do Windows.
- **O executável tem ~26 MB** porque o toast nativo usa as projeções WinRT
  (`Microsoft.Windows.SDK.NET.dll`), e WPF não suporta *trimming*. Ainda é uma fração
  de um app Electron.
- **.NET 8 sai de suporte em novembro de 2026.** Migrar para `net10.0` é trocar o
  `TargetFramework` dos três projetos; o código não depende da versão.

## Licença

[MIT](LICENSE) © 2026 brenoacp

