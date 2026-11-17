using DevelApp.JsonSchemaBuilder;
using System;
using System.Collections.Generic;
using Xunit;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using Manatee.Json;
using DevelApp.Utility.Model;
using System.Linq;
using DevelApp.JsonSchemaBuilder.CodeGeneration;
using DevelApp.JsonSchemaBuilder.DataTypes;
using Manatee.Json.Serialization;
using System.Net.Mail;
using UriType = DevelApp.JsonSchemaBuilder.DataTypes.Uri;
using GuidType = DevelApp.JsonSchemaBuilder.DataTypes.Guid;

namespace JsonSchemaBuilder.Test
{
    public class TestSchemaBuilding
    {
        [Fact]
        public void BuildNoValidation()
        {
            NoValidationJsonSchema noValidationJsonSchema = new NoValidationJsonSchema();
        }

        #region Email Tests
        
        [Fact]
        public void TestEmailDataClass()
        {
            // Test Email creation from string
            Email email1 = new Email("test@example.com");
            Assert.Equal("test@example.com", email1.Address);
            
            // Test implicit conversion from string
            Email email2 = "another@example.com";
            Assert.Equal("another@example.com", email2.Address);
            
            // Test implicit conversion to string
            string emailStr = email1;
            Assert.Equal("test@example.com", emailStr);
            
            // Test equality
            Email email3 = new Email("test@example.com");
            Assert.Equal(email1, email3);
        }

        [Fact]
        public void TestEmailFromMailAddress()
        {
            // Test Email creation from MailAddress
            MailAddress mailAddress = new MailAddress("user@example.com");
            Email email = new Email(mailAddress);
            Assert.Equal("user@example.com", email.Address);
            
            // Test implicit conversion from MailAddress
            Email email2 = mailAddress;
            Assert.Equal("user@example.com", email2.Address);
            
            // Test implicit conversion to MailAddress
            MailAddress converted = email;
            Assert.Equal("user@example.com", converted.Address);
        }

        [Fact]
        public void TestEmailNullHandling()
        {
            Email email1 = new Email("test@example.com");
            Email email2 = new Email("test@example.com");
            Email email3 = new Email("other@example.com");
            
            // Test equality with same address
            Assert.True(email1.Equals(email2));
            
            // Test inequality with different address
            Assert.False(email1.Equals(email3));
            
            // Test equality with string
            Assert.True(email1.Equals("test@example.com"));
            Assert.False(email1.Equals("other@example.com"));
            
            // Test with null
            Assert.False(email1.Equals(null));
        }

        [Fact]
        public void TestEmailProperties()
        {
            Email email = new Email("user@example.com");
            Assert.Equal("user@example.com", email.Address);
            Assert.Equal("example.com", email.Host);
            Assert.Equal("user", email.User);
            Assert.NotNull(email.MailAddress);
        }

        #endregion

        #region Integer Enum Tests

        [Fact]
        public void TestIntegerEnum()
        {
            List<long?> enums = new List<long?>() { 1, 2, 3, 5, 8 };
            var intEnum = new JSBInteger("FibNumber", "Fibonacci number", enums: enums, defaultValue: 3);
            var schema = intEnum.AsJsonSchema();
            Assert.NotNull(schema);
        }

