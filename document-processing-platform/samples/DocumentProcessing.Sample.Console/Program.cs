using System.Diagnostics;
using DocumentProcessing.Contracts.Converter;
using DocumentProcessing.Converters.Office.DependencyInjection;
using DocumentProcessing.Converters.Text.DependencyInjection;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: DocumentProcessing.Sample.Console <input-file> <output-directory>");
    return 1;
}

string inputPath = Path.GetFullPath(args[0]);
string outputDir = Path.GetFullPath(args[1]);

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"[{ConversionErrorCodes.InputNotFound}] Input file was not found: {inputPath}");
    return 1;
}

DocumentType? sourceType = Path.GetExtension(inputPath).ToLowerInvariant() switch
{
    ".doc" => DocumentType.Doc,
    ".docx" => DocumentType.Docx,
    ".rtf" => DocumentType.Rtf,
    ".xls" => DocumentType.Xls,
    ".xlsx" => DocumentType.Xlsx,
    ".ppt" => DocumentType.Ppt,
    ".pptx" => DocumentType.Pptx,
    ".txt" => DocumentType.Txt,
    ".csv" => DocumentType.Csv,
    _ => null
};

if (sourceType is null)
{
    Console.Error.WriteLine($"[{ConversionErrorCodes.UnsupportedFormat}] Unsupported file extension: {Path.GetExtension(inputPath)}");
    return 1;
}

HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
if (OperatingSystem.IsLinux())
{
    builder.Configuration.AddJsonFile("appsettings.Linux.json", optional: false, reloadOnChange: false);
}

builder.Services.AddOfficeConversion(builder.Configuration);
builder.Services.AddTextConversion(builder.Configuration);

using IHost host = builder.Build();

IEnumerable<IConverterProvider> providers = host.Services.GetRequiredService<IEnumerable<IConverterProvider>>();
IConverterProvider? provider = providers.FirstOrDefault(p => p.CanConvert(sourceType.Value, DocumentType.Pdf));

if (provider is null)
{
    Console.Error.WriteLine($"[{ConversionErrorCodes.UnsupportedFormat}] No converter provider registered for {sourceType} -> Pdf.");
    return 1;
}

byte[] content = await File.ReadAllBytesAsync(inputPath);

ConverterRequest request = new()
{
    Content = content,
    SourceType = sourceType.Value,
    TargetType = DocumentType.Pdf
};

Stopwatch stopwatch = Stopwatch.StartNew();
ConverterResult result = await provider.ConvertAsync(request, CancellationToken.None);
stopwatch.Stop();

if (!result.IsSuccessful || result.Content is null)
{
    Console.Error.WriteLine($"[{result.ErrorCode}] {result.ErrorMessage}");
    return 1;
}

Directory.CreateDirectory(outputDir);
string outputPath = Path.Combine(outputDir, $"{Path.GetFileNameWithoutExtension(inputPath)}.pdf");
await File.WriteAllBytesAsync(outputPath, result.Content);

Console.WriteLine($"OK: {inputPath} -> {outputPath}");
Console.WriteLine($"Engine: {provider.Name}");
Console.WriteLine($"Duration: {stopwatch.ElapsedMilliseconds} ms");

return 0;
