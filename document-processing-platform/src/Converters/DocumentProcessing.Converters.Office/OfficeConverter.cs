using DocumentProcessing.Contracts.Converter;

namespace DocumentProcessing.Converters.Office
{
    public class OfficeConverter
    {
        public ConverterResult Convert(ConverterRequest request)
        {
            return new ConverterResult
            {
                IsSuccessful = true,
                Content = request.Content, // TODO: dönüştürülen byte
                DocumentType = DocumentType.Pdf
            };
        }
    }
}
