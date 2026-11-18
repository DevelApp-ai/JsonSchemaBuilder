using DevelApp.JsonSchemaBuilder;
using DevelApp.JsonSchemaBuilder.CodeGeneration;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using Json.Schema;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace JsonSchemaBuilder.Test
{
    /// <summary>
    /// Tests for the later than draft-7 dialect support (issue #8). The default
    /// stays draft-07 so existing schemas keep their exact wire format while
    /// Draft201909 and Draft202012 switch to the $defs container keyword and
    /// (2020-12) prefixItems for tuple arrays
    /// </summary>
    public class TestDraftVersions
    {
        private static JSBObject TopObjectWithRequiredName()
        {
            return new JSBObject("TopObject", "Top part",
                props: new List<IJSBPart>() { new JSBString("Name", "A property", isRequired: true) });
        }

        private static List<IJSBPart> OneDefinition()
        {
            return new List<IJSBPart>()
            {
                new JSBString("OuterDefinition", "A carried definition", maxLength: 20)
            };
        }

        private static string JsonOf(JsonSchema schema)
        {
            return JsonSerializer.SerializeToNode(schema).ToJsonString();
        }

        private static JsonElement Instance(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                return document.RootElement.Clone();
            }
        }

        /// <summary>
        /// The builder emits "enum":[] for wire format parity with the
        /// previous engine. An empty enum rejects every instance, so for
        /// instance validation tests the empty enums are stripped before the
        /// emitted schema json is parsed back with the dialect it declares
        /// </summary>
        private static JsonSchema ParseStrippedOfEmptyEnums(string json, JSBSchemaVersion schemaVersion)
        {
            JsonObject root = JsonNode.Parse(json).AsObject();
            StripEmptyEnums(root);
            return JsonSchema.FromText(root.ToJsonString(),
                DevelApp.JsonSchemaBuilder.JsonSchemaParts.JSBJsonSchemaBuild.CreateBuildOptions(schemaVersion),
                DevelApp.JsonSchemaBuilder.JsonSchemaParts.JSBJsonSchemaBuild.BaseUri);
        }

        private static void StripEmptyEnums(JsonObject obj)
        {
            if (obj.TryGetPropertyValue("enum", out JsonNode enumNode) && enumNode is JsonArray enumArray && enumArray.Count == 0)
            {
                obj.Remove("enum");
            }
            foreach (KeyValuePair<string, JsonNode> property in obj)
            {
                if (property.Value is JsonObject childObject)
                {
                    StripEmptyEnums(childObject);
                }
                else if (property.Value is JsonArray childArray)
                {
                    foreach (JsonNode item in childArray)
                    {
                        if (item is JsonObject itemObject)
                        {
                            StripEmptyEnums(itemObject);
                        }
                    }
                }
            }
        }

        #region Default version stays draft-7

        [Fact]
        public void DefaultSchemaVersionIsDraft7()
        {
            JSBSchema schema = new JSBSchema("DefaultSchema", "A default schema", TopObjectWithRequiredName(),
                defs: OneDefinition());
            Assert.Equal(JSBSchemaVersion.Draft7, schema.SchemaVersion);

            string json = JsonOf(schema.AsJsonSchema());
            Assert.Contains("\"$schema\":\"http://json-schema.org/draft-07/schema#\"", json);
            Assert.Contains("\"definitions\"", json);
            Assert.DoesNotContain("\"$defs\"", json);
            Assert.DoesNotContain("2019-09", json);
            Assert.DoesNotContain("2020-12", json);
        }

        [Fact]
        public void Draft7TupleFormItemsUnchanged()
        {
            JSBArray tupleArray = new JSBArray("TupleArray", "Array with two item types",
                new List<IJSBPart>()
                {
                    new JSBString("First", "First position"),
                    new JSBInteger("Second", "Second position")
                });

            string json = JsonOf(tupleArray.AsJsonSchema());
            Assert.Contains("\"items\"", json);
            Assert.DoesNotContain("prefixItems", json);
        }

        #endregion

        #region 2019-09

        [Fact]
        public void Draft201909UsesDefsAndSchemaLine()
        {
            JSBSchema schema = new JSBSchema("Draft201909Schema", "A 2019-09 schema", TopObjectWithRequiredName(),
                defs: OneDefinition(), schemaVersion: JSBSchemaVersion.Draft201909);

            string json = JsonOf(schema.AsJsonSchema());
            Assert.Contains("\"$schema\":\"https://json-schema.org/draft/2019-09/schema\"", json);
            Assert.Contains("\"$defs\"", json);
            Assert.DoesNotContain("\"definitions\"", json);
        }

        [Fact]
        public void Draft201909KeepsTupleFormItems()
        {
            JSBArray tupleArray = new JSBArray("TupleArray", "Array with two item types",
                new List<IJSBPart>()
                {
                    new JSBString("First", "First position"),
                    new JSBInteger("Second", "Second position")
                });

            string json = JsonOf(tupleArray.AsJsonSchema(JSBSchemaVersion.Draft201909));
            Assert.Contains("\"items\"", json);
            Assert.DoesNotContain("prefixItems", json);
        }

        [Fact]
        public void Draft201909SchemaValidatesInstances()
        {
            JSBSchema schema = new JSBSchema("Draft201909Schema", "A 2019-09 schema", TopObjectWithRequiredName(),
                schemaVersion: JSBSchemaVersion.Draft201909);

            JsonSchema jsonSchema = ParseStrippedOfEmptyEnums(JsonOf(schema.AsJsonSchema()), JSBSchemaVersion.Draft201909);
            EvaluationResults valid = jsonSchema.Evaluate(Instance("{\"name\":\"test\"}"));
            Assert.True(valid.IsValid, $"Expected instance to be valid: {valid}");
            EvaluationResults invalid = jsonSchema.Evaluate(Instance("{}"));
            Assert.False(invalid.IsValid, "Expected instance without the required property to be invalid");
        }

        #endregion

        #region 2020-12

        [Fact]
        public void Draft202012UsesDefsAndSchemaLine()
        {
            JSBSchema schema = new JSBSchema("Draft202012Schema", "A 2020-12 schema", TopObjectWithRequiredName(),
                defs: OneDefinition(), schemaVersion: JSBSchemaVersion.Draft202012);

            string json = JsonOf(schema.AsJsonSchema());
            Assert.Contains("\"$schema\":\"https://json-schema.org/draft/2020-12/schema\"", json);
            Assert.Contains("\"$defs\"", json);
            Assert.DoesNotContain("\"definitions\"", json);
        }

        [Fact]
        public void Draft202012SchemaCarriesItsOwnVersion()
        {
            JSBSchema schema = new JSBSchema("Draft202012Schema", "A 2020-12 schema", TopObjectWithRequiredName(),
                schemaVersion: JSBSchemaVersion.Draft202012);

            //The schema document carries its own dialect so calling the
            //parameterless and version overloads both emit 2020-12
            Assert.Contains("2020-12", JsonOf(schema.AsJsonSchema()));
            Assert.Contains("2020-12", JsonOf(schema.AsJsonSchema(JSBSchemaVersion.Draft7)));
            Assert.Contains("2020-12", JsonOf(new JSBSchemaBuilderWrapper(schema).AsJsonSchema()));
        }

        [Fact]
        public void Draft202012TupleArrayBecomesPrefixItems()
        {
            JSBArray tupleArray = new JSBArray("TupleArray", "Array with two item types",
                new List<IJSBPart>()
                {
                    new JSBString("First", "First position"),
                    new JSBInteger("Second", "Second position")
                });

            string json = JsonOf(tupleArray.AsJsonSchema(JSBSchemaVersion.Draft202012));
            Assert.Contains("\"prefixItems\"", json);
            Assert.DoesNotContain("\"items\"", json);
        }

        [Fact]
        public void Draft202012SingleItemArrayStillUsesItems()
        {
            JSBArray singleItemArray = new JSBArray("SingleItemArray", "Array with one item type",
                new List<IJSBPart>() { new JSBString("Only", "The only item type") });

            string json = JsonOf(singleItemArray.AsJsonSchema(JSBSchemaVersion.Draft202012));
            Assert.Contains("\"items\"", json);
            Assert.DoesNotContain("prefixItems", json);
        }

        [Fact]
        public void Draft202012SchemaValidatesInstances()
        {
            JSBSchema schema = new JSBSchema("Draft202012Schema", "A 2020-12 schema", TopObjectWithRequiredName(),
                schemaVersion: JSBSchemaVersion.Draft202012);

            JsonSchema jsonSchema = ParseStrippedOfEmptyEnums(JsonOf(schema.AsJsonSchema()), JSBSchemaVersion.Draft202012);
            EvaluationResults valid = jsonSchema.Evaluate(Instance("{\"name\":\"test\"}"));
            Assert.True(valid.IsValid, $"Expected instance to be valid: {valid}");
            EvaluationResults invalid = jsonSchema.Evaluate(Instance("{}"));
            Assert.False(invalid.IsValid, "Expected instance without the required property to be invalid");
        }

        [Fact]
        public void Draft202012PrefixItemsValidatesPositions()
        {
            JSBArray tupleArray = new JSBArray("TupleArray", "Array with two item types",
                new List<IJSBPart>()
                {
                    new JSBString("First", "First position"),
                    new JSBInteger("Second", "Second position")
                });

            JsonSchema jsonSchema = ParseStrippedOfEmptyEnums(JsonOf(tupleArray.AsJsonSchema(JSBSchemaVersion.Draft202012)),
                JSBSchemaVersion.Draft202012);
            Assert.True(jsonSchema.Evaluate(Instance("[\"a\",5]")).IsValid, "Expected matching tuple to be valid");
            Assert.False(jsonSchema.Evaluate(Instance("[5,\"a\"]")).IsValid, "Expected swapped tuple to be invalid");
        }

        [Fact]
        public void Draft7TupleItemsValidatesPositions()
        {
            JSBArray tupleArray = new JSBArray("TupleArray", "Array with two item types",
                new List<IJSBPart>()
                {
                    new JSBString("First", "First position"),
                    new JSBInteger("Second", "Second position")
                });

            JsonSchema jsonSchema = ParseStrippedOfEmptyEnums(JsonOf(tupleArray.AsJsonSchema()), JSBSchemaVersion.Draft7);
            Assert.True(jsonSchema.Evaluate(Instance("[\"a\",5]")).IsValid, "Expected matching tuple to be valid");
            Assert.False(jsonSchema.Evaluate(Instance("[5,\"a\"]")).IsValid, "Expected swapped tuple to be invalid");
        }

        [Fact]
        public void Draft202012PartLevelDefsUseDefsContainer()
        {
            JSBObject nestedObject = new JSBObject("NestedObject", "Object carrying definitions on a part level",
                props: new List<IJSBPart>() { new JSBString("Name", "A property", isRequired: true) },
                defs: OneDefinition());

            string json = JsonOf(nestedObject.AsJsonSchema(JSBSchemaVersion.Draft202012));
            Assert.Contains("\"$defs\"", json);
            Assert.DoesNotContain("\"definitions\"", json);
        }

        [Fact]
        public void Draft202012CompositionDefsUseDefsContainer()
        {
            JSBAnyOf anyOf = new JSBAnyOf("AnyOfPart", "AnyOf with definitions",
                new List<IJSBPart>()
                {
                    new JSBInteger("CompositionInt", "Integer variant"),
                    new JSBString("CompositionString", "String variant")
                },
                defs: OneDefinition());

            string json = JsonOf(anyOf.AsJsonSchema(JSBSchemaVersion.Draft202012));
            Assert.Contains("\"$defs\"", json);
            Assert.DoesNotContain("\"definitions\"", json);
        }

        #endregion

        #region Reference resolution leniency for $defs

        [Fact]
        public void ResolveJsonPointerFindsDefsContainer()
        {
            CodeGenerator codeGenerator = new CodeGenerator("applicationRoot", "jsonSchemaApplicationRoot");
            JSBSchema schema = new JSBSchema("PointerSchema", "Schema with defs", TopObjectWithRequiredName(),
                defs: OneDefinition(), schemaVersion: JSBSchemaVersion.Draft202012);
            JsonNode schemaJson = codeGenerator.ToJsonNode(schema);

            //Exact key
            Assert.NotNull(codeGenerator.ResolveJsonPointer(schemaJson, "/$defs/OuterDefinition"));
            //Lenient camelCase matching inside a defs container
            Assert.NotNull(codeGenerator.ResolveJsonPointer(schemaJson, "/$defs/outerDefinition"));
        }

        [Fact]
        public void LookupReferencedPartFromDefsFragment()
        {
            CodeGenerator codeGenerator = new CodeGenerator("applicationRoot", "jsonSchemaApplicationRoot");
            JSBSchema schema = new JSBSchema("PointerSchema", "Schema with defs", TopObjectWithRequiredName(),
                defs: OneDefinition(), schemaVersion: JSBSchemaVersion.Draft202012);

            IJSBPart referencedPart = codeGenerator.LookupReferencedPartFromDefinitions(schema, "/$defs/OuterDefinition");
            Assert.NotNull(referencedPart);
            Assert.Equal("OuterDefinition", referencedPart.Name.ToString());

            //A $defs fragment also resolves against the definitions of the top part
            JSBObject topWithOwnDefs = new JSBObject("TopObject", "Top part with its own defs",
                props: new List<IJSBPart>() { new JSBString("Name", "A property", isRequired: true) },
                defs: OneDefinition());
            JSBSchema schemaWithTopPartDefs = new JSBSchema("PointerSchema", "Schema", topWithOwnDefs,
                schemaVersion: JSBSchemaVersion.Draft202012);
            IJSBPart topPartDefinition = codeGenerator.LookupReferencedPartFromDefinitions(schemaWithTopPartDefs, "/$defs/OuterDefinition");
            Assert.NotNull(topPartDefinition);
        }

        [Fact]
        public void ResolveJsonPointerStillFindsDefinitionsContainer()
        {
            CodeGenerator codeGenerator = new CodeGenerator("applicationRoot", "jsonSchemaApplicationRoot");
            JSBSchema schema = new JSBSchema("PointerSchema", "Schema with definitions", TopObjectWithRequiredName(),
                defs: OneDefinition());
            JsonNode schemaJson = codeGenerator.ToJsonNode(schema);

            Assert.NotNull(codeGenerator.ResolveJsonPointer(schemaJson, "/definitions/OuterDefinition"));
            Assert.NotNull(codeGenerator.ResolveJsonPointer(schemaJson, "/definitions/outerDefinition"));
        }

        #endregion

        /// <summary>
        /// Small helper so the builder overload can be exercised through the
        /// interface the way consuming code composes parts
        /// </summary>
        private class JSBSchemaBuilderWrapper
        {
            private readonly JSBSchema _schema;

            public JSBSchemaBuilderWrapper(JSBSchema schema)
            {
                _schema = schema;
            }

            public JsonSchema AsJsonSchema()
            {
                return DevelApp.JsonSchemaBuilder.JsonSchemaParts.JSBJsonSchemaBuild.Build(_schema.AsJsonSchemaBuilder());
            }
        }
    }
}
