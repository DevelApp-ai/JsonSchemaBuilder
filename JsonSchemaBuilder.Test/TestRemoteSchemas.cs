using DevelApp.JsonSchemaBuilder;
using DevelApp.JsonSchemaBuilder.CodeGeneration;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Json.Schema;
using JsonSchemaBuilder.Test.RemoteFixtures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace JsonSchemaBuilder.Test
{
    /// <summary>
    /// Tests for the remote schema pipeline (issue #10): remote references are
    /// opt-in, the first opt-in download writes a pin file with the url and the
    /// SHA-256 of the content, later runs re-download and verify against the pin
    /// and a mismatch is a hard failure. The downloaded schema is parsed into
    /// builder parts and code is generated from it. All tests use a fake
    /// downloader so no network is involved
    /// </summary>
    public class TestRemoteSchemas
    {
        private const string ApplicationRoot = "Remote";
        private const string JsonSchemaApplicationRoot = "RemoteJsonSchema";
        private const string AddressUrl = "https://example.com/Remote/address.schema.json";
        private const string GeoUrl = "https://example.com/Remote/geo.schema.json";

        private static readonly string AddressSchema = @"
{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""title"": ""RemoteAddress"",
  ""description"": ""An address parsed from a remote schema"",
  ""type"": ""object"",
  ""properties"": {
    ""street"": { ""type"": ""string"", ""description"": ""The street"" },
    ""zipCode"": { ""type"": ""string"", ""pattern"": ""^[0-9]{4}$"", ""description"": ""The zip code"" }
  },
  ""required"": [""street""],
  ""additionalProperties"": false
}";

        private static readonly string GeoSchema = @"
{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""title"": ""RemoteGeo"",
  ""description"": ""A geo location parsed from a remote schema"",
  ""type"": ""object"",
  ""properties"": {
    ""latitude"": { ""type"": ""number"" },
    ""longitude"": { ""type"": ""number"" }
  }
}";

        private static string AddressSchemaWithGeoRef = @"
{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""title"": ""RemoteAddress"",
  ""description"": ""An address parsed from a remote schema"",
  ""type"": ""object"",
  ""properties"": {
    ""street"": { ""type"": ""string"", ""description"": ""The street"" },
    ""geo"": { ""$ref"": """ + GeoUrl + @""", ""description"": ""The location"" }
  }
}";

        /// <summary>
        /// Fake downloader so no test does network calls. Records the urls asked for
        /// </summary>
        private class FakeSchemaDownloader : ISchemaDownloader
        {
            private readonly Dictionary<string, string> _contentByUrl = new Dictionary<string, string>();

            public List<string> DownloadedUrls { get; } = new List<string>();

            public void Serve(string url, string content)
            {
                _contentByUrl[url] = content;
            }

            public string Download(Uri uri)
            {
                DownloadedUrls.Add(uri.ToString());
                if (_contentByUrl.TryGetValue(uri.ToString(), out string content))
                {
                    return content;
                }
                throw new InvalidOperationException($"No fake content registered for {uri}");
            }
        }

        private static void CleanTestFolders()
        {
            foreach (string folder in new string[] { ApplicationRoot, JsonSchemaApplicationRoot })
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void RemoteReferenceWithoutOptInThrowsInstructiveException()
        {
            CleanTestFolders();

            CodeGenerator codeGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            codeGenerator.Register(new RemoteOwnerJsonSchema());

            Exception exception = Assert.ThrowsAny<Exception>(() => codeGenerator.Generate(Code.CSharp));
            Assert.Contains("opt-in", exception.Message);
            Assert.Contains(AddressUrl, exception.Message);
        }

        [Fact]
        public void FirstOptInRunDownloadsPinsRegistersAndGeneratesCode()
        {
            CleanTestFolders();

            FakeSchemaDownloader fakeDownloader = new FakeSchemaDownloader();
            fakeDownloader.Serve(AddressUrl, AddressSchema);
            CodeGenerator codeGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            codeGenerator.EnableRemoteSchemas = true;
            codeGenerator.RemoteSchemaDownloader = fakeDownloader;
            codeGenerator.Register(new RemoteOwnerJsonSchema());

            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.CSharp);

            //The mirror file lands under the json schema application root
            string mirror = Path.Combine(JsonSchemaApplicationRoot, "example", "com", "Remote", "address.schema.json");
            Assert.True(File.Exists(mirror), $"The mirror file {mirror} was not written");

            //The pin file records the url and the sha256 of the content
            string pinFile = mirror + ".pin";
            Assert.True(File.Exists(pinFile), $"The pin file {pinFile} was not written");
            JsonNode pin = JsonNode.Parse(File.ReadAllText(pinFile));
            Assert.Equal(AddressUrl, (string)pin["url"]);
            string expectedSha = Sha256HexOf(AddressSchema);
            Assert.Equal(expectedSha, (string)pin["sha256"]);

            //Code is generated from the parsed remote schema
            Assert.Contains(generated, tuple => tuple.fileName.Contains("RemoteAddress") && tuple.code.Contains("Street"));
            Assert.Contains(generated, tuple => tuple.fileName.Contains("RemoteOwner"));

            //Only one download for one unique remote url
            Assert.Single(fakeDownloader.DownloadedUrls);
            Assert.Equal(AddressUrl, fakeDownloader.DownloadedUrls[0]);
        }

        [Fact]
        public void ReferenceFragmentsIntoTheRemoteDefinitionsResolve()
        {
            CleanTestFolders();

            string addressSchema = @"
{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""title"": ""RemoteAddress"",
  ""description"": ""An address parsed from a remote schema"",
  ""type"": ""object"",
  ""properties"": {
    ""street"": { ""type"": ""string"" }
  },
  ""definitions"": {
    ""zip"": { ""type"": ""string"", ""pattern"": ""^[0-9]{4}$"" }
  }
}";
            FakeSchemaDownloader fakeDownloader = new FakeSchemaDownloader();
            fakeDownloader.Serve(AddressUrl, addressSchema);
            CodeGenerator codeGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            codeGenerator.EnableRemoteSchemas = true;
            codeGenerator.RemoteSchemaDownloader = fakeDownloader;
            codeGenerator.Register(new FragmentRefSchema());

            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.CSharp);

            (string fileName, string code) remoteClass = generated.FirstOrDefault(tuple => tuple.fileName.Contains("RemoteAddress"));
            Assert.NotEqual(default, remoteClass);
            Assert.Contains("Zip", remoteClass.code);
        }

        [Fact]
        public void LaterRunVerifiesAgainstThePinAndSucceeds()
        {
            CleanTestFolders();

            FakeSchemaDownloader fakeDownloader = new FakeSchemaDownloader();
            fakeDownloader.Serve(AddressUrl, AddressSchema);
            CodeGenerator firstGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            firstGenerator.EnableRemoteSchemas = true;
            firstGenerator.RemoteSchemaDownloader = fakeDownloader;
            firstGenerator.Register(new RemoteOwnerJsonSchema());
            firstGenerator.Generate(Code.CSharp);

            Assert.Single(fakeDownloader.DownloadedUrls);

            //A later run with a fresh generator downloads again, the content hash
            //matches the pinned content so the run succeeds
            FakeSchemaDownloader secondDownloader = new FakeSchemaDownloader();
            secondDownloader.Serve(AddressUrl, AddressSchema);
            CodeGenerator secondGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            secondGenerator.EnableRemoteSchemas = true;
            secondGenerator.RemoteSchemaDownloader = secondDownloader;
            secondGenerator.Register(new RemoteOwnerJsonSchema());
            List<(string fileName, string code)> generated = secondGenerator.Generate(Code.CSharp);

            Assert.Contains(generated, tuple => tuple.fileName.Contains("RemoteAddress"));
            Assert.Single(secondDownloader.DownloadedUrls);
        }

        [Fact]
        public void ChangedRemoteContentFailsHardAgainstThePin()
        {
            CleanTestFolders();

            FakeSchemaDownloader fakeDownloader = new FakeSchemaDownloader();
            fakeDownloader.Serve(AddressUrl, AddressSchema);
            CodeGenerator firstGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            firstGenerator.EnableRemoteSchemas = true;
            firstGenerator.RemoteSchemaDownloader = fakeDownloader;
            firstGenerator.Register(new RemoteOwnerJsonSchema());
            firstGenerator.Generate(Code.CSharp);

            string changedSchema = AddressSchema.Replace("\"An address parsed from a remote schema\"", "\"A new description\"");

            FakeSchemaDownloader changedDownloader = new FakeSchemaDownloader();
            changedDownloader.Serve(AddressUrl, changedSchema);
            CodeGenerator secondGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            secondGenerator.EnableRemoteSchemas = true;
            secondGenerator.RemoteSchemaDownloader = changedDownloader;
            secondGenerator.Register(new RemoteOwnerJsonSchema());

            Exception exception = Assert.ThrowsAny<Exception>(() => secondGenerator.Generate(Code.CSharp));
            Assert.Contains("changed since it was pinned", exception.Message);
        }

        [Fact]
        public void RemoteSchemaPullingInAnotherRemoteSchemaResolvesToFixpoint()
        {
            CleanTestFolders();

            FakeSchemaDownloader fakeDownloader = new FakeSchemaDownloader();
            fakeDownloader.Serve(AddressUrl, AddressSchemaWithGeoRef);
            fakeDownloader.Serve(GeoUrl, GeoSchema);
            CodeGenerator codeGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            codeGenerator.EnableRemoteSchemas = true;
            codeGenerator.RemoteSchemaDownloader = fakeDownloader;
            codeGenerator.Register(new RemoteOwnerJsonSchema());

            //The remote address references another remote schema: the walk must
            //register and generate code for both
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.CSharp);

            Assert.Contains(generated, tuple => tuple.fileName.Contains("RemoteAddress"));
            Assert.Contains(generated, tuple => tuple.fileName.Contains("RemoteGeo"));
            Assert.Contains(fakeDownloader.DownloadedUrls, url => url.Equals(AddressUrl));
            Assert.Contains(fakeDownloader.DownloadedUrls, url => url.Equals(GeoUrl));
        }

        [Fact]
        public void UnparsableRemoteSchemaFailsLoud()
        {
            CleanTestFolders();

            string unparsable = @"
{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""type"": ""object"",
  ""properties"": {
    ""weird"": { ""type"": ""string"", ""patternProperties"": { ""^a"": { ""type"": ""string"" } } }
  }
}";
            FakeSchemaDownloader fakeDownloader = new FakeSchemaDownloader();
            fakeDownloader.Serve(AddressUrl, unparsable);
            CodeGenerator codeGenerator = new CodeGenerator(ApplicationRoot, JsonSchemaApplicationRoot);
            codeGenerator.EnableRemoteSchemas = true;
            codeGenerator.RemoteSchemaDownloader = fakeDownloader;
            codeGenerator.Register(new RemoteOwnerJsonSchema());

            Exception exception = Assert.ThrowsAny<Exception>(() => codeGenerator.Generate(Code.CSharp));
            Assert.Contains(AddressUrl, exception.Message);
            Assert.Contains("patternProperties", exception.Message);
        }

        private static string Sha256HexOf(string content)
        {
            using (System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
                return string.Concat(hash.Select(hashByte => hashByte.ToString("x2")));
            }
        }
    }
}
