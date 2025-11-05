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
    }
}
