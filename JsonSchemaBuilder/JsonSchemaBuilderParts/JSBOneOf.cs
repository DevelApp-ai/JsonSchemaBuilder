using DevelApp.Utility.Model;
using Manatee.Json.Schema;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// OneOf schema composition - validates against exactly one of the specified schemas
    /// TODO: Implement full OneOf support for schema composition
    /// </summary>
    public class JSBOneOf : AbstractJSBPart<object>
    {
        public JSBOneOf(
                IdentifierString oneOfName,
                string description,
                List<IJSBPart> schemas,
                bool isRequired = false)
            : base(oneOfName, description, isRequired, null, null, null)
        {
            Schemas = schemas ?? new List<IJSBPart>();
        }

        public List<IJSBPart> Schemas { get; }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.OneOf;
            }
        }

        public override JsonSchema AsJsonSchema()
        {
            // TODO: Implement OneOf schema generation
            var returnSchema = InitialJsonSchema();
            // returnSchema.OneOf(...) - needs implementation
            return returnSchema;
        }
    }
}
