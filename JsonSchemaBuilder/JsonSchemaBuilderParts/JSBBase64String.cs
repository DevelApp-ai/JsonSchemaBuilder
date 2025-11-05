using DevelApp.Utility.Model;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Convenience base64-encoded string definition in Json Schema.
    /// </summary>
    public class JSBBase64String : JSBString
    {
        public JSBBase64String(
                IdentifierString base64StringName,
                string description,
                string defaultValue = null,
                bool isRequired = false)
            : base(base64StringName,
                description,
                format: null,
                defaultValue: defaultValue,
                pattern: "^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$",
                isRequired: isRequired)
        {
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
