using Json.Schema;

namespace DevelApp.JsonSchemaBuilder
{
    /// <summary>
    /// Json Schema dialect the builder emits. Draft7 is the default so existing
    /// schemas keep their exact wire format. The later dialects use the $defs
    /// container keyword instead of definitions and Draft202012 replaces the
    /// tuple form of items with prefixItems
    /// </summary>
    public enum JSBSchemaVersion
    {
        /// <summary>
        /// http://json-schema.org/draft-07/schema# - the historical wire format
        /// of this library (definitions container, tuple form items)
        /// </summary>
        Draft7 = 0,

        /// <summary>
        /// https://json-schema.org/draft/2019-09/schema - $defs container,
        /// tuple form items still allowed
        /// </summary>
        Draft201909 = 1,

        /// <summary>
        /// https://json-schema.org/draft/2020-12/schema - $defs container,
        /// tuple form items replaced by prefixItems
        /// </summary>
        Draft202012 = 2
    }

    /// <summary>
    /// Centralizes the mapping from JSBSchemaVersion to the $schema uri and
    /// the json-everything dialect used to build the schema
    /// </summary>
    public static class JSBSchemaVersionExtensions
    {
        /// <summary>
        /// The $schema uri for the dialect
        /// </summary>
        public static string SchemaUri(this JSBSchemaVersion schemaVersion)
        {
            switch (schemaVersion)
            {
                case JSBSchemaVersion.Draft201909:
                    return "https://json-schema.org/draft/2019-09/schema";
                case JSBSchemaVersion.Draft202012:
                    return "https://json-schema.org/draft/2020-12/schema";
                default:
                    return "http://json-schema.org/draft-07/schema#";
            }
        }

        /// <summary>
        /// The json-everything dialect for the schema version
        /// </summary>
        public static Dialect ToDialect(this JSBSchemaVersion schemaVersion)
        {
            switch (schemaVersion)
            {
                case JSBSchemaVersion.Draft201909:
                    return Dialect.Draft201909;
                case JSBSchemaVersion.Draft202012:
                    return Dialect.Draft202012;
                default:
                    return Dialect.Draft07;
            }
        }

        /// <summary>
        /// True when the dialect uses the $defs container keyword instead of
        /// the draft-7 definitions keyword
        /// </summary>
        public static bool UsesDefsContainer(this JSBSchemaVersion schemaVersion)
        {
            return schemaVersion != JSBSchemaVersion.Draft7;
        }
    }
}