        [Fact]
        public void TestIntegerEnumWithoutDefault()
        {
            List<long?> enums = new List<long?>() { 100, 200, 404, 500 };
            var intEnum = new JSBInteger("StatusCode", "HTTP status code", enums: enums);
            var schema = intEnum.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region Number Enum Tests

        [Fact]
        public void TestNumberEnum()
        {
            List<double?> enums = new List<double?>() { 1.5, 2.7, 3.14, 9.81 };
            var numEnum = new JSBNumber("Constant", "Mathematical constant", enums: enums, defaultValue: 3.14);
            var schema = numEnum.AsJsonSchema();
            Assert.NotNull(schema);
        }

        [Fact]
        public void TestNumberEnumWithNegative()
        {
            List<double?> enums = new List<double?>() { -1.0, 0.0, 1.0 };
            var numEnum = new JSBNumber("Temperature", "Temperature value", enums: enums);
            var schema = numEnum.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region Boolean Enum Tests

        [Fact]
        public void TestBooleanEnum()
        {
            List<bool?> enums = new List<bool?>() { true, false };
            var boolEnum = new JSBBoolean("Flag", "Boolean flag", enums: enums, defaultValue: true);
            var schema = boolEnum.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region Date Enum Tests

        [Fact]
        public void TestDateEnum()
        {
            var date = new JSBDate("ImportantDate", "Important dates", new DateTime(2020, 1, 1));
            var schema = date.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region DateTime Enum Tests

        [Fact]
        public void TestDateTimeEnum()
        {
            var dateTime = new JSBDateTime("Timestamp", "Event timestamp", new DateTime(2020, 1, 1, 12, 0, 0));
            var schema = dateTime.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region Time Enum Tests

        [Fact]
        public void TestTimeEnum()
        {
            var time = new JSBTime("EventTime", "Event time", new DateTime(2020, 1, 1, 15, 30, 0));
            var schema = time.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region Base64String Tests

        [Fact]
        public void TestBase64String()
        {
            var base64 = new JSBBase64String("EncodedData", "Base64 encoded data", defaultValue: "SGVsbG8gV29ybGQ=");
            var schema = base64.AsJsonSchema();
            Assert.NotNull(schema);
        }

        [Fact]
        public void TestBase64StringWithoutDefault()
        {
            var base64 = new JSBBase64String("BinaryData", "Binary data in base64");
            var schema = base64.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region JSBEmail Tests

        [Fact]
        public void TestJSBEmail()
        {
            var email = new JSBEmail("ContactEmail", "Contact email address", "test@example.com");
            var schema = email.AsJsonSchema();
            Assert.NotNull(schema);
        }

        [Fact]
        public void TestJSBEmailWithoutDefault()
        {
            var email = new JSBEmail("UserEmail", "User email address");
            var schema = email.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region Uri Data Type Tests

        [Fact]
        public void TestUriDataClass()
        {
            // Test Uri creation from string
            var uri1 = new UriType("https://example.com");
            Assert.Equal("https://example.com/", uri1.AbsoluteUri);
            
            // Test implicit conversion from string
            UriType uri2 = "https://google.com";
            Assert.Equal("https://google.com/", uri2.AbsoluteUri);
            
            // Test implicit conversion to string
            string uriStr = uri1;
            Assert.Equal("https://example.com/", uriStr);
            
            // Test properties
            Assert.Equal("https", uri1.Scheme);
            Assert.Equal("example.com", uri1.Host);
        }

        [Fact]
        public void TestUriFromSystemUri()
        {
            var systemUri = new System.Uri("https://example.com/path");
            UriType uri = systemUri;
            Assert.Equal("https://example.com/path", uri.AbsoluteUri);
            
            // Test conversion back
            System.Uri converted = uri;
            Assert.Equal(systemUri.AbsoluteUri, converted.AbsoluteUri);
        }

        [Fact]
        public void TestJSBUri()
        {
            var uri = new JSBUri("WebsiteUrl", "Website URL", "https://example.com");
            var schema = uri.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region Guid Data Type Tests

        [Fact]
        public void TestGuidDataClass()
        {
            // Test Guid creation from string
            string guidStr = "550e8400-e29b-41d4-a716-446655440000";
            var guid1 = new GuidType(guidStr);
            Assert.Equal(guidStr, guid1.ToString());
            
            // Test implicit conversion from string
            GuidType guid2 = guidStr;
            Assert.Equal(guidStr, guid2.ToString());
            
            // Test implicit conversion to string
            string converted = guid1;
            Assert.Equal(guidStr, converted);
            
            // Test equality
            Assert.Equal(guid1, guid2);
        }

        [Fact]
        public void TestGuidFromSystemGuid()
        {
            var systemGuid = System.Guid.NewGuid();
            GuidType guid = systemGuid;
            Assert.Equal(systemGuid.ToString(), guid.ToString());
            
            // Test conversion back
            System.Guid converted = guid;
            Assert.Equal(systemGuid, converted);
        }

        [Fact]
        public void TestGuidNewGuid()
        {
            var guid1 = GuidType.NewGuid();
            var guid2 = GuidType.NewGuid();
            Assert.NotEqual(guid1, guid2);
        }

        [Fact]
        public void TestJSBGuid()
        {
            var guid = new JSBGuid("RecordId", "Record identifier", "550e8400-e29b-41d4-a716-446655440000");
            var schema = guid.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region PhoneNumber Data Type Tests

        [Fact]
        public void TestPhoneNumberDataClass()
        {
            // Test PhoneNumber creation
            var phone1 = new PhoneNumber("+1 (555) 123-4567");
            Assert.Equal("+1 (555) 123-4567", phone1.Value);
            Assert.Equal("15551234567", phone1.DigitsOnly);
            
            // Test implicit conversion from string
            PhoneNumber phone2 = "+1-555-123-4567";
            Assert.Equal("15551234567", phone2.DigitsOnly);
            
            // Test implicit conversion to string
            string phoneStr = phone1;
            Assert.Equal("+1 (555) 123-4567", phoneStr);
        }

        [Fact]
        public void TestPhoneNumberEquality()
        {
            var phone1 = new PhoneNumber("+1 (555) 123-4567");
            var phone2 = new PhoneNumber("555-123-4567");
            var phone3 = new PhoneNumber("5551234567");
            
            // Different formatting but same digits should be equal
            Assert.Equal(phone2, phone3);
            
            // Different numbers should not be equal
            Assert.NotEqual(phone1, phone2); // Different due to country code
        }

        [Fact]
        public void TestJSBPhoneNumber()
        {
            var phone = new JSBPhoneNumber("ContactPhone", "Contact phone number", "+1-555-123-4567");
            var schema = phone.AsJsonSchema();
            Assert.NotNull(schema);
        }

        #endregion

        #region DataString Data Type Tests

        [Fact]
        public void TestDataStringDataClass()
        {
            // Test DataString creation from string
            DataString dataString1 = new DataString("Hello world");
            Assert.Equal("Hello world", dataString1.Value);
            Assert.Equal(11, dataString1.Length);
            Assert.False(dataString1.HasLimitations);

            // Test implicit conversion from string
            DataString dataString2 = "Another value";
            Assert.Equal("Another value", dataString2.Value);

            // Test implicit conversion to string
            string str = dataString1;
            Assert.Equal("Hello world", str);

            // Test equality (ordinal like ordinary strings)
            DataString dataString3 = new DataString("Hello world");
            Assert.Equal(dataString1, dataString3);
            Assert.True(dataString1.Equals("Hello world"));
            Assert.False(dataString1.Equals("hello world"));
            Assert.False(dataString1.Equals(null));
        }

        [Fact]
        public void TestDataStringLimitations()
        {
            // Value within all limitations
            DataString dataString = new DataString("abc123", minLength: 3, maxLength: 10, pattern: "^[a-z0-9]+$");
            Assert.True(dataString.HasLimitations);
            Assert.True(dataString.IsPatternMatched);
            Assert.Equal(3u, dataString.MinLength);
            Assert.Equal(10u, dataString.MaxLength);
            Assert.Equal("^[a-z0-9]+$", dataString.Pattern);

            // Too short
            Assert.Throws<ArgumentException>(() => new DataString("ab", minLength: 3));

            // Too long
            Assert.Throws<ArgumentException>(() => new DataString("abcdefghijk", maxLength: 10));

            // Pattern mismatch
            Assert.Throws<ArgumentException>(() => new DataString("ABC", pattern: "^[a-z]+$"));
        }

        [Fact]
        public void TestDataStringNullHandling()
        {
            // Null converts to null so nullable members behave like string
            DataString dataString = null;
            Assert.Null(dataString);

            string str = dataString;
            Assert.Null(str);

            // Explicit null rejection on construction
            Assert.Throws<ArgumentNullException>(() => new DataString(null));
        }

        [Fact]
        public void TestJSBDataString()
        {
            var dataString = new JSBDataString("MyDataString", "Data string with limitations", defaultValue: "abc123",
                minLength: 3, maxLength: 10, pattern: "^[a-z0-9]+$");
            var schema = dataString.AsJsonSchema();
            Assert.NotNull(schema);

            //The limitations are emitted to the schema
            //NB: default values are not emitted to the schema, they are only carried by code generation
            string json = schema.ToJson(new JsonSerializer()).ToString();
            Assert.Contains("\"type\":\"string\"", json);
            Assert.Contains("minLength", json);
            Assert.Contains("maxLength", json);
            Assert.Contains("pattern", json);
        }

        [Fact]
        public void TestJSBDataStringWithoutLimitations()
        {
            var dataString = new JSBDataString("MyPlainDataString", "Data string without limitations");
            var schema = dataString.AsJsonSchema();
            Assert.NotNull(schema);

            string json = schema.ToJson(new JsonSerializer()).ToString();
            Assert.Contains("\"type\":\"string\"", json);
            Assert.DoesNotContain("minLength", json);
            Assert.DoesNotContain("maxLength", json);
            Assert.DoesNotContain("pattern", json);
        }

        #endregion

        #region Schema Composition Tests (AnyOf, OneOf, AllOf)

        private static List<IJSBPart> CompositionSubSchemas()
        {
            return new List<IJSBPart>()
            {
                new JSBInteger("CompositionInt", "Integer variant", defaultValue: 42),
                new JSBString("CompositionString", "String variant", minLength: 1)
            };
        }

        [Fact]
        public void TestAnyOfComposition()
        {
            var anyOf = new JSBAnyOf("AnyOfPart", "AnyOf composition", CompositionSubSchemas());
            JsonValue schemaJson = anyOf.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            Assert.True(schemaJson.Object.ContainsKey("anyOf"), $"Expected anyOf keyword in schema: {schemaJson}");
            Assert.Equal(2, schemaJson.Object["anyOf"].Array.Count);
        }

        [Fact]
        public void TestOneOfComposition()
        {
            var oneOf = new JSBOneOf("OneOfPart", "OneOf composition", CompositionSubSchemas());
            JsonValue schemaJson = oneOf.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            Assert.True(schemaJson.Object.ContainsKey("oneOf"), $"Expected oneOf keyword in schema: {schemaJson}");
            Assert.Equal(2, schemaJson.Object["oneOf"].Array.Count);
        }

        [Fact]
        public void TestAllOfComposition()
        {
            var allOf = new JSBAllOf("AllOfPart", "AllOf composition", CompositionSubSchemas());
            JsonValue schemaJson = allOf.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            Assert.True(schemaJson.Object.ContainsKey("allOf"), $"Expected allOf keyword in schema: {schemaJson}");
            Assert.Equal(2, schemaJson.Object["allOf"].Array.Count);
        }

        [Fact]
        public void TestNestedDefinitionsOnObjectPart()
        {
            JSBString innerDefinition = new JSBString("InnerDefinition", "A definition carried by an object part", maxLength: 20);
            JSBObject nestedObject = new JSBObject("NestedObject", "Object carrying definitions on a part level",
                props: new List<IJSBPart>() { new JSBString("Name", "A property", isRequired: true) },
                defs: new List<IJSBPart>() { innerDefinition });
            JsonValue schemaJson = nestedObject.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            Assert.True(schemaJson.Object.ContainsKey("definitions"), $"Expected definitions keyword in the object part schema: {schemaJson}");
            Assert.True(schemaJson.Object["definitions"].Object.ContainsKey("InnerDefinition"), $"Expected the nested definition in the object part schema: {schemaJson}");
        }

        [Fact]
        public void TestNestedDefinitionsOnCompositionParts()
        {
            JSBString innerDefinition = new JSBString("InnerDefinition", "A definition carried by a composition part", maxLength: 20);
            List<IJSBPart> defs = new List<IJSBPart>() { innerDefinition };

            var anyOf = new JSBAnyOf("AnyOfPart", "AnyOf with definitions", CompositionSubSchemas(), defs: defs);
            JsonValue anyOfJson = anyOf.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            Assert.True(anyOfJson.Object.ContainsKey("definitions") && anyOfJson.Object["definitions"].Object.ContainsKey("InnerDefinition"), $"Expected the nested definition in the anyOf schema: {anyOfJson}");

            var oneOf = new JSBOneOf("OneOfPart", "OneOf with definitions", CompositionSubSchemas(), defs: defs);
            JsonValue oneOfJson = oneOf.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            Assert.True(oneOfJson.Object.ContainsKey("definitions") && oneOfJson.Object["definitions"].Object.ContainsKey("InnerDefinition"), $"Expected the nested definition in the oneOf schema: {oneOfJson}");

            var allOf = new JSBAllOf("AllOfPart", "AllOf with definitions", CompositionSubSchemas(), defs: defs);
            JsonValue allOfJson = allOf.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            Assert.True(allOfJson.Object.ContainsKey("definitions") && allOfJson.Object["definitions"].Object.ContainsKey("InnerDefinition"), $"Expected the nested definition in the allOf schema: {allOfJson}");
        }

        [Fact]
        public void TestDeeplyNestedDefinitionsInSchema()
        {
            //A definition carried by the top part that itself carries a definition
            JSBString innerDefinition = new JSBString("InnerDefinition", "Innermost definition", maxLength: 20);
            JSBObject middleDefinition = new JSBObject("MiddleDefinition", "Definition carrying a nested definition",
                props: new List<IJSBPart>() { new JSBString("Value", "A value in the middle definition", isRequired: true) },
                defs: new List<IJSBPart>() { innerDefinition });
            JSBObject topObject = new JSBObject("TopObject", "Top part with local definitions",
                props: new List<IJSBPart>() { new JSBString("Name", "A property", isRequired: true) },
                defs: new List<IJSBPart>() { middleDefinition });
            JSBSchema schema = new JSBSchema("DeeplyNestedSchema", "Schema with definitions below the top schema", topPart: topObject);
            JsonValue schemaJson = schema.AsJsonSchema().ToJson(new Manatee.Json.Serialization.JsonSerializer());
            //The top part is the root of the emitted schema so its definitions land at the root
            Assert.True(schemaJson.Object.ContainsKey("definitions"), $"Expected definitions at the root of the schema: {schemaJson}");
            Assert.True(schemaJson.Object["definitions"].Object.ContainsKey("MiddleDefinition"), $"Expected the top part definition at the root of the schema: {schemaJson}");
            JsonValue middleDefinitionJson = schemaJson.Object["definitions"].Object["MiddleDefinition"];
            Assert.True(middleDefinitionJson.Object.ContainsKey("definitions") && middleDefinitionJson.Object["definitions"].Object.ContainsKey("InnerDefinition"), $"Expected the nested definition below the middle definition: {schemaJson}");
        }

        [Fact]
        public void TestCompositionWithoutSubschemasThrows()
        {
            // The composition parts throw a JsonSchemaBuilderException (internal) when built
            // without subschemas, surfaced to callers as an Exception
            Assert.ThrowsAny<Exception>(() => new JSBAnyOf("EmptyAnyOf", "Empty", new List<IJSBPart>()).AsJsonSchema());
            Assert.ThrowsAny<Exception>(() => new JSBOneOf("EmptyOneOf", "Empty", new List<IJSBPart>()).AsJsonSchema());
            Assert.ThrowsAny<Exception>(() => new JSBAllOf("EmptyAllOf", "Empty", new List<IJSBPart>()).AsJsonSchema());
        }

        #endregion
    }
}
