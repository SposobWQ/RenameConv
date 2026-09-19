# RenameConv

> 🚀 Automatically converts files when you change their extension in Windows Explorer.

RenameConv is a lightweight Windows background application that watches your file system. Simply rename a file from, for example, `movie.mkv` to `movie.webm`, and RenameConv automatically converts it in the background using FFmpeg.

No console windows. No manual conversion. Just rename the file.

---

## ✨ Features

- 🔄 Automatic conversion after renaming a file extension
- ⚡ Fast remuxing when transcoding is unnecessary
- 🎬 Powered by FFmpeg
- 🖥️ Runs silently in the Windows system tray
- 💾 Supports local, removable and network drives
- 📁 Can watch specific folders or the entire system
- 🛡️ Safe conversion using temporary files
- ⚙️ Settings opened from the tray icon
- 🌐 Russian and English interfaces
- 🎨 System, light, and dark themes with a selectable tray-icon colour
- ⬇️ Automatic download of FFmpeg and LibreOffice Portable when needed
- 🔄 In-app application and dependency updates

---

## 📸 How it works

1. Start `RenameConv.exe`.
2. Rename a file in Windows Explorer.

Example:

```text
movie.mkv
      ↓ rename
movie.webm
```

RenameConv detects the extension change and:

- checks the actual file format;
- attempts fast stream copy (remux);
- automatically falls back to transcoding if needed;
- replaces the renamed file only after successful conversion.

---

## 🚀 Usage

### Watch all drives

```bash
RenameConv.exe
```

Automatically monitors:

- Local drives
- USB drives
- Network drives
- Newly connected drives

### Watch specific folders

```bash
RenameConv.exe "D:\Videos" "E:\Music"
```

---

## 📦 Supported Formats

### 🎥 Video

- MP4
- MKV
- AVI
- MOV
- WEBM
- FLV
- WMV
- MPG
- MPEG
- M4V
- 3GP
- OGV

### 🎵 Audio

- MP3
- WAV
- FLAC
- AAC
- M4A
- OGG
- OPUS
- WMA
- AIFF

### 🖼 Images

- JPG / JPEG
- PNG
- WEBP
- BMP
- TIFF
- GIF

### 📄 Documents

- PDF
- DOC / DOCX
- ODT
- RTF
- TXT
- HTML
- XLS / XLSX
- ODS
- CSV
- PPT / PPTX
- ODP

---

## ⚙️ How conversion works

RenameConv doesn't rely only on the file extension.

Instead, it:

- detects the real file format;
- selects the optimal conversion method;
- preserves the original file until conversion completes successfully.

If remuxing is impossible, FFmpeg automatically performs transcoding.

---

## ⚠️ Limitations

Some files cannot be converted because of:

- unsupported codecs;
- DRM protection;
- encrypted media;
- unsupported container features.

Errors are written to:

```text
%LOCALAPPDATA%\RenameConv\RenameConv.log
```

---

## 🛠 Requirements

- Windows 10/11
- x64 processor
- Internet access only for the compact build's first download of FFmpeg or LibreOffice

## 📦 Builds

Two Windows x64 packages are available:

- **Portable** — FFmpeg and LibreOffice Portable are already included. Extract the entire archive to an ASCII-only path, for example `C:\RenameConvPortable`, and run `RenameConv.exe`.
- **Online** — smaller package. FFmpeg downloads automatically at launch when absent; LibreOffice Portable downloads when document conversion first requires it. Downloaded tools are stored in `%LOCALAPPDATA%\RenameConv\tools`.

Both packages contain `RenameConvUpdater.exe`. RenameConv checks GitHub releases, downloads the matching ZIP update, closes itself, applies the update, and restarts without opening a browser.

---

## 📜 License

MIT License

---

Made with ❤️ in C#

---

## Version history

See [CHANGELOG.md](CHANGELOG.md) for the complete history beginning with version 1.0.0.

## Project Layout

```text
Models/       Conversion data models
Services/     Conversion, file watcher, log, and dependency services
Updates/      GitHub release discovery and update dialog
Dependencies/ FFmpeg and LibreOffice Portable installation and refresh
Localization/ Russian and English interface strings
Personalization/ Theme and tray-icon appearance
RenameConvUpdater/  External updater project next to this repository
```

Downloaded tools are kept separately in `%LOCALAPPDATA%\RenameConv\tools`, so the portable application folder remains movable. Automatic dependency updates are optional in Settings; LibreOffice is a large download and is fetched only when required or when its update check is enabled.

## Release Package

Run `C:\Users\SposobWQ\Documents\publish-portable.ps1` to build the portable release. It creates `RenameConvPortable-1.1.0-win-x64.zip` in `C:\Users\SposobWQ\Documents\publish`. Build the online package with `IncludeBundledTools=false`; it must contain `RenameConv.exe` and `RenameConvUpdater.exe`, but no `tools` folder.

Upload that ZIP as the GitHub release asset. RenameConv selects this asset automatically when checking for a newer release.
