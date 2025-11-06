using DevelApp.JsonSchemaBuilder.Exceptions;
using System;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// TypeScript code generator for JSON Schema
    /// TODO: Implement full TypeScript code generation support
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
            // TODO: Implement TypeScript code generation
            throw new NotImplementedException("TypeScript code generation is not yet implemented. This is a placeholder for future development.");
        }
    }
}
