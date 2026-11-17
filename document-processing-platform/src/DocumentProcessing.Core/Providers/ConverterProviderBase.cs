using DocumentProcessing.Contracts.Converter;
using DocumentProcessing.Core.Abstractions;
using DocumentProcessing.Core.Errors;

namespace DocumentProcessing.Core.Providers
{
    public abstract class ConverterProviderBase : IConverterProvider
    {
        public abstract string Name { get; }

        public abstract bool CanConvert(DocumentType sourceType, DocumentType targetType);

        public async Task<ConverterResult> ConvertAsync(ConverterRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                if (request.Content is null || request.Content.Length == 0)
                {
                    throw new ConverterOperationException(
                        ConversionErrorCodes.SourceFileCorrupted,
                        "Doküman içeriği boş olamaz.");
                }

                if (!CanConvert(request.SourceType, request.TargetType))
                {
                    return new ConverterResult
                    {
                        IsSuccessful = false,
                        DocumentType = request.TargetType,
                        ErrorCode = ConversionErrorCodes.UnsupportedFormat,
                        ErrorMessage = $"{Name}, {request.SourceType} -> {request.TargetType} dönüşümünü desteklemiyor."
                    };
                }

                byte[] content = await ConvertCoreAsync(request, cancellationToken);

                return new ConverterResult
                {
                    IsSuccessful = true,
                    Content = content,
                    DocumentType = request.TargetType
                };
            }
            catch (ConverterOperationException ex)
            {
                return new ConverterResult
                {
                    IsSuccessful = false,
                    DocumentType = request.TargetType,
                    ErrorCode = ex.ErrorCode,
                    ErrorMessage = ex.Message
                };
            }
        }

        protected abstract Task<byte[]> ConvertCoreAsync(ConverterRequest request, CancellationToken cancellationToken);
    }
}
