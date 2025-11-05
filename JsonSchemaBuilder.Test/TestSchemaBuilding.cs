using DevelApp.JsonSchemaBuilder;
using System;
using System.Collections.Generic;
using Xunit;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using System.Linq;
using DevelApp.JsonSchemaBuilder.CodeGeneration;
using DevelApp.JsonSchemaBuilder.DataTypes;
using System.Net.Mail;

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
    }
}
