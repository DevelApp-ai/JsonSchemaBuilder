using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    public class CodeGenerator
    {
        /// <summary>
        /// The schema file ending used for mirror files and registry keys
        /// </summary>
        private const string SCHEMA_FILE_ENDING = ".schema.json";

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
        /// Opt-in for remote schema resolution. Remote schema references are
        /// only downloaded, pinned and parsed into builder parts when this is
        /// enabled. The default is false so code generation stays offline and
        /// deterministic until the opt-in is set
        /// </summary>
        public bool EnableRemoteSchemas { get; set; } = false;

        /// <summary>
        /// The downloader used for remote schema downloads. Injectable so tests
        /// and hosts can supply deterministic fake downloads
        /// </summary>
        public ISchemaDownloader RemoteSchemaDownloader { get; set; } = new HttpSchemaDownloader();

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
        /// Makes schema resolution and moves to local file structure. Remote
        /// registrations add entries to RegisteredJsonSchemas during the walk,
        /// so the walk processes a snapshot and loops until no new schemas were
        /// registered. Remote schemas are walked like local ones so references
        /// inside them can pull in further remotes
        /// </summary>
        /// <param name="schema"></param>
        private void DoReferenceResolution()
        {
            HashSet<string> walkedFileNames = new HashSet<string>();
            bool walkedAll = false;
            while (!walkedAll)
            {
                walkedAll = true;
                List<IJsonSchemaDefinition> snapshot = new List<IJsonSchemaDefinition>(RegisteredJsonSchemas.Values);
                foreach (IJsonSchemaDefinition schema in snapshot)
                {
                    if (walkedFileNames.Add(schema.FileName))
                    {
                        walkedAll = false;
                        DoReferenceResolution(schema.JsonSchemaBuilderSchema, schema.Module);
                    }
                }
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

                        //The placement of an absolute uri starts with a path separator,
                        //which Path.Combine would treat as a rooted path on POSIX systems.
                        //It is trimmed so the mirror lands under the application root
                        string localFile = Path.Combine(JsonSchemaApplicationRoot,
                            TransformToCamelCase(relativeLocalFileWithExpandedDot)
                                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                        string uriWithoutFragmentString = jsonSchemaBuilderIriReference.IriReference.OriginalString;
                        if (!string.IsNullOrWhiteSpace(jsonSchemaBuilderIriReference.Fragment))
                        {
                            uriWithoutFragmentString = uriWithoutFragmentString.Replace("#" + jsonSchemaBuilderIriReference.Fragment, "");
                        }
                        if (Uri.TryCreate(uriWithoutFragmentString, UriKind.RelativeOrAbsolute, out Uri uriWithoutFragment))
                        {
                            bool isRemote = jsonSchemaBuilderIriReference.IriReference.IsAbsoluteUri && !jsonSchemaBuilderIriReference.IriReference.IsLoopback;
                            if (RegisteredJsonSchemas.ContainsKey(relativeLocalFileWithExpandedDot))
                            {
                                //Already registered in this run, for example pulled in by another remote reference
                            }
                            else if (isRemote)
                            {
                                if (!EnableRemoteSchemas)
                                {
                                    if (!File.Exists(localFile))
                                    {
                                        throw new CodeGenerationException($"Remote schema reference {uriWithoutFragment} was not found locally at {localFile}. Remote schema resolution is opt-in: set {nameof(EnableRemoteSchemas)} to true to download, pin and generate code from remote schemas");
                                    }
                                    //A pre-placed mirror without registration keeps the historical raw JSON fallback behavior
                                }
                                else
                                {
                                    DownloadAndRegisterRemoteSchema(uriWithoutFragment, relativeLocalFileWithExpandedDot, localFile);
                                }
                            }
                            else if (!File.Exists(localFile))
                            {
                                throw new CodeGenerationException($"Iri reference {uriWithoutFragment} was not found locally at {localFile}");
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
        /// Downloads a remote schema, verifies it against the pin file (or pins
        /// it on the first opt-in download), writes the local mirror and parses
        /// the document into builder parts so code is generated from it like
        /// from a locally authored schema. The content is downloaded on every
        /// run so drift from the pinned content is a hard failure
        /// </summary>
        /// <param name="uriWithoutFragment"></param>
        /// <param name="relativeLocalFileWithExpandedDot">The registry key the reference is looked up with</param>
        /// <param name="localFile">The mirror file path</param>
        private void DownloadAndRegisterRemoteSchema(Uri uriWithoutFragment, string relativeLocalFileWithExpandedDot, string localFile)
        {
            string url = uriWithoutFragment.ToString();

            string content;
            try
            {
                content = RemoteSchemaDownloader.Download(uriWithoutFragment);
            }
            catch (Exception ex)
            {
                throw new CodeGenerationException($"Resource at {url} could not be downloaded", ex);
            }
            string sha256 = Sha256Hex(content);

            string pinFile = localFile + ".pin";
            if (File.Exists(pinFile))
            {
                //Verify against the pin: a remote schema that changed without repinning is a hard failure
                string pinUrl = null;
                string pinSha256 = null;
                try
                {
                    JsonNode pinNode = JsonNode.Parse(File.ReadAllText(pinFile));
                    pinUrl = pinNode?["url"]?.GetValue<string>();
                    pinSha256 = pinNode?["sha256"]?.GetValue<string>();
                }
                catch (Exception ex)
                {
                    throw new CodeGenerationException($"Pin file {pinFile} could not be read. If it is stale, delete it and re-run", ex);
                }
                if (!string.Equals(pinUrl, url, StringComparison.Ordinal))
                {
                    throw new CodeGenerationException($"Pin file {pinFile} pins url {pinUrl} but the reference points at {url}. Delete the pin file and re-run to pin the new url");
                }
                if (!string.Equals(pinSha256, sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CodeGenerationException($"The remote schema at {url} changed since it was pinned (pinned sha256 {pinSha256}, downloaded sha256 {sha256}). If the change is intentional, delete the pin file {pinFile} and re-run to pin the new content");
                }
            }
            else
            {
                //First opt-in download: pin the url and the content hash
                try
                {
                    JsonObject pinNode = new JsonObject();
                    pinNode["url"] = url;
                    pinNode["sha256"] = sha256;
                    FileInfo pinFileInfo = new FileInfo(pinFile);
                    if (!Directory.Exists(pinFileInfo.Directory.FullName))
                    {
                        Directory.CreateDirectory(pinFileInfo.Directory.FullName);
                    }
                    File.WriteAllText(pinFile, pinNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception ex)
                {
                    throw new CodeGenerationException($"Pin file {pinFile} could not be written", ex);
                }
            }

            //Refresh the local mirror so the schema is on disk like locally referenced schemas
            try
            {
                FileInfo fileInfo = new FileInfo(localFile);
                if (!Directory.Exists(fileInfo.Directory.FullName))
                {
                    Directory.CreateDirectory(fileInfo.Directory.FullName);
                }
                File.WriteAllText(localFile, content);
            }
            catch (Exception ex)
            {
                throw new CodeGenerationException($"Local mirror {localFile} could not be written", ex);
            }

            //Parse the document into builder parts and register it under the
            //exact key references are looked up with
            JsonNode schemaJson;
            try
            {
                schemaJson = JsonNode.Parse(content);
            }
            catch (Exception ex)
            {
                throw new CodeGenerationException($"The remote schema at {url} is not valid JSON", ex);
            }

            JSBSchema parsedSchema;
            try
            {
                string fileName = Path.GetFileName(localFile);
                if (fileName.EndsWith(SCHEMA_FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                {
                    fileName = fileName.Substring(0, fileName.Length - SCHEMA_FILE_ENDING.Length);
                }
                parsedSchema = JSBSchemaParser.ParseDocument(schemaJson, new IdentifierString(TransformToTitleCase(SanitizeToNamespaceText(fileName))), $"Schema parsed from the remote schema at {url}");
            }
            catch (Exception ex)
            {
                throw new CodeGenerationException($"The remote schema at {url} could not be parsed into builder parts: {ex.Message}", ex);
            }

            NamespaceString module;
            try
            {
                module = new NamespaceString(DeriveRemoteModule(relativeLocalFileWithExpandedDot));
            }
            catch (Exception ex)
            {
                throw new CodeGenerationException($"A namespace could not be derived for the remote schema at {url}: {ex.Message}", ex);
            }

            RemoteSchemaDefinition remoteSchemaDefinition = new RemoteSchemaDefinition(parsedSchema, module, relativeLocalFileWithExpandedDot);
            RegisteredJsonSchemas.Add(relativeLocalFileWithExpandedDot, remoteSchemaDefinition);
        }

        /// <summary>
        /// Derives the module (namespace) from the mirror path: the host and path
        /// segments of the mirror become namespace segments, dots become path
        /// separators again when the module is turned back into file paths by
        /// the generators. Characters that are not identifier-like are stripped
        /// and segments starting with a digit are prefixed with x
        /// </summary>
        /// <param name="relativeLocalFileWithExpandedDot"></param>
        /// <returns></returns>
        private string DeriveRemoteModule(string relativeLocalFileWithExpandedDot)
        {
            string[] pathSegments = relativeLocalFileWithExpandedDot.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> namespaceSegments = new List<string>();
            foreach (string pathSegment in pathSegments)
            {
                if (pathSegment.EndsWith(SCHEMA_FILE_ENDING, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                string cleaned = SanitizeToNamespaceText(pathSegment);
                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    namespaceSegments.Add(cleaned);
                }
            }
            if (namespaceSegments.Count == 0)
            {
                throw new CodeGenerationException($"A namespace could not be derived from {relativeLocalFileWithExpandedDot}");
            }
            return string.Join(".", namespaceSegments);
        }

        private static string Sha256Hex(string content)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
                StringBuilder stringBuilder = new StringBuilder(hash.Length * 2);
                foreach (byte hashByte in hash)
                {
                    stringBuilder.Append(hashByte.ToString("x2"));
                }
                return stringBuilder.ToString();
            }
        }

        /// <summary>
        /// Strips characters that are not usable in identifiers so path segments
        /// and file names can be turned into names and namespace segments
        /// </summary>
        /// <param name="raw"></param>
        /// <returns></returns>
        private static string SanitizeToNamespaceText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char character in raw)
            {
                if ((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9'))
                {
                    stringBuilder.Append(character);
                }
            }
            string cleaned = stringBuilder.ToString();
            if (cleaned.Length == 0)
            {
                return cleaned;
            }
            if (cleaned[0] >= '0' && cleaned[0] <= '9')
            {
                cleaned = "x" + cleaned;
            }
            return cleaned;
        }

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
        public (IJSBPart refPart, JsonNode schemaValue) LookupReferencedPart(string relativeFileNameWithExpandedDot, JSBRef jSBRef)
        {
            if (RegisteredJsonSchemas.TryGetValue(relativeFileNameWithExpandedDot, out IJsonSchemaDefinition jsonSchemaDefinition))
            {
                string fragmentLower = jSBRef.Fragment.ToLowerInvariant();
                if (fragmentLower.StartsWith("/definitions/") || fragmentLower.StartsWith("/$defs/"))
                {
                    IJSBPart referencedPart = LookupReferencedPartFromDefinitions(jsonSchemaDefinition.JsonSchemaBuilderSchema, jSBRef.Fragment);
                    if (referencedPart != null)
                    {
                        return (referencedPart, null);
                    }
                    //fall through: a /definitions/ or /$defs/ fragment the part tree
                    //cannot resolve resolves against the emitted schema json below
                }
                else if (string.IsNullOrWhiteSpace(jSBRef.Fragment) ||
                    jSBRef.Fragment.Equals("/"))
                {
                    return (jsonSchemaDefinition.JsonSchemaBuilderSchema, null);
                }
                return (null, ResolveJsonPointer(ToJsonNode(jsonSchemaDefinition.JsonSchemaBuilderSchema), jSBRef.Fragment));
            }

            //Search for the schema file as .schema.json and .json and load it when found.
            //The placement of an absolute uri starts with a path separator, which
            //Path.Combine would treat as a rooted path on POSIX systems, so it is trimmed
            string schemaString = string.Empty;

            string localfile = Path.Combine(JsonSchemaApplicationRoot,
                relativeFileNameWithExpandedDot
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (File.Exists(localfile))
            {
                schemaString = File.ReadAllText(localfile);
            }
            else
            {
                throw new CodeGenerationException($"Schema could not be found at the path {localfile}");
            }

            // make a schema and generate code from that
            JsonNode jsonValueOfSchema = JsonNode.Parse(schemaString);
            return (null, ResolveJsonPointer(jsonValueOfSchema, jSBRef.Fragment));
        }

        /// <summary>
        /// Serializes a builder schema to its emitted json so reference fragments
        /// can be resolved against exactly what a schema consumer sees
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        public JsonNode ToJsonNode(JSBSchema schema)
        {
            return JsonSerializer.SerializeToNode(schema.AsJsonSchema(), JSBJsonSchemaBuild.SerializerOptions);
        }

        /// <summary>
        /// Evaluates a reference fragment as an RFC 6901 JSON Pointer (without the
        /// leading '#') against a schema given as JsonNode. An empty fragment or
        /// '/' returns the entire schema. Keys inside a definitions container keep
        /// the historical lenient matching (exact, then TitleCase, then camelCase)
        /// since builder parts name definitions in TitleCase while authored
        /// references usually use camelCase. All other segments match exactly
        /// </summary>
        /// <param name="schemaValue"></param>
        /// <param name="fragment"></param>
        /// <returns></returns>
        public JsonNode ResolveJsonPointer(JsonNode schemaValue, string fragment)
        {
            if (string.IsNullOrWhiteSpace(fragment) || fragment == "/")
            {
                return schemaValue;
            }
            string trimmedFragment = fragment.StartsWith("/") ? fragment.Substring(1) : fragment;
            string[] rawSegments = trimmedFragment.Split('/');
            JsonNode current = schemaValue;
            //True while the segment being looked up is a key inside a definitions container
            bool insideDefinitions = false;
            foreach (string rawSegment in rawSegments)
            {
                string segment = rawSegment.Replace("~1", "/").Replace("~0", "~");
                if (current is JsonObject currentObject)
                {
                    JsonNode next = null;
                    if (!currentObject.TryGetPropertyValue(segment, out next) && insideDefinitions)
                    {
                        string titleKey = TransformToTitleCase(segment);
                        string camelKey = TransformToCamelCase(segment);
                        if (!currentObject.TryGetPropertyValue(titleKey, out next))
                        {
                            currentObject.TryGetPropertyValue(camelKey, out next);
                        }
                    }
                    if (next == null)
                    {
                        throw new CodeGenerationException($"Json reference fragment '{fragment}' does not resolve: '{segment}' was not found");
                    }
                    insideDefinitions = segment == "definitions" || segment == "$defs";
                    current = next;
                }
                else if (current is JsonArray currentArray)
                {
                    if (!int.TryParse(segment, out int index) || index < 0 || index >= currentArray.Count)
                    {
                        throw new CodeGenerationException($"Json reference fragment '{fragment}' does not resolve: '{segment}' is not a valid array index");
                    }
                    insideDefinitions = false;
                    current = currentArray[index];
                }
                else
                {
                    throw new CodeGenerationException($"Json reference fragment '{fragment}' does not resolve: cannot descend into a scalar at '{segment}'");
                }
            }
            return current;
        }

        /// <summary>
        /// Resolves a /definitions/ or /$defs/ fragment against a registered schema, including nested
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
            string[] segments = fragment.Split(new[] { "/definitions/", "/$defs/" }, StringSplitOptions.RemoveEmptyEntries);
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
