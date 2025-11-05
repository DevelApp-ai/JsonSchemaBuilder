using DevelApp.JsonSchemaBuilder.Exceptions;
using System;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// C# Entity code generator for JSON Schema (Code First approach)
    /// TODO: Implement full C# Entity code generation support for Code First development
    /// </summary>
    public class CSharpEntities
    {
        private const string FILE_ENDING = ".cs";

        /// <summary>
        /// Generate fileName and entity code for C# Code First
        /// </summary>
        /// <param name="codeGenerator"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            // TODO: Implement C# Entity generation for Code First approach
            throw new NotImplementedException("C# Entities code generation is not yet implemented. This is a placeholder for future development.");
        }
    }
}
