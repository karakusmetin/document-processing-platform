using DocumentProcessing.Contracts.Converter;

namespace DocumentProcessing.Converters.Image
{
    public class ImageConverter
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
