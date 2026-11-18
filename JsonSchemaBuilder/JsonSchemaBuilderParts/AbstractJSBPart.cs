using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.Utility.Model;
using Json.Schema;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    public abstract class AbstractJSBPart<T> : IJSBPart
    {
        public AbstractJSBPart(IdentifierString name, string description, bool isRequired, 
            T defaultValue, List<T> examples, List<T> enums)
        {
            var type = typeof(T);
            _tIsString = type.Name.Equals("String");
            _tIsJsonNode = type.Name.Equals("JsonNode");
            _tIsNullable = Nullable.GetUnderlyingType(type) != null;
            if (!(_tIsString || _tIsJsonNode || _tIsNullable))
            {
                throw new JsonSchemaBuilderException($"Only allowed types are nullable or classes string and JsonNode as direct use of value types gives errors");
            }

            Name = name;
            Description = description;
            IsRequired = isRequired;
            if (!Equals(defaultValue, default(T)))
            {
                DefaultValue = defaultValue;
            }
            if (examples == null)
            {
                Examples = new List<T>();
            }
            else
            {
                Examples = examples;
            }
            if (enums == null)
            {
                Enums = new List<T>();
            }
            else
            {
                Enums = enums;
            }
        }

        private bool _tIsString;
        private bool _tIsJsonNode;
        private bool _tIsNullable;

        public IdentifierString Name { get; }

        public string Description { get; }

        public bool IsRequired { get; }

        public T DefaultValue { get; }

        public List<T> Examples { get; }

        public List<T> Enums { get; }

        /// <summary>
        /// Definitions carried by this part. Emitted as definitions in the JSON schema
        /// of the part so definitions are supported on other levels than the top schema
        /// </summary>
        public Dictionary<string, IJSBPart> Definitions { get; } = new Dictionary<string, IJSBPart>();

        public abstract JSBPartType PartType { get; }

        /// <summary>
        /// Returns the part as a fully built Json.Schema JsonSchema with
        /// draft-07 semantics
        /// </summary>
        public virtual JsonSchema AsJsonSchema()
        {
            return JSBJsonSchemaBuild.Build(AsJsonSchemaBuilder());
        }

        /// <summary>
        /// Returns the part as a fully built Json.Schema JsonSchema with the
        /// semantics of the given schema version
        /// </summary>
        /// <param name="schemaVersion"></param>
        /// <returns></returns>
        public virtual JsonSchema AsJsonSchema(JSBSchemaVersion schemaVersion)
        {
            return JSBJsonSchemaBuild.Build(AsJsonSchemaBuilder(schemaVersion), schemaVersion);
        }

        /// <summary>
        /// Returns the part as a JsonSchemaBuilder with draft-07 semantics
        /// </summary>
        public virtual JsonSchemaBuilder AsJsonSchemaBuilder()
        {
            return AsJsonSchemaBuilder(JSBSchemaVersion.Draft7);
        }

        /// <summary>
        /// Returns the part as a JsonSchemaBuilder with the semantics of the
        /// given schema version
        /// </summary>
        /// <param name="schemaVersion"></param>
        /// <returns></returns>
        public abstract JsonSchemaBuilder AsJsonSchemaBuilder(JSBSchemaVersion schemaVersion);

        /// <summary>
        /// Starts every schema part with the common keywords: title, description,
        /// $comment, examples, enum and the definitions carried by the part.
        /// The container keyword for definitions follows the schema version:
        /// definitions for draft-7 and $defs for the later dialects
        /// </summary>
        protected JsonSchemaBuilder InitialJsonSchema(JSBSchemaVersion schemaVersion)
        {
            JsonSchemaBuilder builder = new JsonSchemaBuilder()
                .Title(Name)
                .Description(Description)
                .Comment($"Generated with JsonSchemaBuilder");
            if (Examples != null)
            {
                List<JsonNode> examplesOut = new List<JsonNode>();
                foreach (T item in Examples)
                {
                    examplesOut.Add(TAsJsonValue(item));
                }
                builder.Examples(examplesOut);
            }
            if (Enums != null)
            {
                List<JsonNode> enumsOut = new List<JsonNode>();
                foreach (T item in Enums)
                {
                    enumsOut.Add(TAsJsonValue(item));
                }
                builder.Enum(enumsOut);
            }
            //Add definitions carried by this part so definitions are supported
            //on other levels than the top schema
            List<(string name, JsonSchemaBuilder definition)> definitionsOut =
                new List<(string name, JsonSchemaBuilder definition)>();
            foreach (var pair in Definitions)
            {
                definitionsOut.Add((pair.Key, pair.Value.AsJsonSchemaBuilder(schemaVersion)));
            }
            if (definitionsOut.Count > 0)
            {
                if (schemaVersion.UsesDefsContainer())
                {
                    builder.Defs(definitionsOut.ToArray());
                }
                else
                {
                    builder.Definitions(definitionsOut.ToArray());
                }
            }

            return builder;
        }

        private JsonNode TAsJsonValue(T item)
        {
            if (_tIsJsonNode)
            {
                return item as JsonNode;
            }
            else if (_tIsString)
            {
                return item as string;
            }
            else if (_tIsNullable)
            {
                if (item is bool?)
                {
                    return (item as bool?).Value;
                }
                if (item is long?)
                {
                    return (item as long?).Value;
                }
                if (item is double?)
                {
                    return (item as double?).Value;
                }
                throw new JsonSchemaBuilderException($"The nullable used ({typeof(T).Name}) is not supported");
            }
            throw new JsonSchemaBuilderException($"Should not get here as exception has been thrown in contructor");
        }


        /// <summary>
        /// Returns a identifier with small letter as start
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        protected string StartWithSmallLetter(IdentifierString name)
        {
            return name.ToString().Substring(0, 1).ToLowerInvariant() + name.ToString().Substring(1);
        }
    }
}
