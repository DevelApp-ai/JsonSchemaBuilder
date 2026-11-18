using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Manatee.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// Java code generator for JSON Schema. Ports the structure of the CSharp generator,
    /// emitting one Java file per registered schema with the same class layout, adapted
    /// to Java conventions: packages from modules, private fields with getters/setters,
    /// Jackson annotations for serialization and enum value mapping through @JsonValue.
    /// </summary>
    public class Java
    {
        private const string FILE_ENDING = ".java";

        /// <summary>
        /// Generate fileName and code for Java
        /// </summary>
        /// <param name="codeGenerator"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            List<(string fileName, string code)> generatedCode = new List<(string fileName, string code)>();
            foreach (IJsonSchemaDefinition schema in codeGenerator.RegisteredJsonSchemas.Values)
            {
                string fileName = Path.Combine(codeGenerator.ApplicationRoot, schema.Module.ToFilePath, schema.Name + FILE_ENDING);

                Java java = new Java(codeGenerator, schema.Module, schema.Name);
                generatedCode.Add((fileName, java.GenerateCode(schema.JsonSchemaBuilderSchema)));
            }
            return generatedCode;
        }

        private NamespaceString _startNameSpace;
        private string _applicationRoot;
        private JSBSchema _rootSchema;
        private CodeGenerator _codeGenerator;
        private HashSet<string> _emittedAllOfClassNames = new HashSet<string>();
        //The first class emitted in a file is a top level class, all following classes are nested
        //(Java has no partial classes, nested classes must be declared static)
        private bool _nextClassIsTopLevel = true;
        //Java requires the public top level class name to match the file name, so the schema
        //file name is used as class name instead of the internal top part name
        private string _topLevelClassName;
        //The internal top part names of registered schemas mapped to their file names, so
        //references across files emit the same class name as the file that declares it
        private Dictionary<string, string> _topPartNameToSchemaName = new Dictionary<string, string>();
        //Raw schema objects referenced through json pointers, queued for class emission
        private Queue<(string typeName, JsonValue rawSchema)> _rawTypeQueue;
        private HashSet<string> _enqueuedRawTypeNames;

        private Java(CodeGenerator codeGenerator, NamespaceString startNameSpace, IdentifierString topLevelClassName)
        {
            _applicationRoot = codeGenerator.ApplicationRoot;
            _startNameSpace = startNameSpace;
            _codeGenerator = codeGenerator;
            _topLevelClassName = topLevelClassName.ToString();
            foreach (IJsonSchemaDefinition registeredSchema in codeGenerator.RegisteredJsonSchemas.Values)
            {
                if (registeredSchema.JsonSchemaBuilderSchema.TopPart != null)
                {
                    string topPartName = registeredSchema.JsonSchemaBuilderSchema.TopPart.Name.ToString();
                    if (!_topPartNameToSchemaName.ContainsKey(topPartName))
                    {
                        _topPartNameToSchemaName.Add(topPartName, registeredSchema.Name.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Generate code from JsonSchemaBuilderSchema
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        private string GenerateCode(JSBSchema schema)
        {
            CodeBuilder codeBuilder = new CodeBuilder();
            _rootSchema = schema;
            _rawTypeQueue = new Queue<(string typeName, JsonValue rawSchema)>();
            _enqueuedRawTypeNames = new HashSet<string>();
            GenerateStartOfSchema(codeBuilder, schema);
            if (schema.TopPart != null)
            {
                if (schema.TopPart.PartType == JSBPartType.Object)
                {
                    //Java requires the top level class name to match the file name
                    GenerateCodeForBuilderPart(codeBuilder, _topLevelClassName, schema.TopPart, schema.Definitions);
                }
                else
                {
                    List<IJSBPart> properties = new List<IJSBPart>();
                    properties.Add(schema.TopPart);

                    JSBObject encasingObject = new JSBObject(schema.Name, schema.Description, props: properties);
                    GenerateCodeForBuilderPart(codeBuilder, _topLevelClassName, encasingObject, schema.Definitions);
                }
            }
            else
            {
                List<IJSBPart> properties = new List<IJSBPart>();
                JSBObject encasingObject = new JSBObject(schema.Name, schema.Description, props: properties);
                GenerateCodeForBuilderPart(codeBuilder, _topLevelClassName, encasingObject, schema.Definitions);
            }
            //Emit classes queued for raw schemas referenced through json pointers.
            //They are appended at file scope after the top level class
            while (_rawTypeQueue.Count > 0)
            {
                (string rawTypeName, JsonValue rawSchema) = _rawTypeQueue.Dequeue();
                GenerateRawClass(codeBuilder, rawTypeName, rawSchema);
            }
            GenerateEndOfSchema(codeBuilder, schema);
            return codeBuilder.Build();
        }

        /// <summary>
        /// Generates package declaration and imports
        /// </summary>
        /// <param name="codeBuilder"></param>
        /// <param name="schema"></param>
        private void GenerateStartOfSchema(CodeBuilder codeBuilder, JSBSchema schema)
        {
            //TODO run through schema children to get all references to make sure they are included
            codeBuilder
                .L($"package {MakePackageName(_startNameSpace.ToString())};")
                .EmptyLine()
                .L("import com.fasterxml.jackson.annotation.JsonProperty;")
                .L("import com.fasterxml.jackson.annotation.JsonValue;")
                .L("import java.time.LocalDate;")
                .L("import java.time.OffsetDateTime;")
                .L("import java.time.OffsetTime;")
                .L("import java.util.ArrayList;")
                .L("import java.util.Arrays;")
                .L("import java.util.List;")
                .L("import java.net.URI;")
                .L("import java.util.UUID;")
                .EmptyLine();
        }

        private void GenerateEndOfSchema(CodeBuilder codeBuilder, JSBSchema schema)
        {
            //Java has no namespace block, the file ends with the last class
        }

        private void GenerateCodeForBuilderPart(CodeBuilder codeBuilder, IdentifierString key, IJSBPart value, Dictionary<string, IJSBPart> definitions = null, bool parentIsArray = false, bool isDefinition = false)
        {
            switch (value.PartType)
            {
                case JSBPartType.Array:
                    JSBArray jsonSchemaBuilderArray = value as JSBArray;
                    if (jsonSchemaBuilderArray.Enums != null && jsonSchemaBuilderArray.Enums.Count > 0)
                    {
                        GenerateEnumArray(codeBuilder, key, jsonSchemaBuilderArray);
                    }
                    else
                    {
                        GenerateOrdinaryArray(codeBuilder, key, jsonSchemaBuilderArray);
                    }
                    break;
                case JSBPartType.Boolean:
                    JSBBoolean jsonSchemaBuilderBoolean = value as JSBBoolean;
                    if (jsonSchemaBuilderBoolean.Enums != null && jsonSchemaBuilderBoolean.Enums.Count > 0)
                    {
                        GenerateEnumBoolean(codeBuilder, key, jsonSchemaBuilderBoolean);
                    }
                    else
                    {
                        if (!parentIsArray)
                        {
                            GenerateOrdinaryBoolean(codeBuilder, key, jsonSchemaBuilderBoolean);
                        }
                    }
                    break;
                case JSBPartType.Date:
                    JSBDate jsonSchemaBuilderDate = value as JSBDate;
                    if (jsonSchemaBuilderDate.Enums != null && jsonSchemaBuilderDate.Enums.Count > 0)
                    {
                        GenerateEnumDate(codeBuilder, key, jsonSchemaBuilderDate);
                    }
                    else
                    {
                        if (!parentIsArray)
                        {
                            GenerateOrdinaryDate(codeBuilder, key, jsonSchemaBuilderDate);
                        }
                    }
                    break;
                case JSBPartType.DateTime:
                    JSBDateTime jsonSchemaBuilderDateTime = value as JSBDateTime;
                    if (jsonSchemaBuilderDateTime.Enums != null && jsonSchemaBuilderDateTime.Enums.Count > 0)
                    {
                        GenerateEnumDateTime(codeBuilder, key, jsonSchemaBuilderDateTime);
                    }
                    else
                    {
                        if (!parentIsArray)
                        {
                            GenerateOrdinaryDateTime(codeBuilder, key, jsonSchemaBuilderDateTime);
                        }
                    }
                    break;
                case JSBPartType.Email:
                    JSBEmail jsonSchemaBuilderEmail = value as JSBEmail;
                    if (jsonSchemaBuilderEmail.Enums != null && jsonSchemaBuilderEmail.Enums.Count > 0)
                    {
                        GenerateEnumEmail(codeBuilder, key, jsonSchemaBuilderEmail);
                    }
                    else
                    {
                        GenerateOrdinaryEmail(codeBuilder, key, jsonSchemaBuilderEmail);
                    }
                    break;
                case JSBPartType.DataString:
                    JSBDataString jsonSchemaBuilderDataString = value as JSBDataString;
                    if (jsonSchemaBuilderDataString.Enums != null && jsonSchemaBuilderDataString.Enums.Count > 0)
                    {
                        GenerateEnumString(codeBuilder, key, jsonSchemaBuilderDataString, isDefinition);
                    }
                    else
                    {
                        GenerateOrdinaryDataString(codeBuilder, key, jsonSchemaBuilderDataString);
                    }
                    break;
                case JSBPartType.Integer:
                    JSBInteger jsonSchemaBuilderInteger = value as JSBInteger;
                    if (jsonSchemaBuilderInteger.Enums != null && jsonSchemaBuilderInteger.Enums.Count > 0)
                    {
                        GenerateEnumInteger(codeBuilder, key, jsonSchemaBuilderInteger);
                    }
                    else
                    {
                        if (!parentIsArray)
                        {
                            GenerateOrdinaryInteger(codeBuilder, key, jsonSchemaBuilderInteger);
                        }
                    }
                    break;
                case JSBPartType.Number:
                    JSBNumber jsonSchemaBuilderNumber = value as JSBNumber;
                    if (jsonSchemaBuilderNumber.Enums != null && jsonSchemaBuilderNumber.Enums.Count > 0)
                    {
                        GenerateEnumNumber(codeBuilder, key, jsonSchemaBuilderNumber);
                    }
                    else
                    {
                        if (!parentIsArray)
                        {
                            GenerateOrdinaryNumber(codeBuilder, key, jsonSchemaBuilderNumber);
                        }
                    }
                    break;
                case JSBPartType.Object:
                    JSBObject jsonSchemaBuilderObject = value as JSBObject;
                    if (jsonSchemaBuilderObject.Enums != null && jsonSchemaBuilderObject.Enums.Count > 0)
                    {
                        GenerateEnumObject(codeBuilder, key, jsonSchemaBuilderObject, definitions);
                    }
                    else
                    {
                        GenerateOrdinaryObject(codeBuilder, key, jsonSchemaBuilderObject, definitions, parentIsArray);
                    }
                    break;
                case JSBPartType.String:
                    JSBString jsonSchemaBuilderString = value as JSBString;
                    if (jsonSchemaBuilderString.Enums != null && jsonSchemaBuilderString.Enums.Count > 0)
                    {
                        GenerateEnumString(codeBuilder, key, jsonSchemaBuilderString, isDefinition);
                    }
                    else
                    {
                        GenerateOrdinaryString(codeBuilder, key, jsonSchemaBuilderString);
                    }
                    break;
                case JSBPartType.Time:
                    JSBTime jsonSchemaBuilderTime = value as JSBTime;
                    if (jsonSchemaBuilderTime.Enums != null && jsonSchemaBuilderTime.Enums.Count > 0)
                    {
                        GenerateEnumTime(codeBuilder, key, jsonSchemaBuilderTime);
                    }
                    else
                    {
                        GenerateOrdinaryTime(codeBuilder, key, jsonSchemaBuilderTime);
                    }
                    break;
                case JSBPartType.IriReference:
                    //Reference cannot be an enum
                    if (!parentIsArray)
                    {
                        JSBRef jsonSchemaBuilderUriReference = value as JSBRef;
                        GenerateOrdinaryUriReference(codeBuilder, key, jsonSchemaBuilderUriReference);
                    }
                    break;
                case JSBPartType.Base64String:
                    JSBBase64String jsonSchemaBuilderBase64String = value as JSBBase64String;
                    GenerateOrdinaryBase64String(codeBuilder, key, jsonSchemaBuilderBase64String);
                    break;
                case JSBPartType.Uri:
                    JSBUri jsonSchemaBuilderUri = value as JSBUri;
                    GenerateOrdinaryUri(codeBuilder, key, jsonSchemaBuilderUri);
                    break;
                case JSBPartType.Guid:
                    JSBGuid jsonSchemaBuilderGuid = value as JSBGuid;
                    GenerateOrdinaryGuid(codeBuilder, key, jsonSchemaBuilderGuid);
                    break;
                case JSBPartType.PhoneNumber:
                    JSBPhoneNumber jsonSchemaBuilderPhoneNumber = value as JSBPhoneNumber;
                    GenerateOrdinaryPhoneNumber(codeBuilder, key, jsonSchemaBuilderPhoneNumber);
                    break;
                case JSBPartType.AllOf:
                    JSBAllOf jsonSchemaBuilderAllOf = value as JSBAllOf;
                    GenerateOrdinaryAllOf(codeBuilder, key, jsonSchemaBuilderAllOf, parentIsArray);
                    break;
                case JSBPartType.AnyOf:
                case JSBPartType.OneOf:
                    //The value validates against at least one (AnyOf) or exactly one (OneOf) of the subschemas, which maps to object
                    if (!parentIsArray)
                    {
                        GenerateOrdinaryCompositionChoice(codeBuilder, key, value);
                    }
                    break;
                default:
                    throw new NotImplementedException($"Java code generation for PartType {value.PartType} has not been implemented");
            }
        }

        #region Generate Array

        private void GenerateEnumArray(CodeBuilder codeBuilder, IdentifierString key, JSBArray jsonSchemaBuilderArray)
        {
            throw new NotImplementedException("Enum for array has not been implemented");
        }

        private void GenerateOrdinaryArray(CodeBuilder codeBuilder, IdentifierString key, JSBArray jsonSchemaBuilderArray)
        {
            foreach (IJSBPart part in jsonSchemaBuilderArray.Items)
            {
                GenerateCodeForBuilderPart(codeBuilder, part.Name, part, parentIsArray: true);
            }

            if (jsonSchemaBuilderArray.Items.Count == 1)
            {
                GenerateProperty(codeBuilder, key, jsonSchemaBuilderArray, $"List<{MakeCorrectItemType(jsonSchemaBuilderArray.Items[0])}>", GenerateDefaultIfExisting(key, jsonSchemaBuilderArray));
            }
            else
            {
                throw new NotImplementedException("Array with more than one item is not implemented");
            }
        }

        private string MakeCorrectItemType(IJSBPart jsonSchemaBuilderPart)
        {
            switch (jsonSchemaBuilderPart.PartType)
            {
                case JSBPartType.Array:
                    //TODO support multiple items
                    return $"List<{MakeCorrectItemType((jsonSchemaBuilderPart as JSBArray).Items.First())}>";
                case JSBPartType.Object:
                    return MapClassName(jsonSchemaBuilderPart.Name);
                case JSBPartType.Integer:
                    return "Long";
                case JSBPartType.Number:
                    return "Double";
                case JSBPartType.Boolean:
                    return "Boolean";
                case JSBPartType.String:
                    JSBString jSBString = jsonSchemaBuilderPart as JSBString;
                    if (jSBString.Enums.Count > 0)
                    {
                        return jsonSchemaBuilderPart.Name + "Enum";
                    }
                    return "String";
                case JSBPartType.Date:
                    return "LocalDate";
                case JSBPartType.DateTime:
                    return "OffsetDateTime";
                case JSBPartType.Time:
                    return "OffsetTime";
                case JSBPartType.DataString:
                    //A DataString with enums uses the generated string enum as item type
                    JSBString dataStringItem = jsonSchemaBuilderPart as JSBString;
                    if (dataStringItem.Enums.Count > 0)
                    {
                        return jsonSchemaBuilderPart.Name + "Enum";
                    }
                    return "String";
                case JSBPartType.Email:
                case JSBPartType.Base64String:
                case JSBPartType.PhoneNumber:
                    return "String";
                case JSBPartType.Uri:
                    return "URI";
                case JSBPartType.Guid:
                    return "UUID";
                case JSBPartType.AllOf:
                    //The AllOf part is merged into one class through conversion
                    return MapClassName(jsonSchemaBuilderPart.Name);
                case JSBPartType.AnyOf:
                case JSBPartType.OneOf:
                    //The value validates against one of the subschemas, which maps to object
                    return "Object";
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
                    (IJSBPart refPart, JsonValue schemaValue) pair = LookupReferencedPart(jsbRef);
                    if (pair.refPart != null)
                    {
                        return MakeCorrectItemType(pair.refPart);
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

        private (IJSBPart refPart, JsonValue schemaValue) LookupReferencedPart(JSBRef jSBRef)
        {
            string relativeLocalFileWithExpandedDot = ExpandRelativelocalFile(jSBRef.RelativeLocalFile);
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

        #region Generate Boolean

        private void GenerateOrdinaryBoolean(CodeBuilder codeBuilder, IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderBoolean, "Boolean", GenerateDefaultIfExisting(key, jsonSchemaBuilderBoolean));
        }

        private void GenerateEnumBoolean(CodeBuilder codeBuilder, IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            List<string> rawNames = jsonSchemaBuilderBoolean.Enums.Select(enumValue => enumValue.HasValue ?
                (enumValue.Value ? "True" : "False") : "Null").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);
            List<string> memberValueLiterals = jsonSchemaBuilderBoolean.Enums.Select(enumValue => enumValue.HasValue ?
                (enumValue.Value ? "1" : "0") : "2").ToList();
            GenerateEnum(codeBuilder, enumName, memberNames, memberValueLiterals, "int");

            GenerateProperty(codeBuilder, key, jsonSchemaBuilderBoolean, enumName, GenerateDefaultIfExisting(key, jsonSchemaBuilderBoolean));
        }

        #endregion

        #region Generate Date

        private void GenerateOrdinaryDate(CodeBuilder codeBuilder, IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderDate, "LocalDate", GenerateDefaultIfExisting(key, jsonSchemaBuilderDate));
        }

        private void GenerateEnumDate(CodeBuilder codeBuilder, IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            //Create valid enum member names from the date strings (e.g. "2020-01-15" -> "Date_2020_01_15")
            List<string> rawNames = jsonSchemaBuilderDate.Enums.Select(enumValue => $"Date_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);
            //The counter is the enum value, matching the CSharp generator
            List<string> memberValueLiterals = memberNames.Select((_, counter) => counter.ToString(CultureInfo.InvariantCulture)).ToList();
            GenerateEnum(codeBuilder, enumName, memberNames, memberValueLiterals, "int");

            GenerateProperty(codeBuilder, key, jsonSchemaBuilderDate, enumName, GenerateDefaultIfExisting(key, jsonSchemaBuilderDate));
        }

        #endregion

        #region Generate DateTime

        private void GenerateOrdinaryDateTime(CodeBuilder codeBuilder, IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderDateTime, "OffsetDateTime", GenerateDefaultIfExisting(key, jsonSchemaBuilderDateTime));
        }

        private void GenerateEnumDateTime(CodeBuilder codeBuilder, IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            //Create valid enum member names from the datetime strings
            List<string> rawNames = jsonSchemaBuilderDateTime.Enums.Select(enumValue => $"DateTime_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);
            List<string> memberValueLiterals = memberNames.Select((_, counter) => counter.ToString(CultureInfo.InvariantCulture)).ToList();
            GenerateEnum(codeBuilder, enumName, memberNames, memberValueLiterals, "int");

            GenerateProperty(codeBuilder, key, jsonSchemaBuilderDateTime, enumName, GenerateDefaultIfExisting(key, jsonSchemaBuilderDateTime));
        }

        #endregion

        #region Generate Email

        private void GenerateEnumEmail(CodeBuilder codeBuilder, IdentifierString key, JSBEmail jsonSchemaBuilderEmail)
        {
            throw new NotImplementedException("Enum on email has not been implemented");
        }

        private void GenerateOrdinaryEmail(CodeBuilder codeBuilder, IdentifierString key, JSBEmail jsonSchemaBuilderEmail)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderEmail, "String", GenerateDefaultIfExisting(key, jsonSchemaBuilderEmail));
        }

        #endregion

        #region Generate DataString

        private void GenerateOrdinaryDataString(CodeBuilder codeBuilder, IdentifierString key, JSBDataString jsonSchemaBuilderDataString)
        {
            //The DataString value object with its limitation handling is a C#/VB side concept,
            //so Java gets the plain wire type like Email, PhoneNumber and Base64String
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderDataString, "String", GenerateDefaultIfExisting(key, jsonSchemaBuilderDataString));
        }

        #endregion

        #region Generate Integer

        private void GenerateOrdinaryInteger(CodeBuilder codeBuilder, IdentifierString key, JSBInteger jsonSchemaBuilderInteger)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderInteger, "Long", GenerateDefaultIfExisting(key, jsonSchemaBuilderInteger));
        }

        private void GenerateEnumInteger(CodeBuilder codeBuilder, IdentifierString key, JSBInteger jsonSchemaBuilderInteger)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            //Negative values use a Minus prefix so the member name stays a valid identifier
            List<string> memberNames = MakeEnumMemberNames(jsonSchemaBuilderInteger.Enums.Select(enumValue => enumValue.HasValue ?
                $"Value{(enumValue.Value < 0 ? $"Minus{(-enumValue.Value).ToString(CultureInfo.InvariantCulture)}" : enumValue.Value.ToString(CultureInfo.InvariantCulture))}" :
                "ValueNull"));
            //The member value is the actual integer, matching the CSharp generator
            List<string> memberValueLiterals = jsonSchemaBuilderInteger.Enums.Select(enumValue => enumValue.HasValue ?
                $"{enumValue.Value.ToString(CultureInfo.InvariantCulture)}L" : "0L").ToList();
            GenerateEnum(codeBuilder, enumName, memberNames, memberValueLiterals, "long");

            GenerateProperty(codeBuilder, key, jsonSchemaBuilderInteger, enumName, GenerateDefaultIfExisting(key, jsonSchemaBuilderInteger));
        }

        #endregion

        #region Generate Number

        private void GenerateOrdinaryNumber(CodeBuilder codeBuilder, IdentifierString key, JSBNumber jsonSchemaBuilderNumber)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderNumber, "Double", GenerateDefaultIfExisting(key, jsonSchemaBuilderNumber));
        }

        private void GenerateEnumNumber(CodeBuilder codeBuilder, IdentifierString key, JSBNumber jsonSchemaBuilderNumber)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            //Use the counter as the enum value to avoid collisions, matching the CSharp generator
            List<string> memberNames = MakeEnumMemberNames(jsonSchemaBuilderNumber.Enums.Select(enumValue => enumValue.HasValue ?
                $"Value{enumValue.Value.ToString(CultureInfo.InvariantCulture).Replace(".", "_").Replace("-", "Minus")}" :
                "ValueNull"));
            List<string> memberValueLiterals = memberNames.Select((_, counter) => $"{counter.ToString(CultureInfo.InvariantCulture)}L").ToList();
            GenerateEnum(codeBuilder, enumName, memberNames, memberValueLiterals, "long");

            GenerateProperty(codeBuilder, key, jsonSchemaBuilderNumber, enumName, GenerateDefaultIfExisting(key, jsonSchemaBuilderNumber));
        }

        #endregion

        #region Generate Objects

        private void GenerateEnumObject(CodeBuilder codeBuilder, IdentifierString key, JSBObject jsonSchemaBuilderObject, Dictionary<string, IJSBPart> definitions)
        {
            throw new NotImplementedException("Enum on objects has not been implemented");
        }

        private void GenerateOrdinaryObject(CodeBuilder codeBuilder, IdentifierString key, JSBObject jsonSchemaBuilderObject, Dictionary<string, IJSBPart> definitions, bool parentIsArray)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderObject);

            string className = TransformToTitleCase(key);
            //Java has no partial classes: only the first class of a file is a top level class,
            //all classes emitted inside it must be declared static (nested static classes)
            bool isTopLevel = _nextClassIsTopLevel;
            _nextClassIsTopLevel = false;
            codeBuilder
                .L($"public {(isTopLevel ? string.Empty : "static ")}class {className}")
                .L("{")
                .IndentIncrease();

            //Add definitions
            if (definitions != null)
            {
                foreach (KeyValuePair<string, IJSBPart> pair in definitions)
                {
                    GenerateCodeForBuilderPart(codeBuilder, pair.Key, pair.Value, isDefinition: true);
                    codeBuilder.EmptyLine();
                }
            }

            //TODO Add own code

            //Add children
            foreach (KeyValuePair<IdentifierString, IJSBPart> pair in jsonSchemaBuilderObject.Properties)
            {
                GenerateCodeForBuilderPart(codeBuilder, pair.Key, pair.Value);
                codeBuilder.EmptyLine();
            }

            codeBuilder
                .IndentDecrease()
                .L("}");

            if (definitions == null && !parentIsArray)// Assume not top part
            {
                GenerateProperty(codeBuilder, key, jsonSchemaBuilderObject, className, string.Empty, $"{TransformToTitleCase(key)}Prop");
            }
        }

        /// <summary>
        /// Generates an AllOf part by conversion: when all subschemas are objects their
        /// properties are merged into one class. If any subschema is not an object the
        /// merged class cannot be built and the part degrades to an object property.
        /// </summary>
        private void GenerateOrdinaryAllOf(CodeBuilder codeBuilder, IdentifierString key, JSBAllOf jsonSchemaBuilderAllOf, bool parentIsArray)
        {
            bool allSubSchemasAreObjects = jsonSchemaBuilderAllOf.Schemas.All(subSchema => subSchema.PartType == JSBPartType.Object);

            if (!allSubSchemasAreObjects)
            {
                if (!parentIsArray)
                {
                    GenerateOrdinaryCompositionChoice(codeBuilder, key, jsonSchemaBuilderAllOf);
                }
                return;
            }

            GenerateComments(codeBuilder, key, jsonSchemaBuilderAllOf);

            string mergedClassName = TransformToTitleCase(key);
            //The same AllOf part can be referenced both as a property and as an array
            //item in one schema. The merged class is only emitted once per file,
            //otherwise the generated code would contain duplicate class declarations.
            bool mergedClassAlreadyEmitted = !_emittedAllOfClassNames.Add(TransformToTitleCase(key));

            if (!mergedClassAlreadyEmitted)
            {
                bool isTopLevel = _nextClassIsTopLevel;
                _nextClassIsTopLevel = false;
                codeBuilder
                    .L($"public {(isTopLevel ? string.Empty : "static ")}class {mergedClassName}")
                    .L("{")
                    .IndentIncrease();

                //Merge the properties of all subschemas. A property name present in several
                //subschemas is only emitted once, matching the AllOf requirement that the
                //same named property must validate against every subschema that defines it.
                HashSet<string> emittedPropertyNames = new HashSet<string>();
                foreach (IJSBPart subSchema in jsonSchemaBuilderAllOf.Schemas)
                {
                    JSBObject subObject = subSchema as JSBObject;
                    foreach (KeyValuePair<IdentifierString, IJSBPart> pair in subObject.Properties)
                    {
                        if (emittedPropertyNames.Add(pair.Key.ToString()))
                        {
                            GenerateCodeForBuilderPart(codeBuilder, pair.Key, pair.Value);
                            codeBuilder.EmptyLine();
                        }
                    }
                }

                codeBuilder
                    .IndentDecrease()
                    .L("}");
            }

            if (!parentIsArray)
            {
                GenerateProperty(codeBuilder, key, jsonSchemaBuilderAllOf, mergedClassName, string.Empty, $"{TransformToTitleCase(key)}Prop");
            }
        }

        /// <summary>
        /// Generates a composition part (AnyOf or OneOf) as an object property since Java has no union type.
        /// </summary>
        private void GenerateOrdinaryCompositionChoice(CodeBuilder codeBuilder, IdentifierString key, IJSBPart jsonSchemaBuilderCompositionChoice)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderCompositionChoice, "Object", string.Empty);
        }

        #endregion

        #region Generate String

        private void GenerateEnumString(CodeBuilder codeBuilder, IdentifierString key, JSBString jsonSchemaBuilderString, bool isDefinition)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            List<string> memberNames = MakeEnumMemberNames(jsonSchemaBuilderString.Enums.Select(enumValue => TransformToTitleCase(enumValue) ?? string.Empty));
            //The backing value is the original enum string so the wire format matches the schema exactly
            List<string> memberValueLiterals = jsonSchemaBuilderString.Enums.Select(enumValue => $"\"{EscapeJavaStringLiteral(enumValue ?? string.Empty)}\"").ToList();
            GenerateEnum(codeBuilder, enumName, memberNames, memberValueLiterals, "String");

            if (!isDefinition)
            {
                GenerateProperty(codeBuilder, key, jsonSchemaBuilderString, enumName, GenerateDefaultIfExisting(key, jsonSchemaBuilderString));
            }
        }

        private void GenerateOrdinaryString(CodeBuilder codeBuilder, IdentifierString key, JSBString jsonSchemaBuilderString)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderString, "String", GenerateDefaultIfExisting(key, jsonSchemaBuilderString));
        }

        #endregion

        #region Generate Base64String

        private void GenerateOrdinaryBase64String(CodeBuilder codeBuilder, IdentifierString key, JSBBase64String jsonSchemaBuilderBase64String)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderBase64String, "String", GenerateDefaultIfExisting(key, jsonSchemaBuilderBase64String));
        }

        #endregion

        #region Generate Uri

        private void GenerateOrdinaryUri(CodeBuilder codeBuilder, IdentifierString key, JSBUri jsonSchemaBuilderUri)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderUri, "URI", GenerateDefaultIfExisting(key, jsonSchemaBuilderUri));
        }

        #endregion

        #region Generate Guid

        private void GenerateOrdinaryGuid(CodeBuilder codeBuilder, IdentifierString key, JSBGuid jsonSchemaBuilderGuid)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderGuid, "UUID", GenerateDefaultIfExisting(key, jsonSchemaBuilderGuid));
        }

        #endregion

        #region Generate PhoneNumber

        private void GenerateOrdinaryPhoneNumber(CodeBuilder codeBuilder, IdentifierString key, JSBPhoneNumber jsonSchemaBuilderPhoneNumber)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderPhoneNumber, "String", GenerateDefaultIfExisting(key, jsonSchemaBuilderPhoneNumber));
        }

        #endregion

        #region Generate Time

        private void GenerateEnumTime(CodeBuilder codeBuilder, IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            //Create valid enum member names from the time strings
            List<string> rawNames = jsonSchemaBuilderTime.Enums.Select(enumValue => $"Time_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);
            List<string> memberValueLiterals = memberNames.Select((_, counter) => counter.ToString(CultureInfo.InvariantCulture)).ToList();
            GenerateEnum(codeBuilder, enumName, memberNames, memberValueLiterals, "int");

            GenerateProperty(codeBuilder, key, jsonSchemaBuilderTime, enumName, GenerateDefaultIfExisting(key, jsonSchemaBuilderTime));
        }

        private void GenerateOrdinaryTime(CodeBuilder codeBuilder, IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderTime, "OffsetTime", GenerateDefaultIfExisting(key, jsonSchemaBuilderTime));
        }

        #endregion

        #region Generate Uri Reference

        private void GenerateOrdinaryUriReference(CodeBuilder codeBuilder, IdentifierString key, JSBRef jsonSchemaBuilderIriReference)
        {
            try
            {
                (IJSBPart refPart, JsonValue schemaValue) pair = LookupReferencedPart(jsonSchemaBuilderIriReference);
                if (pair.refPart != null)
                {
                    GenerateCodeFromInternalPart(codeBuilder, pair.refPart, key, jsonSchemaBuilderIriReference);
                }
                else if (pair.schemaValue != null)
                {
                    GenerateCodeFromSchema(codeBuilder, pair.schemaValue, key, jsonSchemaBuilderIriReference);
                }
                else
                {
                    throw new CodeGenerationException($"LookupReferencedPart failed for reference {jsonSchemaBuilderIriReference.IriReference.OriginalString}");
                }
            }
            catch (CodeGenerationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CodeGenerationException($"GenerateOrdinaryUriReference failed for some reason", ex);
            }
        }

        private void GenerateCodeFromInternalPart(CodeBuilder codeBuilder, IJSBPart referencedPart, IdentifierString key, JSBRef jsonSchemaBuilderUriReference)
        {
            GenerateProperty(codeBuilder, key, jsonSchemaBuilderUriReference, MakeCorrectItemType(referencedPart), GenerateDefaultIfExisting(key, jsonSchemaBuilderUriReference));
        }

        private void GenerateCodeFromSchema(CodeBuilder codeBuilder, JsonValue jsonValueOfSchema, IdentifierString key, JSBRef jsonSchemaBuilderUriReference)
        {
            if (jsonValueOfSchema.Type == JsonValueType.Object)
            {
                GenerateProperty(codeBuilder, key, jsonSchemaBuilderUriReference, MakeCorrectItemType(jsonValueOfSchema, key), GenerateDefaultIfExisting(key, jsonSchemaBuilderUriReference));
            }
            else
            {
                throw new CodeGenerationException($"GenerateOrdinaryUriReference {key} references an invalid schema {jsonSchemaBuilderUriReference.IriReference.OriginalString}");
            }
        }

        /// <summary>
        /// Maps a raw schema json value to a Java type. Object schemas are queued for
        /// class emission through EnqueueRawType since they have no builder parts.
        /// The nameHint is used as class name fallback for schemas without $id or title
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
            string format = string.Empty;
            if (jsonObjectOfSchema.TryGetValue("format", out JsonValue jsonValueFormat))
            {
                format = jsonValueFormat.String;
            }
            switch (type)
            {
                case "string":
                    if (string.IsNullOrWhiteSpace(format))
                    {
                        return "String";
                    }
                    switch (format)
                    {
                        case "time":
                            return "OffsetTime";
                        case "date":
                            return "LocalDate";
                        case "datetime":
                        case "date-time":
                            return "OffsetDateTime";
                        case "uri":
                            return "URI";
                        case "uuid":
                            return "UUID";
                        default:
                            //The format vocabulary is open ended: email, phone and all
                            //unknown string formats map to String like the part based generator
                            return "String";
                    }
                case "boolean":
                    return "Boolean";
                case "object":
                    return EnqueueRawType(TransformToTitleCase(name), jsonValueOfSchema);
                case "number":
                    return "Double";
                case "integer":
                    return "Long";
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
                            return $"List<{MakeCorrectItemType(itemSchema, nameHint)}>";
                        }
                    }
                    throw new NotImplementedException($"Array schema {name} without a single items schema is not supported for reference code generation");
                default:
                    throw new NotImplementedException($"Type {type} with name {name} and format {format} is not supported");
            }
        }

        /// <summary>
        /// Queues a raw schema object for class emission and returns the class name.
        /// The class is emitted once per module across all files of a generation run
        /// </summary>
        /// <param name="typeName"></param>
        /// <param name="rawSchema"></param>
        /// <returns></returns>
        private string EnqueueRawType(string typeName, JsonValue rawSchema)
        {
            string moduleKey = $"{_startNameSpace}|{typeName}";
            //Emitted by another file in the same module: reference the existing class
            if (!_codeGenerator.EmittedRawTypeNames.Add(moduleKey))
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
        /// Emits a class for a raw schema object referenced through a json pointer.
        /// The class is appended at file scope after the top level class, so it is
        /// emitted package private: Java allows only one public top level class per
        /// file and that one must match the file name
        /// </summary>
        /// <param name="codeBuilder"></param>
        /// <param name="typeName"></param>
        /// <param name="rawSchema"></param>
        private void GenerateRawClass(CodeBuilder codeBuilder, string typeName, JsonValue rawSchema)
        {
            JsonObject rawObject = rawSchema.Object;

            string description = "Schema referenced without builder parts";
            if (rawObject.TryGetValue("description", out JsonValue descriptionValue) && descriptionValue.Type == JsonValueType.String)
            {
                description = descriptionValue.String;
            }
            List<string> commentLines = GenerateCommentLines(typeName, description, minSplitLength: 70, maxSplitLength: 90);

            codeBuilder
                .EmptyLine()
                .L("/**");
            foreach (string commentLine in commentLines)
            {
                codeBuilder.L($" * {commentLine}");
            }
            codeBuilder
                .L(" */")
                .L($"class {typeName}")
                .L("{")
                .IndentIncrease();

            if (rawObject.TryGetValue("properties", out JsonValue propertiesValue) && propertiesValue.Type == JsonValueType.Object)
            {
                foreach (KeyValuePair<string, JsonValue> propertyPair in propertiesValue.Object)
                {
                    string itemType = MakeCorrectItemType(propertyPair.Value, propertyPair.Key);
                    GenerateProperty(codeBuilder, propertyPair.Key, null, itemType, string.Empty);
                }
            }

            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
        }

        #endregion

        #region Generate Property

        /// <summary>
        /// Emits a private field with getter and setter, annotated with JsonProperty.
        /// propertyNameOverride is used for the trailing reference properties ({Key}Prop).
        /// </summary>
        private void GenerateProperty(CodeBuilder codeBuilder, IdentifierString key, IJSBPart commentPart, string type, string defaultFragment, string propertyNameOverride = null)
        {
            if (commentPart != null)
            {
                GenerateComments(codeBuilder, key, commentPart);
            }
            string titleCaseName = propertyNameOverride ?? TransformToTitleCase(key);
            string fieldName = MakeFieldName(titleCaseName);
            codeBuilder
                .L($"@JsonProperty(\"{TransformToCamelCase(key)}\")")
                .L($"private {type} {fieldName}{defaultFragment};")
                .L($"public {type} get{titleCaseName}() {{")
                .IndentIncrease()
                .L($"return {fieldName};")
                .IndentDecrease()
                .L("}")
                .L($"public void set{titleCaseName}({type} {fieldName}) {{")
                .IndentIncrease()
                .L($"this.{fieldName} = {fieldName};")
                .IndentDecrease()
                .L("}")
                .EmptyLine();
        }

        /// <summary>
        /// Emits an enum declaration with a backing value exposed through @JsonValue so the
        /// wire format matches the schema. backingType is String, long or int.
        /// </summary>
        private void GenerateEnum(CodeBuilder codeBuilder, string enumName, List<string> memberNames, List<string> memberValueLiterals, string backingType)
        {
            codeBuilder
                .L($"public enum {enumName}")
                .L("{")
                .IndentIncrease();
            for (int counter = 0; counter < memberNames.Count; counter += 1)
            {
                bool last = counter + 1 == memberNames.Count;
                codeBuilder
                    .L($"{memberNames[counter]}({memberValueLiterals[counter]}){(last ? ";" : ",")}");
            }
            codeBuilder
                .EmptyLine()
                .L($"private final {backingType} value;")
                .EmptyLine()
                .L($"{enumName}({backingType} value) {{")
                .IndentIncrease()
                .L("this.value = value;")
                .IndentDecrease()
                .L("}")
                .EmptyLine()
                .L("@JsonValue")
                .L($"public {backingType} getValue() {{")
                .IndentIncrease()
                .L("return value;")
                .IndentDecrease()
                .L("}")
                .IndentDecrease()
                .L("}")
                .EmptyLine();
        }

        #endregion

        #region Generate Comments

        /// <summary>
        /// Generates comments for the parts that need it
        /// </summary>
        /// <param name="codeBuilder"></param>
        /// <param name="key"></param>
        /// <param name="jsonSchemaBuilderPart"></param>
        private void GenerateComments(CodeBuilder codeBuilder, IdentifierString key, IJSBPart jsonSchemaBuilderPart)
        {
            List<string> commentLines = GenerateCommentLines(key, jsonSchemaBuilderPart.Description, minSplitLength: 70, maxSplitLength: 90);

            codeBuilder.L("/**");
            foreach (string commentLine in commentLines)
            {
                codeBuilder.L($" * {commentLine}");
            }
            codeBuilder.L(" */");
        }

        private List<string> GenerateCommentLines(IdentifierString key, string description, int minSplitLength, int maxSplitLength)
        {
            string stringToSplit = $"{key}: {description}";

            return Regex.Split(stringToSplit, @"(.{1," + maxSplitLength.ToString() + @"})(?:\s|$)|(.{" + maxSplitLength.ToString() + @"})")
              .Where(x => x.Length > 0)
              .ToList();
        }

        #endregion

        #region Generate Defaults

        private string GenerateDefaultIfExisting(IdentifierString key, JSBString jsonSchemaBuilderString)
        {
            if (string.IsNullOrWhiteSpace(jsonSchemaBuilderString.DefaultValue))
            {
                return string.Empty;
            }

            //A string part with enums references the matching enum member instead of a string literal
            if (jsonSchemaBuilderString.PartType == JSBPartType.String &&
                jsonSchemaBuilderString.Enums != null && jsonSchemaBuilderString.Enums.Count > 0)
            {
                List<string> rawNames = jsonSchemaBuilderString.Enums.Select(enumValue => TransformToTitleCase(enumValue) ?? string.Empty).ToList();
                List<string> memberNames = MakeEnumMemberNames(rawNames);
                return GenerateEnumDefault(key, rawNames, memberNames, TransformToTitleCase(jsonSchemaBuilderString.DefaultValue) ?? string.Empty);
            }
            return $" = \"{EscapeJavaStringLiteral(jsonSchemaBuilderString.DefaultValue)}\"";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBEmail jsonSchemaBuilderEmail)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderEmail.DefaultValue) ? string.Empty : $" = \"{EscapeJavaStringLiteral(jsonSchemaBuilderEmail.DefaultValue)}\"";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBUri jsonSchemaBuilderUri)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderUri.DefaultValue) ? string.Empty : $" = URI.create(\"{EscapeJavaStringLiteral(jsonSchemaBuilderUri.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBGuid jsonSchemaBuilderGuid)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderGuid.DefaultValue) ? string.Empty : $" = UUID.fromString(\"{EscapeJavaStringLiteral(jsonSchemaBuilderGuid.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBPhoneNumber jsonSchemaBuilderPhoneNumber)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderPhoneNumber.DefaultValue) ? string.Empty : $" = \"{EscapeJavaStringLiteral(jsonSchemaBuilderPhoneNumber.DefaultValue)}\"";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            if (string.IsNullOrWhiteSpace(jsonSchemaBuilderDateTime.DefaultValue))
            {
                return string.Empty;
            }

            if (jsonSchemaBuilderDateTime.Enums != null && jsonSchemaBuilderDateTime.Enums.Count > 0)
            {
                List<string> rawNames = jsonSchemaBuilderDateTime.Enums.Select(enumValue => $"DateTime_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
                return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), $"DateTime_{MakeValidEnumName(jsonSchemaBuilderDateTime.DefaultValue)}");
            }
            return $" = OffsetDateTime.parse(\"{EscapeJavaStringLiteral(jsonSchemaBuilderDateTime.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            if (string.IsNullOrWhiteSpace(jsonSchemaBuilderDate.DefaultValue))
            {
                return string.Empty;
            }

            if (jsonSchemaBuilderDate.Enums != null && jsonSchemaBuilderDate.Enums.Count > 0)
            {
                List<string> rawNames = jsonSchemaBuilderDate.Enums.Select(enumValue => $"Date_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
                return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), $"Date_{MakeValidEnumName(jsonSchemaBuilderDate.DefaultValue)}");
            }
            return $" = LocalDate.parse(\"{EscapeJavaStringLiteral(jsonSchemaBuilderDate.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            if (string.IsNullOrWhiteSpace(jsonSchemaBuilderTime.DefaultValue))
            {
                return string.Empty;
            }

            if (jsonSchemaBuilderTime.Enums != null && jsonSchemaBuilderTime.Enums.Count > 0)
            {
                List<string> rawNames = jsonSchemaBuilderTime.Enums.Select(enumValue => $"Time_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
                return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), $"Time_{MakeValidEnumName(jsonSchemaBuilderTime.DefaultValue)}");
            }
            return $" = OffsetTime.parse(\"{EscapeJavaStringLiteral(jsonSchemaBuilderTime.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            if (jsonSchemaBuilderBoolean.DefaultValue.HasValue)
            {
                if (jsonSchemaBuilderBoolean.Enums != null && jsonSchemaBuilderBoolean.Enums.Count > 0)
                {
                    return GenerateEnumDefault(key,
                        jsonSchemaBuilderBoolean.Enums.Select(enumValue => enumValue.HasValue ? (enumValue.Value ? "True" : "False") : "Null").ToList(),
                        jsonSchemaBuilderBoolean.Enums.Select(enumValue => enumValue.HasValue ? (enumValue.Value ? "True" : "False") : "Null").ToList(),
                        jsonSchemaBuilderBoolean.DefaultValue.Value ? "True" : "False");
                }
                string defaultValue = jsonSchemaBuilderBoolean.DefaultValue.Value ? "Boolean.TRUE" : "Boolean.FALSE";
                return $" = {defaultValue}";
            }
            else
            {
                return string.Empty;
            }
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBInteger jsonSchemaBuilderInteger)
        {
            if (jsonSchemaBuilderInteger.DefaultValue.HasValue)
            {
                if (jsonSchemaBuilderInteger.Enums != null && jsonSchemaBuilderInteger.Enums.Count > 0)
                {
                    List<string> rawNames = jsonSchemaBuilderInteger.Enums.Select(enumValue => enumValue.HasValue ?
                        $"Value{(enumValue.Value < 0 ? $"Minus{(-enumValue.Value).ToString(CultureInfo.InvariantCulture)}" : enumValue.Value.ToString(CultureInfo.InvariantCulture))}" :
                        "ValueNull").ToList();
                    string defaultRawName = jsonSchemaBuilderInteger.DefaultValue.Value < 0 ?
                        $"ValueMinus{(-jsonSchemaBuilderInteger.DefaultValue.Value).ToString(CultureInfo.InvariantCulture)}" :
                        $"Value{jsonSchemaBuilderInteger.DefaultValue.Value.ToString(CultureInfo.InvariantCulture)}";
                    return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), defaultRawName);
                }
                return $" = {jsonSchemaBuilderInteger.DefaultValue.Value.ToString(CultureInfo.InvariantCulture)}L";
            }
            else
            {
                return string.Empty;
            }
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBNumber jsonSchemaBuilderNumber)
        {
            if (jsonSchemaBuilderNumber.DefaultValue.HasValue)
            {
                if (jsonSchemaBuilderNumber.Enums != null && jsonSchemaBuilderNumber.Enums.Count > 0)
                {
                    List<string> rawNames = jsonSchemaBuilderNumber.Enums.Select(enumValue => enumValue.HasValue ?
                        $"Value{enumValue.Value.ToString(CultureInfo.InvariantCulture).Replace(".", "_").Replace("-", "Minus")}" :
                        "ValueNull").ToList();
                    string defaultRawName = $"Value{jsonSchemaBuilderNumber.DefaultValue.Value.ToString(CultureInfo.InvariantCulture).Replace(".", "_").Replace("-", "Minus")}";
                    return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), defaultRawName);
                }
                //The D suffix keeps the literal a double also for whole numbers
                return $" = {jsonSchemaBuilderNumber.DefaultValue.Value.ToString(CultureInfo.InvariantCulture)}D";
            }
            else
            {
                return string.Empty;
            }
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBArray jsonSchemaBuilderArray)
        {
            if (jsonSchemaBuilderArray.DefaultValue != null && jsonSchemaBuilderArray.DefaultValue.Type == JsonValueType.Array)
            {
                List<string> literals = new List<string>();
                foreach (JsonValue item in jsonSchemaBuilderArray.DefaultValue.Array)
                {
                    literals.Add(ConvertJsonValueToJavaLiteral(item));
                }
                return $" = new ArrayList<>(Arrays.asList({string.Join(", ", literals)}))";
            }
            return " = new ArrayList<>()";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBRef jsonSchemaBuilderUriReference)
        {
            if (jsonSchemaBuilderUriReference.DefaultValue != null)
            {
                throw new NotImplementedException($"Default value for Reference is not implemented");
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Builds a default value that references the enum member matching the default value.
        /// The default value of an enum part is required to be one of the enum values.
        /// </summary>
        private string GenerateEnumDefault(IdentifierString key, List<string> rawValues, List<string> memberNames, string defaultRawValue)
        {
            string enumName = TransformToTitleCase(key) + "Enum";
            for (int counter = 0; counter < rawValues.Count; counter += 1)
            {
                if (string.Equals(rawValues[counter], defaultRawValue, StringComparison.Ordinal))
                {
                    return $" = {enumName}.{memberNames[counter]}";
                }
            }
            //Fallback mirrors the CSharp generator: build the member name from the default value
            return $" = {enumName}.{SanitizeJavaIdentifier(defaultRawValue)}";
        }

        #endregion

        #region HelperFunctions

        /// <summary>
        /// Maps the internal name of a registered schema top part to the schema file name,
        /// which is the class name the declaring file emits. Unknown names pass through
        /// unchanged (nested classes, merged AllOf classes and definitions).
        /// </summary>
        private string MapClassName(string partName)
        {
            if (_topPartNameToSchemaName.TryGetValue(partName, out string schemaName))
            {
                return schemaName;
            }
            return partName;
        }

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
        /// Creates a valid enum name from a string value by replacing special characters
        /// </summary>
        /// <param name="value">The value to convert to an enum name</param>
        /// <returns>A valid enum name</returns>
        private string MakeValidEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Unknown";
            }
            return value.Replace("-", "_")
                        .Replace(":", "_")
                        .Replace(".", "_")
                        .Replace(" ", "_")
                        .Replace("T", "_");
        }

        /// <summary>
        /// Java reserved words. Java has no identifier escaping, so colliding field
        /// names get a trailing underscore instead.
        /// </summary>
        private static readonly HashSet<string> JavaKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char", "class", "const",
            "continue", "default", "do", "double", "else", "enum", "extends", "final", "finally", "float",
            "for", "goto", "if", "implements", "import", "instanceof", "int", "interface", "long", "native",
            "new", "package", "private", "protected", "public", "return", "short", "static", "strictfp",
            "super", "switch", "synchronized", "this", "throw", "throws", "transient", "try", "void",
            "volatile", "while", "var", "record", "yield", "sealed", "permits"
        };

        /// <summary>
        /// Builds the Java field name for a property. Reserved words get a trailing
        /// underscore since Java has no identifier escaping.
        /// </summary>
        private string MakeFieldName(string titleCaseName)
        {
            string fieldName = TransformToCamelCase(titleCaseName);
            if (JavaKeywords.Contains(fieldName))
            {
                fieldName = fieldName + "_";
            }
            return fieldName;
        }

        /// <summary>
        /// Builds the package name from the module. Package segments are lowercased,
        /// reserved words get a trailing underscore since Java has no identifier escaping
        /// (the module Default becomes the package default_).
        /// </summary>
        private string MakePackageName(string namespaceName)
        {
            return string.Join(".", namespaceName.Split('.').Select(segment =>
            {
                string lowerSegment = segment.ToLowerInvariant();
                return JavaKeywords.Contains(lowerSegment) ? lowerSegment + "_" : lowerSegment;
            }));
        }

        /// <summary>
        /// Makes a valid Java identifier from a string: invalid characters become underscores
        /// and a leading digit gets an underscore prefix.
        /// </summary>
        private string SanitizeJavaIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }
            string sanitized = Regex.Replace(value, @"[^a-zA-Z0-9_$]", "_");
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }
            return sanitized;
        }

        /// <summary>
        /// Deduplicates enum member names so the emitted enum declaration always compiles.
        /// </summary>
        private List<string> MakeEnumMemberNames(IEnumerable<string> rawNames)
        {
            List<string> memberNames = new List<string>();
            HashSet<string> usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (string rawName in rawNames)
            {
                string sanitized = SanitizeJavaIdentifier(rawName);
                string memberName = sanitized;
                int dedupCounter = 1;
                while (!usedNames.Add(memberName))
                {
                    memberName = $"{sanitized}_{dedupCounter}";
                    dedupCounter += 1;
                }
                memberNames.Add(memberName);
            }
            return memberNames;
        }

        /// <summary>
        /// Escapes backslash and double quote so the value can be used in a Java string literal
        /// </summary>
        private string EscapeJavaStringLiteral(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// Converts a JsonValue to a Java literal for use in an Arrays.asList initializer
        /// </summary>
        private string ConvertJsonValueToJavaLiteral(JsonValue value)
        {
            switch (value.Type)
            {
                case JsonValueType.String:
                    return $"\"{EscapeJavaStringLiteral(value.String)}\"";
                case JsonValueType.Number:
                    return value.Number.ToString(CultureInfo.InvariantCulture);
                case JsonValueType.Boolean:
                    return value.Boolean ? "Boolean.TRUE" : "Boolean.FALSE";
                case JsonValueType.Null:
                    return "null";
                default:
                    return value.ToString();
            }
        }

        #endregion
    }
}
