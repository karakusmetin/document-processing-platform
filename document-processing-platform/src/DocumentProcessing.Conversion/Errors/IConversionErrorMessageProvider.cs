using DocumentProcessing.Core.Errors;

namespace DocumentProcessing.Conversion.Errors;

public interface IConversionErrorMessageProvider
{
    string GetMessage(ConversionErrorDefinition error);
}
