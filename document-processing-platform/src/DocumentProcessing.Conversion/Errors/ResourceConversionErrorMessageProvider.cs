using System.Globalization;
using System.Resources;
using DocumentProcessing.Core.Errors;

namespace DocumentProcessing.Conversion.Errors;

public sealed class ResourceConversionErrorMessageProvider : IConversionErrorMessageProvider
{
    private static readonly ResourceManager Messages = new(
        "DocumentProcessing.Conversion.Resources.ConversionMessages",
        typeof(ResourceConversionErrorMessageProvider).Assembly);

    public string GetMessage(ConversionErrorDefinition error) =>
        Messages.GetString(error.Code, CultureInfo.CurrentUICulture) ??
        Messages.GetString(ConversionErrorCodes.Unexpected, CultureInfo.CurrentUICulture) ??
        ConversionErrorCodes.Unexpected;
}
