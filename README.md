# Open Port Explorer WIN

<p align="center">
  <a href="https://github.com/jomardyan/Open-Port-Explorer-WIN/releases/latest">
    <img src="https://img.shields.io/github/v/release/jomardyan/Open-Port-Explorer-WIN?label=Latest%20Release&style=for-the-badge" alt="Latest Release">
  </a>
  <a href="https://github.com/jomardyan/Open-Port-Explorer-WIN/releases/latest">
    <img src="https://img.shields.io/github/downloads/jomardyan/Open-Port-Explorer-WIN/total?style=for-the-badge" alt="Total Downloads">
  </a>
  <a href="https://github.com/jomardyan/Open-Port-Explorer-WIN/actions/workflows/build-and-publish.yml">
    <img src="https://img.shields.io/github/actions/workflow/status/jomardyan/Open-Port-Explorer-WIN/build-and-publish.yml?branch=main&label=Build&style=for-the-badge" alt="Build Status">
  </a>
  <a href="LICENSE.txt">
    <img src="https://img.shields.io/github/license/jomardyan/Open-Port-Explorer-WIN?style=for-the-badge" alt="License">
  </a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D6?style=for-the-badge&logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 10">
</p>

**Open Port Explorer WIN** is a powerful, real-time Windows desktop application for monitoring open network ports, established connections, and their associated processes. Built with WPF and C# on .NET 10, it gives you a comprehensive, at-a-glance view of your system's network activity — complete with advanced filtering, custom rule management, suspicion scoring, and historical tracking.

---

## ⬇️ Download (Pre-built)

> **No installation required.** The release ships as a single, self-contained `.exe` — just download and run.

1. Go to the [**Releases page**](https://github.com/jomardyan/Open-Port-Explorer-WIN/releases/latest).
2. Under **Assets**, download **`Open.Port.Explorer.WIN.exe`**.
3. (Optional) Right-click the file → **Properties** → click **Unblock** if Windows SmartScreen prompts you.
4. Right-click the `.exe` → **Run as administrator** for full functionality.

> **Requirements:** Windows 10 or Windows 11 (x64). No separate .NET installation needed — the runtime is bundled inside the executable.

---

## ✨ Features

| Category | Details |
|---|---|
| **Real-Time Monitoring** | Auto-refreshes to show listening and established connections (TCP/UDP, IPv4/IPv6) |
| **Process Mapping** | Maps every connection to its process — PID, executable name, and full file path |
| **Port & Process Descriptions** | Built-in database of ~200 well-known ports and ~550 common processes to identify legitimate vs. suspicious traffic |
| **Rules & Watchlists** | Mark ports or processes as **Trusted**, **Blocked**, or **Watched** |
| **Suspicion Scoring** | Automatically flags risky ports, non-loopback listeners, and connections with missing process metadata |
| **Snapshot Baselines** | Capture a "known good" state and diff it against the current view to spot new or closed ports |
| **Activity History** | Persistent log of connection events: opened, closed, state changes, and alerts |
| **Advanced Filtering** | Filter by port, address, process name, PID, protocol, IP family, connection state, and rule status |
| **Data Export** | Export connection lists to **CSV** or **JSON**; save activity logs to **text files** |
| **Dark / Light Themes** | Toggle UI themes; preference is saved automatically |
| **Process Management** | Open the executable location in Explorer or forcefully terminate a process directly from the app |

---

## 🖥️ System Requirements

- **OS:** Windows 10 (x64) or Windows 11 (x64)
- **Runtime:** None — the self-contained executable bundles the .NET 10 runtime
- **Privileges:** Standard user is supported, but **Run as administrator** is strongly recommended to read full process metadata and allow process termination

---

## 🚀 Quick Start

1. [Download the latest release](#️-download-pre-built) (see above).
2. Run **`Open.Port.Explorer.WIN.exe`** (ideally as administrator).
3. The connection grid populates automatically. Use the toolbar controls to filter, set rules, or export data.

---

## 📖 Usage Guide

### Auto-Refresh
Use the interval dropdown in the toolbar to set the refresh rate (1–30 seconds), or toggle auto-refresh on/off entirely.

### Setting Rules
1. Select a connection row in the main grid.
2. Click **Watch**, **Trust**, or **Block** in the toolbar.
3. Use the **Rule Target** dropdown to scope the rule to that specific **Port** or the associated **Process**.

### Viewing Connection Details
Click any row to expand the detail panel on the right side, which shows full port descriptions, process information (path, PID), and resolved hostnames.

### Snapshots & Baselines
Click **Set Baseline** to capture the current state. Later, use **Compare** to see exactly which ports were opened or closed since the snapshot.

### Administrator Elevation
If the application was not started with admin privileges, an amber banner is shown at the top of the window. Click it to re-launch the app elevated.

---

## 💾 Data Storage

All settings — rules, watchlists, and theme preferences — are stored locally at:

```
%LOCALAPPDATA%\OpenPortExplorerWin\preferences.json
```

No data is sent outside your machine.

---

## 🔧 Building from Source

If you prefer to compile the application yourself:

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (includes pre-release if needed)
- Windows 10/11 (required for WPF)
- Visual Studio 2022 or later (recommended) **or** the .NET CLI

### Steps

```bash
# Clone the repository
git clone https://github.com/jomardyan/Open-Port-Explorer-WIN.git
cd Open-Port-Explorer-WIN

# Restore dependencies
dotnet restore "Open Port Explorer WIN.slnx" --runtime win-x64

# Build (Debug)
dotnet build "Open Port Explorer WIN.slnx" -p:EnableWindowsTargeting=true

# Publish as a single self-contained executable (Release)
dotnet publish "Open Port Explorer WIN/Open Port Explorer WIN.csproj" \
  --configuration Release \
  --runtime win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  --output ./publish
```

The compiled executable will be at `./publish/Open Port Explorer WIN.exe`.

Alternatively, open **`Open Port Explorer WIN.slnx`** in Visual Studio and press **F5** to build and run.

---

## 🤝 Contributing

Contributions, bug reports, and feature requests are welcome!

1. [Open an issue](https://github.com/jomardyan/Open-Port-Explorer-WIN/issues) to discuss your idea or report a bug.
2. Fork the repository and create a feature branch.
3. Submit a pull request with a clear description of the change.

---

## 📄 License

Distributed under the terms of the [LICENSE](LICENSE.txt) file in this repository.
