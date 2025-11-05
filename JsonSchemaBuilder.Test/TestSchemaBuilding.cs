using DevelApp.JsonSchemaBuilder;
using System;
using System.Collections.Generic;
using Xunit;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using System.Linq;
using DevelApp.JsonSchemaBuilder.CodeGeneration;
using DevelApp.JsonSchemaBuilder.DataTypes;

namespace JsonSchemaBuilder.Test
{
    public class TestSchemaBuilding
    {
        [Fact]
        public void BuildNoValidation()
        {
            NoValidationJsonSchema noValidationJsonSchema = new NoValidationJsonSchema();
        }

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
        public void TestIntegerEnum()
        {
            List<long?> enums = new List<long?>() { 1, 2, 3, 5, 8 };
            var intEnum = new JSBInteger("FibNumber", "Fibonacci number", enums: enums, defaultValue: 3);
            var schema = intEnum.AsJsonSchema();
            Assert.NotNull(schema);
        }

        [Fact]
        public void TestBase64String()
        {
            var base64 = new JSBBase64String("EncodedData", "Base64 encoded data", defaultValue: "SGVsbG8gV29ybGQ=");
            var schema = base64.AsJsonSchema();
            Assert.NotNull(schema);
        }
    }
}
