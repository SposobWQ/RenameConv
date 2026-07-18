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
- FFmpeg (included with Release)
- LibreOffice (included with Release)

---

## 📜 License

MIT License

---

Made with ❤️ in C#