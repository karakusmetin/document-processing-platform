using DocumentProcessing.Core.Errors;
using DocumentProcessing.Core.Models;

namespace DocumentProcessing.Conversion.Errors;

public interface IConversionFailureMapper
{
    ConversionExecutionResult Create(
        ConversionErrorDefinition error,
        ConversionStage stage);

    ConversionExecutionResult FromException(
        Exception exception,
        ConversionStage stage);
}
