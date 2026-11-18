using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Json.Schema;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// A schema definition that was pulled in from a remote uri during
    /// reference resolution, parsed into builder parts and registered like a
    /// locally authored schema. FileName is the mirror file path so references
    /// from other schemas resolve against the exact registry key they were
    /// looked up with
    /// </summary>
    public class RemoteSchemaDefinition : IJsonSchemaDefinition
    {
        private JsonSchema _jsonSchema;

        public RemoteSchemaDefinition(JSBSchema jsonSchemaBuilderSchema, NamespaceString module, string fileName)
        {
            JsonSchemaBuilderSchema = jsonSchemaBuilderSchema;
            Module = module;
            FileName = fileName;
        }

        /// <summary>
        /// The name of the schema parsed from the remote document
        /// </summary>
        public IdentifierString Name
        {
            get
            {
                return JsonSchemaBuilderSchema.Name;
            }
        }

        /// <summary>
        /// The namespace derived from the remote mirror path (host segments first)
        /// </summary>
        public NamespaceString Module { get; }

        /// <summary>
        /// The description of the parsed schema document
        /// </summary>
        public string Description
        {
            get
            {
                return JsonSchemaBuilderSchema.Description;
            }
        }

        /// <summary>
        /// The mirror file path of the remote schema, which is also the registry key
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// The complete parsed builder part tree of the remote schema
        /// </summary>
        public JSBSchema JsonSchemaBuilderSchema { get; }

        /// <summary>
        /// Returns the complete JsonSchema built with Json.Schema (json-everything)
        /// </summary>
        public JsonSchema JsonSchema
        {
            get
            {
                if (_jsonSchema == null)
                {
                    _jsonSchema = JsonSchemaBuilderSchema.AsJsonSchema();
                }
                return _jsonSchema;
            }
        }

        /// <summary>
        /// Write the schema to the mirror file location
        /// </summary>
        /// <param name="applicationRoot"></param>
        public void WriteSchemaToFile(string applicationRoot)
        {
            JsonNode schemaInJson = JsonSerializer.SerializeToNode(JsonSchema, JSBJsonSchemaBuild.SerializerOptions);
            //The mirror path of an absolute uri starts with a path separator,
            //which Path.Combine would treat as a rooted path on POSIX systems, so it is trimmed
            string localFile = Path.Combine(applicationRoot,
                FileName.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            FileInfo fileInfo = new FileInfo(localFile);
            string directory = fileInfo.Directory.FullName;
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(localFile, schemaInJson.ToJsonString(JSBJsonSchemaBuild.IndentedSerializerOptions));
        }

        private CodeGenerator CodeGeneration(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            codeGenerator.Register(this);
            return codeGenerator;
        }

        /// <summary>
        /// Generate code to memory and suggest a filename
        /// </summary>
        /// <param name="code"></param>
        /// <param name="applicationRoot"></param>
        /// <param name="jsonSchemaApplicationRoot"></param>
        /// <returns></returns>
        public (string fileName, string code) GenerateCode(Code code, string applicationRoot, string jsonSchemaApplicationRoot)
        {
            return CodeGeneration(applicationRoot, jsonSchemaApplicationRoot).Generate(code).FirstOrDefault();
        }

        /// <summary>
        /// Generate code to a path
        /// </summary>
        /// <param name="code"></param>
        /// <param name="applicationRoot"></param>
        /// <param name="jsonSchemaApplicationRoot"></param>
        public void GenerateCodeToFile(Code code, string applicationRoot, string jsonSchemaApplicationRoot)
        {
            CodeGeneration(applicationRoot, jsonSchemaApplicationRoot).GenerateToFile(code);
        }
    }
}
