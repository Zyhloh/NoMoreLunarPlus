# No More Lunar+

A small Windows console tool that removes ads and Lunar+ / store upsells from the Lunar Client launcher, so it's just a launcher.

It patches your local launcher install in place, keeps a backup of the original files, and can put everything back with one command.

> **Please read the [Disclaimer](#disclaimer) before using this.**

## What it removes

| Area | Removed |
| --- | --- |
| Ads | Every launcher ad slot (home, explore, mission control, satellite), the Lunar+ banner under each ad, Overwolf notification ads, the "Optimized Ads" and "Ads & Data" settings |
| Promotions | "Watch a video for a free cosmetic" promotions in the sidebar, home and news feed |
| Store | Sidebar store button, store entry in search, store entry in the tray menu, store promos in the carousel, news and navigation |
| Coins | The coins shop button in the top bar |
| Radio | The "Upgrade to Premium" button and the subscribe banner in station selection |
| Chat | Lunar+ upsells on the message limit, group chat slots and best friend limit |

Everything else in the launcher works as normal. Each group can be turned off individually in `config.json`.

## How it works

The launcher is an Electron app. Its UI and logic live in `resources\app.asar`.

1. Finds your Lunar Client install (registry uninstall entries for every user, plus the usual install folders), or asks you for the folder.
2. Closes the launcher if it's running.
3. Backs up the stock `app.asar` and `Lunar Client.exe` to `C:\ProgramData\NoMoreLunarPlus\backups`.
4. Applies a set of targeted patches to the launcher's JavaScript and rebuilds `app.asar` with correct per-file integrity data.
5. Updates the asar header hash embedded in `Lunar Client.exe` so Electron's integrity check accepts the patched archive.

Patches are matched by pattern rather than exact positions, so they usually survive small launcher updates. If a patch doesn't match a newer version, it is skipped and reported instead of breaking anything.

## Requirements

- Windows 10 or 11 (x64)
- Lunar Client installed
- To build: [.NET 10 SDK](https://dotnet.microsoft.com/download) and the Visual Studio **Desktop development with C++** workload (needed for Native AOT)

## Building

No prebuilt binaries are provided. Build it yourself:

```
git clone https://github.com/Zyhloh/NoMoreLunarPlus.git
cd NoMoreLunarPlus
dotnet publish src/NoMoreLunarPlus -c Release -o publish
```

The result is a single self-contained executable at `publish\NoMoreLunarPlus.exe`. It does not need .NET installed to run.

If the build fails at the linking step with `'vswhere.exe' is not recognized`, add `C:\Program Files (x86)\Microsoft Visual Studio\Installer` to your `PATH` and build again.

## Usage

Run `NoMoreLunarPlus.exe` as administrator. It will find your install, close Lunar Client, patch it and show the result of every patch.

If the install is already patched, it asks whether you want to re-apply the patches, restore the stock launcher, or quit.

Command line options:

| Option | Description |
| --- | --- |
| `--patch` | Patch without prompting (re-applies if already patched) |
| `--restore` | Restore the stock launcher from the backup |
| `--check` | Dry run: show which patches match, change nothing |
| `--path <folder>` | Use a specific Lunar Client install folder |
| `--no-pause` | Don't wait for a key press before exiting |

### config.json

Created next to the executable on first run.

```json
{
  "installPath": "",
  "pauseOnExit": true,
  "features": {
    "ads": true,
    "promotions": true,
    "store": true,
    "coins": true,
    "radio": true,
    "chat": true
  }
}
```

- `installPath`: leave empty to auto-detect, or set it to your Lunar Client folder.
- `features`: set any group to `false` to leave that part of the launcher untouched.

### After a Lunar Client update

Launcher updates replace the patched files. Run the tool again after updating.

### Restoring

Run `NoMoreLunarPlus.exe --restore`, or run it normally and choose restore. Reinstalling Lunar Client also restores it completely.

## Known side effects

- Overwolf's packages (its ad engine and in-game overlay) no longer load in a patched launcher. The only Lunar Client feature that uses the overlay is in-game store checkout.
- `Lunar Client.exe` loses its valid code signature because its embedded integrity hash is changed. It still runs normally.
- Lunar Client's in-game (Minecraft) UI is not modified. This only touches the launcher.

## Disclaimer

This project is not affiliated with, endorsed by, sponsored by, or associated with Moonsworth LLC, Lunar Client, or Overwolf Ltd. "Lunar Client", "Lunar+" and related names and marks are the property of their respective owners and are used here only to describe what this software works with.

This repository contains no Lunar Client or Overwolf code or binaries. The tool only modifies files that already exist on the user's own computer, and no prebuilt executables are distributed.

This software is provided for educational and personal use, "as is", without warranty of any kind. Modifying the Lunar Client launcher may violate Lunar Client's Terms of Service and could result in action against your account. By building or using this software you accept full responsibility for doing so. The author is not responsible for how this software is used, or for any consequences of using it, including but not limited to damaged installs, data loss, account restrictions or bans, or any other loss or damage.

If you are a rights holder and have a concern about this project, please open an issue.

## License

[MIT](LICENSE)
