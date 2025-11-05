using DevelApp.Utility.Model;
using Manatee.Json.Schema;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// AnyOf schema composition - validates against any of the specified schemas
    /// TODO: Implement full AnyOf support for schema composition
    /// </summary>
    public class JSBAnyOf : AbstractJSBPart<object>
    {
        public JSBAnyOf(
                IdentifierString anyOfName,
                string description,
                List<IJSBPart> schemas,
                bool isRequired = false)
            : base(anyOfName, description, isRequired, null, null, null)
        {
            Schemas = schemas ?? new List<IJSBPart>();
        }

        public List<IJSBPart> Schemas { get; }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.AnyOf;
            }
        }

        public override JsonSchema AsJsonSchema()
        {
            // TODO: Implement AnyOf schema generation
            var returnSchema = InitialJsonSchema();
            // returnSchema.AnyOf(...) - needs implementation
            return returnSchema;
        }
    }
}
