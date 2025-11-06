using DevelApp.JsonSchemaBuilder.Exceptions;
using System;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// TSQL DDL generator for JSON Schema (Database First approach)
    /// TODO: Implement full TSQL DDL generation support
    /// </summary>
    public class TSQL
    {
        private const string FILE_ENDING = ".sql";

        /// <summary>
        /// Generate fileName and DDL code for TSQL
        /// </summary>
        /// <param name="codeGenerator"></param>
        /// <returns></returns>
        internal static List<(string fileName, string code)> GenerateCode(CodeGenerator codeGenerator)
        {
            // TODO: Implement TSQL DDL generation for Database First approach
            throw new NotImplementedException("TSQL DDL generation is not yet implemented. This is a placeholder for future development.");
        }
    }
}
