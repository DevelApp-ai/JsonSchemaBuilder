using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Manatee.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// PHP code generator for JSON Schema
    ///
    /// Emits one PHP file per schema with typed properties, mirroring the
    /// semantics of the C# generator:
    /// - Objects become classes with public typed properties named after the
    ///   JSON property keys in camelCase, nullable when the part is not required
    /// - Enums on string parts become string backed enums and enums on integer
    ///   parts become int backed enums (PHP 8.1). Enums on number and boolean
    ///   parts become literal union docblocks since PHP enums cannot be backed
    ///   by float or bool
    /// - AllOf merges all object subschemas into one class
    /// - AnyOf/OneOf map to object since the runtime shape is not fixed
    /// - Date, DateTime and Time map to DateTimeImmutable since PHP has no
    ///   date only value object. Email, PhoneNumber, Guid, Uri,
    ///   Base64String and DataString map to string since PHP has no
    ///   built-in equivalents
    /// - References to the top level type of another registered schema use the
    ///   referenced class and import it with a use statement when it lives in
    ///   another module
    /// - Non object top parts are encased in a class like in the C# generator
    ///
    /// The generated code requires PHP 8.1 or later
    /// </summary>
    public class PHP
    {
        private const string FILE_ENDING = ".php";

        /// <summary>
        /// Generate fileName and code for PHP
        /// </summary>
        /// <param name="codeGenerator"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            List<(string fileName, string code)> generatedCode = new List<(string fileName, string code)>();
            foreach (IJsonSchemaDefinition schema in codeGenerator.RegisteredJsonSchemas.Values)
            {
                string fileName = Path.Combine(codeGenerator.ApplicationRoot, schema.Module.ToFilePath, schema.Name + FILE_ENDING);

                PHP php = new PHP(codeGenerator, schema.Module);
                generatedCode.Add((fileName, php.GenerateCode(schema.JsonSchemaBuilderSchema)));
            }
            return generatedCode;
        }

        private CodeGenerator _codeGenerator;
        private NamespaceString _startNameSpace;
        private JSBSchema _rootSchema;
        private HashSet<string> _enqueuedTypeNames;
        private HashSet<string> _uses;
        private Queue<IJSBPart> _typeQueue;

        private PHP(CodeGenerator codeGenerator, NamespaceString startNameSpace)
        {
            _codeGenerator = codeGenerator;
            _startNameSpace = startNameSpace;
        }

        /// <summary>
        /// Generate PHP code from JsonSchemaBuilderSchema
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        private string GenerateCode(JSBSchema schema)
        {
            _rootSchema = schema;
            _enqueuedTypeNames = new HashSet<string>();
            _uses = new HashSet<string>();
            _typeQueue = new Queue<IJSBPart>();

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
                //A schema without a top part validates everything: an empty class
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

            //The use statements are collected while the types are generated,
            //so the file is assembled after the fact
            string header = $"<?php{Environment.NewLine}{Environment.NewLine}"
                + $"declare(strict_types=1);{Environment.NewLine}{Environment.NewLine}"
                + $"namespace {NamespaceName};{Environment.NewLine}{Environment.NewLine}";
            if (_uses.Count > 0)
            {
                string uses = string.Join(Environment.NewLine, _uses.OrderBy(useStatement => useStatement));
                header += uses + Environment.NewLine + Environment.NewLine;
            }
            return header + codeBuilder.Build();
        }

        #region Generate types

        /// <summary>
        /// Emits a single type declaration for a part: a class for objects and
        /// merged AllOf parts, or a backed enum for string and integer enums
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
                    GenerateClass(codeBuilder, part.Name, part.Description, jsonSchemaBuilderObject.Properties.Values);
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
                        GenerateClass(codeBuilder, part.Name, part.Description, mergedProperties);
                    }
                    break;
                default:
                    (string backingType, List<string> caseLines) backedEnum = GetBackedEnumCases(part);
                    if (backedEnum.backingType != null)
                    {
                        GenerateBackedEnum(codeBuilder, part, backedEnum.backingType, backedEnum.caseLines);
                    }
                    else
                    {
                        throw new NotImplementedException($"PHP generation of a standalone {part.PartType} part has not been implemented");
                    }
                    break;
            }
        }

        /// <summary>
        /// Emits a class with one typed property per schema property
        /// </summary>
        private void GenerateClass(CodeBuilder codeBuilder, IdentifierString name, string description, IEnumerable<IJSBPart> properties)
        {
            string className = TransformToTitleCase(name);

            GenerateComments(codeBuilder, name, description, null);

            codeBuilder
                .L($"class {className}")
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
        /// Emits a single typed class property. The property is nullable when
        /// the part is not required, and its docblock carries the item type of
        /// arrays and the literal union of enums that cannot be backed enums
        /// </summary>
        private void GenerateProperty(CodeBuilder codeBuilder, IJSBPart part)
        {
            string nullable = part.IsRequired ? string.Empty : "?";
            string type = MakeCorrectItemType(part);
            string docblockType = MakeDocblockVarType(part) + (part.IsRequired ? string.Empty : "|null");

            GenerateComments(codeBuilder, part.Name, part.Description, docblockType);

            codeBuilder
                .L($"public {nullable}{type} ${TransformToCamelCase(part.Name)};");
        }

        /// <summary>
        /// Emits a string or int backed enum (PHP 8.1) for the enums of a part
        /// </summary>
        private void GenerateBackedEnum(CodeBuilder codeBuilder, IJSBPart part, string backingType, List<string> caseLines)
        {
            string typeName = TransformToTitleCase(part.Name) + "Enum";

            GenerateComments(codeBuilder, part.Name, part.Description, null);

            codeBuilder
                .L($"enum {typeName}: {backingType}")
                .L("{")
                .IndentIncrease();

            foreach (string caseLine in caseLines)
            {
                codeBuilder.L(caseLine);
            }

            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
        }

        /// <summary>
        /// Returns the backing type and the case lines for the enums of a part
        /// when they map to a PHP backed enum, otherwise null. Only string and
        /// int are valid backing types, so number and boolean enums fall back
        /// to literal union docblocks. Integer enums with null entries cannot
        /// be represented as an int backed enum and fall back as well
        /// </summary>
        private (string backingType, List<string> caseLines) GetBackedEnumCases(IJSBPart part)
        {
            if (part is JSBString jsbString)
            {
                if (jsbString.Enums.Count == 0)
                {
                    return (null, null);
                }
                HashSet<string> caseNames = new HashSet<string>();
                List<string> caseLines = new List<string>();
                foreach (string enumValue in jsbString.Enums)
                {
                    string caseName = MakeUniqueCaseName(MakeValidEnumCaseName(enumValue), caseNames);
                    caseNames.Add(caseName);
                    caseLines.Add($"case {caseName} = '{EscapePhpString(enumValue)}';");
                }
                return ("string", caseLines);
            }
            if (part is JSBInteger jsbInteger)
            {
                if (jsbInteger.Enums.Count == 0 || jsbInteger.Enums.Any(enumValue => !enumValue.HasValue))
                {
                    return (null, null);
                }
                HashSet<string> caseNames = new HashSet<string>();
                List<string> caseLines = new List<string>();
                foreach (long? enumValue in jsbInteger.Enums)
                {
                    string caseName = MakeUniqueCaseName(MakeValidEnumCaseName(enumValue.Value.ToString()), caseNames);
                    caseNames.Add(caseName);
                    caseLines.Add($"case {caseName} = {enumValue.Value};");
                }
                return ("int", caseLines);
            }
            return (null, null);
        }

        /// <summary>
        /// Returns the PHP literal tokens for the enums of a part, or null when
        /// the part has no enums or its enums map to a backed enum. Number and
        /// boolean enums are only expressed through these docblock unions
        /// </summary>
        private List<string> GetEnumLiterals(IJSBPart part)
        {
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
        /// Emits a docblock mirroring the comments of the C# generator, with
        /// an optional @var line for properties
        /// </summary>
        private void GenerateComments(CodeBuilder codeBuilder, IdentifierString key, string description, string docblockType)
        {
            codeBuilder
                .L("/**")
                .L($" * {key}: {description}");
            if (docblockType != null)
            {
                codeBuilder.L($" * @var {docblockType}");
            }
            codeBuilder.L(" */");
        }

        #endregion

        #region Make type

        /// <summary>
        /// Maps a part to its PHP type and makes sure the type declaration for
        /// objects, merged AllOf parts and backed enums is emitted
        /// </summary>
        private string MakeCorrectItemType(IJSBPart jsonSchemaBuilderPart)
        {
            //Value type parts with string or integer enums become backed enums
            if (jsonSchemaBuilderPart.PartType != JSBPartType.Array && jsonSchemaBuilderPart.PartType != JSBPartType.Object
                && GetBackedEnumCases(jsonSchemaBuilderPart).backingType != null)
            {
                EnqueueType(jsonSchemaBuilderPart);
                return TransformToTitleCase(jsonSchemaBuilderPart.Name) + "Enum";
            }
            switch (jsonSchemaBuilderPart.PartType)
            {
                case JSBPartType.Array:
                    //TODO support multiple items
                    JSBArray jsonSchemaBuilderArray = jsonSchemaBuilderPart as JSBArray;
                    //The item type is carried by the docblock, the type stays array
                    MakeCorrectItemType(jsonSchemaBuilderArray.Items.First());
                    return "array";
                case JSBPartType.Object:
                    EnqueueType(jsonSchemaBuilderPart);
                    return TransformToTitleCase(jsonSchemaBuilderPart.Name);
                case JSBPartType.Integer:
                    return "int";
                case JSBPartType.Number:
                    return "float";
                case JSBPartType.Boolean:
                    return "bool";
                case JSBPartType.String:
                case JSBPartType.Email:
                case JSBPartType.Base64String:
                case JSBPartType.DataString:
                case JSBPartType.Uri:
                case JSBPartType.Guid:
                case JSBPartType.PhoneNumber:
                    //PHP has no built-in equivalents, the wire format is a JSON string
                    return "string";
                case JSBPartType.Date:
                case JSBPartType.DateTime:
                case JSBPartType.Time:
                    //PHP has no date only value object, so the immutable DateTime is used
                    return "\\DateTimeImmutable";
                case JSBPartType.AllOf:
                    JSBAllOf jsonSchemaBuilderAllOf = jsonSchemaBuilderPart as JSBAllOf;
                    bool allSubSchemasAreObjects = jsonSchemaBuilderAllOf.Schemas.All(subSchema => subSchema.PartType == JSBPartType.Object);
                    if (allSubSchemasAreObjects)
                    {
                        EnqueueType(jsonSchemaBuilderPart);
                        return TransformToTitleCase(jsonSchemaBuilderPart.Name);
                    }
                    //A merged class cannot be built when a subschema is not an object,
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
                        return MakeCorrectItemType(pair.schemaValue);
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
        /// Maps a part to the @var type of its docblock. Arrays carry the item
        /// type, and number and boolean enums carry their literal union
        /// </summary>
        private string MakeDocblockVarType(IJSBPart jsonSchemaBuilderPart)
        {
            if (jsonSchemaBuilderPart.PartType == JSBPartType.Array)
            {
                //TODO support multiple items
                JSBArray jsonSchemaBuilderArray = jsonSchemaBuilderPart as JSBArray;
                return MakeDocblockVarType(jsonSchemaBuilderArray.Items.First()) + "[]";
            }
            List<string> enumLiterals = GetEnumLiterals(jsonSchemaBuilderPart);
            if (enumLiterals != null)
            {
                return string.Join("|", enumLiterals);
            }
            return MakeCorrectItemType(jsonSchemaBuilderPart);
        }

        /// <summary>
        /// Maps a part resolved from a reference to its PHP type. The top level
        /// type of another registered schema lives in its own generated file and
        /// is imported with a use statement when it lives in another module.
        /// Referenced definitions are local types and are emitted in this file
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
                    //Objects, merged AllOf parts and backed enums are named types in
                    //the referenced file and have to be imported when their module differs
                    if (referencedTopPart.PartType == JSBPartType.Object)
                    {
                        return MaybeAddUse(TransformToTitleCase(referencedTopPart.Name), referencedRelativeLocalFile);
                    }
                    if (referencedTopPart.PartType == JSBPartType.AllOf)
                    {
                        JSBAllOf referencedAllOf = referencedTopPart as JSBAllOf;
                        if (referencedAllOf.Schemas.All(subSchema => subSchema.PartType == JSBPartType.Object))
                        {
                            return MaybeAddUse(TransformToTitleCase(referencedTopPart.Name), referencedRelativeLocalFile);
                        }
                        //A merged class cannot be built when a subschema is not an
                        //object, so the part degrades to object
                        return "object";
                    }
                    if (referencedTopPart.PartType != JSBPartType.Array && GetBackedEnumCases(referencedTopPart).backingType != null)
                    {
                        return MaybeAddUse(TransformToTitleCase(referencedTopPart.Name) + "Enum", referencedRelativeLocalFile);
                    }
                    //Other top parts are encased in a class in the referenced file,
                    //so the reference resolves to the wire type of the top part
                    //mirroring the C# generator
                    return MakeCorrectItemType(referencedTopPart);
                }
                //A reference to the root schema itself resolves to the root top part
                return MakeCorrectItemType(referencedSchema.TopPart);
            }
            //Referenced definitions and subschemas are emitted locally
            return MakeCorrectItemType(referencedPart);
        }

        /// <summary>
        /// Adds a use statement for a type in another module and returns the
        /// type name. A type in the same module already shares the namespace
        /// and needs no import
        /// </summary>
        private string MaybeAddUse(string typeName, string referencedRelativeLocalFile)
        {
            string referencedDirectory = (Path.GetDirectoryName(referencedRelativeLocalFile) ?? string.Empty).Replace('\\', '/');
            string rootDirectory = _startNameSpace.ToFilePath.Replace('\\', '/');
            if (!string.IsNullOrEmpty(referencedDirectory) && !referencedDirectory.Equals(rootDirectory, StringComparison.Ordinal))
            {
                _uses.Add($"use {NamespaceFromModulePath(referencedDirectory)}\\{typeName};");
            }
            return typeName;
        }

        /// <summary>
        /// Maps a schema JSON fragment resolved from a file to its PHP type
        /// </summary>
        private string MakeCorrectItemType(JsonValue jsonValueOfSchema)
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
            else
            {
                throw new CodeGenerationException($"MakeCorrectItemType has neither title nor id");
            }
            string type = string.Empty;
            if (jsonObjectOfSchema.TryGetValue("type", out JsonValue jsonValueType))
            {
                type = jsonValueType.String;
            }
            else
            {
                throw new CodeGenerationException($"MakeCorrectItemType has no type");
            }
            switch (type)
            {
                case "string":
                    return "string";
                case "integer":
                    return "int";
                case "number":
                    return "float";
                case "boolean":
                    return "bool";
                case "object":
                    return TransformToTitleCase(name);
                default:
                    throw new NotImplementedException($"MakeCorrectItemType for {type} has not been implemented");
            }
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
            else if (GetBackedEnumCases(part).backingType != null)
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
                relativeLocalFileWithExpandedDot = Path.Combine(_startNameSpace.ToFilePath, _rootSchema.Name + ".schema.json");
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

        /// <summary>
        /// Returns the PHP namespace of the module the generator emits
        /// </summary>
        private string NamespaceName
        {
            get
            {
                return _startNameSpace.ToString().Replace(".", @"\");
            }
        }

        /// <summary>
        /// Maps a module path like Funny/Onion to a PHP namespace like Funny\Onion
        /// </summary>
        private string NamespaceFromModulePath(string modulePath)
        {
            return modulePath.Replace("/", @"\");
        }

        /// <summary>
        /// Makes a valid PHP enum case name from an enum value. Characters that
        /// are not valid in a PHP label become underscores and a leading digit
        /// is prefixed with an underscore
        /// </summary>
        private string MakeValidEnumCaseName(string enumValue)
        {
            string caseName = Regex.Replace(enumValue, "[^a-zA-Z0-9_]", "_");
            if (string.IsNullOrEmpty(caseName) || char.IsDigit(caseName[0]))
            {
                caseName = "_" + caseName;
            }
            return caseName;
        }

        /// <summary>
        /// Makes sure an enum case name is only used once by adding a suffix
        /// </summary>
        private string MakeUniqueCaseName(string caseName, HashSet<string> usedCaseNames)
        {
            if (!usedCaseNames.Contains(caseName))
            {
                return caseName;
            }
            int suffix = 1;
            while (usedCaseNames.Contains($"{caseName}_{suffix}"))
            {
                suffix += 1;
            }
            return $"{caseName}_{suffix}";
        }

        /// <summary>
        /// Escapes a value for use inside a single quoted PHP string
        /// </summary>
        private string EscapePhpString(string value)
        {
            return value.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        #endregion
    }
}
