using DevelApp.Utility.Model;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Convenience phone number definition in Json Schema.
    /// </summary>
    public class JSBPhoneNumber : JSBString
    {
        public JSBPhoneNumber(
                IdentifierString phoneNumberName,
                string description,
                string defaultValue = null,
                bool isRequired = false)
            : base(phoneNumberName,
                description,
                format: null,
                defaultValue: defaultValue,
                pattern: @"^[\d\s\-\+\(\)\.]+$",
                isRequired: isRequired)
        {
        }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.PhoneNumber;
            }
        }
    }
}
