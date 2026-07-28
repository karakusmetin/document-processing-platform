using System;
using System.Collections.Generic;
using System.Text;

namespace DocumentProcessing.Contracts.Converter
{
    public class ConverterRequest
    {
        /// <summary>
        /// Çevrilecek dokümanın byte bilgisi.
        /// </summary>
        public required byte[] Content { get; init; }

        /// <summary>
        /// Kaynak doküman tipi.
        /// </summary>
        public required DocumentType SourceType { get; init; }

        /// <summary>
        /// Hedef doküman tipi.
        /// </summary>
        public required DocumentType TargetType { get; init; }
    }
}
