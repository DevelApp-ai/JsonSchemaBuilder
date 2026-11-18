using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using Json.Schema;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    public interface IJSBPart
    {
        /// <summary>
        /// Name of the schema part
        /// </summary>
        IdentifierString Name { get; }

        /// <summary>
        /// Desciption of the schema part
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Is the schema part required
        /// </summary>
        bool IsRequired { get; }

        /// <summary>
        /// Returns JsonSchemaPart as a Json.Schema JsonSchema built as draft-07
        /// </summary>
        /// <returns></returns>
        JsonSchema AsJsonSchema();

        /// <summary>
        /// Returns JsonSchemaPart as a Json.Schema JsonSchema built with the
        /// given schema version so parts can be embedded in a schema of a
        /// later dialect
        /// </summary>
        /// <param name="schemaVersion"></param>
        /// <returns></returns>
        JsonSchema AsJsonSchema(JSBSchemaVersion schemaVersion);

        /// <summary>
        /// Returns JsonSchemaPart as a JsonSchemaBuilder built as draft-07 so
        /// it can be composed into a larger schema before conversion
        /// </summary>
        /// <returns></returns>
        JsonSchemaBuilder AsJsonSchemaBuilder();

        /// <summary>
        /// Returns JsonSchemaPart as a JsonSchemaBuilder built with the given
        /// schema version (container keyword and array items form follow the
        /// dialect) so it can be composed into a larger schema before
        /// conversion
        /// </summary>
        /// <param name="schemaVersion"></param>
        /// <returns></returns>
        JsonSchemaBuilder AsJsonSchemaBuilder(JSBSchemaVersion schemaVersion);

        /// <summary>
        /// Returns the PartType of the JsonSchemaPart
        /// </summary>
        JSBPartType PartType { get; }

        /// <summary>
        /// Definitions carried by this part. Emitted as definitions in the JSON schema
        /// of the part so definitions are supported on other levels than the top schema
        /// </summary>
        Dictionary<string, IJSBPart> Definitions { get; }
    }
}
