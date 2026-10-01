# Maintenance checkpoint — 2026-09-09

## Current checkpoint — 2026-10-01, v0.7.2 SupCraft

## Build and package

On Windows, run `powershell -ExecutionPolicy Bypass -File build.ps1` in this directory.
The .NET Framework compiler creates `app/SupCraft.exe`; all bundled
conversion dependencies are copied from `third_party`, including VSFilterMod.
The build no longer requires an old AssRocket directory or an existing app folder.
The AviSynth workflow still requires installed 32-bit AviSynth+; SUPer needs 64-bit Windows.

Run `powershell -ExecutionPolicy Bypass -File package.ps1` to build and save the
runtime ZIP, complete source ZIP (including dependency binaries), and SHA256SUMS.txt
in the parent directory. Runtime archives omit logs, test outputs and user subtitles.
The source archive excludes nested Git metadata; extracted source can build independently.

## Licensing materials — 2026-10-01

- The copyright holder authorizes unrestricted use of SupCraft's own code; LICENSE is 0BSD, with explicit third-party scope exclusions in LICENSING.md.
- THIRD-PARTY-NOTICES.md credits upstream authors; legal/ retains original notices, five pinned complete upstream archives, bundled avs2bdnxml source, integrity indexes and modification/source status records.
- Existing GPL-derived SUPer files retain upstream GPL notices. No third-party component is relicensed as 0BSD.
- package.ps1 includes materials in Windows runtime and source snapshots. Mac v0.7.2 collects actual Python/NuGet notices and native build records, includes legal materials before signing and emits a separate legal ZIP.
- Unverified historical DLL/native source provenance remains documented; these additions are not a blanket compliance certification.

## macOS v0.7.2

`macos/` contains the independent .NET 10 / Avalonia desktop application and portable conversion core. It uses only libass / SUPer, never AviSynth. GPU configuration is fixed to enabled; the Python integration logs OpenCL failures and falls back to CPU. Directory fonts are passed to libass without system registration. Native packaging runs only on macOS 15+ Apple Silicon (arm64); the build script rejects other architectures and `.github/workflows/macos.yml` has a single arm64 job. The desktop now has a blue-gray grid, navy header, ruled task table, bordered settings and persistent progress/log panels. See `macos/README.md` for dependencies and commands. Exclude `macos/vendor`, `macos/dist`, `bin` and `obj` from source archives.

## Main files and accepted behavior

- `src/Program.cs`: WinForms UI, font checks, both conversion pipelines and output validation.
- `src/app.manifest`: application manifest.
- `ui-layout-check.ps1`: x86 PowerShell layout checks, with optional `-FontSize 14`.
- Wide windows show parameters on one line; narrower windows wrap whole groups.
- Parameter box height follows content height, with bottom padding at larger font sizes.
- Progress bar fills the content width; status text is below it.
- PyOpenCL remains bundled as requested. GPU use depends on upstream support and drivers.
- 1440p/2160p BDN currently scales 1080p images and coordinates; it is not native high-resolution libass rendering.
- SUP remains limited to the resolutions supported by the bundled encoder.

## Font recognition fix — 2026-09-10

- Read localized family, full, PostScript and typographic names from installed TTF/OTF files and every TTC face.
- Resolve relative per-user registry paths in the per-user font directory and ignore nonexistent registry targets.
- Use the same lookup for preflight, font reports and batch conversion. Preserve punctuation to avoid merging distinct names; trim whitespace and vertical-font prefixes.
- Collect fonts by exact alias instead of substring guessing.
- Built with the .NET Framework x86 compiler. Verified all 36 referenced names in the supplied 12 ASS files against local font files; checked TTF/OTF/TTC aliases, vertical prefixes, nonexistent names and punctuation distinctions in x86 PowerShell. Rendering output was not tested.
- package.ps1 writes separate v0.4-fontfix runtime/source archives, retaining the original v0.4 archives.
