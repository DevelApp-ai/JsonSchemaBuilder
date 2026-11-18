using System;
using System.Runtime.Serialization;

namespace DevelApp.JsonSchemaBuilder.Exceptions
{
    /// <summary>
    /// Thrown when a schema document uses keywords or constructs that the builder
    /// parts cannot represent without changing the meaning of the schema. The
    /// parser fails loud on these instead of silently dropping semantics
    /// </summary>
    [Serializable]
    internal class SchemaParseException : JsonSchemaBuilderException
    {
        public SchemaParseException()
        {
        }

        public SchemaParseException(string message) : base(message)
        {
        }

        public SchemaParseException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SchemaParseException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
