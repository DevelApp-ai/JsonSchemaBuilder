using DevelApp.Utility.Model;
using Json.Schema;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    public class JSBBoolean : AbstractJSBPart<bool?>
    {
        public JSBBoolean(IdentifierString boolName, string description, 
            bool? defaultValue = null, List<bool?> examples = null, 
            List<bool?> enums = null, bool isRequired = false) : 
            base(boolName, description, isRequired, defaultValue: defaultValue, examples: examples, enums: enums)
        {
        }


        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Boolean;
            }
        }

        public override JsonSchemaBuilder AsJsonSchemaBuilder(JSBSchemaVersion schemaVersion)
        {
            JsonSchemaBuilder builder = InitialJsonSchema(schemaVersion)
                .Type(SchemaValueType.Boolean);

            return builder;
        }
    }
}
