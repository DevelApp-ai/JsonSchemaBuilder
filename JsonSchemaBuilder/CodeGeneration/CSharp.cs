using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Manatee.Json;
using Manatee.Json.Schema;
using Manatee.Json.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    public class CSharp
    {
        private const string FILE_ENDING = ".cs";

        /// <summary>
        /// Generate fileName and code
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            List<(string fileName, string code)> generatedCode = new List<(string fileName, string code)>();
            foreach (var schema in codeGenerator.RegisteredJsonSchemas.Values)
            {
                string fileName = Path.Combine(codeGenerator.ApplicationRoot, schema.Module.ToFilePath, schema.Name + FILE_ENDING);

                CSharp cSharp = new CSharp(codeGenerator, schema.Module);
                generatedCode.Add((fileName, cSharp.GenerateCode(schema.JsonSchemaBuilderSchema)));
            }
            return generatedCode;
        }

        private NamespaceString _startNameSpace;
        private string _applicationRoot;
        private JSBSchema _rootSchema;
        private Dictionary<string, IJsonSchemaDefinition> _registeredSchemas;
        private CodeGenerator _codeGenerator;

        private CSharp(CodeGenerator codeGenerator, NamespaceString startNameSpace)
        {
            _applicationRoot = codeGenerator.ApplicationRoot;
            _startNameSpace = startNameSpace;
            _registeredSchemas = codeGenerator.RegisteredJsonSchemas;
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
        /// Generates using parameters and initial class specification
        /// </summary>
        /// <param name="codeBuilder"></param>
        /// <param name="schema"></param>
        private void GenerateStartOfSchema(CodeBuilder codeBuilder, JSBSchema schema)
        {
            //TODO run through schema children to get all references to make sure they are included
            codeBuilder
                .L("using System;")
                .L("using Newtonsoft.Json;")
                .L("using Newtonsoft.Json.Converters;")
                .L("using Ardalis.SmartEnum;")
                .L("using System.Collections.Generic;")
                .L("using System.Net.Mail;")
                .L("using DevelApp.JsonSchemaBuilder.DataTypes;")
                .EmptyLine()
                .L($"namespace {_startNameSpace}")
                .L("{")
                .IndentIncrease();
        }

        private void GenerateEndOfSchema(CodeBuilder codeBuilder, JSBSchema schema)
        {
            codeBuilder
                .IndentDecrease()
                .L("}");
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
                            GenerateOrdinaryBoolaen(codeBuilder, key, jsonSchemaBuilderBoolean);
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
                default:
                    codeBuilder.L($"throw new NotImplementedException(\"PartType {value.PartType} is not implemented\");");
                    break;
            }
        }

        #region Generate Array

        private void GenerateEnumArray(CodeBuilder codeBuilder, IdentifierString key, JSBArray jsonSchemaBuilderArray)
        {
            throw new NotImplementedException("Enum for array has not been implemented");
        }

        private void GenerateOrdinaryArray(CodeBuilder codeBuilder, IdentifierString key, JSBArray jsonSchemaBuilderArray)
        {
            foreach(IJSBPart part in jsonSchemaBuilderArray.Items)
            {
                GenerateCodeForBuilderPart(codeBuilder, part.Name, part, parentIsArray: true);
            }
            GenerateComments(codeBuilder, key, jsonSchemaBuilderArray);

            if (jsonSchemaBuilderArray.Items.Count == 1)
            {
                codeBuilder
                    .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                    .L($"public List<{MakeCorrectItemType(jsonSchemaBuilderArray.Items[0])}> {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderArray)}")
                    .EmptyLine();
            }
            else
            {
                throw new NotImplementedException("Array with more than one item is not implemented");
            }
        }

        private string MakeCorrectItemType(IJSBPart jsonSchemaBuilderPart)
        {
            switch(jsonSchemaBuilderPart.PartType)
            {
                case JSBPartType.Array:
                    //TODO support multiple items
                    return $"List<{MakeCorrectItemType((jsonSchemaBuilderPart as JSBArray).Items.First())}>";
                case JSBPartType.Object:
                    return jsonSchemaBuilderPart.Name;
                case JSBPartType.Integer:
                    return "long";
                case JSBPartType.Number:
                    return "double";
                case JSBPartType.Boolean:
                    return "bool";
                case JSBPartType.String:
                    JSBString jSBString = jsonSchemaBuilderPart as JSBString;
                    if(jSBString.Enums.Count > 0)
                    {
                        return jsonSchemaBuilderPart.Name + "Enum";
                    }
                    return "string";
                case JSBPartType.Date:
                    return "DateTime";
                case JSBPartType.DateTime:
                    return "DateTime";
                case JSBPartType.Email:
                    return "Email";
                case JSBPartType.Time:
                    return "DateTime";
                case JSBPartType.Base64String:
                    return "string";
                case JSBPartType.Uri:
                    return "Uri";
                case JSBPartType.Guid:
                    return "Guid";
                case JSBPartType.PhoneNumber:
                    return "PhoneNumber";
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
                    var pair = LookupReferencedPart(jsbRef);
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
            string relativeLocalFileWithExpandedDot =  ExpandRelativelocalFile(jSBRef.RelativeLocalFile);
            if(string.IsNullOrWhiteSpace(relativeLocalFileWithExpandedDot))
            {
                relativeLocalFileWithExpandedDot = Path.Combine(_startNameSpace.ToFilePath,_rootSchema.Name + ".schema.json");
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

        private void GenerateOrdinaryBoolaen(CodeBuilder codeBuilder, IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderBoolean);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public bool{BuildRequired(jsonSchemaBuilderBoolean.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderBoolean)}")
                .EmptyLine();
        }

        private void GenerateEnumBoolean(CodeBuilder codeBuilder, IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            codeBuilder
                .L($"public enum {TransformToTitleCase(key)}Enum")
                .L("{")
                .IndentIncrease();
            for(int counter = 0; counter < jsonSchemaBuilderBoolean.Enums.Count; counter += 1)
            {
                bool last = counter + 1 == jsonSchemaBuilderBoolean.Enums.Count;
                string enumName = jsonSchemaBuilderBoolean.Enums[counter].HasValue ? 
                    (jsonSchemaBuilderBoolean.Enums[counter].Value ? "True" : "False") : "Null";
                string enumValue = jsonSchemaBuilderBoolean.Enums[counter].HasValue ?
                    (jsonSchemaBuilderBoolean.Enums[counter].Value ? "1" : "0") : "2";
                string enumString = $"{enumName} = {enumValue}{(last ? "" : ",")}";
                codeBuilder
                    .L(enumString);
            }
            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
            
            GenerateComments(codeBuilder, key, jsonSchemaBuilderBoolean);
            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public {TransformToTitleCase(key)}Enum{BuildRequired(jsonSchemaBuilderBoolean.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderBoolean)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Date

        private void GenerateOrdinaryDate(CodeBuilder codeBuilder, IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderDate);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public DateTime{BuildRequired(jsonSchemaBuilderDate.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDate)}")
                .EmptyLine();
        }

        private void GenerateEnumDate(CodeBuilder codeBuilder, IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            codeBuilder
                .L($"public enum {TransformToTitleCase(key)}Enum")
                .L("{")
                .IndentIncrease();
            for(int counter = 0; counter < jsonSchemaBuilderDate.Enums.Count; counter += 1)
            {
                bool last = counter + 1 == jsonSchemaBuilderDate.Enums.Count;
                string dateValue = jsonSchemaBuilderDate.Enums[counter] ?? "Unknown";
                // Create valid enum name from date string (e.g., "2020-01-15" -> "Date_2020_01_15")
                string enumName = $"Date_{MakeValidEnumName(dateValue)}";
                string enumString = $"{enumName} = {counter}{(last ? "" : ",")}";
                codeBuilder
                    .L(enumString);
            }
            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
            
            GenerateComments(codeBuilder, key, jsonSchemaBuilderDate);
            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public {TransformToTitleCase(key)}Enum{BuildRequired(jsonSchemaBuilderDate.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDate)}")
                .EmptyLine();
        }

        #endregion

        #region Generate DateTime

        private void GenerateOrdinaryDateTime(CodeBuilder codeBuilder, IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderDateTime);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public DateTime{BuildRequired(jsonSchemaBuilderDateTime.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDateTime)}")
                .EmptyLine();
        }

        private void GenerateEnumDateTime(CodeBuilder codeBuilder, IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            codeBuilder
                .L($"public enum {TransformToTitleCase(key)}Enum")
                .L("{")
                .IndentIncrease();
            for(int counter = 0; counter < jsonSchemaBuilderDateTime.Enums.Count; counter += 1)
            {
                bool last = counter + 1 == jsonSchemaBuilderDateTime.Enums.Count;
                string dateTimeValue = jsonSchemaBuilderDateTime.Enums[counter] ?? "Unknown";
                // Create valid enum name from datetime string
                string enumName = $"DateTime_{MakeValidEnumName(dateTimeValue)}";
                string enumString = $"{enumName} = {counter}{(last ? "" : ",")}";
                codeBuilder
                    .L(enumString);
            }
            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
            
            GenerateComments(codeBuilder, key, jsonSchemaBuilderDateTime);
            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public {TransformToTitleCase(key)}Enum{BuildRequired(jsonSchemaBuilderDateTime.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderDateTime)}")
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
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public Email {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderEmail)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Integer

        private void GenerateOrdinaryInteger(CodeBuilder codeBuilder, IdentifierString key, JSBInteger jsonSchemaBuilderInteger)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderInteger);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public long{BuildRequired(jsonSchemaBuilderInteger.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderInteger)}")
                .EmptyLine();
        }

        private void GenerateEnumInteger(CodeBuilder codeBuilder, IdentifierString key, JSBInteger jsonSchemaBuilderInteger)
        {
            codeBuilder
                .L($"public enum {TransformToTitleCase(key)}Enum")
                .L("{")
                .IndentIncrease();
            for(int counter = 0; counter < jsonSchemaBuilderInteger.Enums.Count; counter += 1)
            {
                bool last = counter + 1 == jsonSchemaBuilderInteger.Enums.Count;
                string enumName = $"Value{jsonSchemaBuilderInteger.Enums[counter]?.ToString() ?? "Null"}";
                string enumString = $"{enumName} = {jsonSchemaBuilderInteger.Enums[counter]?.ToString() ?? "0"}{(last ? "" : ",")}";
                codeBuilder
                    .L(enumString);
            }
            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
            
            GenerateComments(codeBuilder, key, jsonSchemaBuilderInteger);
            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public {TransformToTitleCase(key)}Enum{BuildRequired(jsonSchemaBuilderInteger.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderInteger)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Number

        private void GenerateOrdinaryNumber(CodeBuilder codeBuilder, IdentifierString key, JSBNumber jsonSchemaBuilderNumber)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderNumber);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public double{BuildRequired(jsonSchemaBuilderNumber.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderNumber)}")
                .EmptyLine();
        }

        private void GenerateEnumNumber(CodeBuilder codeBuilder, IdentifierString key, JSBNumber jsonSchemaBuilderNumber)
        {
            codeBuilder
                .L($"public enum {TransformToTitleCase(key)}Enum : long")
                .L("{")
                .IndentIncrease();
            
            // Use dictionary to track used values and avoid collisions
            Dictionary<long, string> usedValues = new Dictionary<long, string>();
            
            for(int counter = 0; counter < jsonSchemaBuilderNumber.Enums.Count; counter += 1)
            {
                bool last = counter + 1 == jsonSchemaBuilderNumber.Enums.Count;
                double? value = jsonSchemaBuilderNumber.Enums[counter];
                
                // Use counter as the enum value to avoid collisions
                long enumValue = counter;
                string valueName = value.HasValue ? value.Value.ToString().Replace(".", "_").Replace("-", "Minus") : "Null";
                string enumName = $"Value{valueName}";
                
                string enumString = $"{enumName} = {enumValue}{(last ? "" : ",")}";
                codeBuilder
                    .L(enumString);
            }
            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
            
            GenerateComments(codeBuilder, key, jsonSchemaBuilderNumber);
            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public {TransformToTitleCase(key)}Enum{BuildRequired(jsonSchemaBuilderNumber.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderNumber)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Objects

        private void GenerateEnumObject(CodeBuilder codeBuilder, IdentifierString key, JSBObject jsonSchemaBuilderObject, Dictionary<string, IJSBPart> definitions)
        {
            throw new NotImplementedException("Enum on objects has not been implemented");
        }

        private void GenerateOrdinaryObject(CodeBuilder codeBuilder, IdentifierString key, JSBObject jsonSchemaBuilderObject, Dictionary<string, IJSBPart> definitions, bool parentIsArray, bool isDefinition = false)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderObject);

            codeBuilder
                .L($"public partial class {TransformToTitleCase(key)}")
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
                codeBuilder
                    .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                    .L($"public {TransformToTitleCase(key)} {TransformToTitleCase(key)}Prop {{ get; set; }} ")
                    .EmptyLine();
            }
        }

        #endregion

        #region Generate String

        private void GenerateEnumString(CodeBuilder codeBuilder, IdentifierString key, JSBString jsonSchemaBuilderString, bool isDefinition)
        {
            codeBuilder
                .L($"public enum {TransformToTitleCase(key)}Enum")
                .L("{")
                .IndentIncrease();
            for(int counter = 0;counter < jsonSchemaBuilderString.Enums.Count;counter += 1)
            {
                bool last = counter + 1 == jsonSchemaBuilderString.Enums.Count;
                string enumString = TransformToTitleCase(jsonSchemaBuilderString.Enums[counter]) + (last?"":",");
                codeBuilder
                    .L(enumString);
            }
            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
            if (!isDefinition)
            {
                GenerateComments(codeBuilder, key, jsonSchemaBuilderString);
                codeBuilder
                    .L($"[JsonProperty(\"{TransformToCamelCase(key)}\"), JsonConverter(typeof(StringEnumConverter))]")
                    .L($"public {TransformToTitleCase(key)}Enum {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderString)}")
                    .EmptyLine();
            }
        }

        private void GenerateOrdinaryString(CodeBuilder codeBuilder, IdentifierString key, JSBString jsonSchemaBuilderString)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderString);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public string {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderString)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Base64String

        private void GenerateOrdinaryBase64String(CodeBuilder codeBuilder, IdentifierString key, JSBBase64String jsonSchemaBuilderBase64String)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderBase64String);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public string {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderBase64String)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Uri

        private void GenerateOrdinaryUri(CodeBuilder codeBuilder, IdentifierString key, JSBUri jsonSchemaBuilderUri)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderUri);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public Uri {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderUri)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Guid

        private void GenerateOrdinaryGuid(CodeBuilder codeBuilder, IdentifierString key, JSBGuid jsonSchemaBuilderGuid)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderGuid);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public Guid {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderGuid)}")
                .EmptyLine();
        }

        #endregion

        #region Generate PhoneNumber

        private void GenerateOrdinaryPhoneNumber(CodeBuilder codeBuilder, IdentifierString key, JSBPhoneNumber jsonSchemaBuilderPhoneNumber)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderPhoneNumber);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public PhoneNumber {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderPhoneNumber)}")
                .EmptyLine();
        }

        #endregion

        #region Generate Time

        private void GenerateEnumTime(CodeBuilder codeBuilder, IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            codeBuilder
                .L($"public enum {TransformToTitleCase(key)}Enum")
                .L("{")
                .IndentIncrease();
            for(int counter = 0; counter < jsonSchemaBuilderTime.Enums.Count; counter += 1)
            {
                bool last = counter + 1 == jsonSchemaBuilderTime.Enums.Count;
                string timeValue = jsonSchemaBuilderTime.Enums[counter] ?? "Unknown";
                // Create valid enum name from time string
                string enumName = $"Time_{MakeValidEnumName(timeValue)}";
                string enumString = $"{enumName} = {counter}{(last ? "" : ",")}";
                codeBuilder
                    .L(enumString);
            }
            codeBuilder
                .IndentDecrease()
                .L("}")
                .EmptyLine();
            
            GenerateComments(codeBuilder, key, jsonSchemaBuilderTime);
            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public {TransformToTitleCase(key)}Enum{BuildRequired(jsonSchemaBuilderTime.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderTime)}")
                .EmptyLine();
        }

        private void GenerateOrdinaryTime(CodeBuilder codeBuilder, IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            GenerateComments(codeBuilder, key, jsonSchemaBuilderTime);

            codeBuilder
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public DateTime{BuildRequired(jsonSchemaBuilderTime.IsRequired)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderTime)}")
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
                .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                .L($"public {MakeCorrectItemType(referencedPart)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderUriReference)}")
                .EmptyLine();
        }

        private void GenerateCodeFromSchema(CodeBuilder codeBuilder, JsonValue jsonValueOfSchema, IdentifierString key, JSBRef jsonSchemaBuilderUriReference)
        {
            if (jsonValueOfSchema.Type == JsonValueType.Object)
            {

                GenerateComments(codeBuilder, key, jsonSchemaBuilderUriReference);

                codeBuilder
                    .L($"[JsonProperty(\"{TransformToCamelCase(key)}\")]")
                    .L($"public {MakeCorrectItemType(jsonValueOfSchema)} {TransformToTitleCase(key)} {{ get; set; }}{GenerateDefaultIfExisting(key, jsonSchemaBuilderUriReference)}")
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
            switch(type)
            {
                case "string":
                    if(string.IsNullOrWhiteSpace(format))
                    {
                        return "string";
                    }
                    switch(format)
                    {
                        case "time":
                        case "date":
                        case "datetime":
                            return "DateTime";
                        case "email":
                            return "Email";
                    }
                    break;
                case "boolean":
                    return "bool";
                case "object":
                    return TransformToTitleCase(name);
                case "number":
                    return "double";
                case "integer":
                    return "long";
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

            codeBuilder.L("/// <summary>");
            foreach (string commentLine in commentLines)
            {
                codeBuilder.L($"/// {commentLine}");
            }
            codeBuilder.L("/// </summary>");
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
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderString.DefaultValue) ? string.Empty : $" = \"{jsonSchemaBuilderString.DefaultValue}\";";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBEmail jsonSchemaBuilderEmail)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderEmail.DefaultValue) ? string.Empty : $" = new Email(\"{jsonSchemaBuilderEmail.DefaultValue}\");";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBUri jsonSchemaBuilderUri)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderUri.DefaultValue) ? string.Empty : $" = new Uri(\"{jsonSchemaBuilderUri.DefaultValue}\");";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBGuid jsonSchemaBuilderGuid)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderGuid.DefaultValue) ? string.Empty : $" = new Guid(\"{jsonSchemaBuilderGuid.DefaultValue}\");";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBPhoneNumber jsonSchemaBuilderPhoneNumber)
        {
            return string.IsNullOrWhiteSpace(jsonSchemaBuilderPhoneNumber.DefaultValue) ? string.Empty : $" = new PhoneNumber(\"{jsonSchemaBuilderPhoneNumber.DefaultValue}\");";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBDateTime jsonSchemaBuilderDateTime)
        {
            if (string.IsNullOrWhiteSpace(jsonSchemaBuilderDateTime.DefaultValue))
            {
                return string.Empty;
            }
            
            if (jsonSchemaBuilderDateTime.Enums != null && jsonSchemaBuilderDateTime.Enums.Count > 0)
            {
                string enumName = $"DateTime_{jsonSchemaBuilderDateTime.DefaultValue.Replace("-", "_").Replace(":", "_").Replace(".", "_").Replace(" ", "_").Replace("T", "_")}";
                return $" = {TransformToTitleCase(key)}Enum.{enumName};";
            }
            return $" = DateTime.Parse(\"{jsonSchemaBuilderDateTime.DefaultValue}\");";
        }
        
        private string GenerateDefaultIfExisting(IdentifierString key, JSBDate jsonSchemaBuilderDate)
        {
            if (string.IsNullOrWhiteSpace(jsonSchemaBuilderDate.DefaultValue))
            {
                return string.Empty;
            }
            
            if (jsonSchemaBuilderDate.Enums != null && jsonSchemaBuilderDate.Enums.Count > 0)
            {
                string enumName = $"Date_{jsonSchemaBuilderDate.DefaultValue.Replace("-", "_").Replace(":", "_").Replace(".", "_")}";
                return $" = {TransformToTitleCase(key)}Enum.{enumName};";
            }
            return $" = DateTime.Parse(\"{jsonSchemaBuilderDate.DefaultValue}\");";
        }
        
        private string GenerateDefaultIfExisting(IdentifierString key, JSBTime jsonSchemaBuilderTime)
        {
            if (string.IsNullOrWhiteSpace(jsonSchemaBuilderTime.DefaultValue))
            {
                return string.Empty;
            }
            
            if (jsonSchemaBuilderTime.Enums != null && jsonSchemaBuilderTime.Enums.Count > 0)
            {
                string enumName = $"Time_{jsonSchemaBuilderTime.DefaultValue.Replace(":", "_").Replace(".", "_")}";
                return $" = {TransformToTitleCase(key)}Enum.{enumName};";
            }
            return $" = DateTime.Parse(\"{jsonSchemaBuilderTime.DefaultValue}\");";
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBBoolean jsonSchemaBuilderBoolean)
        {
            if (jsonSchemaBuilderBoolean.DefaultValue.HasValue)
            {
                if (jsonSchemaBuilderBoolean.Enums != null && jsonSchemaBuilderBoolean.Enums.Count > 0)
                {
                    string enumName = jsonSchemaBuilderBoolean.DefaultValue.Value ? "True" : "False";
                    return $" = {TransformToTitleCase(key)}Enum.{enumName};";
                }
                string defaultValue = jsonSchemaBuilderBoolean.DefaultValue.Value ? "true" : "false";
                return $" = {defaultValue};";
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
                    return $" = {TransformToTitleCase(key)}Enum.Value{jsonSchemaBuilderInteger.DefaultValue.Value};";
                }
                return $" = {jsonSchemaBuilderInteger.DefaultValue.Value};";
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
                    string valueName = jsonSchemaBuilderNumber.DefaultValue.Value.ToString().Replace(".", "_").Replace("-", "Minus");
                    return $" = {TransformToTitleCase(key)}Enum.Value{valueName};";
                }
                return $" = {jsonSchemaBuilderNumber.DefaultValue.Value};";
            }
            else
            {
                return string.Empty;
            }
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBArray jsonSchemaBuilderArray)
        {
            if (jsonSchemaBuilderArray.DefaultValue != null)
            {
                return $" = new List<{MakeCorrectItemType(jsonSchemaBuilderArray.Items[0])}>();{{{jsonSchemaBuilderArray.DefaultValue.ToString()}}}";
            }
            else
            {
                return $" = new List<{MakeCorrectItemType(jsonSchemaBuilderArray.Items[0])}>();";
            }
        }

        private string GenerateDefaultIfExisting(IdentifierString key, JSBRef jsonSchemaBuilderUriReference)
        {
            if(jsonSchemaBuilderUriReference.DefaultValue != null)
            {
                throw new NotImplementedException($"Default value for Reference is not implemented");
            }
            else
            {
                return string.Empty;
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
        /// Returns ? if required to build Nullable valuetypes if not required
        /// </summary>
        /// <param name="isRequired"></param>
        /// <returns></returns>
        private string BuildRequired(bool isRequired)
        {
            return isRequired ? string.Empty: "?";
        }

        /// <summary>
        /// Creates a valid C# enum name from a string value by replacing special characters
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

        #endregion
    }
}
