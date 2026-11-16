using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.Utility.Model;
using Manatee.Json.Schema;
using System.Collections.Generic;
using System.Linq;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// AnyOf schema composition - validates against exactly one of the specified schemas
    /// </summary>
    public class JSBOneOf : IJSBPart
    {
        public JSBOneOf(
                IdentifierString oneOfName,
                string description,
                List<IJSBPart> schemas,
                bool isRequired = false)
        {
            Name = oneOfName;
            Description = description;
            IsRequired = isRequired;
            Schemas = schemas ?? new List<IJSBPart>();
        }

        public IdentifierString Name { get; }

        public string Description { get; }

        public bool IsRequired { get; }

        public List<IJSBPart> Schemas { get; }

        public JSBPartType PartType
        {
            get
            {
                return JSBPartType.OneOf;
            }
        }

        public JsonSchema AsJsonSchema()
        {
            if (Schemas.Count == 0)
            {
                throw new JsonSchemaBuilderException($"The OneOf part {Name} must contain at least one subschema");
            }

            var returnSchema = new JsonSchema()
                .Title(Name)
                .Description(Description)
                .Comment($"Generated with JsonSchemaBuilder");
            returnSchema.OneOf(Schemas.Select(schema => schema.AsJsonSchema()).ToArray());
            return returnSchema;
        }
    }
}
