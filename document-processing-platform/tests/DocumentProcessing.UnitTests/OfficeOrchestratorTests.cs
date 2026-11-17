using System.IO.Compression;
using System.Text;
using DocumentProcessing.Contracts.Converter;
using DocumentProcessing.Conversion.Detection;
using DocumentProcessing.Conversion.Errors;
using DocumentProcessing.Conversion.Providers;
using DocumentProcessing.Conversion.Services;
using DocumentProcessing.Conversion.Validation;
using DocumentProcessing.Converters.Office;
using DocumentProcessing.Converters.Office.Options;
using DocumentProcessing.Converters.Office.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;
using DocumentProcessing.Infrastructure.ProcessExecution;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DocumentProcessing.UnitTests;

public sealed class OfficeOrchestratorTests
{
    public static IEnumerable<object[]> Formats => FileFormat.OfficeFormats.Keys.Select(t => new object[] { t });

    [Fact]
    public async Task InvalidRequest_UsesStandardizedFailureContract()
    {
        Store store = new();
        ConversionOrchestrator orchestrator = CreateOrchestrator(
            new Source(Input(DocumentType.Docx)),
            store,
            [],
            [new Converter()]);

        ConversionExecutionResult result = await orchestrator.ExecuteAsync(
            Request(DocumentType.Docx) with { JobId = Guid.Empty },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ConversionErrorCodes.InvalidRequest, result.ErrorCode);
        Assert.Equal("Conversion request is invalid.", result.ErrorMessage);
        Assert.Equal("request-validation", result.FailedStage);
        Assert.False(result.Retryable);
        Assert.Null(store.Content);
    }

    [Fact]
    public void FailureMapper_DoesNotExposeInternalExceptionDetails()
    {
        ConversionFailureMapper mapper = new(
            NullLogger<ConversionFailureMapper>.Instance,
            new ResourceConversionErrorMessageProvider());

        ConversionExecutionResult result = mapper.FromException(
            new IOException("C:\\secret\\internal-path"),
            ConversionStage.ArtifactWrite);

        Assert.Equal(ConversionErrorCodes.ArtifactWriteFailed, result.ErrorCode);
        Assert.Equal("The converted document could not be stored.", result.ErrorMessage);
        Assert.DoesNotContain("secret", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Retryable);
        Assert.Equal("artifact-write", result.FailedStage);
    }

