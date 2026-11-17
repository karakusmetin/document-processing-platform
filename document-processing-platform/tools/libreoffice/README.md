# Bundled LibreOffice Windows runtime

The Office adapter launches the official LibreOffice Community runtime from the application artifact. Customers do not install LibreOffice and the adapter does not resolve it from PATH.

## Prepare the pinned runtime

Download the official MSI referenced by `runtime-manifest.win-x64.json`. Run PowerShell with permission to perform Windows Installer administrative extraction:

```powershell
./tools/libreoffice/bootstrap-libreoffice.ps1 `
  -MsiPath ./artifacts/runtime-source/LibreOffice_26.2.5_Win_x86-64.msi `
  -TargetDirectory ./artifacts/libreoffice-extract/26.2.5-win-x64
```

The script verifies the pinned SHA-256 and executable version, extracts with `msiexec /a` (not a normal installation), and copies the complete runtime to the Console sample and Worker. The administrative image's MSI is excluded from the runtime. Runtime binaries are ignored by Git; the manifest and preparation script are versioned.

The script also generates a minimal DOCX and performs one real DOCX-to-PDF conversion. The resulting profile is stored as `profile-template.zip` beside the runtime and extracted to a job-specific profile directory for every conversion. This avoids Windows path-length problems, keeps concurrent jobs isolated, and prevents LibreOffice's first-run profile initialization cost from being paid for every document.

Use a separate, empty extraction directory when changing versions. Do not overlay a new release onto an older staged runtime. Prepare a fresh staging directory using `-DestinationDirectory` for version upgrades.

## Distribute

```powershell
dotnet publish ./src/DocumentProcessing.Worker/DocumentProcessing.Worker.csproj -c Release -o ./artifacts/publish/worker
```

Worker runtime files are copied during publish, not during normal build/test. Distribute the entire publish directory, including `runtimes/libreoffice/win-x64`, configuration, `LICENSE.html`, `license.txt`, `NOTICE`, and other upstream runtime files. A converter DLL alone cannot contain this CLI runtime.

The default publish above is framework-dependent and needs the matching .NET runtime. A self-contained .NET publish is a separate distribution choice; it does not remove the need to carry LibreOffice's runtime directory.

## Smoke test

```powershell
dotnet run --project ./samples/DocumentProcessing.Sample.Console -- ./input.docx ./artifacts/output
```

The sample resolves its configuration and LibreOffice runtime from `AppContext.BaseDirectory`. It supports DOC, DOCX, RTF, XLS, XLSX, PPT and PPTX for Office-to-PDF routing. Successful conversion verifies basic PDF output; visual compatibility still requires a document corpus.

This preparation path covers Windows x64. Linux uses a separate packaged runtime and `program/soffice`; the Windows MSI is not a Linux runtime. Preserve upstream notices and complete redistribution/source-availability obligations before a product release.
