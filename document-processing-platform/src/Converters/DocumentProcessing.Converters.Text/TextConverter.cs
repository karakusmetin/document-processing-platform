using DocumentProcessing.Contracts.Converter;

namespace DocumentProcessing.Converters.Text
{
    public class TextConverter
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
