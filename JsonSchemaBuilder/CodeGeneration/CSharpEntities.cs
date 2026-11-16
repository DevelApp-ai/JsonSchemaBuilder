using System;
using System.Collections.Generic;
using System.Linq;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// C# Entity code generator for JSON Schema (Code First approach).
    ///
    /// Entities are the same classes the CSharp generator emits with the
    /// serialization specifics removed, so they can be used as plain
    /// Code First domain entities without any Newtonsoft.Json dependency:
    /// - the [JsonProperty] mapping attributes, including the JsonConverter they carry
    /// - the using directives that only the serialization classes need
    /// </summary>
    public class CSharpEntities
    {
        /// <summary>
        /// Generate fileName and entity code for C# Code First
        /// </summary>
        /// <param name="codeGenerator"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            List<(string fileName, string code)> generatedCode = CSharp.GenerateCode(codeGenerator);
            return generatedCode
                .Select(generatedFile => (generatedFile.fileName, GenerateEntityCode(generatedFile.code)))
                .ToList();
        }

        /// <summary>
        /// Removes the serialization specifics from CSharp generated code so the
        /// result is plain entity code for Code First development. Class, property,
        /// documentation and default value structure is kept as generated.
        /// </summary>
        /// <param name="cSharpCode"></param>
        /// <returns></returns>
        internal static string GenerateEntityCode(string cSharpCode)
        {
            List<string> resultLines = new List<string>();
            foreach (string line in cSharpCode.Replace("\r\n", "\n").Split('\n'))
            {
                string trimmedLine = line.TrimStart();

                //The whole attribute line is removed including any JsonConverter, since the
                //json contract is handled by the generated serialization classes instead
                if (trimmedLine.StartsWith("[JsonProperty", StringComparison.Ordinal))
                {
                    continue;
                }

                //usings that only the serialization classes need
                if (trimmedLine == "using Newtonsoft.Json;"
                    || trimmedLine == "using Newtonsoft.Json.Converters;"
                    || trimmedLine == "using Ardalis.SmartEnum;"
                    || trimmedLine == "using System.Net.Mail;")
                {
                    continue;
                }

                resultLines.Add(line);
            }
            return string.Join("\n", resultLines);
        }
    }
}
