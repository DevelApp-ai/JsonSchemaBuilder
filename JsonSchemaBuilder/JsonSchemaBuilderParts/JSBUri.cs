using DevelApp.Utility.Model;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Convenience URI definition in Json Schema.
    /// </summary>
    public class JSBUri : JSBString
    {
        public JSBUri(
                IdentifierString uriName,
                string description,
                string defaultValue = null,
                bool isRequired = false)
            : base(uriName,
                description,
                format: "uri",
                defaultValue: defaultValue,
                pattern: null,
                isRequired: isRequired)
        {
        }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Uri;
            }
        }
    }
}
