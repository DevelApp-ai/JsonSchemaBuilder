using DevelApp.Utility.Model;
using System;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Base64 encoded string with conversions
    /// TODO: Implement full Base64String with encoding/decoding conversions
    /// </summary>
    public class JSBBase64String : JSBString
    {
        public JSBBase64String(
                IdentifierString base64Name,
                string description,
                string defaultValue = null,
                bool isRequired = false)
            : base(base64Name,
                description,
                format: "byte",
                defaultValue: defaultValue,
                isRequired: isRequired)
        {
            // TODO: Add Base64 encoding/decoding functionality
        }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Base64String;
            }
        }
    }
}
