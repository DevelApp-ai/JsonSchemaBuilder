using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Manatee.Json;
using Manatee.Json.Schema;
using Manatee.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    public class CodeGenerator
    {
        public Dictionary<string, IJsonSchemaDefinition> RegisteredJsonSchemas { get; } = new Dictionary<string, IJsonSchemaDefinition>();

        /// <summary>
        /// Raw schema class names (keyed 'module|typeName') that have already been
        /// emitted somewhere in this generation run, so a raw schema referenced by
        /// several registered schemas in the same module is only emitted once
        /// </summary>
        public HashSet<string> EmittedRawTypeNames { get; } = new HashSet<string>();

        public CodeGenerator(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            ApplicationRoot = applicationRoot;
            JsonSchemaApplicationRoot = jsonSchemaApplicationRoot;
        }

        public string ApplicationRoot { get; }
        public string JsonSchemaApplicationRoot { get; }

        /// <summary>
        /// Generate a list of code to memory and suggest a filename from registered IJsonSchemaDefinitions.
        /// The serializer option is only consumed by Code.CSharp; the other code types ignore it
        /// </summary>
        /// <param name="code"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public List<(string fileName, string code)> Generate(Code code, Serializer serializer = Serializer.Newtonsoft)
        {
            DoReferenceResolution();

            switch (code)
            {
                case Code.CSharp:
                    return CSharp.GenerateCode(this, serializer);
                case Code.PHP:
                    return PHP.GenerateCode(this);
                case Code.TypeScript:
                    return TypeScript.GenerateCode(this);
                case Code.VB:
                    return VB.GenerateCode(this);
                case Code.Java:
                    return Java.GenerateCode(this);
                case Code.TSQL_DDL:
                    return TSQL.GenerateCode(this);
                case Code.CSharp_Entities:
                    return CSharpEntities.GenerateCode(this);
                default:
                    throw new CodeGenerationException($"Code generation of {code} is not supported");
            }
        }

        /// <summary>
        /// Makes schema resolution and moves to local file structure
        /// </summary>
        /// <param name="schema"></param>
        private void DoReferenceResolution()
        {
            foreach (IJsonSchemaDefinition schema in RegisteredJsonSchemas.Values)
            {
                DoReferenceResolution(schema.JsonSchemaBuilderSchema, schema.Module);
            }
        }

        /// <summary>
        /// Register all the IJsonSchemaDefinition generated in one go to avoid ordering problems for references
        /// </summary>
        /// <param name="jsonSchema"></param>
        public void Register(IJsonSchemaDefinition jsonSchema)
        {
            RegisteredJsonSchemas.Add(jsonSchema.FileName, jsonSchema);
        }

        /// <summary>
        /// Finds the registry key (FileName) of the registered schema definition carrying
        /// the given schema. Fragment only references resolve against the schema being
        /// generated, and the registry is keyed by the definition FileName, which is
        /// derived from the definition class name and can differ from the internal
        /// schema name
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="registeredFileName"></param>
        /// <returns></returns>
        public bool TryGetRegisteredFileName(JSBSchema schema, out string registeredFileName)
        {
            foreach (var pair in RegisteredJsonSchemas)
            {
                if (ReferenceEquals(pair.Value.JsonSchemaBuilderSchema, schema))
                {
                    registeredFileName = pair.Key;
                    return true;
                }
            }
            registeredFileName = null;
            return false;
        }

        private void DoReferenceResolution(IJSBPart jsonSchemaBuilderPart, NamespaceString module)
        {
            //Walk definitions carried by any part so definitions on other levels
            //than the top schema get reference resolution as well
            foreach (IJSBPart definition in jsonSchemaBuilderPart.Definitions.Values)
            {
                DoReferenceResolution(definition, module);
            }
            switch (jsonSchemaBuilderPart.PartType)
            {
                case JSBPartType.IriReference:
                    JSBRef jsonSchemaBuilderIriReference = jsonSchemaBuilderPart as JSBRef;
                    if (!jsonSchemaBuilderIriReference.IsFragmentOnly)
                    {
                        string relativeLocalFileWithExpandedDot = jsonSchemaBuilderIriReference.RelativeLocalFile;
                        if (relativeLocalFileWithExpandedDot.StartsWith("./") || relativeLocalFileWithExpandedDot.StartsWith(".\\"))
                        {
                            relativeLocalFileWithExpandedDot = Path.Combine(module.ToFilePath, relativeLocalFileWithExpandedDot.Remove(0, 2));
                        }

                        string localFile = Path.Combine(JsonSchemaApplicationRoot, TransformToCamelCase(relativeLocalFileWithExpandedDot));
                        string uriWithoutFragmentString = jsonSchemaBuilderIriReference.IriReference.OriginalString;
                        if (!string.IsNullOrWhiteSpace(jsonSchemaBuilderIriReference.Fragment))
                        {
                            uriWithoutFragmentString = uriWithoutFragmentString.Replace("#" + jsonSchemaBuilderIriReference.Fragment, "");
                        }
                        if (Uri.TryCreate(uriWithoutFragmentString, UriKind.RelativeOrAbsolute, out Uri uriWithoutFragment))
                        {
                            if (!File.Exists(localFile))
                            {
                                if (jsonSchemaBuilderIriReference.IriReference.IsAbsoluteUri && !jsonSchemaBuilderIriReference.IriReference.IsLoopback)
                                {
                                    try
                                    {
                                        FileInfo fileInfo = new FileInfo(localFile);
                                        string directory = fileInfo.Directory.FullName;
                                        if (!Directory.Exists(directory))
                                        {
                                            Directory.CreateDirectory(directory);
                                        }
                                        using (WebClient myWebClient = new WebClient())
                                        {
                                            // Download the Web resource and save it into the current filesystem folder.
                                            myWebClient.DownloadFile(uriWithoutFragment, localFile);
                                            //TODO generate builderparts from schema and register in _registeredJsonSchemas
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new CodeGenerationException($"Resource at {uriWithoutFragment} could not be downloaded", ex);
                                    }
                                }
                                else
                                {
                                    throw new CodeGenerationException($"Iri reference {uriWithoutFragment} was not found locally at {localFile}");
                                }
                            }
                        }
                    }
                    break;
                case JSBPartType.Schema:
                    //Definitions of the schema part are walked by the shared walk above
                    JSBSchema jsonSchemaBuilderSchema = jsonSchemaBuilderPart as JSBSchema;
                    if (jsonSchemaBuilderSchema.TopPart != null)
                    {
                        DoReferenceResolution(jsonSchemaBuilderSchema.TopPart, module);
                    }
                    break;
                case JSBPartType.Array:
                    JSBArray jsonSchemaBuilderArray = jsonSchemaBuilderPart as JSBArray;
                    foreach (IJSBPart part in jsonSchemaBuilderArray.Items)
                    {
                        DoReferenceResolution(part, module);
                    }
                    break;
                case JSBPartType.Object:
                    JSBObject jsonSchemaBuilderObject = jsonSchemaBuilderPart as JSBObject;
                    foreach (IJSBPart property in jsonSchemaBuilderObject.Properties.Values)
                    {
                        DoReferenceResolution(property, module);
                    }
                    break;
                case JSBPartType.AllOf:
                    JSBAllOf jsonSchemaBuilderAllOf = jsonSchemaBuilderPart as JSBAllOf;
                    foreach (IJSBPart subSchema in jsonSchemaBuilderAllOf.Schemas)
                    {
                        DoReferenceResolution(subSchema, module);
                    }
                    break;
                case JSBPartType.AnyOf:
                    JSBAnyOf jsonSchemaBuilderAnyOf = jsonSchemaBuilderPart as JSBAnyOf;
                    foreach (IJSBPart subSchema in jsonSchemaBuilderAnyOf.Schemas)
                    {
                        DoReferenceResolution(subSchema, module);
                    }
                    break;
                case JSBPartType.OneOf:
                    JSBOneOf jsonSchemaBuilderOneOf = jsonSchemaBuilderPart as JSBOneOf;
                    foreach (IJSBPart subSchema in jsonSchemaBuilderOneOf.Schemas)
                    {
                        DoReferenceResolution(subSchema, module);
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Generate code to a path from registered IJsonSchemaDefinitions
        /// </summary>
        /// <param name="code"></param>
        public void GenerateToFile(Code code, Serializer serializer = Serializer.Newtonsoft)
        {
            List<(string fileName, string code)> tupleList = Generate(code, serializer);

            foreach (var tuple in tupleList)
            {
                FileInfo fileInfo = new FileInfo(tuple.fileName);
                string directory = fileInfo.Directory.FullName;
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
                catch (Exception ex)
                {
                    throw new CodeGenerationException($"Could not create path {directory}", ex);
                }

                try
                {
                    using (StreamWriter outputFile = new StreamWriter(tuple.fileName))
                    {
                        outputFile.Write(tuple.code);
                        outputFile.Flush();
                    }
                }
                catch (Exception ex)
                {
                    throw new CodeGenerationException($"Could not write code file to {tuple.fileName}", ex);
                }
            }
        }

        /// <summary>
        /// Returns null if not found
        /// </summary>
        /// <param name="relativeFileNameWithExpandedDot"></param>
        /// <param name="jSBRef"></param>
        /// <returns></returns>
        public (IJSBPart refPart, JsonValue schemaValue) LookupReferencedPart(string relativeFileNameWithExpandedDot, JSBRef jSBRef)
        {
            if (RegisteredJsonSchemas.TryGetValue(relativeFileNameWithExpandedDot, out IJsonSchemaDefinition jsonSchemaDefinition))
            {
                if (jSBRef.Fragment.ToLowerInvariant().StartsWith("/definitions/"))
                {
                    IJSBPart referencedPart = LookupReferencedPartFromDefinitions(jsonSchemaDefinition.JsonSchemaBuilderSchema, jSBRef.Fragment);
                    if (referencedPart != null)
                    {
                        return (referencedPart, null);
                    }
                    //fall through: a /definitions/ fragment the part tree cannot resolve
                    //resolves against the emitted schema json below
                }
                else if (string.IsNullOrWhiteSpace(jSBRef.Fragment) ||
                    jSBRef.Fragment.Equals("/"))
                {
                    return (jsonSchemaDefinition.JsonSchemaBuilderSchema, null);
                }
                return (null, ResolveJsonPointer(ToJsonValue(jsonSchemaDefinition.JsonSchemaBuilderSchema), jSBRef.Fragment));
            }

            //Search for the schema file as .schema.json and .json and load it when found
            string schemaString = string.Empty;

            string localfile = Path.Combine(JsonSchemaApplicationRoot, relativeFileNameWithExpandedDot);
            if (File.Exists(localfile))
            {
                schemaString = File.ReadAllText(localfile);
            }
            else
            {
                throw new CodeGenerationException($"Schema could not be found at the path {localfile}");
            }

            // make a schema and generate code from that
            JsonValue jsonValueOfSchema = JsonValue.Parse(schemaString);
            return (null, ResolveJsonPointer(jsonValueOfSchema, jSBRef.Fragment));
        }

        /// <summary>
        /// Serializes a builder schema to its emitted json so reference fragments
        /// can be resolved against exactly what a schema consumer sees
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        public JsonValue ToJsonValue(JSBSchema schema)
        {
            return new JsonSerializer().Serialize(schema.AsJsonSchema());
        }

        /// <summary>
        /// Evaluates a reference fragment as an RFC 6901 JSON Pointer (without the
        /// leading '#') against a schema given as JsonValue. An empty fragment or
        /// '/' returns the entire schema. Keys inside a definitions container keep
        /// the historical lenient matching (exact, then TitleCase, then camelCase)
        /// since builder parts name definitions in TitleCase while authored
        /// references usually use camelCase. All other segments match exactly
        /// </summary>
        /// <param name="schemaValue"></param>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public JsonValue ResolveJsonPointer(JsonValue schemaValue, string fragment)
        {
            if (string.IsNullOrWhiteSpace(fragment) || fragment == "/")
            {
                return schemaValue;
            }
            string trimmedFragment = fragment.StartsWith("/") ? fragment.Substring(1) : fragment;
            string[] rawSegments = trimmedFragment.Split('/');
            JsonValue current = schemaValue;
            //True while the segment being looked up is a key inside a definitions container
            bool insideDefinitions = false;
            foreach (string rawSegment in rawSegments)
            {
                string segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
                if (current.Type == JsonValueType.Object)
                {
                    JsonValue next = null;
                    if (!current.Object.TryGetValue(segment, out next) && insideDefinitions)
                    {
                        string titleKey = TransformToTitleCase(segment);
                        string camelKey = TransformToCamelCase(segment);
                        if (!current.Object.TryGetValue(titleKey, out next))
                        {
                            current.Object.TryGetValue(camelKey, out next);
                        }
                    }
                    if (next == null)
                    {
                        throw new CodeGenerationException($"Json reference fragment '{fragment}' does not resolve: '{segment}' was not found");
                    }
                    insideDefinitions = segment == "definitions";
                    current = next;
                }
                else if (current.Type == JsonValueType.Array)
                {
                    if (!int.TryParse(segment, out int index) || index < 0 || index >= current.Array.Count)
                    {
                        throw new CodeGenerationException($"Json reference fragment '{fragment}' does not resolve: '{segment}' is not a valid array index");
                    }
                    insideDefinitions = false;
                    current = current.Array[index];
                }
                else
                {
                    throw new CodeGenerationException($"Json reference fragment '{fragment}' does not resolve: cannot descend into a scalar at '{segment}'");
                }
            }
            return current;
        }

        /// <summary>
        /// Resolves a /definitions/ fragment against a registered schema, including nested
        /// definition paths like #/definitions/outer/definitions/inner now that parts can
        /// carry definitions on other levels than the top schema. The first segment can
        /// resolve against the schema definitions or against the definitions of the top
        /// part since the top part is the root of the emitted schema. Returns null when
        /// the fragment cannot be resolved
        /// </summary>
        /// <param name="schema"></param>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public IJSBPart LookupReferencedPartFromDefinitions(JSBSchema schema, string fragment)
        {
            string[] segments = fragment.Split(new[] { "/definitions/" }, StringSplitOptions.RemoveEmptyEntries);
            IJSBPart currentPart = schema;
            foreach (string segment in segments)
            {
                string definitionKey = TransformToTitleCase(segment);
                IJSBPart nextPart = null;
                if (!currentPart.Definitions.TryGetValue(definitionKey, out nextPart)
                    && ReferenceEquals(currentPart, schema)
                    && schema.TopPart != null)
                {
                    schema.TopPart.Definitions.TryGetValue(definitionKey, out nextPart);
                }
                if (nextPart == null)
                {
                    return null;
                }
                currentPart = nextPart;
            }
            if (ReferenceEquals(currentPart, schema))
            {
                return null;
            }
            return currentPart;
        }

        /// <summary>
        /// Transform string from TitleCase to camelCase. Ignores everything before \ or / if existing
        /// </summary>
        /// <param name="stringToTransform"></param>
        /// <returns></returns>
        public string TransformToCamelCase(string stringToTransform)
        {
            if(string.IsNullOrWhiteSpace(stringToTransform))
            {
                return stringToTransform;
            }
            int index = stringToTransform.LastIndexOfAny(new char[] { '\\', '/' });

            if (index >= 0 && stringToTransform.Length > index+2)
            {
                return stringToTransform.Substring(0, index + 1) + stringToTransform.Substring(index + 1, 1).ToLowerInvariant() + stringToTransform.Substring(index + 2);
            }
            else
            {
                return stringToTransform.Substring(0, 1).ToLowerInvariant() + stringToTransform.Substring(1);
            }
        }

        /// <summary>
        /// Transform string from camelCase to TitleCase. Ignores everything before \ or / if existing
        /// </summary>
        /// <param name="stringToTransform"></param>
        /// <returns></returns>
        public string TransformToTitleCase(string stringToTransform)
        {
            if (string.IsNullOrWhiteSpace(stringToTransform))
            {
                return stringToTransform;
            }
            int index = stringToTransform.LastIndexOfAny(new char[] { '\\', '/' });

            if (index >= 0 && stringToTransform.Length > index + 2)
            {
                return stringToTransform.Substring(0, index + 1) + stringToTransform.Substring(index + 1, 1).ToUpperInvariant() + stringToTransform.Substring(index + 2);
            }
            else
            {
                return stringToTransform.Substring(0, 1).ToUpperInvariant() + stringToTransform.Substring(1);
            }
        }


    }
}
