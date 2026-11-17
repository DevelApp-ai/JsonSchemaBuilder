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
    /// Visual Basic code generator for JSON Schema. Ports the structure of the CSharp
    /// generator, emitting one VB file per registered schema with the same class layout.
    /// VB is case-insensitive and has a reserved word list, so identifiers derived from
    /// the schema are escaped with brackets where needed.
    /// </summary>
    public class VB
    {
        private const string FILE_ENDING = ".vb";

        /// <summary>
        /// Generate fileName and code for Visual Basic
        /// </summary>
        /// <param name="codeGenerator"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            List<(string fileName, string code)> generatedCode = new List<(string fileName, string code)>();
            foreach (var schema in codeGenerator.RegisteredJsonSchemas.Values)
            {
                string fileName = Path.Combine(codeGenerator.ApplicationRoot, schema.Module.ToFilePath, schema.Name + FILE_ENDING);

                VB vb = new VB(codeGenerator, schema.Module);
                generatedCode.Add((fileName, vb.GenerateCode(schema.JsonSchemaBuilderSchema)));
            }
            return generatedCode;
        }

        private NamespaceString _startNameSpace;
        private string _applicationRoot;
        private JSBSchema _rootSchema;
        private CodeGenerator _codeGenerator;
        private HashSet<string> _emittedAllOfClassNames = new HashSet<string>();

        private VB(CodeGenerator codeGenerator, NamespaceString startNameSpace)
        {
            _applicationRoot = codeGenerator.ApplicationRoot;
            _startNameSpace = startNameSpace;
            _codeGenerator = codeGenerator;
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
            GenerateStartOfSchema(codeBuilder, schema);
            if (schema.TopPart != null)
            {
                if (schema.TopPart.PartType == JSBPartType.Object)
                {
                    GenerateCodeForBuilderPart(codeBuilder, schema.TopPart.Name, schema.TopPart, schema.Definitions);
                }
                else
                {
                    List<IJSBPart> properties = new List<IJSBPart>();
                    properties.Add(schema.TopPart);

                    JSBObject encasingObject = new JSBObject(schema.Name, schema.Description, props: properties);
                    GenerateCodeForBuilderPart(codeBuilder, schema.Name, encasingObject, schema.Definitions);
                }
            }
            else
            {
                List<IJSBPart> properties = new List<IJSBPart>();
                JSBObject encasingObject = new JSBObject(schema.Name, schema.Description, props: properties);
                GenerateCodeForBuilderPart(codeBuilder, schema.Name, encasingObject, schema.Definitions);
            }
            GenerateEndOfSchema(codeBuilder, schema);
            return codeBuilder.Build();
        }

        /// <summary>
        /// Generates imports and namespace start
        /// </summary>
        /// <param name="codeBuilder"></param>
        /// <param name="schema"></param>
        private void GenerateStartOfSchema(CodeBuilder codeBuilder, JSBSchema schema)
        {
            //TODO run through schema children to get all references to make sure they are included
            codeBuilder
                .L("Imports System")
                .L("Imports Newtonsoft.Json")
                .L("Imports Newtonsoft.Json.Converters")
                .L("Imports System.Collections.Generic")
                .L("Imports DevelApp.JsonSchemaBuilder.DataTypes")
                .EmptyLine()
                .L($"Namespace {EscapeNamespace(_startNameSpace.ToString())}")
                .IndentIncrease();
        }

        private void GenerateEndOfSchema(CodeBuilder codeBuilder, JSBSchema schema)
        {
            codeBuilder
                .IndentDecrease()
                .L("End Namespace");
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
                    throw new NotImplementedException($"VB code generation for PartType {value.PartType} has not been implemented");
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
            GenerateComments(codeBuilder, key, jsonSchemaBuilderArray);

            if (jsonSchemaBuilderArray.Items.Count == 1)
            {
                codeBuilder
                    .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                    .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As List(Of {MakeCorrectItemType(jsonSchemaBuilderArray.Items[0])}){GenerateDefaultIfExisting(key, jsonSchemaBuilderArray)}")
                    .EmptyLine();
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
                    return $"List(Of {MakeCorrectItemType((jsonSchemaBuilderPart as JSBArray).Items.First())})";
                case JSBPartType.Object:
                    return EscapeIdentifier(jsonSchemaBuilderPart.Name);
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
                        return EscapeIdentifier(jsonSchemaBuilderPart.Name + "Enum");
                    }
                    return "String";
                case JSBPartType.Date:
                case JSBPartType.DateTime:
                case JSBPartType.Time:
                    return "Date";
                case JSBPartType.Email:
                    return "Email";
                case JSBPartType.DataString:
                    JSBString dataStringItem = jsonSchemaBuilderPart as JSBString;
                    if (dataStringItem.Enums.Count > 0)
                    {
                        return EscapeIdentifier(jsonSchemaBuilderPart.Name + "Enum");
                    }
                    return "DataString";
                case JSBPartType.Base64String:
                    return "String";
                case JSBPartType.Uri:
                    return "Uri";
                case JSBPartType.Guid:
                    return "Guid";
                case JSBPartType.PhoneNumber:
                    return "PhoneNumber";
                case JSBPartType.AllOf:
                    //The AllOf part is merged into one class through conversion
                    return EscapeIdentifier(jsonSchemaBuilderPart.Name);
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
            GenerateComments(codeBuilder, key, jsonSchemaBuilderBoolean);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Boolean{BuildRequired(jsonSchemaBuilderBoolean.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderBoolean)}")
                .EmptyLine();
        }

        private void GenerateEnumBoolean(CodeBuilder codeBuilder, IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            //VB reserved words are escaped with brackets so the member names stay valid
            List<string> memberNames = new List<string> { "True", "False", "Null" };
            List<long> memberValues = new List<long> { 1, 0, 2 };
            codeBuilder
                .L($"Public Enum {enumName}")
                .IndentIncrease();
            for (int counter = 0; counter < jsonSchemaBuilderBoolean.Enums.Count; counter += 1)
            {
                string enumNameForMember = jsonSchemaBuilderBoolean.Enums[counter].HasValue ?
                    (jsonSchemaBuilderBoolean.Enums[counter].Value ? "True" : "False") : "Null";
                codeBuilder
                    .L($"{EscapeIdentifier(enumNameForMember)} = {memberValues[memberNames.IndexOf(enumNameForMember)]}");
            }
            codeBuilder
                .IndentDecrease()
                .L("End Enum")
                .EmptyLine();

            GenerateComments(codeBuilder, key, jsonSchemaBuilderBoolean);
            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {enumName}{BuildRequired(jsonSchemaBuilderBoolean.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderBoolean)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Date

        private void GenerateOrdinaryDate(CodeBuilder codeBuilder, IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderDate);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Date{BuildRequired(jsonSchemaBuilderDate.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDate)}")
                .EmptyLine();
        }

        private void GenerateEnumDate(CodeBuilder codeBuilder, IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            //Create valid enum member names from the date strings (e.g. "2020-01-15" -> "Date_2020_01_15")
            List<string> rawNames = jsonSchemaBuilderDate.Enums.Select(enumValue => $"Date_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);

            codeBuilder
                .L($"Public Enum {enumName}")
                .IndentIncrease();
            for (int counter = 0; counter < memberNames.Count; counter += 1)
            {
                codeBuilder
                    .L($"{EscapeIdentifier(memberNames[counter])} = {counter}");
            }
            codeBuilder
                .IndentDecrease()
                .L("End Enum")
                .EmptyLine();

            GenerateComments(codeBuilder, key, jsonSchemaBuilderDate);
            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {enumName}{BuildRequired(jsonSchemaBuilderDate.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDate)}")
                .EmptyLine();
        }

        #endregion

        #region Generate DateTime

        private void GenerateOrdinaryDateTime(CodeBuilder codeBuilder, IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderDateTime);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Date{BuildRequired(jsonSchemaBuilderDateTime.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDateTime)}")
                .EmptyLine();
        }

        private void GenerateEnumDateTime(CodeBuilder codeBuilder, IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            //Create valid enum member names from the datetime strings
            List<string> rawNames = jsonSchemaBuilderDateTime.Enums.Select(enumValue => $"DateTime_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);

            codeBuilder
                .L($"Public Enum {enumName}")
                .IndentIncrease();
            for (int counter = 0; counter < memberNames.Count; counter += 1)
            {
                codeBuilder
                    .L($"{EscapeIdentifier(memberNames[counter])} = {counter}");
            }
            codeBuilder
                .IndentDecrease()
                .L("End Enum")
                .EmptyLine();

            GenerateComments(codeBuilder, key, jsonSchemaBuilderDateTime);
            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {enumName}{BuildRequired(jsonSchemaBuilderDateTime.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDateTime)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Email

        private void GenerateEnumEmail(CodeBuilder codeBuilder, IdentifierString key, JSBEmail jsonSchemaBuilderEmail)
        {
            throw new NotImplementedException("Enum on email has not been implemented");
        }

        private void GenerateOrdinaryEmail(CodeBuilder codeBuilder, IdentifierString key, JSBEmail jsonSchemaBuilderEmail)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderEmail);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Email{GenerateDefaultIfExisting(key, jsonSchemaBuilderEmail)}")
                .EmptyLine();
        }

        #endregion

        #region Generate DataString

        private void GenerateOrdinaryDataString(CodeBuilder codeBuilder, IdentifierString key, JSBDataString jsonSchemaBuilderDataString)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderDataString);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As DataString{GenerateDefaultIfExisting(key, jsonSchemaBuilderDataString)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Integer

        private void GenerateOrdinaryInteger(CodeBuilder codeBuilder, IdentifierString key, JSBInteger jsonSchemaBuilderInteger)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderInteger);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Long{BuildRequired(jsonSchemaBuilderInteger.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderInteger)}")
                .EmptyLine();
        }

        private void GenerateEnumInteger(CodeBuilder codeBuilder, IdentifierString key, JSBInteger jsonSchemaBuilderInteger)
        {
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            //Negative values use a Minus prefix so the member name stays a valid identifier
            List<string> rawNames = jsonSchemaBuilderInteger.Enums.Select(enumValue => enumValue.HasValue ?
                $"Value{(enumValue.Value < 0 ? $"Minus{(-enumValue.Value).ToString(CultureInfo.InvariantCulture)}" : enumValue.Value.ToString(CultureInfo.InvariantCulture))}" :
                "ValueNull").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);

            codeBuilder
                .L($"Public Enum {enumName}")
                .IndentIncrease();
            for (int counter = 0; counter < memberNames.Count; counter += 1)
            {
                codeBuilder
                    .L($"{EscapeIdentifier(memberNames[counter])} = {jsonSchemaBuilderInteger.Enums[counter]?.ToString(CultureInfo.InvariantCulture) ?? "0"}");
            }
            codeBuilder
                .IndentDecrease()
                .L("End Enum")
                .EmptyLine();

            GenerateComments(codeBuilder, key, jsonSchemaBuilderInteger);
            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {enumName}{BuildRequired(jsonSchemaBuilderInteger.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderInteger)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Number

        private void GenerateOrdinaryNumber(CodeBuilder codeBuilder, IdentifierString key, JSBNumber jsonSchemaBuilderNumber)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderNumber);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Double{BuildRequired(jsonSchemaBuilderNumber.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderNumber)}")
                .EmptyLine();
        }

        private void GenerateEnumNumber(CodeBuilder codeBuilder, IdentifierString key, JSBNumber jsonSchemaBuilderNumber)
        {
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            //Use the counter as the enum value to avoid collisions, matching the CSharp generator
            List<string> rawNames = jsonSchemaBuilderNumber.Enums.Select(enumValue => enumValue.HasValue ?
                $"Value{enumValue.Value.ToString(CultureInfo.InvariantCulture).Replace(".", "_").Replace("-", "Minus")}" :
                "ValueNull").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);

            codeBuilder
                .L($"Public Enum {enumName} As Long")
                .IndentIncrease();
            for (int counter = 0; counter < memberNames.Count; counter += 1)
            {
                codeBuilder
                    .L($"{EscapeIdentifier(memberNames[counter])} = {counter}");
            }
            codeBuilder
                .IndentDecrease()
                .L("End Enum")
                .EmptyLine();

            GenerateComments(codeBuilder, key, jsonSchemaBuilderNumber);
            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {enumName}{BuildRequired(jsonSchemaBuilderNumber.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderNumber)}")
                .EmptyLine();
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

            string className = EscapeIdentifier(TransformToTitleCase(key));
            codeBuilder
                .L($"Partial Public Class {className}")
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
                .L("End Class");

            if (definitions == null && !parentIsArray)// Assume not top part
            {
                codeBuilder
                    .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                    .L($"Public Property {className}Prop As {className}")
                    .EmptyLine();
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

            string mergedClassName = EscapeIdentifier(TransformToTitleCase(key));
            //The same AllOf part can be referenced both as a property and as an array
            //item in one schema. The merged class is only emitted once per file,
            //otherwise the generated code would contain duplicate class declarations.
            bool mergedClassAlreadyEmitted = !_emittedAllOfClassNames.Add(TransformToTitleCase(key));

            if (!mergedClassAlreadyEmitted)
            {
                codeBuilder
                    .L($"Partial Public Class {mergedClassName}")
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
                    .L("End Class");
            }

            if (!parentIsArray)
            {
                codeBuilder
                    .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                    .L($"Public Property {mergedClassName}Prop As {mergedClassName}")
                    .EmptyLine();
            }
        }

        /// <summary>
        /// Generates a composition part (AnyOf or OneOf) as an object property since VB has no union type.
        /// </summary>
        private void GenerateOrdinaryCompositionChoice(CodeBuilder codeBuilder, IdentifierString key, IJSBPart jsonSchemaBuilderCompositionChoice)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderCompositionChoice);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Object")
                .EmptyLine();
        }

        #endregion

        #region Generate String

        private void GenerateEnumString(CodeBuilder codeBuilder, IdentifierString key, JSBString jsonSchemaBuilderString, bool isDefinition)
        {
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            List<string> rawNames = jsonSchemaBuilderString.Enums.Select(enumValue => TransformToTitleCase(enumValue) ?? string.Empty).ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);

            codeBuilder
                .L($"Public Enum {enumName}")
                .IndentIncrease();
            foreach (string memberName in memberNames)
            {
                codeBuilder
                    .L(EscapeIdentifier(memberName));
            }
            codeBuilder
                .IndentDecrease()
                .L("End Enum")
                .EmptyLine();
            if (!isDefinition)
            {
                GenerateComments(codeBuilder, key, jsonSchemaBuilderString);
                codeBuilder
                    .L($"<JsonProperty(\"{TransformToCamelCase(key)}\"), JsonConverter(GetType(StringEnumConverter))>")
                    .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {enumName}{GenerateDefaultIfExisting(key, jsonSchemaBuilderString)}")
                    .EmptyLine();
            }
        }

        private void GenerateOrdinaryString(CodeBuilder codeBuilder, IdentifierString key, JSBString jsonSchemaBuilderString)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderString);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As String{GenerateDefaultIfExisting(key, jsonSchemaBuilderString)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Base64String

        private void GenerateOrdinaryBase64String(CodeBuilder codeBuilder, IdentifierString key, JSBBase64String jsonSchemaBuilderBase64String)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderBase64String);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As String{GenerateDefaultIfExisting(key, jsonSchemaBuilderBase64String)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Uri

        private void GenerateOrdinaryUri(CodeBuilder codeBuilder, IdentifierString key, JSBUri jsonSchemaBuilderUri)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderUri);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Uri{GenerateDefaultIfExisting(key, jsonSchemaBuilderUri)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Guid

        private void GenerateOrdinaryGuid(CodeBuilder codeBuilder, IdentifierString key, JSBGuid jsonSchemaBuilderGuid)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderGuid);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Guid{GenerateDefaultIfExisting(key, jsonSchemaBuilderGuid)}")
                .EmptyLine();
        }

        #endregion

        #region Generate PhoneNumber

        private void GenerateOrdinaryPhoneNumber(CodeBuilder codeBuilder, IdentifierString key, JSBPhoneNumber jsonSchemaBuilderPhoneNumber)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderPhoneNumber);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As PhoneNumber{GenerateDefaultIfExisting(key, jsonSchemaBuilderPhoneNumber)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Time

        private void GenerateEnumTime(CodeBuilder codeBuilder, IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            //Create valid enum member names from the time strings
            List<string> rawNames = jsonSchemaBuilderTime.Enums.Select(enumValue => $"Time_{MakeValidEnumName(enumValue ?? "Unknown")}").ToList();
            List<string> memberNames = MakeEnumMemberNames(rawNames);

            codeBuilder
                .L($"Public Enum {enumName}")
                .IndentIncrease();
            for (int counter = 0; counter < memberNames.Count; counter += 1)
            {
                codeBuilder
                    .L($"{EscapeIdentifier(memberNames[counter])} = {counter}");
            }
            codeBuilder
                .IndentDecrease()
                .L("End Enum")
                .EmptyLine();

            GenerateComments(codeBuilder, key, jsonSchemaBuilderTime);
            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {enumName}{BuildRequired(jsonSchemaBuilderTime.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderTime)}")
                .EmptyLine();
        }

        private void GenerateOrdinaryTime(CodeBuilder codeBuilder, IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderTime);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As Date{BuildRequired(jsonSchemaBuilderTime.IsRequired)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderTime)}")
                .EmptyLine();
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
                    throw new CodeGenerationException($"LookupReferencedPart failed. Might be because Uri reference is not supported other than /definitions/ or entire schema");
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
            GenerateComments(codeBuilder, key, jsonSchemaBuilderUriReference);

            codeBuilder
                .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {MakeCorrectItemType(referencedPart)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderUriReference)}")
                .EmptyLine();
        }

        private void GenerateCodeFromSchema(CodeBuilder codeBuilder, JsonValue jsonValueOfSchema, IdentifierString key, JSBRef jsonSchemaBuilderUriReference)
        {
            if (jsonValueOfSchema.Type == JsonValueType.Object)
            {

                GenerateComments(codeBuilder, key, jsonSchemaBuilderUriReference);

                codeBuilder
                    .L($"<JsonProperty(\"{TransformToCamelCase(key)}\")>")
                    .L($"Public Property {EscapeIdentifier(TransformToTitleCase(key))} As {MakeCorrectItemType(jsonValueOfSchema)}{GenerateDefaultIfExisting(key, jsonSchemaBuilderUriReference)}")
                    .EmptyLine();
            }
            else
            {
                throw new CodeGenerationException($"GenerateOrdinaryUriReference {key} references an invalid schema {jsonSchemaBuilderUriReference.IriReference.OriginalString}");
            }
        }

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
                throw new CodeGenerationException($"GenerateOrdinaryUriReference has neither title nor id");
            }
            string type = string.Empty;
            if (jsonObjectOfSchema.TryGetValue("type", out JsonValue jsonValueType))
            {
                type = jsonValueType.String;
            }
            else
            {
                throw new CodeGenerationException($"GenerateOrdinaryUriReference has no type");
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
                        case "date":
                        case "datetime":
                            return "Date";
                        case "email":
                            return "Email";
                    }
                    break;
                case "boolean":
                    return "Boolean";
                case "object":
                    return EscapeIdentifier(TransformToTitleCase(name));
                case "number":
                    return "Double";
                case "integer":
                    return "Long";
            }
            throw new NotImplementedException($"Type {type} with name {name} and format {format} is not supported");
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

            codeBuilder.L("''' <summary>");
            foreach (string commentLine in commentLines)
            {
                codeBuilder.L($"''' {commentLine}");
            }
            codeBuilder.L("''' </summary>");
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
                List<string> memberNames = MakeEnumMemberNames(jsonSchemaBuilderString.Enums.Select(enumValue => TransformToTitleCase(enumValue) ?? string.Empty));
                return GenerateEnumDefault(key, jsonSchemaBuilderString.Enums.ToList(), memberNames, jsonSchemaBuilderString.DefaultValue);
            }
            return $" = \"{EscapeVbStringLiteral(jsonSchemaBuilderString.DefaultValue)}\"";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBEmail jsonSchemaBuilderEmail)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderEmail.DefaultValue) ? string.Empty : $" = New Email(\"{EscapeVbStringLiteral(jsonSchemaBuilderEmail.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBDataString jsonSchemaBuilderDataString)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderDataString.DefaultValue) ? string.Empty : $" = New DataString(\"{EscapeVbStringLiteral(jsonSchemaBuilderDataString.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBUri jsonSchemaBuilderUri)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderUri.DefaultValue) ? string.Empty : $" = New Uri(\"{EscapeVbStringLiteral(jsonSchemaBuilderUri.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBGuid jsonSchemaBuilderGuid)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderGuid.DefaultValue) ? string.Empty : $" = New Guid(\"{EscapeVbStringLiteral(jsonSchemaBuilderGuid.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBPhoneNumber jsonSchemaBuilderPhoneNumber)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderPhoneNumber.DefaultValue) ? string.Empty : $" = New PhoneNumber(\"{EscapeVbStringLiteral(jsonSchemaBuilderPhoneNumber.DefaultValue)}\")";
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
                return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), $"DateTime_{jsonSchemaBuilderDateTime.DefaultValue.Replace("-", "_").Replace(":", "_").Replace(".", "_").Replace(" ", "_").Replace("T", "_")}");
            }
            return $" = DateTime.Parse(\"{EscapeVbStringLiteral(jsonSchemaBuilderDateTime.DefaultValue)}\")";
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
                return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), $"Date_{jsonSchemaBuilderDate.DefaultValue.Replace("-", "_").Replace(":", "_").Replace(".", "_")}");
            }
            return $" = DateTime.Parse(\"{EscapeVbStringLiteral(jsonSchemaBuilderDate.DefaultValue)}\")";
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
                return GenerateEnumDefault(key, rawNames, MakeEnumMemberNames(rawNames), $"Time_{jsonSchemaBuilderTime.DefaultValue.Replace(":", "_").Replace(".", "_")}");
            }
            return $" = DateTime.Parse(\"{EscapeVbStringLiteral(jsonSchemaBuilderTime.DefaultValue)}\")";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            if (jsonSchemaBuilderBoolean.DefaultValue.HasValue)
            {
                if (jsonSchemaBuilderBoolean.Enums != null && jsonSchemaBuilderBoolean.Enums.Count > 0)
                {
                    string enumNameForMember = jsonSchemaBuilderBoolean.DefaultValue.Value ? "True" : "False";
                    return $" = {EscapeIdentifier(TransformToTitleCase(key) + "Enum")}.{EscapeIdentifier(enumNameForMember)}";
                }
                string defaultValue = jsonSchemaBuilderBoolean.DefaultValue.Value ? "True" : "False";
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
                    string rawName = jsonSchemaBuilderInteger.DefaultValue.Value < 0 ?
                        $"ValueMinus{(-jsonSchemaBuilderInteger.DefaultValue.Value).ToString(CultureInfo.InvariantCulture)}" :
                        $"Value{jsonSchemaBuilderInteger.DefaultValue.Value.ToString(CultureInfo.InvariantCulture)}";
                    return $" = {EscapeIdentifier(TransformToTitleCase(key) + "Enum")}.{EscapeIdentifier(SanitizeVbIdentifier(rawName))}";
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
                    string rawName = $"Value{jsonSchemaBuilderNumber.DefaultValue.Value.ToString(CultureInfo.InvariantCulture).Replace(".", "_").Replace("-", "Minus")}";
                    return $" = {EscapeIdentifier(TransformToTitleCase(key) + "Enum")}.{EscapeIdentifier(SanitizeVbIdentifier(rawName))}";
                }
                return $" = {jsonSchemaBuilderNumber.DefaultValue.Value.ToString(CultureInfo.InvariantCulture)}";
            }
            else
            {
                return string.Empty;
            }
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBArray jsonSchemaBuilderArray)
        {
            string itemType = MakeCorrectItemType(jsonSchemaBuilderArray.Items[0]);
            if (jsonSchemaBuilderArray.DefaultValue == null)
            {
                return $" = New List(Of {itemType})()";
            }
            if (jsonSchemaBuilderArray.DefaultValue.Type == JsonValueType.Array)
            {
                List<string> literals = new List<string>();
                foreach (JsonValue item in jsonSchemaBuilderArray.DefaultValue.Array)
                {
                    literals.Add(ConvertJsonValueToVbLiteral(item));
                }
                return $" = New List(Of {itemType}) From {{{string.Join(", ", literals)}}}";
            }
            return $" = New List(Of {itemType})()";
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
            string enumName = EscapeIdentifier(TransformToTitleCase(key) + "Enum");
            for (int counter = 0; counter < rawValues.Count; counter += 1)
            {
                if (string.Equals(rawValues[counter], defaultRawValue, StringComparison.Ordinal))
                {
                    return $" = {enumName}.{EscapeIdentifier(memberNames[counter])}";
                }
            }
            //Fallback mirrors the CSharp generator: build the member name from the default value
            return $" = {enumName}.{EscapeIdentifier(SanitizeVbIdentifier(defaultRawValue))}";
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
        /// Returns ? if required to build Nullable valuetypes if not required
        /// </summary>
        /// <param name="isRequired"></param>
        /// <returns></returns>
        private string BuildRequired(bool isRequired)
        {
            return isRequired ? string.Empty : "?";
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
        /// VB reserved words. Identifiers that collide are escaped with brackets.
        /// </summary>
        private static readonly HashSet<string> VbKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "AddHandler", "AddressOf", "Alias", "And", "AndAlso", "As", "Boolean", "ByRef", "Byte", "ByVal",
            "Call", "Case", "Catch", "CBool", "CByte", "CChar", "CDate", "CDbl", "CDec", "Char", "CInt", "CLng", "CObj",
            "Const", "Continue", "CSByte", "CShort", "CSng", "CStr", "CType", "CUInt", "CULng", "CUShort",
            "Date", "Decimal", "Declare", "Default", "Delegate", "Dim", "DirectCast", "Do", "Double",
            "Each", "Else", "ElseIf", "End", "Enum", "Erase", "Error", "Event", "Exit", "False",
            "Finally", "For", "Friend", "Function", "Get", "GetType", "GetXMLNamespace", "Global", "GoSub", "GoTo",
            "Handles", "If", "Implements", "Imports", "In", "Inherits", "Integer", "Interface", "Is", "IsNot",
            "Let", "Lib", "Like", "Long", "Loop", "Me", "Mod", "Module", "MustInherit", "MustOverride",
            "MyBase", "MyClass", "Namespace", "Narrowing", "New", "Next", "Not", "Nothing", "NotInheritable", "NotOverridable",
            "Object", "Of", "On", "Operator", "Option", "Optional", "Or", "OrElse", "Overloads", "Overridable",
            "Overrides", "ParamArray", "Partial", "Private", "Property", "Protected", "Public", "RaiseEvent", "ReadOnly", "ReDim",
            "Rem", "RemoveHandler", "Resume", "Return", "SByte", "Select", "Set", "Shadows", "Shared", "Short",
            "Single", "Static", "Step", "Stop", "String", "Structure", "Sub", "SyncLock", "Then", "Throw",
            "To", "True", "Try", "TryCast", "TypeOf", "UInteger", "ULong", "UShort", "Using", "Wend",
            "When", "While", "Widening", "With", "WithEvents", "WriteOnly", "Xor"
        };

        /// <summary>
        /// Escapes an identifier with brackets when it collides with a VB reserved word
        /// </summary>
        private string EscapeIdentifier(string identifier)
        {
            if (!string.IsNullOrWhiteSpace(identifier) && VbKeywords.Contains(identifier))
            {
                return $"[{identifier}]";
            }
            return identifier;
        }

        /// <summary>
        /// Escapes the segments of a dotted namespace with brackets when they collide with VB reserved words
        /// </summary>
        private string EscapeNamespace(string namespaceName)
        {
            return string.Join(".", namespaceName.Split('.').Select(segment => EscapeIdentifier(segment)));
        }

        /// <summary>
        /// Makes a valid VB identifier from a string: invalid characters become underscores
        /// and a leading digit gets an underscore prefix.
        /// </summary>
        private string SanitizeVbIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }
            string sanitized = Regex.Replace(value, @"[^a-zA-Z0-9_]", "_");
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }
            return sanitized;
        }

        /// <summary>
        /// VB is case-insensitive, so enum member names are deduplicated case-insensitively
        /// to guarantee that the emitted enum declaration compiles.
        /// </summary>
        private List<string> MakeEnumMemberNames(IEnumerable<string> rawNames)
        {
            List<string> memberNames = new List<string>();
            HashSet<string> usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawName in rawNames)
            {
                string sanitized = SanitizeVbIdentifier(rawName);
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
        /// Doubles the double quotes so the value can be used in a VB string literal
        /// </summary>
        private string EscapeVbStringLiteral(string value)
        {
            return value.Replace("\"", "\"\"");
        }

        /// <summary>
        /// Converts a JsonValue to a VB literal for use in a collection initializer
        /// </summary>
        private string ConvertJsonValueToVbLiteral(JsonValue value)
        {
            switch (value.Type)
            {
                case JsonValueType.String:
                    return $"\"{EscapeVbStringLiteral(value.String)}\"";
                case JsonValueType.Number:
                    return value.Number.ToString(CultureInfo.InvariantCulture);
                case JsonValueType.Boolean:
                    return value.Boolean ? "True" : "False";
                case JsonValueType.Null:
                    return "Nothing";
                default:
                    return value.ToString();
            }
        }

        #endregion
    }
}
