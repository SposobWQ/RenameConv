# Changelog

All notable changes to RenameConv are documented in this file.

## [1.1.0] - 2026-09-12

### Added

- Settings window opened from the system-tray icon.
- Choice between watching all available drives and selected folders.
- Persistent settings in `%LOCALAPPDATA%\RenameConv\settings.json`.
- Russian and English interface languages.
- System, light, and dark application themes.
- Selectable tray-icon colours: blue, violet, green, and orange.
- In-app check, download, and installation of application updates through `RenameConvUpdater.exe`.
- Automatic download of FFmpeg when it is unavailable.
- Automatic installation of LibreOffice Portable when document conversion needs it.
- Manual dependency refresh from the tray and optional refresh check at application startup.
- Portable package with embedded FFmpeg and LibreOffice Portable.
- Compact online package that downloads only the libraries it needs.

### Changed

- Refactored the application from one source file into models, services, localization, personalization, dependency, and update components.
- Tool discovery now prioritizes downloaded managed tools and then portable bundled tools.
- Release application is a windowless Windows executable.
- Update installation validates archive paths, waits for RenameConv to exit, preserves the running updater executable, and restarts the application after replacement.

### Fixed

- Conversion and watcher work continue in the background without a console window.
- Conversion safety is retained through temporary output files and replacement only after success.

## [1.0.1] - 2026-08-30

### Changed

- Improved queue processing and error handling during conversion.
- Updated the portable publication script.

## [1.0.0] - 2026-08-30

### Added

- Initial release of the background extension-rename converter.
- Recursive monitoring of local, removable, and network drives.
- Optional command-line monitoring of selected folders.
- Media, image, audio, and document conversion support.
- FFmpeg remuxing with automatic transcoding fallback.
- Safe temporary-file conversion and activity log in `%LOCALAPPDATA%\RenameConv\RenameConv.log`.
