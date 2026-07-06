# Elifoot 98 Launcher

Wrapper para rodar o **Elifoot 98** (1998, João Duarte Almeida) em Windows 10/11 modernos, incluindo o editor de equipes e o registrador CRACK.EXE.

Traz UI para configurar resolução e modo janela/fullscreen, e embute [otvdm](https://github.com/otya128/winevdm) (para os executáveis 16-bit) e uma versão portable do DOSBox (para o `CRACK.EXE` DOS-only).

## Estado atual

Em desenvolvimento inicial. Roadmap:

- [x] Scaffold do projeto (C# WinForms + .NET Framework 4.8)
- [ ] Form principal com 3 botões (Elifoot / Editor / CRACK)
- [ ] Diálogo de configurações (resolução + modo janela)
- [ ] Integração com otvdm (bundled) para Elifoot e Editor
- [ ] Integração com DOSBox portable para CRACK
- [ ] Instalador Inno Setup
- [ ] Testes em VM Windows 10 e 11

## Por que este projeto

Elifoot 98 é um app Windows 3.x (Delphi 1 + VBX 16-bit). Windows 10/11 não roda mais binários 16-bit nativamente. otvdm resolve isso, mas configurá-lo, apontar resoluções e alternar janelado/fullscreen dá trabalho a cada execução. Este launcher automatiza tudo.

## Stack

- **C# 12** + **.NET Framework 4.8** (pré-instalado em todo Win 10/11 desde 2019 — zero dependência para o usuário)
- **WinForms** para UI
- **[otvdm](https://github.com/otya128/winevdm)** (MIT) — emulação 16-bit
- **[DOSBox](https://www.dosbox.com/)** (GPL-2.0) — para o CRACK.EXE
- **[Inno Setup](https://jrsoftware.org/isinfo.php)** — instalador Windows

## Build local (Linux ou Windows)

Requer .NET SDK 8+. Em Linux Mint/Ubuntu, se o SDK apt não trouxer a workload de Windows Desktop, use o script oficial:

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0 --install-dir ~/.dotnet
export PATH="$HOME/.dotnet:$PATH"
```

Depois:

```bash
cd src
dotnet build -c Release
# saída em src/bin/Release/net48/ElifootLauncher.exe
```

## Roadmap futuro

Além do launcher em si:

- Atualização de rosters 2025/2026 via edição dos arquivos `.EFT` (o codec já foi decodificado — implementação num tool separado)
- Tradução da UI (patch nas strings do `ELIFOOT.EXE`)
- Correções de bugs conhecidos do binário (patch)

## Licença

Este launcher é distribuído sob **GPL-3.0** (compatível com DOSBox, que é GPL-2.0-or-later).

O jogo Elifoot 98 é obra de **João Duarte Almeida**, distribuído originalmente como shareware. Seu autor faleceu em 2019. Este projeto tem propósito de preservação e não tem fins lucrativos.

## Créditos

- Elifoot 98 — João Duarte Almeida (1998)
- Inspiração: [elifoot98web](https://github.com/elifoot98web/elifoot98web) — versão browser
- otvdm — otya128
- DOSBox — DOSBox Team
