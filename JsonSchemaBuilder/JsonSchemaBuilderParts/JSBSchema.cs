using DevelApp.Utility.Model;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    public class JSBSchema : AbstractJSBPart<JsonNode>
    {
        public static JSBSchema BuildSchema(JsonSchema jsonSchema)
        {
            if (jsonSchema == null)
            {
                return new JSBSchema(new IdentifierString("NoValidation"), "Represents an empty schema with disabled validation");
            }

            //Parse the schema document into the builder part tree instead of only
            //walking the $id and description as before. Keywords that the builder
            //parts cannot represent fail loud with a SchemaParseException
            JsonNode jsonSchemaAsNode = System.Text.Json.JsonSerializer.SerializeToNode(jsonSchema, JSBJsonSchemaBuild.SerializerOptions);
            if (jsonSchemaAsNode is JsonObject rootObject && rootObject.Count == 0)
            {
                return new JSBSchema(new IdentifierString("NoValidation"), "Represents an empty schema with disabled validation");
            }
            return JSBSchemaParser.ParseDocument(jsonSchemaAsNode);
        }

        public JSBSchema(IdentifierString schemaName, string description, 
            IJSBPart topPart = null, 
            List<IJSBPart> defs = null, JsonNode defaultValue = null,
            List<JsonNode> examples = null, List<JsonNode> enums = null, bool isRequired = false,
            JSBSchemaVersion schemaVersion = JSBSchemaVersion.Draft7) 
            : base(schemaName, description, isRequired, defaultValue, examples, enums)
        {
            TopPart = topPart;
            SchemaVersion = schemaVersion;
            if (defs != null)
            {
                foreach (IJSBPart part in defs)
                {
                    Definitions.Add(part.Name, part);
                }
            }
        }

        //Definitions is inherited from AbstractJSBPart so definitions are supported
        //on other levels than the top schema. The schema level Definitions is the root level

        public IJSBPart TopPart { get; }

        /// <summary>
        /// The dialect the schema document is emitted as. The schema document
        /// carries its own dialect so this wins over any version supplied to
        /// the AsJsonSchema and AsJsonSchemaBuilder overloads
        /// </summary>
        public JSBSchemaVersion SchemaVersion { get; }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Schema;
            }
        }

        /// <summary>
        /// The schema document carries its own dialect so SchemaVersion is used
        /// regardless of the version this overload is called with
        /// </summary>
        public override JsonSchema AsJsonSchema()
        {
            return JSBJsonSchemaBuild.Build(AsJsonSchemaBuilder(), SchemaVersion);
        }

        /// <summary>
        /// The schema document carries its own dialect so SchemaVersion is used
        /// regardless of the version this overload is called with
        /// </summary>
        public override JsonSchema AsJsonSchema(JSBSchemaVersion schemaVersion)
        {
            return JSBJsonSchemaBuild.Build(AsJsonSchemaBuilder(), SchemaVersion);
        }

        public override JsonSchemaBuilder AsJsonSchemaBuilder()
        {
            return AsJsonSchemaBuilder(SchemaVersion);
        }

        /// <summary>
        /// The schema document carries its own dialect so SchemaVersion is used
        /// regardless of the version this overload is called with
        /// </summary>
        public override JsonSchemaBuilder AsJsonSchemaBuilder(JSBSchemaVersion schemaVersion)
        {
            JSBSchemaVersion effectiveVersion = SchemaVersion;
            if (TopPart != null)
            {
                JsonSchemaBuilder builder = TopPart.AsJsonSchemaBuilder(effectiveVersion)
                    .Id(Name)
                    .Schema(effectiveVersion.SchemaUri());
                List<(string name, JsonSchemaBuilder definition)> definitionsOut =
                    new List<(string name, JsonSchemaBuilder definition)>();
                foreach (var pair in Definitions)
                {
                    definitionsOut.Add((pair.Key, pair.Value.AsJsonSchemaBuilder(effectiveVersion)));
                }
                //The definitions carried by the top part were emitted into the
                //same builder by the part itself and this container call would
                //replace them: merge them in so definitions on all parts survive.
                //Schema level definitions win on a key collision
                if (TopPart.Definitions != null)
                {
                    foreach (var pair in TopPart.Definitions)
                    {
                        if (!Definitions.ContainsKey(pair.Key))
                        {
                            definitionsOut.Add((pair.Key, pair.Value.AsJsonSchemaBuilder(effectiveVersion)));
                        }
                    }
                }
                if (definitionsOut.Count > 0)
                {
                    if (effectiveVersion.UsesDefsContainer())
                    {
                        builder.Defs(definitionsOut.ToArray());
                    }
                    else
                    {
                        builder.Definitions(definitionsOut.ToArray());
                    }
                }

                return builder;
            }
            else
            {
                //A fresh builder is used so no shared state is mutated between
                //schemas built after the first
                JsonSchemaBuilder builder = new JsonSchemaBuilder()
                    .Id(Name)
                    .Schema(effectiveVersion.SchemaUri())
                    .Title(Name)
                    .Description(Description);

                return builder;
            }
        }
    }
}
