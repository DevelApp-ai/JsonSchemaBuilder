using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.Utility.Model;
using Json.Schema;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    /// <summary>
    /// Convenience array definition in Json Schema. Requires Items definition.
    /// </summary>
    public class JSBArray : AbstractJSBPart<JsonNode>
    {
        public JSBArray(IdentifierString arrayName, string description, List<IJSBPart> items,
            uint? minItems = null, uint? maxItems = null, bool uniqueItems = false, JsonNode defaultValue = null, List<JsonNode> examples = null, List<JsonNode> enums = null, bool isRequired = false) 
            : base(arrayName, description, isRequired, defaultValue, examples, enums)
        {
            if(items == null || items.Count == 0)
            {
                throw new JsonSchemaBuilderException($"An array without defined content does not make sense. {nameof(items)} has not been defined");
            }
            Items = items;
            MinItems = minItems;
            MaxItems = maxItems;
            UniqueItems = uniqueItems;
        }

        public List<IJSBPart> Items { get; }

        public uint? MinItems { get; }
        public uint? MaxItems { get; }

        public bool UniqueItems { get; }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Array;
            }
        }

        public override JsonSchemaBuilder AsJsonSchemaBuilder()
        {
            JsonSchemaBuilder builder = InitialJsonSchema()
                .Type(SchemaValueType.Array);
            if (Items.Count == 1)
            {
                builder = builder.Items(Items[0].AsJsonSchemaBuilder());
            }
            else
            {
                builder = builder.Items(Items.Select(item => item.AsJsonSchemaBuilder()));
            }
            if (MinItems.HasValue)
            {
                builder = builder.MinItems(MinItems.Value);
            }
            if (MaxItems.HasValue)
            {
                builder = builder.MaxItems(MaxItems.Value);
            }
            if(UniqueItems)
            {
                builder = builder.UniqueItems(UniqueItems);
            }
            return builder;
        }
    }
}
