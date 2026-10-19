using DocumentProcessing.Contracts.Converter;

namespace DocumentProcessing.Core.Abstractions
{
    public interface IConverterProvider
    {
        /// <summary>
        /// Provider adı.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Bu provider belirtilen dönüşümü destekliyor mu?
        /// </summary>
        bool CanConvert(DocumentType sourceType, DocumentType targetType);

        /// <summary>
        /// Dokümanı dönüştürür.
        /// </summary>
        Task<ConverterResult> ConvertAsync(ConverterRequest request, CancellationToken cancellationToken = default);
    }
}
