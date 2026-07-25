# NetVsMcp Broker Installer

This project builds a per-user MSI for the NetVsMcp Broker tray app using WiX Toolset SDK.

Build the MSI:

```powershell
dotnet build .\src\NetVsMcp.Installer\NetVsMcp.Installer.wixproj -c Release
```

The installer project publishes `NetVsMcp.Broker` before packaging. By default the broker publish output is written to:

```text
artifacts\publish\NetVsMcp.Broker
```

Override the product version or broker publish directory when needed:

```powershell
dotnet build .\src\NetVsMcp.Installer\NetVsMcp.Installer.wixproj -c Release /p:ProductVersion=0.2.0
dotnet build .\src\NetVsMcp.Installer\NetVsMcp.Installer.wixproj -c Release /p:BrokerPublishDir=C:\Temp\NetVsMcp.Broker\
```

The MSI installs the tray app under `%LocalAppData%\NetVsMcp\Broker` (a true per-user install, so no UAC elevation is required), adds a Start Menu shortcut, and installs the MIT license as `LICENSE.txt`.
