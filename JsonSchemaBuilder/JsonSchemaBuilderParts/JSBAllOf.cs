using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.Utility.Model;
using Json.Schema;
using System.Collections.Generic;
using System.Linq;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    /// <summary>
    /// AllOf schema composition - validates against all of the specified schemas
    /// </summary>
    public class JSBAllOf : IJSBPart
    {
        public JSBAllOf(
                IdentifierString allOfName,
                string description,
                List<IJSBPart> schemas,
                bool isRequired = false,
                List<IJSBPart> defs = null)
        {
            Name = allOfName;
            Description = description;
            IsRequired = isRequired;
            Schemas = schemas ?? new List<IJSBPart>();
            if (defs != null)
            {
                foreach (IJSBPart part in defs)
                {
                    Definitions.Add(part.Name, part);
                }
            }
        }

        public IdentifierString Name { get; }

        public string Description { get; }

        public bool IsRequired { get; }

        public List<IJSBPart> Schemas { get; }

        /// <summary>
        /// Definitions carried by this part so definitions are supported
        /// on other levels than the top schema
        /// </summary>
        public Dictionary<string, IJSBPart> Definitions { get; } = new Dictionary<string, IJSBPart>();

        public JSBPartType PartType
        {
            get
            {
                return JSBPartType.AllOf;
            }
        }

        public JsonSchema AsJsonSchema()
        {
            return JSBJsonSchemaBuild.Build(AsJsonSchemaBuilder());
        }

        public JsonSchemaBuilder AsJsonSchemaBuilder()
        {
            if (Schemas.Count == 0)
            {
                throw new JsonSchemaBuilderException($"The AllOf part {Name} must contain at least one subschema");
            }

            var builder = new JsonSchemaBuilder()
                .Title(Name)
                .Description(Description)
                .Comment($"Generated with JsonSchemaBuilder");
            //Add definitions carried by this part so definitions are supported
            //on other levels than the top schema
            List<(string name, JsonSchemaBuilder definition)> definitionsOut =
                new List<(string name, JsonSchemaBuilder definition)>();
            foreach (var pair in Definitions)
            {
                definitionsOut.Add((pair.Key, pair.Value.AsJsonSchemaBuilder()));
            }
            if (definitionsOut.Count > 0)
            {
                builder.Definitions(definitionsOut.ToArray());
            }
            builder.AllOf(Schemas.Select(schema => schema.AsJsonSchemaBuilder()));
            return builder;
        }
    }
}
