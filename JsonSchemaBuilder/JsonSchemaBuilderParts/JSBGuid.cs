using DevelApp.Utility.Model;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Convenience UUID/GUID definition in Json Schema.
    /// </summary>
    public class JSBGuid : JSBString
    {
        public JSBGuid(
                IdentifierString guidName,
                string description,
                string defaultValue = null,
                bool isRequired = false)
            : base(guidName,
                description,
                format: "uuid",
                defaultValue: defaultValue,
                pattern: "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$",
                isRequired: isRequired)
        {
        }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Guid;
            }
        }
    }
}
