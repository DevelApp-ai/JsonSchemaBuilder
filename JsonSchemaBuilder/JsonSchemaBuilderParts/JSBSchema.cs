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
            var rootKeywords = jsonSchema?.Root?.Keywords;
            if (rootKeywords == null || rootKeywords.Length == 0)
            {
                return new JSBSchema("NoValidation", "Represents an empty schema with disabled validation");
            }

            IJSBPart topPart = null;
            
            //TODO build from the schema

            string schemaId = null;
            string schemaDescription = null;
            foreach (var keywordData in rootKeywords)
            {
                if (keywordData.Handler.Name.Equals("$id"))
                {
                    schemaId = keywordData.RawValue.ToString();
                }
                else if (keywordData.Handler.Name.Equals("description"))
                {
                    schemaDescription = keywordData.RawValue.ToString();
                }
            }

            JSBSchema jsonSchemaBuilderSchema = new JSBSchema(schemaId, schemaDescription, topPart);

            return jsonSchemaBuilderSchema;
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
