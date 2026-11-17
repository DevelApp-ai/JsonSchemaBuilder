using DevelApp.Utility.Model;
using System;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Data string part that maps to the DataTypes.DataString value object with included
    /// handling of the string limitations (minLength, maxLength, pattern), so the limitations
    /// are both emitted to the schema and carried by the generated C# and VB code.
    /// </summary>
    public class JSBDataString : JSBString
    {
        public JSBDataString(
                IdentifierString dataStringName,
                string description,
                string defaultValue = null,
                bool isRequired = false,
                string format = null,
                uint minLength = 0,
                uint? maxLength = null,
                string pattern = null)
            : base(dataStringName,
                description,
                format: format,
                minLength: minLength,
                maxLength: maxLength,
                pattern: pattern,
                defaultValue: defaultValue,
                isRequired: isRequired)
        {
        }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.DataString;
            }
        }
    }
}
