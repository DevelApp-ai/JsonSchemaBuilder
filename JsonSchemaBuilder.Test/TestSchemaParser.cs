using DevelApp.JsonSchemaBuilder;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace JsonSchemaBuilder.Test
{
    /// <summary>
    /// Tests for the JSBSchemaParser that turns a schema document given as JSON
    /// into the builder part tree (issue #10). Builder emitted schemas must
    /// round trip through the parser, externally authored schemas must parse
    /// into the same parts and keywords the parts cannot represent must fail
    /// loud instead of silently changing the meaning of the schema
    /// </summary>
    public class TestSchemaParser
    {
        #region Parser tests on externally authored documents

        [Fact]
        public void ParseDraft7ObjectDocument()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""type"": ""object"",
  ""description"": ""A person"",
  ""properties"": {
    ""name"": { ""type"": ""string"", ""minLength"": 1, ""maxLength"": 50 },
    ""age"": { ""type"": ""integer"", ""minimum"": 0, ""maximum"": 130 },
    ""height"": { ""type"": ""number"" },
    ""active"": { ""type"": ""boolean"", ""default"": true },
    ""email"": { ""type"": ""string"", ""format"": ""email"" },
    ""nickName"": { ""type"": [""string"", ""null""] }
  },
  ""required"": [""name"", ""nickName""],
  ""additionalProperties"": false,
  ""definitions"": {
    ""address"": {
      ""type"": ""object"",
      ""properties"": {
        ""street"": { ""type"": ""string"" },
        ""zipCode"": { ""type"": ""string"", ""pattern"": ""^[0-9]{4}$"" }
      },
      ""required"": [""street""]
    }
  }
}");
            JSBSchema parsed = JSBSchemaParser.ParseDocument(document, new DevelApp.Utility.Model.IdentifierString("Person"));

            Assert.Equal(JSBSchemaVersion.Draft7, parsed.SchemaVersion);
            Assert.Equal("Person", parsed.Name.ToString());
            Assert.IsType<JSBObject>(parsed.TopPart);

            JSBObject topObject = (JSBObject)parsed.TopPart;
            Assert.False(topObject.IsExpandable);
            IJSBPart namePart = topObject.Properties["Name"];
            JSBString nameString = Assert.IsType<JSBString>(namePart);
            Assert.True(nameString.IsRequired);
            IJSBPart nickNamePart = topObject.Properties["NickName"];
            //The type array [string, null] is approximated as the base type
            //marked optional: the parts cannot express required but nullable
            Assert.IsType<JSBString>(nickNamePart);
            Assert.False(nickNamePart.IsRequired);
            //The typed email format is mapped to the JSBEmail part when
            //unconstrained
            IJSBPart emailPart = topObject.Properties["Email"];
            Assert.Equal(JSBPartType.Email, emailPart.PartType);
            //Documents authored with a default keep it on the parsed part
            IJSBPart activePart = topObject.Properties["Active"];
            Assert.True(((JSBBoolean)activePart).DefaultValue);
            //The definitions keep the original wire keys they were authored with
            Assert.Contains("address", parsed.Definitions.Keys);
            JSBObject addressDefinition = Assert.IsType<JSBObject>(parsed.Definitions["address"]);
            Assert.True(addressDefinition.Properties["Street"].IsRequired);
        }

        [Fact]
        public void ParseDefsWith202012Dialect()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
  ""type"": ""object"",
  ""properties"": {
    ""tags"": { ""type"": ""array"", ""items"": { ""type"": ""string"" } }
  },
  ""$defs"": {
    ""color"": { ""type"": ""string"", ""enum"": [""red"", ""green""] }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);

            Assert.Equal(JSBSchemaVersion.Draft202012, parsed.SchemaVersion);
            Assert.Contains("color", parsed.Definitions.Keys);
            JSBString colorDefinition = Assert.IsType<JSBString>(parsed.Definitions["color"]);
            Assert.Equal(2, colorDefinition.Enums.Count);

            //Re-emitted with the same dialect the $defs container is used
            JsonNode emitted = JsonSerializer.SerializeToNode(parsed.AsJsonSchema());
            Assert.NotNull(((JsonObject)emitted)["$defs"]);
            Assert.Null(((JsonObject)emitted)["definitions"]);
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", ((JsonObject)emitted)["$schema"].GetValue<string>());
        }

        [Fact]
        public void Parse2019DraftIsDetected()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""$schema"": ""https://json-schema.org/draft/2019-09/schema"",
  ""type"": ""string""
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);

            Assert.Equal(JSBSchemaVersion.Draft201909, parsed.SchemaVersion);
        }

        [Fact]
        public void ParseUnknownDialectThrows()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""$schema"": ""http://json-schema.org/draft-06/schema#"",
  ""type"": ""string""
}");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("not supported", exception.Message);
            Assert.Contains("draft-06", exception.Message);
        }

        [Fact]
        public void ParseTupleArrayAndConstraints()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
  ""type"": ""array"",
  ""prefixItems"": [
    { ""type"": ""string"" },
    { ""type"": ""integer"" }
  ],
  ""minItems"": 2,
  ""maxItems"": 2,
  ""uniqueItems"": true
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);

            JSBArray arrayPart = Assert.IsType<JSBArray>(parsed.TopPart);
            Assert.Equal(2, arrayPart.Items.Count);
            Assert.Equal<uint>(2, arrayPart.MinItems.Value);
            Assert.Equal<uint>(2, arrayPart.MaxItems.Value);
            Assert.True(arrayPart.UniqueItems);
        }

        [Fact]
        public void ParseCompositions()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""mixed"": {
      ""oneOf"": [
        { ""type"": ""string"" },
        { ""type"": ""integer"" }
      ]
    }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);
            JSBObject topObject = (JSBObject)parsed.TopPart;
            IJSBPart mixedPart = topObject.Properties["Mixed"];
            JSBOneOf oneOf = Assert.IsType<JSBOneOf>(mixedPart);
            Assert.Equal(2, oneOf.Schemas.Count);
        }

        [Fact]
        public void ParseRefPartWithFragment()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""home"": { ""$ref"": ""#/definitions/address"", ""description"": ""Home address"" }
  },
  ""definitions"": {
    ""address"": { ""type"": ""object"" }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);
            JSBObject topObject = (JSBObject)parsed.TopPart;
            IJSBPart homePart = topObject.Properties["Home"];
            JSBRef refPart = Assert.IsType<JSBRef>(homePart);
            //The fragment keeps the leading path separator of the fragment part
            //of the uri exactly as JSBRef stores it for relative references
            Assert.Equal("/definitions/address", refPart.Fragment);
        }

        [Fact]
        public void RefWithConstraintSiblingThrows()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""home"": { ""$ref"": ""#/definitions/address"", ""minLength"": 2 }
  }
}");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("minLength", exception.Message);
        }

        [Theory]
        [InlineData("{\"type\":\"object\",\"patternProperties\":{\"^S_\":{\"type\":\"string\"}}}", "patternProperties")]
        [InlineData("{\"type\":\"object\",\"propertyNames\":{\"type\":\"string\"}}", "propertyNames")]
        [InlineData("{\"type\":\"object\",\"if\":{\"type\":\"string\"}}", "if")]
        [InlineData("{\"type\":\"string\",\"not\":{\"type\":\"string\"}}", "not")]
        [InlineData("{\"type\":\"array\",\"contains\":{\"type\":\"string\"}}", "contains")]
        [InlineData("{\"type\":\"object\",\"minProperties\":1}", "minProperties")]
        [InlineData("{\"type\":\"string\",\"exclusiveMinimum\":1}", "exclusiveMinimum")]
        [InlineData("{\"type\":\"array\",\"items\":{\"type\":\"string\"},\"additionalItems\":{\"type\":\"string\"}}", "additionalItems")]
        [InlineData("{\"type\":\"object\",\"dependentSchemas\":{\"a\":{\"type\":\"string\"}}}", "dependentSchemas")]
        [InlineData("{\"type\":\"object\",\"unevaluatedProperties\":false}", "unevaluatedProperties")]
        public void UnrepresentableKeywordsThrow(string schema, string keyword)
        {
            JsonNode document = JsonNode.Parse(schema);

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains(keyword, exception.Message);
        }

        [Fact]
        public void NonScalarConstThrows()
        {
            JsonNode document = JsonNode.Parse("{\"type\":\"string\",\"const\":{\"a\":1}}");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("const", exception.Message);
        }

        [Fact]
        public void BooleanSchemaThrows()
        {
            //A boolean subschema inside the document hits the boolean schema guard
            JsonNode document = JsonNode.Parse("{\"type\":\"object\",\"properties\":{\"flag\":true}}");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("Boolean schemas", exception.Message);
        }

        [Fact]
        public void RootMustBeAnObject()
        {
            //A boolean document is not an object so the document guard rejects it
            JsonNode document = JsonNode.Parse("true");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("must be a JSON object", exception.Message);
        }

        [Fact]
        public void TypeUnionBeyondNullThrows()
        {
            JsonNode document = JsonNode.Parse("{\"type\":[\"string\",\"integer\"]}");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("type array", exception.Message);
        }

        [Fact]
        public void UntypedSchemaThrowsWithHelpfulMessage()
        {
            JsonNode document = JsonNode.Parse("{\"enum\":[1,2,3]}");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("cannot be determined", exception.Message);
        }

        [Fact]
        public void ConstrainedFormatStaysPlainString()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""homepage"": { ""type"": ""string"", ""format"": ""uri"", ""maxLength"": 200 },
    ""friendEmail"": { ""type"": ""string"", ""format"": ""email"", ""pattern"": ""^a.*"" }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);
            JSBObject topObject = (JSBObject)parsed.TopPart;
            //A format with constraints the typed part cannot carry stays a plain
            //string part that carries the format through
            JSBString homepage = Assert.IsType<JSBString>(topObject.Properties["Homepage"]);
            Assert.Equal("uri", homepage.Format);
            Assert.Null(homepage.Pattern);
            JSBString friendEmail = Assert.IsType<JSBString>(topObject.Properties["FriendEmail"]);
            Assert.Equal("email", friendEmail.Format);
            Assert.Equal("^a.*", friendEmail.Pattern);
        }

        [Fact]
        public void TypedFormatsWithCanonicalDefaultsKeepTheTypedPart()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""birthDate"": { ""type"": ""string"", ""format"": ""date"", ""default"": ""2015-05-16"" },
    ""alarmTime"": { ""type"": ""string"", ""format"": ""time"", ""default"": ""07:15:30.1230000+00:00"" },
    ""updated"": { ""type"": ""string"", ""format"": ""date-time"", ""default"": ""2026-01-01T07:15:30.1230000+00:00"" },
    ""contact"": { ""type"": ""string"", ""format"": ""email"", ""default"": ""user@example.com"" },
    ""id"": { ""type"": ""string"", ""format"": ""uuid"", ""default"": ""123e4567-e89b-12d3-a456-426614174000"" },
    ""signature"": { ""type"": ""string"", ""format"": ""byte"", ""default"": ""aGVsbG8="" },
    ""homepage"": { ""type"": ""string"", ""format"": ""uri"", ""default"": ""https://example.com"" }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);
            JSBObject topObject = (JSBObject)parsed.TopPart;
            //A default the typed part can carry without changing the emitted
            //wire is mapped into the typed part
            JSBDate birthDate = Assert.IsType<JSBDate>(topObject.Properties["BirthDate"]);
            Assert.Equal("2015-05-16", birthDate.DefaultValue);
            JSBTime alarmTime = Assert.IsType<JSBTime>(topObject.Properties["AlarmTime"]);
            Assert.Equal("07:15:30.1230000+00:00", alarmTime.DefaultValue);
            JSBDateTime updated = Assert.IsType<JSBDateTime>(topObject.Properties["Updated"]);
            Assert.Equal("2026-01-01T07:15:30.1230000+00:00", updated.DefaultValue);
            JSBEmail contact = Assert.IsType<JSBEmail>(topObject.Properties["Contact"]);
            Assert.Equal("user@example.com", contact.DefaultValue);
            JSBGuid id = Assert.IsType<JSBGuid>(topObject.Properties["Id"]);
            Assert.Equal("123e4567-e89b-12d3-a456-426614174000", id.DefaultValue);
            JSBBase64String signature = Assert.IsType<JSBBase64String>(topObject.Properties["Signature"]);
            Assert.Equal("aGVsbG8=", signature.DefaultValue);
            JSBUri homepage = Assert.IsType<JSBUri>(topObject.Properties["Homepage"]);
            Assert.Equal("https://example.com", homepage.DefaultValue);

            //Builder emission expands the authored document (title,
            //description, examples/enum), so the guarantee to assert is
            //idempotence: parse the emission, emit again, compare emissions
            JsonNode emitted = JsonSerializer.SerializeToNode(parsed.AsJsonSchema());
            JSBSchema reparsed = JSBSchemaParser.ParseDocument(emitted, parsed.Name);
            JSBObject reparsedTop = (JSBObject)reparsed.TopPart;
            Assert.Equal(JSBPartType.Date, reparsedTop.Properties["BirthDate"].PartType);
            Assert.Equal(JSBPartType.Time, reparsedTop.Properties["AlarmTime"].PartType);
            Assert.Equal(JSBPartType.DateTime, reparsedTop.Properties["Updated"].PartType);
            Assert.Equal(JSBPartType.Email, reparsedTop.Properties["Contact"].PartType);
            Assert.Equal(JSBPartType.Guid, reparsedTop.Properties["Id"].PartType);
            Assert.Equal(JSBPartType.Base64String, reparsedTop.Properties["Signature"].PartType);
            Assert.Equal(JSBPartType.Uri, reparsedTop.Properties["Homepage"].PartType);
            JsonNode reemitted = JsonSerializer.SerializeToNode(reparsed.AsJsonSchema());
            AssertJsonNodesSemanticallyEqual(emitted, reemitted);
        }

        [Fact]
        public void TypedTimeDefaultWithANonCanonicalOffsetDegradesToPlainString()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""alarmTime"": { ""type"": ""string"", ""format"": ""time"", ""default"": ""07:15:30.1230000+05:00"" }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);
            JSBObject topObject = (JSBObject)parsed.TopPart;
            //A default the typed part would silently shift on re-emission is
            //kept verbatim by a plain string part: wire fidelity beats typed
            //part fidelity
            JSBString alarmTime = Assert.IsType<JSBString>(topObject.Properties["AlarmTime"]);
            Assert.Equal(JSBPartType.String, alarmTime.PartType);
            Assert.Equal("time", alarmTime.Format);
            Assert.Equal("07:15:30.1230000+05:00", alarmTime.DefaultValue);
        }

        [Fact]
        public void TypedFormatWithADefaultNotMatchingTheNativePatternDegradesToPlainString()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""birthDate"": { ""type"": ""string"", ""format"": ""date"", ""default"": ""not a date"" }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);
            JSBObject topObject = (JSBObject)parsed.TopPart;
            JSBString birthDate = Assert.IsType<JSBString>(topObject.Properties["BirthDate"]);
            Assert.Equal(JSBPartType.String, birthDate.PartType);
            Assert.Equal("date", birthDate.Format);
            Assert.Equal("not a date", birthDate.DefaultValue);
        }

        [Fact]
        public void NonStringDefaultOnAStringPartThrows()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""count"": { ""type"": ""string"", ""default"": 42 }
  }
}");

            Exception exception = Assert.ThrowsAny<Exception>(() => JSBSchemaParser.ParseDocument(document));
            Assert.Contains("default", exception.Message);
        }

        [Fact]
        public void SanitizedPropertyKeysKeepGenerating()
        {
            JsonNode document = JsonNode.Parse(@"
{
  ""type"": ""object"",
  ""properties"": {
    ""first-name"": { ""type"": ""string"" }
  }
}");

            JSBSchema parsed = JSBSchemaParser.ParseDocument(document);
            JSBObject topObject = (JSBObject)parsed.TopPart;
            Assert.Single(topObject.Properties);
            //Sanitized to an identifier-like part name with a leading
            //capital (Firstname, not FirstName); documented that exotic
            //wire keys diverge on re-emission
            Assert.Equal("Firstname", topObject.Properties["Firstname"].Name.ToString());
        }

        #endregion

        #region Round trip tests: builder emitted schemas parse back to the same parts

        [Fact]
        public void RoundTripDraft7Document()
        {
            JSBString namePart = new JSBString("Name", "A name property", maxLength: 50, pattern: "^\\S+$", isRequired: true);
            JSBInteger agePart = new JSBInteger("Age", "An age property", minimum: 0, maximum: 130, multipleOf: 1);
            JSBNumber heightPart = new JSBNumber("Height", "A height", minimum: 0.1, maximum: 2.5);
            JSBBoolean activePart = new JSBBoolean("Active", "Active flag", defaultValue: true);
            JSBDate birthDatePart = new JSBDate("BirthDate", "The birth date");
            JSBEmail emailPart = new JSBEmail("Email", "The email", isRequired: true);
            JSBString cityPart = new JSBString("City", "The city", format: "city");
            JSBArray tagsPart = new JSBArray("Tags", "The tags", new List<IJSBPart>() { new JSBString("TagsItem", "One tag") }, minItems: 1, uniqueItems: true);
            JSBArray tuplePart = new JSBArray("Tuple", "A tuple", new List<IJSBPart>()
            {
                new JSBString("TupleItem0", "First position"),
                new JSBInteger("TupleItem1", "Second position")
            });
            JSBOneOf oneOfPart = new JSBOneOf("Mixed", "String or integer", new List<IJSBPart>()
            {
                new JSBString("Mixed0", "String variant"),
                new JSBInteger("Mixed1", "Integer variant")
            });

            JSBObject topPart = new JSBObject("Person", "The person top part",
                props: new List<IJSBPart>() { namePart, agePart, heightPart, activePart, birthDatePart, emailPart, cityPart, tagsPart, tuplePart, oneOfPart },
                isRequired: true,
                isExpandable: false,
                defs: new List<IJSBPart>() { new JSBObject("CarriedDefinition", "A carried definition",
                    props: new List<IJSBPart>() { new JSBString("Inner", "Inner property") }) });

            JSBSchema original = new JSBSchema(
                new DevelApp.Utility.Model.IdentifierString("Person"),
                "The schema of a person",
                topPart,
                defs: new List<IJSBPart>() { new JSBString("TopDefinition", "A top level definition", maxLength: 20) });

            JsonNode emitted = JsonSerializer.SerializeToNode(original.AsJsonSchema());
            JSBSchema parsed = JSBSchemaParser.ParseDocument(emitted, original.Name);

            AssertParsedPartsMatch(original, parsed);
            JsonNode reemitted = JsonSerializer.SerializeToNode(parsed.AsJsonSchema());
            AssertJsonNodesSemanticallyEqual(emitted, reemitted);
        }

        [Fact]
        public void RoundTripTypedStringPartsKeepPartType()
        {
            JSBDate datePart = new JSBDate("DateOfBirth", "A date");
            JSBTime timePart = new JSBTime("WakeupTime", "A time");
            JSBDateTime dateTimePart = new JSBDateTime("Updated", "A datetime");
            JSBGuid guidPart = new JSBGuid("Identifier", "A uuid");
            JSBUri uriPart = new JSBUri("Website", "A uri");
            JSBBase64String base64Part = new JSBBase64String("Signature", "Base64 content");
            JSBObject topPart = new JSBObject("TypedStrings", "Typed string parts",
                props: new List<IJSBPart>() { datePart, timePart, dateTimePart, guidPart, uriPart, base64Part });

            JSBSchema original = new JSBSchema(new DevelApp.Utility.Model.IdentifierString("TypedStrings"), "Typed string schemas", topPart);

            JsonNode emitted = JsonSerializer.SerializeToNode(original.AsJsonSchema());
            JSBSchema parsed = JSBSchemaParser.ParseDocument(emitted, original.Name);

            //The native hardcoded patterns of the typed parts make them parse back
            //to the same part types
            JSBObject topObject = (JSBObject)parsed.TopPart;
            Assert.Equal(JSBPartType.Date, topObject.Properties["DateOfBirth"].PartType);
            Assert.Equal(JSBPartType.Time, topObject.Properties["WakeupTime"].PartType);
            Assert.Equal(JSBPartType.DateTime, topObject.Properties["Updated"].PartType);
            Assert.Equal(JSBPartType.Guid, topObject.Properties["Identifier"].PartType);
            Assert.Equal(JSBPartType.Uri, topObject.Properties["Website"].PartType);
            Assert.Equal(JSBPartType.Base64String, topObject.Properties["Signature"].PartType);

            JsonNode reemitted = JsonSerializer.SerializeToNode(parsed.AsJsonSchema());
            AssertJsonNodesSemanticallyEqual(emitted, reemitted);
        }

        [Fact]
        public void RoundTripEnumExamplesAndDefault()
        {
            JSBString colorPart = new JSBString("Color", "A color",
                defaultValue: "red",
                examples: new List<string>() { "red", "green" },
                enums: new List<string>() { "red", "green", "blue" });
            JSBInteger codePart = new JSBInteger("Code", "A code", defaultValue: 3, enums: new List<long?>() { 1, 3 });
            JSBObject topPart = new JSBObject("Constraints", "Constraints",
                props: new List<IJSBPart>() { colorPart, codePart });

            JSBSchema original = new JSBSchema(new DevelApp.Utility.Model.IdentifierString("Constraints"), "Constraint schemas", topPart);

            JsonNode emitted = JsonSerializer.SerializeToNode(original.AsJsonSchema());
            JSBSchema parsed = JSBSchemaParser.ParseDocument(emitted, original.Name);

            JSBObject topObject = (JSBObject)parsed.TopPart;
            JSBString parsedColor = (JSBString)topObject.Properties["Color"];
            //Default values are emitted to the schema document by the parts,
            //so a round trip recovers what was authored
            Assert.Equal("red", parsedColor.DefaultValue);
            Assert.Equal(new List<string>() { "red", "green" }, parsedColor.Examples);
            Assert.Equal(new List<string>() { "red", "green", "blue" }, parsedColor.Enums);
            JSBInteger parsedCode = (JSBInteger)topObject.Properties["Code"];
            Assert.Equal(3L, parsedCode.DefaultValue);
            Assert.Equal(2, parsedCode.Enums.Count);

            JsonNode reemitted = JsonSerializer.SerializeToNode(parsed.AsJsonSchema());
            AssertJsonNodesSemanticallyEqual(emitted, reemitted);
        }

        [Fact]
        public void RoundTripRefPartKeepsFragment()
        {
            JSBRef refPart = new JSBRef("Address", "A reference to an address", new Uri("./address.schema.json", UriKind.Relative), isRequired: true);
            JSBArray addressesPart = new JSBArray("Addresses", "The addresses", new List<IJSBPart>() { refPart });
            JSBObject topPart = new JSBObject("RefHolder", "Holds a reference", props: new List<IJSBPart>() { addressesPart });

            JSBSchema original = new JSBSchema(new DevelApp.Utility.Model.IdentifierString("RefHolder"), "Reference schemas", topPart);

            JsonNode emitted = JsonSerializer.SerializeToNode(original.AsJsonSchema());
            JSBSchema parsed = JSBSchemaParser.ParseDocument(emitted, original.Name);

            JSBObject topObject = (JSBObject)parsed.TopPart;
            JSBArray addresses = (JSBArray)topObject.Properties["Addresses"];
            JSBRef parsedRef = Assert.IsType<JSBRef>(addresses.Items[0]);
            Assert.Equal("./address.schema.json", parsedRef.IriReference.OriginalString);
            //Requiredness of array items does not exist on the wire so the
            //parsed item part is not required even if it was authored so
            Assert.False(parsedRef.IsRequired);

            JsonNode reemitted = JsonSerializer.SerializeToNode(parsed.AsJsonSchema());
            AssertJsonNodesSemanticallyEqual(emitted, reemitted);
        }

        [Fact]
        public void IdentifierStringValueSemanticsSupportDictionaryLookups()
        {
            //The pinned DevelApp.Utility must implement value equality so part
            //dictionaries can be looked up with fresh instances: version 1.0.6
            //(the last pre-fix package) broke this, version 1.0.7 added the
            //missing GetHashCode. All Properties[...] tests in this suite look
            //up by fresh instance so a bad regression pin fails loud here
            DevelApp.Utility.Model.IdentifierString first = new DevelApp.Utility.Model.IdentifierString("Name");
            DevelApp.Utility.Model.IdentifierString second = new DevelApp.Utility.Model.IdentifierString("Name");
            Assert.True(first.Equals(second));
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());

            Dictionary<DevelApp.Utility.Model.IdentifierString, string> lookup =
                new Dictionary<DevelApp.Utility.Model.IdentifierString, string>();
            lookup[first] = "value";
            Assert.Equal("value", lookup[second]);
        }

        private static void AssertParsedPartsMatch(JSBSchema original, JSBSchema parsed)
        {
            Assert.Equal(original.Name.ToString(), parsed.Name.ToString());
            Assert.Equal(original.SchemaVersion, parsed.SchemaVersion);
            Assert.Equal(original.PartType, parsed.PartType);
            JSBObject originalTop = (JSBObject)original.TopPart;
            JSBObject parsedTop = (JSBObject)parsed.TopPart;
            Assert.Equal(originalTop.Properties.Count, parsedTop.Properties.Count);
            Assert.Equal(originalTop.IsExpandable, parsedTop.IsExpandable);
            //Emission merges the definitions carried by the top part into the
            //root level container next to the schema level definitions so the
            //parse finds both as root level definitions
            Assert.Equal(original.Definitions.Count + originalTop.Definitions.Count, parsed.Definitions.Count);
        }

        /// <summary>
        /// Compares two JSON documents ignoring object key order. Array order
        /// matters. Value comparison: the builder emission pipeline is
        /// deterministic so the raw representations must match
        /// </summary>
        private static void AssertJsonNodesSemanticallyEqual(JsonNode expected, JsonNode actual)
        {
            if (expected == null && actual == null)
            {
                return;
            }
            Assert.NotNull(expected);
            Assert.NotNull(actual);
            if (expected is JsonObject expectedObject)
            {
                JsonObject actualObject = Assert.IsType<JsonObject>(actual);
                Assert.Equal(expectedObject.Count, actualObject.Count);
                foreach (KeyValuePair<string, JsonNode> expectedPair in expectedObject)
                {
                    Assert.True(actualObject.TryGetPropertyValue(expectedPair.Key, out JsonNode actualValue),
                        $"The key '{expectedPair.Key}' was not re-emitted. Expected: {expectedObject.ToJsonString()} Actual: {actualObject.ToJsonString()}");
                    AssertJsonNodesSemanticallyEqual(expectedPair.Value, actualValue);
                }
                return;
            }
            if (expected is JsonArray expectedArray)
            {
                JsonArray actualArray = Assert.IsType<JsonArray>(actual);
                Assert.Equal(expectedArray.Count, actualArray.Count);
                for (int i = 0; i < expectedArray.Count; i++)
                {
                    AssertJsonNodesSemanticallyEqual(expectedArray[i], actualArray[i]);
                }
                return;
            }
            Assert.Equal(expected.ToJsonString(), actual.ToJsonString());
        }

        #endregion
    }
}
