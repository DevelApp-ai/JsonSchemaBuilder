using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Manatee.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// TypeScript code generator for JSON Schema
    ///
    /// Emits exported interfaces for objects and exported string literal union
    /// types for enums, mirroring the semantics of the C# generator:
    /// - AllOf merges all object subschemas into one interface
    /// - AnyOf/OneOf map to object since the runtime shape is not fixed
    /// - Date, DateTime, Time, Email, PhoneNumber, Guid, Uri, Base64String and
    ///   DataString map to string since that is the wire format of the generated JSON Schema
    /// - Properties are optional when the part is not required
    /// </summary>
    public class TypeScript
    {
        private const string FILE_ENDING = ".ts";

        /// <summary>
        /// Generate fileName and code for TypeScript
        /// </summary>
        /// <param name="codeGenerator"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            List<(string fileName, string code)> generatedCode = new List<(string fileName, string code)>();
            foreach (IJsonSchemaDefinition schema in codeGenerator.RegisteredJsonSchemas.Values)
            {
                string fileName = Path.Combine(codeGenerator.ApplicationRoot, schema.Module.ToFilePath, schema.Name + FILE_ENDING);

                TypeScript typeScript = new TypeScript(codeGenerator, schema.Module);
                generatedCode.Add((fileName, typeScript.GenerateCode(schema.JsonSchemaBuilderSchema)));
            }
            return generatedCode;
        }

        private CodeGenerator _codeGenerator;
        private NamespaceString _startNameSpace;
        private JSBSchema _rootSchema;
        private HashSet<string> _enqueuedTypeNames;
        private HashSet<string> _imports;
        private Queue<IJSBPart> _typeQueue;
        private Queue<(string typeName, JsonValue rawSchema)> _rawTypeQueue;
        private HashSet<string> _enqueuedRawTypeNames;

        private TypeScript(CodeGenerator codeGenerator, NamespaceString startNameSpace)
        {
            _codeGenerator = codeGenerator;
            _startNameSpace = startNameSpace;
        }

        /// <summary>
        /// Generate TypeScript code from JsonSchemaBuilderSchema
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        private string GenerateCode(JSBSchema schema)
        {
            _rootSchema = schema;
            _enqueuedTypeNames = new HashSet<string>();
            _imports = new HashSet<string>();
            _typeQueue = new Queue<IJSBPart>();
            _rawTypeQueue = new Queue<(string typeName, JsonValue rawSchema)>();
            _enqueuedRawTypeNames = new HashSet<string>();

            //Types referenced through /definitions/ are emitted as own types
            //mirroring the definitions emitted by the C# generator
            foreach (IJSBPart definition in schema.Definitions.Values)
            {
                EnqueueType(definition);
            }

            if (schema.TopPart != null)
            {
                if (schema.TopPart.PartType == JSBPartType.Object)
                {
                    EnqueueType(schema.TopPart);
                }
                else
                {
                    //A non object top part is encased in an object mirroring the C# generator
                    List<IJSBPart> properties = new List<IJSBPart>();
                    properties.Add(schema.TopPart);

                    JSBObject encasingObject = new JSBObject(schema.Name, schema.Description, props: properties);
                    EnqueueType(encasingObject);
                }
            }
            else
            {
                //A schema without a top part validates everything: an empty interface
                List<IJSBPart> properties = new List<IJSBPart>();
                JSBObject encasingObject = new JSBObject(schema.Name, schema.Description, props: properties);
                EnqueueType(encasingObject);
            }

            CodeBuilder codeBuilder = new CodeBuilder();
            while (_typeQueue.Count > 0)
            {
                IJSBPart part = _typeQueue.Dequeue();
                GenerateTypeDeclaration(codeBuilder, part);
            }
            //Emit interfaces for schemas referenced through json pointers that
            //have no builder parts. Nested objects enqueue more raw types while
            //emitting, so the loop drains the queue completely
            while (_rawTypeQueue.Count > 0)
            {
                (string rawTypeName, JsonValue rawSchema) = _rawTypeQueue.Dequeue();
                GenerateRawInterface(codeBuilder, rawTypeName, rawSchema);
            }

            if (_imports.Count > 0)
            {
                string imports = string.Join(Environment.NewLine, _imports
                    .OrderBy(importLine => importLine));
                return imports + Environment.NewLine + Environment.NewLine + codeBuilder.Build();
            }
            return codeBuilder.Build();
        }

        #region Generate types

        /// <summary>
        /// Emits a single type declaration for a part: an interface for objects
        /// and merged AllOf parts, or a string literal union for enums
        /// </summary>
        private void GenerateTypeDeclaration(CodeBuilder codeBuilder, IJSBPart part)
        {
            switch (part.PartType)
            {
                case JSBPartType.Object:
                    JSBObject jsonSchemaBuilderObject = part as JSBObject;
                    if (jsonSchemaBuilderObject.Enums != null && jsonSchemaBuilderObject.Enums.Count > 0)
                    {
                        throw new NotImplementedException("Enum on objects has not been implemented");
                    }
                    GenerateInterface(codeBuilder, part.Name, part.Description, jsonSchemaBuilderObject.Properties.Values);
                    break;
                case JSBPartType.AllOf:
                    JSBAllOf jsonSchemaBuilderAllOf = part as JSBAllOf;
                    bool allSubSchemasAreObjects = jsonSchemaBuilderAllOf.Schemas.All(subSchema => subSchema.PartType == JSBPartType.Object);
                    if (allSubSchemasAreObjects)
                    {
                        //Merge the properties of all subschemas. A property name present in
                        //several subschemas is only emitted once, matching the AllOf requirement
                        //that the same named property must validate against every subschema
                        HashSet<string> emittedPropertyNames = new HashSet<string>();
                        List<IJSBPart> mergedProperties = new List<IJSBPart>();
                        foreach (IJSBPart subSchema in jsonSchemaBuilderAllOf.Schemas)
                        {
                            JSBObject subObject = subSchema as JSBObject;
                            foreach (IJSBPart subProperty in subObject.Properties.Values)
                            {
                                if (emittedPropertyNames.Add(subProperty.Name.ToString()))
                                {
                                    mergedProperties.Add(subProperty);
                                }
                            }
                        }
                        GenerateInterface(codeBuilder, part.Name, part.Description, mergedProperties);
                    }
                    break;
                default:
                    List<string> enumLiterals = GetEnumLiterals(part);
                    if (enumLiterals != null)
                    {
                        GenerateEnumUnionType(codeBuilder, part, enumLiterals);
                    }
                    else
                    {
                        throw new NotImplementedException($"TypeScript generation of a standalone {part.PartType} part has not been implemented");
                    }
                    break;
            }
        }

        /// <summary>
        /// Emits an exported interface with one member per property
        /// </summary>
        private void GenerateInterface(CodeBuilder codeBuilder, IdentifierString name, string description, IEnumerable<IJSBPart> properties)
        {
            string interfaceName = TransformToTitleCase(name);

            GenerateComments(codeBuilder, name, description);

            codeBuilder
                .L($"export interface {interfaceName}")
                .L("{")
                .IndentIncrease();

            foreach (IJSBPart property in properties)
            {
                GenerateProperty(codeBuilder, property);
                codeBuilder.EmptyLine();
            }

            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
        }

        /// <summary>
        /// Emits a single interface member with its type
        /// </summary>
        private void GenerateProperty(CodeBuilder codeBuilder, IJSBPart part)
        {
            string optional = part.IsRequired ? string.Empty : "?";
            string type = MakeCorrectItemType(part);

            GenerateComments(codeBuilder, part.Name, part.Description);

            codeBuilder
                .L($"{TransformToCamelCase(part.Name)}{optional}: {type};");
        }

        /// <summary>
        /// Emits an exported string literal union type for the enums of a part
        /// </summary>
        private void GenerateEnumUnionType(CodeBuilder codeBuilder, IJSBPart part, List<string> enumLiterals)
        {
            string typeName = TransformToTitleCase(part.Name) + "Enum";

            GenerateComments(codeBuilder, part.Name, part.Description);

            string union = string.Join(" | ", enumLiterals);
            codeBuilder
                .L($"export type {typeName} = {union};")
                .EmptyLine();
        }

        /// <summary>
        /// Returns the TypeScript literal tokens for the enums of a part, or null
        /// when the part has no enums or its enums do not map to TypeScript
        /// literals. Enums on objects and arrays are not supported, mirroring
        /// the C# generator
        /// </summary>
        private List<string> GetEnumLiterals(IJSBPart part)
        {
            if (part is JSBString jsbString)
            {
                if (jsbString.Enums.Count == 0)
                {
                    return null;
                }
                return jsbString.Enums.Select(enumValue => $"'{enumValue}'").ToList();
            }
            if (part is JSBInteger jsbInteger)
            {
                return jsbInteger.Enums.Count == 0 ? null
                    : jsbInteger.Enums.Select(enumValue => enumValue?.ToString() ?? "null").ToList();
            }
            if (part is JSBNumber jsbNumber)
            {
                return jsbNumber.Enums.Count == 0 ? null
                    : jsbNumber.Enums.Select(enumValue => enumValue?.ToString() ?? "null").ToList();
            }
            if (part is JSBBoolean jsbBoolean)
            {
                return jsbBoolean.Enums.Count == 0 ? null
                    : jsbBoolean.Enums.Select(enumValue => enumValue?.ToString().ToLowerInvariant() ?? "null").ToList();
            }
            return null;
        }

        /// <summary>
        /// Emits a JSDoc comment mirroring the comments of the C# generator
        /// </summary>
        private void GenerateComments(CodeBuilder codeBuilder, IdentifierString key, string description)
        {
            codeBuilder
                .L("/**")
                .L($" * {key}: {description}")
                .L(" */");
        }

        #endregion

        #region Make type

        /// <summary>
        /// Maps a part to its TypeScript type and makes sure the type declaration
        /// for objects, merged AllOf parts and enums is emitted
        /// </summary>
        private string MakeCorrectItemType(IJSBPart jsonSchemaBuilderPart)
        {
            //Value type parts with enums become string or number literal union types
            if (jsonSchemaBuilderPart.PartType != JSBPartType.Array && jsonSchemaBuilderPart.PartType != JSBPartType.Object
                && GetEnumLiterals(jsonSchemaBuilderPart) != null)
            {
                EnqueueType(jsonSchemaBuilderPart);
                return TransformToTitleCase(jsonSchemaBuilderPart.Name) + "Enum";
            }
            switch (jsonSchemaBuilderPart.PartType)
            {
                case JSBPartType.Array:
                    //TODO support multiple items
                    JSBArray jsonSchemaBuilderArray = jsonSchemaBuilderPart as JSBArray;
                    return $"{MakeCorrectItemType(jsonSchemaBuilderArray.Items.First())}[]";
                case JSBPartType.Object:
                    EnqueueType(jsonSchemaBuilderPart);
                    return TransformToTitleCase(jsonSchemaBuilderPart.Name);
                case JSBPartType.Integer:
                case JSBPartType.Number:
                    return "number";
                case JSBPartType.Boolean:
                    return "boolean";
                case JSBPartType.String:
                case JSBPartType.Date:
                case JSBPartType.DateTime:
                case JSBPartType.Time:
                case JSBPartType.Email:
                case JSBPartType.Base64String:
                case JSBPartType.DataString:
                case JSBPartType.Uri:
                case JSBPartType.Guid:
                case JSBPartType.PhoneNumber:
                    //The wire format of all of these is a JSON string
                    return "string";
                case JSBPartType.AllOf:
                    JSBAllOf jsonSchemaBuilderAllOf = jsonSchemaBuilderPart as JSBAllOf;
                    bool allSubSchemasAreObjects = jsonSchemaBuilderAllOf.Schemas.All(subSchema => subSchema.PartType == JSBPartType.Object);
                    if (allSubSchemasAreObjects)
                    {
                        EnqueueType(jsonSchemaBuilderPart);
                        return TransformToTitleCase(jsonSchemaBuilderPart.Name);
                    }
                    //A merged interface cannot be built when a subschema is not an object,
                    //so the part degrades to object mirroring the C# generator
                    return "object";
                case JSBPartType.AnyOf:
                case JSBPartType.OneOf:
                    //The value validates against one of the subschemas, which maps to object
                    return "object";
                case JSBPartType.Schema:
                    JSBSchema jSBSchema = jsonSchemaBuilderPart as JSBSchema;
                    if (jSBSchema.TopPart != null)
                    {
                        return MakeCorrectItemType(jSBSchema.TopPart);
                    }
                    else
                    {
                        throw new NotImplementedException($"MakeCorrectItemType for Schema without a topPart has not been implemented");
                    }
                case JSBPartType.IriReference:
                    JSBRef jsbRef = jsonSchemaBuilderPart as JSBRef;
                    (IJSBPart refPart, JsonValue schemaValue) pair = LookupReferencedPart(jsbRef, out string referencedRelativeLocalFile);
                    if (pair.refPart != null)
                    {
                        return MakeCorrectItemTypeForReferencedPart(pair.refPart, referencedRelativeLocalFile);
                    }
                    else if (pair.schemaValue != null)
                    {
                        return MakeCorrectItemType(pair.schemaValue, jsbRef.Name);
                    }
                    else
                    {
                        throw new CodeGenerationException($"Could not find reference to {jsbRef.RelativeLocalFile}");
                    }
                default:
                    throw new NotImplementedException($"MakeCorrectItemType for {jsonSchemaBuilderPart.PartType} has not been implemented");
            }
        }

        /// <summary>
        /// Maps a part resolved from a reference to its TypeScript type. The top
        /// level type of another registered schema lives in its own generated
        /// file and is imported from there. Referenced definitions are local
        /// types and are emitted in this file
        /// </summary>
        private string MakeCorrectItemTypeForReferencedPart(IJSBPart referencedPart, string referencedRelativeLocalFile)
        {
            if (referencedPart.PartType == JSBPartType.Schema)
            {
                JSBSchema referencedSchema = referencedPart as JSBSchema;
                if (referencedSchema.TopPart == null)
                {
                    throw new CodeGenerationException($"Referenced schema {referencedSchema.Name} has no top part");
                }
                if (referencedSchema.Name.ToString() != _rootSchema.Name.ToString())
                {
                    IJSBPart referencedTopPart = referencedSchema.TopPart;
                    //Objects, merged AllOf parts and enums are named types in the
                    //referenced file and have to be imported
                    if (referencedTopPart.PartType == JSBPartType.Object)
                    {
                        return AddImport(TransformToTitleCase(referencedTopPart.Name), referencedRelativeLocalFile);
                    }
                    if (referencedTopPart.PartType == JSBPartType.AllOf)
                    {
                        JSBAllOf referencedAllOf = referencedTopPart as JSBAllOf;
                        if (referencedAllOf.Schemas.All(subSchema => subSchema.PartType == JSBPartType.Object))
                        {
                            return AddImport(TransformToTitleCase(referencedTopPart.Name), referencedRelativeLocalFile);
                        }
                        //A merged interface cannot be built when a subschema is
                        //not an object, so the part degrades to object
                        return "object";
                    }
                    if (GetEnumLiterals(referencedTopPart) != null)
                    {
                        return AddImport(TransformToTitleCase(referencedTopPart.Name) + "Enum", referencedRelativeLocalFile);
                    }
                    //Other top parts are encased in an interface in the referenced
                    //file, so the reference resolves to the wire type of the top
                    //part mirroring the C# generator
                    return MakeCorrectItemType(referencedTopPart);
                }
                //A reference to the root schema itself resolves to the root top part
                return MakeCorrectItemType(referencedSchema.TopPart);
            }
            //Referenced definitions and subschemas are emitted locally
            return MakeCorrectItemType(referencedPart);
        }

        /// <summary>
        /// Adds an import of a named type from another generated schema file and
        /// returns the type name. The import path is relative to the file of the
        /// root schema
        /// </summary>
        private string AddImport(string typeName, string referencedRelativeLocalFile)
        {
            string schemaFileName = Path.GetFileName(referencedRelativeLocalFile).Replace(".schema.json", string.Empty);
            string referencedSchemaName = TransformToTitleCase(schemaFileName);
            string referencedDirectory = (Path.GetDirectoryName(referencedRelativeLocalFile) ?? string.Empty).Replace('\\', '/');
            string rootDirectory = _startNameSpace.ToFilePath.Replace('\\', '/');
            string relativePath = referencedDirectory.Equals(rootDirectory, StringComparison.Ordinal)
                ? "./" + referencedSchemaName
                : "../" + referencedDirectory + "/" + referencedSchemaName;
            _imports.Add($"import type {{ {typeName} }} from '{relativePath}';");
            return typeName;
        }

        /// <summary>
        /// Maps a schema JSON fragment resolved from a file to its TypeScript type
        /// </summary>
        /// <summary>
        /// Maps a raw schema json value to a TypeScript type. Object schemas are
        /// queued for interface emission through EnqueueRawType since they have
        /// no builder parts. The nameHint is used as type name fallback for
        /// schemas without $id or title
        /// </summary>
        /// <param name="jsonValueOfSchema"></param>
        /// <param name="nameHint"></param>
        /// <returns></returns>
        private string MakeCorrectItemType(JsonValue jsonValueOfSchema, IdentifierString nameHint = null)
        {
            JsonObject jsonObjectOfSchema = jsonValueOfSchema.Object;
            string name = string.Empty;
            if (jsonObjectOfSchema.TryGetValue("$id", out JsonValue jsonValueId))
            {
                name = jsonValueId.String;
            }
            else if (jsonObjectOfSchema.TryGetValue("title", out JsonValue jsonValueTitle))
            {
                name = jsonValueTitle.String;
            }
            else if (nameHint != null)
            {
                name = nameHint;
            }
            else
            {
                throw new CodeGenerationException($"Referenced schema has neither title nor id and no fallback name is available");
            }
            string type = string.Empty;
            if (jsonObjectOfSchema.TryGetValue("type", out JsonValue jsonValueType))
            {
                type = jsonValueType.String;
            }
            else
            {
                throw new CodeGenerationException($"Referenced schema {name} has no type");
            }
            switch (type)
            {
                case "string":
                    return "string";
                case "integer":
                case "number":
                    return "number";
                case "boolean":
                    return "boolean";
                case "object":
                    return EnqueueRawType(TransformToTitleCase(name), jsonValueOfSchema);
                case "array":
                    if (jsonObjectOfSchema.TryGetValue("items", out JsonValue itemsValue))
                    {
                        //Manatee emits a single item schema as an object, the tuple
                        //form is an array (only the first item schema is supported,
                        //mirroring the part based generator)
                        JsonValue itemSchema = itemsValue;
                        bool hasItemSchema = false;
                        if (itemsValue.Type == JsonValueType.Array && itemsValue.Array.Count > 0)
                        {
                            itemSchema = itemsValue.Array[0];
                            hasItemSchema = true;
                        }
                        else if (itemsValue.Type == JsonValueType.Object)
                        {
                            hasItemSchema = true;
                        }
                        if (hasItemSchema)
                        {
                            return $"{MakeCorrectItemType(itemSchema, nameHint)}[]";
                        }
                    }
                    throw new NotImplementedException($"Array schema {name} without a single items schema is not supported for reference code generation");
                default:
                    throw new NotImplementedException($"MakeCorrectItemType for {type} has not been implemented");
            }
        }

        /// <summary>
        /// Queues a raw schema object for interface emission and returns the type
        /// name. The interface is emitted once per module across all files of a
        /// generation run and never shadows an interface already emitted for a
        /// builder part with the same name
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="rawSchema"></param>
        /// <returns></returns>
        private string EnqueueRawType(string typeName, JsonValue rawSchema)
        {
            string moduleKey = $"{_startNameSpace}|{typeName}";
            //Emitted by another file in the same module: reference the existing interface
            if (!_codeGenerator.EmittedRawTypeNames.Add(moduleKey))
            {
                return typeName;
            }
            //A builder part interface with the same name takes precedence
            if (_enqueuedTypeNames.Contains(typeName))
            {
                return typeName;
            }
            if (_enqueuedRawTypeNames.Add(typeName))
            {
                _rawTypeQueue.Enqueue((typeName, rawSchema));
            }
            return typeName;
        }

        /// <summary>
        /// Emits an interface for a raw schema object referenced through a json
        /// pointer, mirroring the member emission of the part based generator:
        /// members not in the schema required list become optional
        /// </summary>
        /// <param name="codeBuilder"></param>
        /// <param name="typeName"></param>
        /// <param name="rawSchema"></param>
        private void GenerateRawInterface(CodeBuilder codeBuilder, string typeName, JsonValue rawSchema)
        {
            JsonObject rawObject = rawSchema.Object;

            string description = "Schema referenced without builder parts";
            if (rawObject.TryGetValue("description", out JsonValue descriptionValue) && descriptionValue.Type == JsonValueType.String)
            {
                description = descriptionValue.String;
            }

            HashSet<string> requiredProperties = new HashSet<string>(StringComparer.Ordinal);
            if (rawObject.TryGetValue("required", out JsonValue requiredValue) && requiredValue.Type == JsonValueType.Array)
            {
                foreach (JsonValue requiredEntry in requiredValue.Array)
                {
                    if (requiredEntry.Type == JsonValueType.String)
                    {
                        requiredProperties.Add(requiredEntry.String);
                    }
                }
            }

            GenerateComments(codeBuilder, typeName, description);

            codeBuilder
                .L($"export interface {typeName}")
                .L("{")
                .IndentIncrease();

            if (rawObject.TryGetValue("properties", out JsonValue propertiesValue) && propertiesValue.Type == JsonValueType.Object)
            {
                foreach (KeyValuePair<string, JsonValue> propertyPair in propertiesValue.Object)
                {
                    string optional = requiredProperties.Contains(propertyPair.Key) ? string.Empty : "?";
                    string itemType = MakeCorrectItemType(propertyPair.Value, propertyPair.Key);
                    codeBuilder
                        .L($"{TransformToCamelCase(propertyPair.Key)}{optional}: {itemType};")
                        .EmptyLine();
                }
            }

            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
        }

        #endregion

        #region Type queue

        /// <summary>
        /// Queues a part for type declaration emission. Each type name is only
        /// emitted once per file even when the part is referenced several times.
        /// </summary>
        private void EnqueueType(IJSBPart part)
        {
            string typeName;
            if (part.PartType == JSBPartType.Object || part.PartType == JSBPartType.AllOf)
            {
                typeName = TransformToTitleCase(part.Name);
            }
            else if (GetEnumLiterals(part) != null)
            {
                typeName = TransformToTitleCase(part.Name) + "Enum";
            }
            else
            {
                //Not a type that is emitted as its own declaration
                return;
            }

            if (!_enqueuedTypeNames.Add(typeName))
            {
                return;
            }
            _typeQueue.Enqueue(part);
        }

        /// <summary>
        /// Resolves a reference to a registered schema, a local definition or a
        /// schema file and returns the expanded relative file name of the
        /// referenced schema
        /// </summary>
        private (IJSBPart refPart, JsonValue schemaValue) LookupReferencedPart(JSBRef jSBRef, out string relativeLocalFileWithExpandedDot)
        {
            relativeLocalFileWithExpandedDot = ExpandRelativelocalFile(jSBRef.RelativeLocalFile);
            if (string.IsNullOrWhiteSpace(relativeLocalFileWithExpandedDot))
            {
                //Fragment only references resolve against the schema being generated.
                //The registry is keyed by the definition FileName, which is derived from
                //the definition class name and can differ from the internal schema
                //name, so the registered FileName is used when the schema is registered
                if (!_codeGenerator.TryGetRegisteredFileName(_rootSchema, out string registeredFileName))
                {
                    registeredFileName = Path.Combine(_startNameSpace.ToFilePath, _rootSchema.Name + ".schema.json");
                }
                relativeLocalFileWithExpandedDot = registeredFileName;
            }
            return _codeGenerator.LookupReferencedPart(_codeGenerator.TransformToCamelCase(relativeLocalFileWithExpandedDot), jSBRef);
        }

        /// <summary>
        /// Expand local file if needed
        /// </summary>
        /// <param name="relativeLocalFile"></param>
        /// <returns></returns>
        private string ExpandRelativelocalFile(string relativeLocalFile)
        {
            if (relativeLocalFile.StartsWith("./") || relativeLocalFile.StartsWith(".\\"))
            {
                return Path.Combine(_startNameSpace.ToFilePath, relativeLocalFile.Remove(0, 2));
            }
            else
            {
                return relativeLocalFile;
            }
        }

        #endregion

        #region HelperFunctions

        /// <summary>
        /// Transform string from TitleCase to camelCase
        /// </summary>
        /// <param name="stringToTransform"></param>
        /// <returns></returns>
        private string TransformToCamelCase(string stringToTransform)
        {
            return _codeGenerator.TransformToCamelCase(stringToTransform);
        }

        /// <summary>
        /// Transform string from camelCase to TitleCase
        /// </summary>
        /// <param name="stringToTransform"></param>
        /// <returns></returns>
        private string TransformToTitleCase(string stringToTransform)
        {
            return _codeGenerator.TransformToTitleCase(stringToTransform);
        }

        #endregion
    }
}
