# Instalador (Inno Setup)

## Requisitos

- Windows (o `iscc` do Inno Setup só roda em Windows)
- [Inno Setup 6+](https://jrsoftware.org/isdl.php) instalado
- Build do launcher pronto em `../src/bin/Release/net48/`
- Deps em `../vendor/` (rodar `bash ../scripts/fetch-deps.sh`)
- Arquivos do Elifoot 98 em `../game/` (colocados manualmente)

## Como compilar o instalador

Do lado Windows (na pasta do repo):

```
bash scripts/fetch-deps.sh              # baixa otvdm e DOSBox
dotnet build src -c Release
copy elifoot98\* game\                  # arquivos do jogo
iscc installer\setup.iss
```

Saída: `installer\Output\elifoot98-launcher-setup-0.1.0.exe`

## Notas

- O instalador checa se `.NET Framework 4.8` está presente (Windows 10 1903+ e Win 11 já vêm com ele).
- Instala em `%LOCALAPPDATA%\Programs\Elifoot98Launcher` (privilégios de usuário — não pede admin).
- Cria atalho no menu Iniciar sempre, no desktop opcional.
- Idiomas: pt-BR (default) e en.
