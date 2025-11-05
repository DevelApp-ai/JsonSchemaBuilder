using DevelApp.JsonSchemaBuilder.Exceptions;
using System;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// Visual Basic code generator for JSON Schema
    /// TODO: Implement full VB code generation support
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
            // TODO: Implement Visual Basic code generation
            throw new NotImplementedException("Visual Basic code generation is not yet implemented. This is a placeholder for future development.");
        }
    }
}