    [Theory]
    [InlineData(ConversionStage.SourceResolve, ConversionErrorCodes.SourceReadFailed)]
    [InlineData(ConversionStage.FormatDetection, ConversionErrorCodes.SourceReadFailed)]
    [InlineData(ConversionStage.Conversion, ConversionErrorCodes.ConversionIoFailed)]
    public void FailureMapper_MapsTransientIoFailuresAsRetryable(
        ConversionStage stage,
        string expectedCode)
    {
        ConversionFailureMapper mapper = new(
            NullLogger<ConversionFailureMapper>.Instance,
            new ResourceConversionErrorMessageProvider());

        ConversionExecutionResult result = mapper.FromException(new IOException("temporary"), stage);

        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.True(result.Retryable);
    }

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task OfficeFormats_DetectRouteAndStoreConverterOutput(DocumentType type)
    {
        byte[] input = Input(type);
        Source source = new(input);
        Converter converter = new();
        Store store = new();
        ConversionOrchestrator orchestrator = CreateOrchestrator(source, store, [], [converter]);
        ConversionExecutionResult result = await orchestrator.ExecuteAsync(Request(type), CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(type, converter.Request!.SourceType);
        Assert.Equal(DocumentType.Pdf, converter.Request.TargetType);
        Assert.Equal(input, converter.Request.Content);
        Assert.Equal(converter.Result.Content, store.Content);
        Assert.Equal(converter.Name, result.Provider);
        Assert.Equal("hash", result.OutputSha256);
        Assert.True(new OfficeConverter(Options.Create(new LibreOfficeOptions()), new UnusedRunner()).CanConvert(type, DocumentType.Pdf));
    }

    [Theory]
    [InlineData(DocumentType.Txt)]
    [InlineData(DocumentType.Csv)]
    public async Task TextFormats_DetectAndRouteToConverter(DocumentType type)
    {
        Converter converter = new();
        Store store = new();
        ConversionOrchestrator orchestrator = CreateOrchestrator(
            new Source(Input(type)),
            store,
            [],
            [converter]);

        ConversionExecutionResult result = await orchestrator.ExecuteAsync(
            Request(type),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(type, converter.Request!.SourceType);
        Assert.NotNull(store.Content);
    }

    [Fact]
    public async Task ConverterFailure_PreservesCodeAndDoesNotStore()
    {
        Converter converter = new() { Result = new() { ErrorCode = ConversionErrorCodes.ConversionTimeout, ErrorMessage = "timeout" } };
        Store store = new();
        ConversionOrchestrator orchestrator = CreateOrchestrator(new Source(Input(DocumentType.Docx)), store, [], [converter]);
        var result = await orchestrator.ExecuteAsync(Request(DocumentType.Docx), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ConversionErrorCodes.ConversionTimeout, result.ErrorCode);
        Assert.Equal("Document conversion timed out.", result.ErrorMessage);
        Assert.Equal("conversion", result.FailedStage);
        Assert.True(result.Retryable);
        Assert.Null(store.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a PDF")]
    public async Task InvalidSuccess_IsRejected(string? content)
    {
        Converter converter = new() { Result = new() { IsSuccessful = true, DocumentType = DocumentType.Pdf, Content = content is null ? null : Encoding.UTF8.GetBytes(content) } };
        Store store = new();
        ConversionOrchestrator orchestrator = CreateOrchestrator(new Source(Input(DocumentType.Docx)), store, [], [converter]);
        var result = await orchestrator.ExecuteAsync(Request(DocumentType.Docx), CancellationToken.None);
        Assert.Equal(ConversionErrorCodes.ConversionOutputInvalid, result.ErrorCode);
        Assert.Null(store.Content);
    }

    [Fact]
    public async Task NoOfficeConverter_ReturnsPermanentUnsupported()
    {
        ConversionOrchestrator orchestrator = CreateOrchestrator(new Source(Input(DocumentType.Docx)), new Store(), [], []);
        var result = await orchestrator.ExecuteAsync(Request(DocumentType.Docx), CancellationToken.None);
        Assert.Equal(ConversionErrorCodes.UnsupportedFormat, result.ErrorCode);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task ContentMismatchAndUnsupportedExtension_DoNotInvokeConverter()
    {
        foreach (string name in new[] { "file.xlsx", "file.docm", "file.odt", "file.xlsm", "file.ods", "file.odp" })
        {
            Converter converter = new();
            ConversionOrchestrator orchestrator = CreateOrchestrator(new Source(Input(DocumentType.Docx)), new Store(), [], [converter]);
            var result = await orchestrator.ExecuteAsync(Request(DocumentType.Docx) with { SourceFileName = name }, CancellationToken.None);
            Assert.Equal(ConversionErrorCodes.UnsupportedFormat, result.ErrorCode);
            Assert.Null(converter.Request);
        }
    }

    [Fact]
    public async Task Cancellation_PropagatesToConverterAndCaller()
    {
        using CancellationTokenSource cts = new();
        Converter converter = new() { Cancel = cts };
        Store store = new();
        ConversionOrchestrator orchestrator = CreateOrchestrator(new Source(Input(DocumentType.Docx)), store, [], [converter]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => orchestrator.ExecuteAsync(Request(DocumentType.Docx), cts.Token));
        Assert.Equal(cts.Token, converter.Token);
        Assert.Null(store.Content);
    }

    [Fact]
    public async Task PdfProviderPath_IsPreserved()
    {
        byte[] pdf = "%PDF-1.7\n"u8.ToArray();
        Store store = new();
        ConversionOrchestrator orchestrator = CreateOrchestrator(new Source(pdf), store, [new PdfPassThroughProvider()], []);
        var result = await orchestrator.ExecuteAsync(Request(DocumentType.Docx) with { SourceFileName = "file.pdf" }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal("pdf-pass-through", result.Provider);
        Assert.Equal(pdf, store.Content);
    }

    [Fact]
    public async Task BrokenZip_IsUnknownAndStreamPositionIsRestored()
    {
        using MemoryStream input = new(new byte[] { 0x50, 0x4B, 3, 4, 1, 2, 3, 4 });
        Assert.Equal(FileFormat.Unknown, await new BasicFileFormatDetector().DetectAsync(input, "file.docx", CancellationToken.None));
        Assert.Equal(0, input.Position);
    }

    [Theory]
    [InlineData(DocumentType.Docx, "pdf:writer_pdf_Export", "program/soffice.com")]
    [InlineData(DocumentType.Xlsx, "pdf:calc_pdf_Export", "program/soffice")]
    [InlineData(DocumentType.Pptx, "pdf:impress_pdf_Export", "program/soffice")]
    public async Task OfficeAdapter_UsesConfiguredExecutableAndFamilyFilter(DocumentType type, string filter, string executableRelativePath)
    {
        string root = Path.Combine(Path.GetTempPath(), "office-adapter-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "program"));
            await File.WriteAllBytesAsync(Path.Combine(root, executableRelativePath), []);
            using (ZipArchive profileTemplate = ZipFile.Open(Path.Combine(root, "profile-template.zip"), ZipArchiveMode.Create))
            {
                using StreamWriter writer = new(profileTemplate.CreateEntry("template.marker").Open());
                await writer.WriteAsync("ready");
            }
            RecordingRunner runner = new();
            OfficeConverter converter = new(Options.Create(new LibreOfficeOptions
            {
                RuntimeRelativePath = root, ExecutableRelativePath = executableRelativePath, WorkspaceRoot = root
            }), runner);
            var result = await converter.ConvertAsync(new ConverterRequest
            {
                Content = Input(type), SourceType = type, TargetType = DocumentType.Pdf
            });
            Assert.True(result.IsSuccessful, result.ErrorMessage);
            Assert.Equal(Path.GetFullPath(Path.Combine(root, executableRelativePath)), runner.Request!.FileName);
            Assert.Contains(filter, runner.Request.Arguments);
            Assert.Contains("--headless", runner.Request.Arguments);
            Assert.Contains("--nofirststartwizard", runner.Request.Arguments);
            Assert.Contains(runner.Request.Arguments, a => a.StartsWith("-env:UserInstallation=file:"));
            Assert.True(runner.ProfileTemplateMarkerWasCopied);
            Assert.EndsWith(FileFormat.OfficeFormats[type].DefaultExtension, runner.Request.Arguments.Last());
            Assert.Empty(Directory.GetDirectories(Path.Combine(root, "docproc-office")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class RecordingRunner : IExternalProcessRunner
    {
        public ProcessRunRequest? Request { get; private set; }
        public bool ProfileTemplateMarkerWasCopied { get; private set; }

        public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken token)
        {
            Request = request;
            string profileArgument = request.Arguments.Single(a => a.StartsWith("-env:UserInstallation=", StringComparison.Ordinal));
            string profileUri = profileArgument["-env:UserInstallation=".Length..];
            ProfileTemplateMarkerWasCopied = File.Exists(Path.Combine(new Uri(profileUri).LocalPath, "template.marker"));
            int index = request.Arguments.ToList().IndexOf("--outdir");
            await File.WriteAllBytesAsync(Path.Combine(request.Arguments[index + 1], "document.pdf"), "%PDF-1.7\n"u8.ToArray(), token);
            return new() { ExitCode = 0, StandardError = string.Empty, TimedOut = false };
        }
    }

    private static ConversionOrchestrator CreateOrchestrator(
        IDocumentSource source,
        IArtifactStore store,
        IEnumerable<IConversionProvider> providers,
        IEnumerable<IConverterProvider> converters)
    {
        IConversionProvider[] registeredProviders =
        [
            .. providers,
            new ConverterProviderAdapter(converters)
        ];

        return new ConversionOrchestrator(
            source,
            new BasicFileFormatDetector(),
            store,
            new ConversionRequestValidator(),
            new ConversionProviderResolver(registeredProviders),
            new PdfConversionOutputValidator(),
            new ConversionFailureMapper(
                NullLogger<ConversionFailureMapper>.Instance,
                new ResourceConversionErrorMessageProvider()));
    }

    [Theory]
    [InlineData("", "program/soffice.com")]
    [InlineData("runtimes/libreoffice/win-x64", "")]
    public void InvalidRuntimeConfiguration_IsRejected(string runtimePath, string executablePath)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["LibreOffice:RuntimeRelativePath"] = runtimePath,
                ["LibreOffice:ExecutableRelativePath"] = executablePath,
                ["LibreOffice:TimeoutSeconds"] = "60"
            }).Build();
        ServiceProvider provider = new ServiceCollection()
            .AddOfficeConversion(configuration)
            .BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<LibreOfficeOptions>>().Value);
    }

    [Fact]
    public async Task ExecutableOutsideRuntime_IsRejected()
    {
        OfficeConverter converter = new(Options.Create(new LibreOfficeOptions
        {
            RuntimeRelativePath = "runtimes/libreoffice/win-x64",
            ExecutableRelativePath = "../soffice.com"
        }), new UnusedRunner());
        ConverterResult result = await converter.ConvertAsync(new ConverterRequest
        {
            Content = Input(DocumentType.Docx), SourceType = DocumentType.Docx, TargetType = DocumentType.Pdf
        });
        Assert.False(result.IsSuccessful);
        Assert.Equal(ConversionErrorCodes.RuntimeNotFound, result.ErrorCode);
    }
    private static ConversionRequest Request(DocumentType type) => new()
    {
        JobId = Guid.NewGuid(), CorrelationId = "correlation", SourceReference = "local:///file",
        SourceFileName = "file" + FileFormat.ConvertibleFormats[type].DefaultExtension, Profile = "display-copy", Attempt = 1
    };

    private static byte[] Input(DocumentType type)
    {
        if (type == DocumentType.Txt) return Encoding.UTF8.GetBytes("sample text");
        if (type == DocumentType.Csv) return Encoding.UTF8.GetBytes("name,value\nsample,1");
        if (type is DocumentType.Doc or DocumentType.Xls or DocumentType.Ppt)
            return [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0];
        if (type == DocumentType.Rtf) return Encoding.ASCII.GetBytes("{\\rtf1 test}");
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, true))
        {
            void Add(string name, string value)
            {
                using StreamWriter writer = new(zip.CreateEntry(name).Open());
                writer.Write(value);
            }
            (string part, string mime) = type switch
            {
                DocumentType.Docx => ("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"),
                DocumentType.Xlsx => ("xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                _ => ("ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml")
            };
            Add(part, "<document/>");
            Add("[Content_Types].xml", $"<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/{part}\" ContentType=\"{mime}\"/></Types>");
        }
        return stream.ToArray();
    }

    private sealed class Source(byte[] content) : IDocumentSource
    {
        public Task<Stream> OpenReadAsync(string reference, CancellationToken token) => Task.FromResult<Stream>(new MemoryStream(content));
    }
    private sealed class Store : IArtifactStore
    {
        public byte[]? Content { get; private set; }
        public Task<bool> ExistsAsync(Guid id, string name, CancellationToken token) => Task.FromResult(false);
        public async Task<ArtifactWriteResult> SaveAsync(Guid id, string name, Stream content, CancellationToken token)
        {
            using MemoryStream stream = new();
            await content.CopyToAsync(stream, token);
            Content = stream.ToArray();
            return new("artifact:///output", Content.Length, "hash");
        }
    }
    private sealed class Converter : IConverterProvider
    {
        public string Name => "test-office";
        public ConverterRequest? Request { get; private set; }
        public CancellationToken Token { get; private set; }
        public CancellationTokenSource? Cancel { get; init; }
        public ConverterResult Result { get; init; } = new() { IsSuccessful = true, DocumentType = DocumentType.Pdf, Content = "%PDF-1.7\n"u8.ToArray() };
        public bool CanConvert(DocumentType source, DocumentType target) => FileFormat.ConvertibleFormats.ContainsKey(source) && target == DocumentType.Pdf;
        public Task<ConverterResult> ConvertAsync(ConverterRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            Token = cancellationToken;
            Cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result);
        }
    }
    private sealed class UnusedRunner : IExternalProcessRunner
    {
        public Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken token) => throw new NotSupportedException();
    }
}
