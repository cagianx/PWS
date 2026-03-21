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
`Platform.Maui.Linux.Gtk4` che porta con sé tutto il necessario.

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

## Node.js e pnpm (solo per la documentazione)

La documentazione usa [Docusaurus](https://docusaurus.io) e richiede **Node.js ≥ 18** e **pnpm**.

```bash
# Verifica versioni
node --version    # ≥ 18
pnpm --version    # qualsiasi versione recente

# Installazione pnpm (se non presente)
npm install -g pnpm
```

## Editor consigliato

- **JetBrains Rider** — supporto nativo C# + XAML
- **VS Code** con estensioni C# Dev Kit e .NET MAUI

