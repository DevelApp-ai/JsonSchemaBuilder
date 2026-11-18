using DevelApp.Utility.Model;
using Json.Schema;
using System.Text.Json.Nodes;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    /// <summary>
    /// Defines JsonSchema object
    /// </summary>
    public class JSBObject : AbstractJSBPart<JsonNode>
    {
        public JSBObject(IdentifierString objectName, string description,
            List<IJSBPart> props = null, bool isRequired = false,
            JsonNode defaultValue = null, List<JsonNode> examples = null, List<JsonNode> enums = null, bool isExpandable = false,
            List<IJSBPart> defs = null)
            : base(objectName, description, isRequired, defaultValue: defaultValue, examples: examples, enums: enums)
        {
            if (props != null)
            {
                foreach(IJSBPart part in props)
                {
                    Properties.Add(part.Name, part);
                }
            }
            if (defs != null)
            {
                foreach (IJSBPart part in defs)
                {
                    Definitions.Add(part.Name, part);
                }
            }
            IsExpandable = isExpandable;
        }

        /// <summary>
        /// Stores the properties as children
        /// </summary>
        public Dictionary<IdentifierString, IJSBPart> Properties { get; } = new Dictionary<IdentifierString, IJSBPart>();

        /// <summary>
        /// Can the object be extended with outher properties
        /// </summary>
        public bool IsExpandable { get; }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Object;
            }
        }

        public override JsonSchemaBuilder AsJsonSchemaBuilder(JSBSchemaVersion schemaVersion)
        {
            JsonSchemaBuilder builder = InitialJsonSchema(schemaVersion)
                .Type(SchemaValueType.Object)
                .AdditionalProperties(IsExpandable);
            //Add properties
            List<(string name, JsonSchemaBuilder property)> propertiesOut =
                new List<(string name, JsonSchemaBuilder property)>();
            foreach (IJSBPart property in Properties.Values)
            {
                propertiesOut.Add((StartWithSmallLetter(property.Name), property.AsJsonSchemaBuilder(schemaVersion)));
            }
            if (propertiesOut.Count > 0)
            {
                builder.Properties(propertiesOut.ToArray());
            }
            //Add required
            List<string> requiredNames = new List<string>();
            foreach (IJSBPart property in Properties.Values)
            {
                if (property.IsRequired)
                {
                    requiredNames.Add(StartWithSmallLetter(property.Name));
                }
            }
            if (requiredNames.Count > 0)
            {
                builder.Required(requiredNames);
            }
            return builder;
        }
    }
}
