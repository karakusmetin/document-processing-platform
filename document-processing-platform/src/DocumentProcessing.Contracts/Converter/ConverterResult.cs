using System;
using System.Collections.Generic;
using System.Text;

namespace DocumentProcessing.Contracts.Converter
{
    public class ConverterResult
    {
        /// <summary>
        /// Dönüştürülmüş doküman.
        /// </summary>
        public byte[]? Content { get; init; }

        /// <summary>
        /// Oluşan doküman tipi.
        /// </summary>
        public DocumentType DocumentType { get; init; }

        /// <summary>
        /// Başarılı mı?
        /// </summary>
        public bool IsSuccessful { get; init; }

        /// <summary>
        /// Hata oluştuysa mesajı.
        /// </summary>
        public string? ErrorMessage { get; init; }
        public string? ErrorCode { get; init; }

    }
}
