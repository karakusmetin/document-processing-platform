using System.IO.Compression;
using System.Xml.Linq;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Detection;

public sealed class BasicFileFormatDetector : IFileFormatDetector
{
    public async Task<FileFormat> DetectAsync(Stream source, string declaredFileName, CancellationToken cancellationToken)
    {
        if (!source.CanSeek) throw new InvalidOperationException("Format detection requires a seekable stream.");
        long position = source.Position;
        try
        {
            byte[] header = new byte[8];
            int read = 0;
            while (read < header.Length)
            {
                int count = await source.ReadAsync(header.AsMemory(read), cancellationToken);
                if (count == 0) break;
                read += count;
            }
            if (read >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) return FileFormat.Pdf;
            FileFormat? declared = FileFormat.ConvertibleFormats.Values.FirstOrDefault(f =>
                string.Equals(f.DefaultExtension, Path.GetExtension(declaredFileName), StringComparison.OrdinalIgnoreCase));
            if (declared is null) return FileFormat.Unknown;
            if (declared == FileFormat.Text || declared == FileFormat.Csv)
                return header.AsSpan(0, read).Contains((byte)0) ? FileFormat.Unknown : declared;
            if (read >= 5 && header.AsSpan(0, 5).SequenceEqual("{\\rtf"u8))
                return declared.Name == "rtf" ? declared : FileFormat.Unknown;
            // Legacy Office shares an OLE signature; the import engine performs document validation.
            if (read == 8 && header.AsSpan().SequenceEqual(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
                return declared.Name is "doc" or "xls" or "ppt" ? declared : FileFormat.Unknown;
            if (read < 4 || !header.AsSpan(0, 4).SequenceEqual(new byte[] { 0x50, 0x4B, 0x03, 0x04 })) return FileFormat.Unknown;
            source.Position = position;
            using ZipArchive archive = new(source, ZipArchiveMode.Read, leaveOpen: true);
            ZipArchiveEntry? types = archive.GetEntry("[Content_Types].xml");
            if (types is null || types.Length > 1024 * 1024) return FileFormat.Unknown;
            using Stream typesStream = types.Open();
            XDocument xml = await XDocument.LoadAsync(typesStream, LoadOptions.None, cancellationToken);
            (string part, string type) = declared.Name switch
            {
                "docx" => ("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"),
                "xlsx" => ("xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                "pptx" => ("ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"),
                _ => (string.Empty, string.Empty)
            };
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
            bool matches = part.Length > 0 && archive.GetEntry(part) is not null &&
                xml.Root?.Elements(ns + "Override").Any(e => (string?)e.Attribute("PartName") == "/" + part &&
                    (string?)e.Attribute("ContentType") == type) == true;
            return matches ? declared : FileFormat.Unknown;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException) { return FileFormat.Unknown; }
        finally { source.Position = position; }
    }
}
