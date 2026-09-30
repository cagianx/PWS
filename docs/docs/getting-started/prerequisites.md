---
sidebar_position: 1
---

# Prerequisiti

Prima di poter compilare e avviare PWS Browser è necessario soddisfare i seguenti requisiti.

## .NET SDK

PWS richiede **.NET 10** o superiore.

```bash
dotnet --version   # deve mostrare 10.x.x
```

Scaricare da [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).

:::warning Workload MAUI e Mono
Il workload MAUI tradizionale (`dotnet workload install maui`) **non è necessario** e
su .NET 10 risulta spesso rotto. PWS usa il pacchetto NuGet
`Microsoft.Maui.Platforms.Linux.Gtk4` (backend GTK4 ufficiale di `dotnet/maui-labs`) che porta
con sé tutto il necessario.

Il progetto include un `Directory.Build.props` alla root che imposta
`MSBuildEnableWorkloadResolver=false` automaticamente — non serve né
installare Mono né aggiungere variabili d'ambiente al profilo di shell.
Il semplice `dotnet build` funziona senza configurazione aggiuntiva.
:::

## Librerie di sistema GTK4

### Debian / Ubuntu
```bash
sudo apt install libgtk-4-dev libwebkitgtk-6.0-dev
```

### Fedora
```bash
sudo dnf install gtk4-devel webkitgtk6.0-devel
```

### Arch / EndeavourOS / Manjaro
```bash
sudo pacman -S webkitgtk-6.0   # gtk4 è già incluso in base
```

:::note WebKitGTK
`libwebkitgtk-6.0-dev` è necessario per la `WebView`. Senza di esso la WebView
non verrà renderizzata e l'applicazione potrebbe andare in crash all'avvio.
:::

:::tip GitHub Actions
Il workflow CI del repository (`.github/workflows/ci.yml`) usa runner `ubuntu-24.04`
e installa automaticamente questi pacchetti con `apt-get`, quindi non è necessario
preconfigurare il runner oltre a `actions/setup-dotnet` e `actions/setup-node`.
:::

## Node.js e pnpm (solo per la documentazione)

La documentazione usa [Docusaurus](https://docusaurus.io) e richiede **pnpm 11**, che a sua
volta richiede **Node.js ≥ 22.13**. Sono le stesse versioni della CI.

```bash
# Verifica versioni
node --version    # ≥ 22.13
pnpm --version    # 11.x

# Installazione pnpm (se non presente)
npm install -g pnpm
```

Da pnpm 11 le impostazioni non si leggono più dal campo `"pnpm"` di `package.json` ma da
`pnpm-workspace.yaml`. Il repository ne ha due: quello alla radice (tooling OpenSpec) e
`docs/pnpm-workspace.yaml`, che tiene `docs/` separato dal progetto della radice, ognuno
con il proprio `pnpm-lock.yaml`.

## Editor consigliato

- **JetBrains Rider** — supporto nativo C# + XAML
- **VS Code** con estensioni C# Dev Kit e .NET MAUI
