using DevelApp.Utility.Model;
using Manatee.Json.Schema;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// AllOf schema composition - validates against all of the specified schemas
    /// TODO: Implement full AllOf support for schema composition (can be done through conversion)
    /// </summary>
    public class JSBAllOf : AbstractJSBPart<object>
    {
        public JSBAllOf(
                IdentifierString allOfName,
                string description,
                List<IJSBPart> schemas,
                bool isRequired = false)
            : base(allOfName, description, isRequired, null, null, null)
        {
            Schemas = schemas ?? new List<IJSBPart>();
        }

        public List<IJSBPart> Schemas { get; }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.AllOf;
            }
        }

        public override JsonSchema AsJsonSchema()
        {
            // TODO: Implement AllOf schema generation through conversion
            var returnSchema = InitialJsonSchema();
            // returnSchema.AllOf(...) - needs implementation
            return returnSchema;
        }
    }
}
