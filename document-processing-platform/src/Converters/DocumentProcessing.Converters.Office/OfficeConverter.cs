using System.IO.Compression;
using DocumentProcessing.Contracts.Converter;
using DocumentProcessing.Converters.Office.Options;
using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Core.Providers;
using DocumentProcessing.Infrastructure.ProcessExecution;
using Microsoft.Extensions.Options;

namespace DocumentProcessing.Converters.Office
{
    public sealed class OfficeConverter(IOptions<LibreOfficeOptions> options, IExternalProcessRunner processRunner) : ConverterProviderBase
    {
        private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

        private readonly LibreOfficeOptions _options = options.Value;

        public override string Name => "Office Converter";

        public override bool CanConvert(DocumentType sourceType, DocumentType targetType)
        {
            bool supportedSource = FileFormat.OfficeFormats.ContainsKey(sourceType);

            return supportedSource && targetType == DocumentType.Pdf;
        }

        protected override async Task<byte[]> ConvertCoreAsync(ConverterRequest request, CancellationToken cancellationToken)
        {
            string sofficePath = ResolveExecutablePath();
            if (!File.Exists(sofficePath))
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.RuntimeNotFound,
                    $"Bundled LibreOffice runtime bulunamadı: {sofficePath}");
            }

            string profileTemplatePath = ResolveProfileTemplatePath();
            if (!File.Exists(profileTemplatePath))
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.RuntimeNotFound,
                    $"LibreOffice profil şablonu bulunamadı: {profileTemplatePath}");
            }

            string workDir = Path.Combine(
                _options.WorkspaceRoot ?? Path.GetTempPath(),
                "docproc-office",
                Guid.NewGuid().ToString("N"));
            string inputDir = Path.Combine(workDir, "input");
            string outputDir = Path.Combine(workDir, "output");
            string profileDir = Path.Combine(workDir, "profile");

            try
            {
                Directory.CreateDirectory(inputDir);
                Directory.CreateDirectory(outputDir);
                ExtractProfileTemplate(profileTemplatePath, profileDir, cancellationToken);

                string extension = GetInputExtension(request.SourceType);
                string inputFile = Path.Combine(inputDir, $"document{extension}");
                await File.WriteAllBytesAsync(inputFile, request.Content, cancellationToken);
                string outputFile = Path.Combine(outputDir, "document.pdf");

                await RunSofficeAsync(sofficePath, inputFile, outputDir, profileDir, request.SourceType, cancellationToken);

                return await ReadAndValidateOutputAsync(outputFile, cancellationToken);
            }
            finally
            {
                TryDeleteDirectory(workDir);
            }
        }

        private async Task RunSofficeAsync(string sofficePath, string inputFile, string outputDir, string profileDir, DocumentType sourceType, CancellationToken cancellationToken)
        {
            string profileUri = new Uri(profileDir).AbsoluteUri;

            ProcessRunRequest runRequest = new()
            {
                FileName = sofficePath,
                Arguments =
                [
                    "--headless",
                    "--nologo",
                    "--nodefault",
                    "--norestore",
                    "--nofirststartwizard",
                    $"-env:UserInstallation={profileUri}",
                    "--convert-to",
                    GetExportFilter(sourceType),
                    "--outdir",
                    outputDir,
                    inputFile
                ],
                Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
            };

            ProcessRunResult result;
            try
            {
                result = await processRunner.RunAsync(runRequest, cancellationToken);
            }
            catch (Exception ex) when (ex is not ConverterOperationException and not OperationCanceledException)
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.ProcessStartFailed,
                    $"LibreOffice process başlatılamadı: {ex.Message}",
                    ex);
            }

            if (result.TimedOut)
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.ConversionTimeout,
                    $"LibreOffice dönüşümü {_options.TimeoutSeconds} saniye içinde tamamlanmadı.");
            }

            if (result.ExitCode != 0)
            {
                string stdErr = result.StandardError;
                string truncatedError = stdErr.Length > 2000 ? stdErr[..2000] : stdErr;
                throw new ConverterOperationException(
                    ConversionErrorCodes.EngineCrashed,
                    $"LibreOffice çıkış kodu {result.ExitCode}: {truncatedError}");
            }
        }

        private static async Task<byte[]> ReadAndValidateOutputAsync(string outputFile, CancellationToken cancellationToken)
        {
            if (!File.Exists(outputFile))
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.OutputNotProduced,
                    "LibreOffice PDF çıktısı üretmedi.");
            }

            byte[] content = await File.ReadAllBytesAsync(outputFile, cancellationToken);

            if (content.Length == 0 ||
                content.Length < PdfSignature.Length ||
                !content.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature))
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.ConversionOutputInvalid,
                    "Üretilen çıktı geçerli bir PDF değil.");
            }

            return content;
        }

        private static string GetInputExtension(DocumentType sourceType) =>
            FileFormat.OfficeFormats.TryGetValue(sourceType, out FileFormat? format)
                ? format.DefaultExtension
                : throw new ConverterOperationException(
                ConversionErrorCodes.UnsupportedFormat,
                $"LibreOffice motoru {sourceType} formatını desteklemiyor.");

        private static string GetExportFilter(DocumentType sourceType) => sourceType switch
        {
            DocumentType.Xls or DocumentType.Xlsx => "pdf:calc_pdf_Export",
            DocumentType.Ppt or DocumentType.Pptx => "pdf:impress_pdf_Export",
            _ => "pdf:writer_pdf_Export"
        };

        private string ResolveExecutablePath()
        {
            string runtimeRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, _options.RuntimeRelativePath));

            return ResolvePathWithinRuntime(
                runtimeRoot,
                _options.ExecutableRelativePath,
                "LibreOffice executable path runtime directory dışına çıkamaz.");
        }

        private string ResolveProfileTemplatePath()
        {
            string runtimeRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, _options.RuntimeRelativePath));

            return ResolvePathWithinRuntime(
                runtimeRoot,
                _options.ProfileTemplateRelativePath,
                "LibreOffice profile template path runtime directory dışına çıkamaz.");
        }

        private static string ResolvePathWithinRuntime(string runtimeRoot, string relativePath, string errorMessage)
        {
            string resolvedPath = Path.GetFullPath(Path.Combine(runtimeRoot, relativePath));
            string relativeToRuntime = Path.GetRelativePath(runtimeRoot, resolvedPath);

            if (relativeToRuntime == ".." ||
                relativeToRuntime.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.RuntimeNotFound,
                    errorMessage);
            }

            return resolvedPath;
        }

        private static void ExtractProfileTemplate(string archivePath, string destination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ZipFile.ExtractToDirectory(archivePath, destination, overwriteFiles: true);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                throw new ConverterOperationException(
                    ConversionErrorCodes.RuntimeNotFound,
                    $"LibreOffice profil şablonu açılamadı: {ex.Message}",
                    ex);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
