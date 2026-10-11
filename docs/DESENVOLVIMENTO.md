# Desenvolvimento

O repositório contém o código. A pasta `dist`, com o instalador, não é publicada no GitHub.

É preciso o SDK .NET 8 com suporte a aplicativos Windows.

## Compilar

Na raiz do repositório:

```powershell
dotnet build Conferencia.slnx -c Release
```

## Gerar o instalador

```powershell
dotnet publish src/ConferenciaCarregamento/ConferenciaCarregamento.csproj -c Release -r win-x64 --self-contained true -o dist/payload
dotnet publish src/Conferencia.Setup/Conferencia.Setup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist/setup
Copy-Item dist/setup/ConferenciaSetup.exe dist/ConferenciaSetup.exe
```

O arquivo para instalar é `dist/ConferenciaSetup.exe`, ao lado da pasta `dist/payload`. A instalação para quem recebe o programa está em `docs/INSTALL.md`.

## Projetos

- `src/Conferencia.Core` — leitura do DANFE, cálculo e parâmetros
- `src/ConferenciaCarregamento` — janela do programa
- `src/Conferencia.Setup` — assistente de instalação
- `src/Conferencia.Verificar` — conferência do exemplo, fora da solução

A solução é `Conferencia.slnx`.
