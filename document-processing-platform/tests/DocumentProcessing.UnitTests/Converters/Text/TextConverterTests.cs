using System.Text;
using DocumentProcessing.Contracts.Converter;
using DocumentProcessing.Converters.Text;
using DocumentProcessing.Converters.Text.Options;
using DocumentProcessing.Core.Errors;
using Microsoft.Extensions.Options;
using Xunit;

namespace DocumentProcessing.UnitTests.Converters.Text;

public sealed class TextConverterTests
{
    [Theory]
    [InlineData(DocumentType.Txt, DocumentType.Pdf, true)]
    [InlineData(DocumentType.Csv, DocumentType.Pdf, true)]
    [InlineData(DocumentType.Html, DocumentType.Pdf, false)]
    [InlineData(DocumentType.Txt, DocumentType.Docx, false)]
    public void CanConvert_ReturnsExpectedResult(DocumentType sourceType, DocumentType targetType, bool expected)
    {
        TextConverter converter = new(Options.Create(new TextConversionOptions()));

        bool result = converter.CanConvert(sourceType, targetType);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task ConvertAsync_Txt_ProducesValidPdf()
    {
        TextConverter converter = new(Options.Create(new TextConversionOptions()));
        byte[] content = Encoding.UTF8.GetBytes(
            "DocumentProcessing native text render testi.\nİkinci satır Türkçe karakter: ığüşöçİĞÜŞÖÇ.\n\nBoş satırdan sonra üçüncü satır.");

        ConverterResult result = await converter.ConvertAsync(new ConverterRequest
        {
            Content = content,
            SourceType = DocumentType.Txt,
            TargetType = DocumentType.Pdf
        }, CancellationToken.None);

        AssertValidPdf(result);
    }

    [Fact]
    public async Task ConvertAsync_Csv_ProducesValidPdf()
    {
        TextConverter converter = new(Options.Create(new TextConversionOptions()));
        byte[] content = Encoding.UTF8.GetBytes(
            "Ad,Soyad,Şehir\n\"Ayşe\",\"Yılmaz\",\"İstanbul\"\n\"Can, Deniz\",\"Öz\",\"Ankara\"");

        ConverterResult result = await converter.ConvertAsync(new ConverterRequest
        {
            Content = content,
            SourceType = DocumentType.Csv,
            TargetType = DocumentType.Pdf
        }, CancellationToken.None);

        AssertValidPdf(result);
    }

    [Fact]
    public async Task ConvertAsync_CsvWithManyColumns_ProducesValidPdf()
    {
        TextConverter converter = new(Options.Create(new TextConversionOptions()));
        string csv = string.Join(',', Enumerable.Range(1, 200).Select(index => $"Column {index}"));

        ConverterResult result = await converter.ConvertAsync(new ConverterRequest
        {
            Content = Encoding.UTF8.GetBytes(csv),
            SourceType = DocumentType.Csv,
            TargetType = DocumentType.Pdf
        }, CancellationToken.None);

        AssertValidPdf(result);
    }

    [Fact]
    public async Task ConvertAsync_EmptyContent_ReturnsStandardizedFailure()
    {
        TextConverter converter = new(Options.Create(new TextConversionOptions()));

        ConverterResult result = await converter.ConvertAsync(new ConverterRequest
        {
            Content = [],
            SourceType = DocumentType.Txt,
            TargetType = DocumentType.Pdf
        }, CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(ConversionErrorCodes.SourceFileCorrupted, result.ErrorCode);
    }

    [Fact]
    public async Task ConvertAsync_WithLongTextRequiringMultiplePages_ProducesValidPdf()
    {
        TextConverter converter = new(Options.Create(new TextConversionOptions()));
        string longLine = string.Join(' ', Enumerable.Repeat("kelime", 20));
        string content = string.Join('\n', Enumerable.Repeat(longLine, 200));

        ConverterResult result = await converter.ConvertAsync(new ConverterRequest
        {
            Content = Encoding.UTF8.GetBytes(content),
            SourceType = DocumentType.Txt,
            TargetType = DocumentType.Pdf
        }, CancellationToken.None);

        AssertValidPdf(result);
    }

    private static void AssertValidPdf(ConverterResult result)
    {
        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.NotNull(result.Content);
        Assert.NotEmpty(result.Content!);
        Assert.Equal("%PDF-"u8.ToArray(), result.Content!.Take(5).ToArray());
    }
}
