using Json.Schema;
using System;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;

    /// <summary>
    /// Centralizes how builder schemas are built into Json.Schema JsonSchema
    /// instances: draft-07 wire format (definitions vocabulary, draft-07
    /// keyword shapes), a fresh SchemaRegistry per call so repeated builds of
    /// the same $id do not collide with the global registry, and a stable
    /// absolute base uri so relative $id and $ref combinations resolve the
    /// same way on every call. The base uri only takes part in reference
    /// resolution, it is not emitted to the schema
    /// </summary>
    public static class JSBJsonSchemaBuild
    {
        /// <summary>
        /// Serializer options that keep the emitted schema json as close as
        /// possible to the json emitted by the previous Manatee based engine:
        /// html sensitive characters like + and &amp; are not escaped
        /// </summary>
        public static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Serializer options as SerializerOptions but with indented output
        /// for schema files written to disk
        /// </summary>
        public static readonly JsonSerializerOptions IndentedSerializerOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        /// <summary>
        /// Stable absolute base uri used when building schemas
        /// </summary>
        public static readonly Uri BaseUri = new Uri("https://jsonschemabuilder.develapp.invalid/");

        /// <summary>
        /// Builds a JsonSchemaBuilder into a JsonSchema with draft-07 semantics
        /// and a fresh schema registry
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static JsonSchema Build(JsonSchemaBuilder builder)
        {
            return Build(builder, JSBSchemaVersion.Draft7);
        }

        /// <summary>
        /// Builds a JsonSchemaBuilder into a JsonSchema with the semantics of
        /// the given schema version and a fresh schema registry
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="schemaVersion"></param>
        /// <returns></returns>
        public static JsonSchema Build(JsonSchemaBuilder builder, JSBSchemaVersion schemaVersion)
        {
            return builder.Build(new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry(),
                Dialect = schemaVersion.ToDialect()
            }, BaseUri);
        }

        /// <summary>
        /// Build options with draft-07 semantics and a fresh schema registry,
        /// for parsing schema json back into a JsonSchema
        /// </summary>
        /// <returns></returns>
        public static BuildOptions CreateBuildOptions()
        {
            return CreateBuildOptions(JSBSchemaVersion.Draft7);
        }

        /// <summary>
        /// Build options with the semantics of the given schema version and a
        /// fresh schema registry, for parsing schema json back into a JsonSchema
        /// </summary>
        /// <param name="schemaVersion"></param>
        /// <returns></returns>
        public static BuildOptions CreateBuildOptions(JSBSchemaVersion schemaVersion)
        {
            return new BuildOptions
            {
                SchemaRegistry = new SchemaRegistry(),
                Dialect = schemaVersion.ToDialect()
            };
        }
    }
}
