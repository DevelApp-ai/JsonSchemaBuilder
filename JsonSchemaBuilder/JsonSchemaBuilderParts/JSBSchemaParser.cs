using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.Utility.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    /// <summary>
    /// Parses a schema document given as a JsonNode into the builder part tree
    /// (a JSBSchema) so externally authored and remote schemas can be registered
    /// for code generation exactly like locally authored builder schemas. The
    /// parser supports the keyword surface the builder parts can represent and
    /// fails loud with a SchemaParseException on keywords that cannot be
    /// represented without changing the meaning of the schema
    ///
    /// Supported keywords: type (single type or a two element array with null),
    /// properties, required, additionalProperties (bool form), items (single
    /// and tuple form), prefixItems, minItems, maxItems, uniqueItems, format,
    /// minLength, maxLength, pattern, minimum, maximum, multipleOf, enum,
    /// const (scalar), default, examples, definitions, $defs, $ref, allOf,
    /// anyOf, oneOf and the annotations title, description and $comment.
    ///
    /// Ignored silently: $id, $schema, $anchor, $recursiveAnchor,
    /// $dynamicAnchor, deprecated and unknown vendor keywords.
    ///
    /// Approximations (documented in the README): type arrays like
    /// ["string","null"] are parsed as the base type with the part marked
    /// optional; property and definition keys that are not identifier-like are
    /// sanitized, which can change the emitted wire keys for exotic keys
    /// </summary>
    public static class JSBSchemaParser
    {
        /// <summary>
        /// Keywords that look normal in a schema document but cannot be
        /// represented by the builder parts. The parser fails loud on these
        /// instead of silently changing the meaning of the schema
        /// </summary>
        private static readonly string[] UNSUPPORTED_KEYWORDS = new string[]
        {
            "patternProperties", "propertyNames", "if", "then", "else", "not", "contains",
            "dependencies", "dependentSchemas", "dependentRequired", "minProperties", "maxProperties",
            "exclusiveMinimum", "exclusiveMaximum", "unevaluatedProperties", "unevaluatedItems",
            "additionalItems", "$dynamicRef", "$recursiveRef"
        };

        /// <summary>
        /// The native hardcoded patterns of the typed convenience string parts.
        /// When a parsed subschema carries the native pattern of its format (or
        /// no pattern at all) and no other constraints the typed part is used,
        /// which keeps builder emitted schemas round tripping to the same part
        /// types. The null value means the typed part has no native pattern
        /// </summary>
        private static readonly Dictionary<string, string> TYPED_STRING_FORMATS = new Dictionary<string, string>
        {
            { "date", @"^(\d{4})-(\d{2})-(\d{2})$" },
            { "time", @"^(\d{2})\:(\d{2})\:(\d{2})(\.\d{1,7})?([+-](\d{2})\:(\d{2}))?$" },
            { "date-time", @"^(\d{4})-(\d{2})-(\d{2})T(\d{2})\:(\d{2})\:(\d{2})(\.\d{1,7})?([+-](\d{2})\:(\d{2}))?$" },
            { "email", @"^[a-z0-9\._%+!$&*=^|~#%{}/\-]+@([a-z0-9\-]+\.){1,}([a-z]{2,22})$" },
            { "uuid", @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$" },
            { "byte", @"^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$" },
            { "uri", null }
        };

        /// <summary>
        /// The canonical default value emission formats of the typed date and
        /// time parts. A default is only mapped into the typed part when the
        /// default string is the exact canonical emission, which guarantees
        /// the re-emitted document never silently shifts fractions or offsets
        /// </summary>
        private const string DateDefaultFormat = "yyyy-MM-dd";
        private const string TimeDefaultFormat = "HH:mm:ss.fffffffzzz";
        private const string DateTimeDefaultFormat = "yyyy-MM-ddTHH:mm:ss.fffffffzzz";

        /// <summary>
        /// Parses a whole schema document into a JSBSchema. The document name is
        /// taken from the document title, else from fallbackName, else from the
        /// $id file name, else the name ParsedSchema is used: a title is the
        /// authored intent so it wins over a name derived from a file. Definitions
        /// and $defs are both accepted as the root level definition container
        /// </summary>
        /// <param name="schemaJson"></param>
        /// <param name="fallbackName"></param>
        /// <param name="fallbackDescription"></param>
        /// <returns></returns>
        public static JSBSchema ParseDocument(JsonNode schemaJson, IdentifierString fallbackName = null, string fallbackDescription = null)
        {
            JsonObject root = schemaJson as JsonObject;
            if (root == null)
            {
                throw new SchemaParseException($"A schema document must be a JSON object but was: {schemaJson?.ToJsonString() ?? "null"}");
            }
            if (root.Count == 0)
            {
                //Keep the historical behavior for empty schemas: an inert schema
                //named NoValidation without a top part
                return new JSBSchema(new IdentifierString("NoValidation"), "Represents an empty schema with disabled validation");
            }

            JSBSchemaVersion version = DetectSchemaVersion(root);
            List<KeyValuePair<string, IJSBPart>> documentDefinitions = ParseDefinitions(root);

            string title = GetStringOrNull(root, "title");
            string id = GetStringOrNull(root, "$id");
            string description = GetStringOrNull(root, "description") ?? fallbackDescription ?? "Schema parsed from a schema document";

            IdentifierString name = CreateIdentifierOrNull(title)
                ?? fallbackName
                ?? CreateIdentifierOrNull(IdFileName(id))
                ?? new IdentifierString("ParsedSchema");

            //The document level definition containers are already collected above
            //so the top part is built from the schema body only
            IJSBPart topPart = ParsePartInternal(name, root, false, false);

            JSBSchema result = new JSBSchema(name, description, topPart, null, null, null, null, false, version);
            //Rekey the definitions with the original wire keys so references from
            //other schemas resolve against the exact container keys the document
            //was authored with
            AddDefinitions(result, documentDefinitions);
            return result;
        }

        /// <summary>
        /// Parses a single subschema into a builder part. The name must already
        /// be identifier-like since it is used as the part name as-is
        /// </summary>
        /// <param name="name"></param>
        /// <param name="subschema"></param>
        /// <returns></returns>
        public static IJSBPart ParsePart(string name, JsonNode subschema)
        {
            return ParsePartInternal(CreateIdentifier(name, $"part name {name}"), subschema, false, true);
        }

        private static IJSBPart ParsePartInternal(IdentifierString name, JsonNode subschema, bool isRequired, bool parseLevelDefinitions)
        {
            JsonObject o = subschema as JsonObject;
            if (o == null)
            {
                if (subschema is JsonValue)
                {
                    throw new SchemaParseException($"Boolean schemas (true/false) are not supported by the builder parts. The subschema '{name}' was: {subschema.ToJsonString()}");
                }
                throw new SchemaParseException($"The subschema '{name}' must be a JSON object but was: {subschema?.ToJsonString() ?? "null"}");
            }
            CheckUnsupportedKeywords(o, name);

            //The builder parts always emit a description so a null description is
            //replaced with a default: the emitted schema must stay valid
            string description = GetStringOrNull(o, "description") ?? $"{name} parsed from the schema document";
            List<KeyValuePair<string, IJSBPart>> definitions = parseLevelDefinitions ? ParseDefinitions(o) : new List<KeyValuePair<string, IJSBPart>>();

            //$ref: a reference part. Only annotation keywords and the definition
            //containers are tolerated as siblings since the builder part cannot
            //apply constraints next to a reference. The empty examples and enum
            //arrays every builder part emits are ignored
            if (o.TryGetPropertyValue("$ref", out JsonNode refNode))
            {
                foreach (string siblingKey in EnumerateKeys(o))
                {
                    if (!siblingKey.Equals("$ref") && !IsToleratedRefSibling(o, siblingKey))
                    {
                        throw new SchemaParseException($"The subschema '{name}' combines '$ref' with '{siblingKey}'. The builder parts cannot apply keywords next to a reference");
                    }
                }
                string refString = AsString(refNode, $"$ref of '{name}'");
                Uri refUri;
                try
                {
                    refUri = new Uri(refString, UriKind.RelativeOrAbsolute);
                }
                catch (Exception ex)
                {
                    throw new SchemaParseException($"The $ref '{refString}' of '{name}' is not a valid uri", ex);
                }
                JSBRef refPart = new JSBRef(name, description, refUri, isRequired: isRequired);
                AddDefinitions(refPart, definitions);
                return refPart;
            }

            //Compositions: allOf, anyOf and oneOf. A composition cannot be
            //combined with other schema body keywords since the builder parts
            //cannot represent the combination
            IJSBPart compositionPart = TryParseComposition(name, description, o, isRequired);
            if (compositionPart != null)
            {
                AddDefinitions(compositionPart, definitions);
                return compositionPart;
            }

            string typeString;
            bool isNullableType;
            ResolveType(o, name, out typeString, out isNullableType);

            bool required = isRequired && !isNullableType;
            IJSBPart part;
            switch (typeString)
            {
                case "object":
                    part = ParseObjectPart(name, description, o, required);
                    break;
                case "array":
                    part = ParseArrayPart(name, description, o, required);
                    break;
                case "string":
                    part = ParseStringPart(name, description, o, required);
                    break;
                case "integer":
                    part = ParseIntegerPart(name, description, o, required);
                    break;
                case "number":
                    part = ParseNumberPart(name, description, o, required);
                    break;
                case "boolean":
                    part = ParseBooleanPart(name, description, o, required);
                    break;
                default:
                    throw new SchemaParseException($"The type '{typeString}' of subschema '{name}' is not supported by the builder parts");
            }
            AddDefinitions(part, definitions);
            return part;
        }

        /// <summary>
        /// Returns the composition part when the subschema is a composition.
        /// Throws when two compositions are combined or a composition is combined
        /// with other schema body keywords
        /// </summary>
        private static IJSBPart TryParseComposition(IdentifierString name, string description, JsonObject o, bool isRequired)
        {
            string compositionKeyword = null;
            JsonArray compositionArray = null;
            foreach (string keyword in new string[] { "allOf", "anyOf", "oneOf" })
            {
                if (o.TryGetPropertyValue(keyword, out JsonNode node))
                {
                    if (compositionKeyword != null)
                    {
                        throw new SchemaParseException($"The subschema '{name}' combines '{compositionKeyword}' with '{keyword}'. The builder parts cannot combine compositions");
                    }
                    compositionKeyword = keyword;
                    compositionArray = node as JsonArray;
                    if (compositionArray == null)
                    {
                        throw new SchemaParseException($"The '{keyword}' of subschema '{name}' must be an array of subschemas");
                    }
                }
            }
            if (compositionKeyword == null)
            {
                return null;
            }
            foreach (string siblingKey in EnumerateKeys(o))
            {
                if (!siblingKey.Equals(compositionKeyword) && !siblingKey.Equals("title")
                    && !siblingKey.Equals("description") && !siblingKey.Equals("$comment")
                    && !siblingKey.Equals("definitions") && !siblingKey.Equals("$defs")
                    && !IsToleratedEmptyAnnotation(o, siblingKey))
                {
                    throw new SchemaParseException($"The subschema '{name}' combines '{compositionKeyword}' with '{siblingKey}'. The builder parts cannot apply keywords next to a composition");
                }
            }
            if (compositionArray.Count == 0)
            {
                throw new SchemaParseException($"The '{compositionKeyword}' of subschema '{name}' is empty so no builder part can represent it");
            }
            List<IJSBPart> subParts = new List<IJSBPart>();
            HashSet<string> usedSubPartNames = new HashSet<string>();
            for (int i = 0; i < compositionArray.Count; i++)
            {
                string positionalName = name.ToString() + Capitalize(compositionKeyword) + i;
                IdentifierString subPartName = ChooseAnonymousPartName(name, positionalName,
                    compositionArray[i], usedSubPartNames, $"composition subschema {i}");
                subParts.Add(ParsePartInternal(subPartName, compositionArray[i], false, true));
            }
            switch (compositionKeyword)
            {
                case "allOf":
                    return new JSBAllOf(name, description, subParts, isRequired);
                case "anyOf":
                    return new JSBAnyOf(name, description, subParts, isRequired);
                default:
                    return new JSBOneOf(name, description, subParts, isRequired);
            }
        }

        private static IJSBPart ParseObjectPart(IdentifierString name, string description, JsonObject o, bool isRequired)
        {
            List<string> requiredKeys = GetStringArray(o, "required");

            bool isExpandable = true;
            if (o.TryGetPropertyValue("additionalProperties", out JsonNode additionalPropertiesNode))
            {
                bool? additionalProperties = AsBoolOrNull(additionalPropertiesNode);
                if (additionalProperties == null)
                {
                    throw new SchemaParseException($"The additionalProperties of '{name}' must be a boolean. Schema-valued additionalProperties cannot be represented by the builder parts");
                }
                isExpandable = additionalProperties.Value;
            }

            List<IJSBPart> properties = new List<IJSBPart>();
            HashSet<string> propertyNames = new HashSet<string>();
            if (o.TryGetPropertyValue("properties", out JsonNode propertiesNode))
            {
                JsonObject propertiesObject = propertiesNode as JsonObject;
                if (propertiesObject == null)
                {
                    throw new SchemaParseException($"The properties of '{name}' must be a JSON object mapping property names to subschemas");
                }
                foreach (KeyValuePair<string, JsonNode> propertyPair in propertiesObject)
                {
                    IdentifierString propertyName = CreateIdentifier(propertyPair.Key, $"property '{propertyPair.Key}' of '{name}'");
                    if (!propertyNames.Add(propertyName.ToString()))
                    {
                        throw new SchemaParseException($"The properties '{propertyPair.Key}' of '{name}' sanitize to the duplicate builder part name '{propertyName}'");
                    }
                    //A type array with null is parsed as the base type marked
                    //optional: the builder parts cannot express required but
                    //nullable so nullability is approximated with optionality
                    bool propertyRequired = requiredKeys.Contains(propertyPair.Key) && !IsNullableTypeArray(propertyPair.Value);
                    IJSBPart propertyPart = ParsePartInternal(propertyName, propertyPair.Value, propertyRequired, true);
                    properties.Add(propertyPart);
                }
            }

            List<JsonNode> enums = GetSchemaNodeEnums(o);
            List<JsonNode> examples = GetSchemaNodeList(o, "examples");
            JsonNode defaultValue = GetJsonNodeOrNull(o, "default");

            return new JSBObject(name, description, properties, isRequired, defaultValue, examples, enums, isExpandable);
        }

        private static IJSBPart ParseArrayPart(IdentifierString name, string description, JsonObject o, bool isRequired)
        {
            JsonNode itemsNode = GetJsonNodeOrNull(o, "items");
            JsonNode prefixItemsNode = GetJsonNodeOrNull(o, "prefixItems");

            List<IJSBPart> itemParts = new List<IJSBPart>();
            HashSet<string> usedItemNames = new HashSet<string>();
            if (prefixItemsNode != null && itemsNode != null)
            {
                throw new SchemaParseException($"The array '{name}' combines prefixItems with trailing items. The builder parts cannot express a tuple with a trailing item schema");
            }
            if (prefixItemsNode is JsonArray prefixItemsArray)
            {
                if (prefixItemsArray.Count == 0)
                {
                    throw new SchemaParseException($"The prefixItems of '{name}' is empty so no builder part can represent the array");
                }
                for (int i = 0; i < prefixItemsArray.Count; i++)
                {
                    string positionalName = name.ToString() + "Item" + i;
                    IdentifierString itemName = ChooseAnonymousPartName(name, positionalName,
                        prefixItemsArray[i], usedItemNames, $"prefix item {i} of '{name}'");
                    itemParts.Add(ParsePartInternal(itemName, prefixItemsArray[i], false, true));
                }
            }
            else if (itemsNode is JsonObject singleItem)
            {
                string positionalName = name.ToString() + "Item";
                IdentifierString itemName = ChooseAnonymousPartName(name, positionalName,
                    singleItem, usedItemNames, $"item of '{name}'");
                itemParts.Add(ParsePartInternal(itemName, singleItem, false, true));
            }
            else if (itemsNode is JsonArray itemsArray)
            {
                if (itemsArray.Count == 0)
                {
                    throw new SchemaParseException($"The items of '{name}' is empty so no builder part can represent the array");
                }
                for (int i = 0; i < itemsArray.Count; i++)
                {
                    string positionalName = name.ToString() + "Item" + i;
                    IdentifierString itemName = ChooseAnonymousPartName(name, positionalName,
                        itemsArray[i], usedItemNames, $"item {i} of '{name}'");
                    itemParts.Add(ParsePartInternal(itemName, itemsArray[i], false, true));
                }
            }
            else if (itemsNode != null || prefixItemsNode != null)
            {
                throw new SchemaParseException($"The items of '{name}' must be a subschema object or an array of subschemas");
            }
            else
            {
                throw new SchemaParseException($"The array '{name}' has no items or prefixItems so no builder part can represent it");
            }

            uint? minItems = GetUInt(o, "minItems", $"minItems of '{name}'");
            uint? maxItems = GetUInt(o, "maxItems", $"maxItems of '{name}'");
            bool uniqueItems = false;
            if (o.TryGetPropertyValue("uniqueItems", out JsonNode uniqueItemsNode))
            {
                bool? uniqueItemsValue = AsBoolOrNull(uniqueItemsNode);
                if (uniqueItemsValue == null || !uniqueItemsValue.Value)
                {
                    throw new SchemaParseException($"The uniqueItems of '{name}' must be the boolean true. uniqueItems: false cannot be distinguished from absence in the builder parts");
                }
                uniqueItems = true;
            }

            List<JsonNode> enums = GetSchemaNodeEnums(o);
            List<JsonNode> examples = GetSchemaNodeList(o, "examples");
            JsonNode defaultValue = GetJsonNodeOrNull(o, "default");

            return new JSBArray(name, description, itemParts, minItems, maxItems, uniqueItems, defaultValue, examples, enums, isRequired);
        }

        private static IJSBPart ParseStringPart(IdentifierString name, string description, JsonObject o, bool isRequired)
        {
            string format = GetStringOrNull(o, "format");
            uint minLength = 0;
            if (o.TryGetPropertyValue("minLength", out JsonNode minLengthNode))
            {
                minLength = GetUInt(o, "minLength", $"minLength of '{name}'").Value;
            }
            uint? maxLength = GetUInt(o, "maxLength", $"maxLength of '{name}'");
            string pattern = GetStringOrNull(o, "pattern");
            List<JsonNode> rawEnums = GetSchemaNodeEnums(o);
            List<JsonNode> examples = GetSchemaNodeList(o, "examples");
            JsonNode defaultValueNode = GetJsonNodeOrNull(o, "default");

            //Typed convenience mapping: only when the format matches a typed part,
            //the pattern is absent or exactly the native pattern of the typed part
            //and no other constraints exist that the typed part cannot carry. A
            //default value is mapped into the typed part when the typed part can
            //carry it without changing the emitted wire, otherwise the part
            //degrades to a plain JSBString which stays wire faithful
            if (format != null && TYPED_STRING_FORMATS.ContainsKey(format))
            {
                bool patternMatchesNative = pattern == null || string.Equals(pattern, TYPED_STRING_FORMATS[format], StringComparison.Ordinal);
                bool noOtherConstraints = minLength == 0 && maxLength == null && rawEnums.Count == 0
                    && examples.Count == 0;
                if (patternMatchesNative && noOtherConstraints)
                {
                    IJSBPart typedPart = TryCreateTypedStringPart(format, name, description, defaultValueNode, isRequired);
                    if (typedPart != null)
                    {
                        return typedPart;
                    }
                }
            }

            string defaultValue = null;
            if (defaultValueNode != null)
            {
                defaultValue = AsString(defaultValueNode, $"default of '{name}'");
            }
            List<string> enums = new List<string>();
            foreach (JsonNode enumNode in rawEnums)
            {
                enums.Add(AsString(enumNode, $"enum of '{name}'"));
            }
            List<string> examplesList = new List<string>();
            foreach (JsonNode exampleNode in examples)
            {
                examplesList.Add(AsString(exampleNode, $"examples of '{name}'"));
            }

            return new JSBString(name, description, format, minLength, maxLength, pattern, defaultValue, examplesList, enums, isRequired);
        }

        /// <summary>
        /// Creates the typed convenience part for a supported format when the
        /// subschema can be represented by it. A default value is only mapped
        /// into the typed part when it is a string and the typed part can
        /// carry it without changing the emitted wire: string based defaults
        /// (email, uuid, byte, uri) must match the native pattern, date and
        /// date-time defaults must be exactly the canonical emission of the
        /// typed part and time defaults must additionally re-emit through
        /// UTC so offsets never silently shift on re-emission. Returns null
        /// when the typed part cannot carry the subschema and the caller
        /// should fall back to a plain JSBString
        /// </summary>
        private static IJSBPart TryCreateTypedStringPart(string format, IdentifierString name, string description, JsonNode defaultValueNode, bool isRequired)
        {
            string typedDefault = null;
            if (defaultValueNode != null)
            {
                JsonValue defaultValue = defaultValueNode as JsonValue;
                if (defaultValue == null || !defaultValue.TryGetValue(out string rawDefault))
                {
                    return null;
                }
                string nativePattern = TYPED_STRING_FORMATS[format];
                if (nativePattern != null && !Regex.IsMatch(rawDefault, nativePattern))
                {
                    return null;
                }
                typedDefault = rawDefault;
            }

            switch (format)
            {
                case "date":
                    if (typedDefault == null)
                    {
                        return new JSBDate(name, description, null, isRequired);
                    }
                    if (!DateTime.TryParseExact(typedDefault, DateDefaultFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateValue))
                    {
                        return null;
                    }
                    return new JSBDate(name, description, dateValue, isRequired);
                case "time":
                    if (typedDefault == null)
                    {
                        return new JSBTime(name, description, null, isRequired);
                    }
                    if (!DateTime.TryParseExact(typedDefault, TimeDefaultFormat, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime timeValue)
                        || !string.Equals(timeValue.ToString(TimeDefaultFormat, CultureInfo.InvariantCulture), typedDefault, StringComparison.Ordinal))
                    {
                        return null;
                    }
                    return new JSBTime(name, description, timeValue, isRequired);
                case "date-time":
                    if (typedDefault == null)
                    {
                        return new JSBDateTime(name, description, null, isRequired);
                    }
                    if (!DateTime.TryParseExact(typedDefault, DateTimeDefaultFormat, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime dateTimeValue)
                        || !string.Equals(dateTimeValue.ToString(DateTimeDefaultFormat, CultureInfo.InvariantCulture), typedDefault, StringComparison.Ordinal))
                    {
                        return null;
                    }
                    return new JSBDateTime(name, description, dateTimeValue, isRequired);
                case "email":
                    return new JSBEmail(name, description, typedDefault, isRequired);
                case "uuid":
                    return new JSBGuid(name, description, typedDefault, isRequired);
                case "uri":
                    return new JSBUri(name, description, typedDefault, isRequired);
                case "byte":
                    return new JSBBase64String(name, description, typedDefault, isRequired);
                default:
                    return null;
            }
        }

        private static IJSBPart ParseIntegerPart(IdentifierString name, string description, JsonObject o, bool isRequired)
        {
            long? minimum = GetInteger(o, "minimum", $"minimum of '{name}'");
            long? maximum = GetInteger(o, "maximum", $"maximum of '{name}'");
            double? multipleOf = GetNumber(o, "multipleOf", $"multipleOf of '{name}'");

            long? defaultValue = null;
            if (o.TryGetPropertyValue("default", out JsonNode defaultNode))
            {
                defaultValue = (long)GetIntegralNumber(defaultNode, $"default of '{name}'");
            }
            List<long?> enums = new List<long?>();
            foreach (JsonNode enumNode in GetSchemaNodeEnums(o))
            {
                enums.Add((long)GetIntegralNumber(enumNode, $"enum of '{name}'"));
            }
            List<long?> examples = new List<long?>();
            foreach (JsonNode exampleNode in GetSchemaNodeList(o, "examples"))
            {
                examples.Add((long)GetIntegralNumber(exampleNode, $"examples of '{name}'"));
            }

            return new JSBInteger(name, description, minimum, maximum, multipleOf, defaultValue, examples, enums, isRequired);
        }

        private static IJSBPart ParseNumberPart(IdentifierString name, string description, JsonObject o, bool isRequired)
        {
            double? minimum = GetNumber(o, "minimum", $"minimum of '{name}'");
            double? maximum = GetNumber(o, "maximum", $"maximum of '{name}'");
            double? multipleOf = GetNumber(o, "multipleOf", $"multipleOf of '{name}'");

            double? defaultValue = null;
            if (o.TryGetPropertyValue("default", out JsonNode defaultNode))
            {
                defaultValue = GetNumber(defaultNode, $"default of '{name}'");
            }
            List<double?> enums = new List<double?>();
            foreach (JsonNode enumNode in GetSchemaNodeEnums(o))
            {
                enums.Add(GetNumber(enumNode, $"enum of '{name}'"));
            }
            List<double?> examples = new List<double?>();
            foreach (JsonNode exampleNode in GetSchemaNodeList(o, "examples"))
            {
                examples.Add(GetNumber(exampleNode, $"examples of '{name}'"));
            }

            return new JSBNumber(name, description, minimum, maximum, multipleOf, defaultValue, examples, enums, isRequired);
        }

        private static IJSBPart ParseBooleanPart(IdentifierString name, string description, JsonObject o, bool isRequired)
        {
            bool? defaultValue = null;
            if (o.TryGetPropertyValue("default", out JsonNode defaultNode))
            {
                bool? defaultBool = AsBoolOrNull(defaultNode);
                if (defaultBool == null)
                {
                    throw new SchemaParseException($"The default of '{name}' must be a boolean but was: {defaultNode.ToJsonString()}");
                }
                defaultValue = defaultBool.Value;
            }
            List<bool?> enums = new List<bool?>();
            foreach (JsonNode enumNode in GetSchemaNodeEnums(o))
            {
                bool? enumBool = AsBoolOrNull(enumNode);
                if (enumBool == null)
                {
                    throw new SchemaParseException($"The enum of '{name}' must only contain booleans but contained: {enumNode.ToJsonString()}");
                }
                enums.Add(enumBool.Value);
            }
            List<bool?> examples = new List<bool?>();
            foreach (JsonNode exampleNode in GetSchemaNodeList(o, "examples"))
            {
                bool? exampleBool = AsBoolOrNull(exampleNode);
                if (exampleBool == null)
                {
                    throw new SchemaParseException($"The examples of '{name}' must only contain booleans but contained: {exampleNode.ToJsonString()}");
                }
                examples.Add(exampleBool.Value);
            }

            return new JSBBoolean(name, description, defaultValue, examples, enums, isRequired);
        }

        /// <summary>
        /// Both the draft-7 definitions container and the $defs container of the
        /// later dialects are accepted on any level. The definitions are keyed
        /// with the original wire keys so emitted schemas keep the authored
        /// container keys
        /// </summary>
        private static List<KeyValuePair<string, IJSBPart>> ParseDefinitions(JsonObject o)
        {
            List<KeyValuePair<string, IJSBPart>> result = new List<KeyValuePair<string, IJSBPart>>();
            HashSet<string> seenWireKeys = new HashSet<string>();
            foreach (string containerKey in new string[] { "definitions", "$defs" })
            {
                if (!o.TryGetPropertyValue(containerKey, out JsonNode containerNode) || containerNode == null)
                {
                    continue;
                }
                JsonObject containerObject = containerNode as JsonObject;
                if (containerObject == null)
                {
                    throw new SchemaParseException($"The {containerKey} container must be a JSON object mapping definition names to subschemas");
                }
                foreach (KeyValuePair<string, JsonNode> definitionPair in containerObject)
                {
                    if (!seenWireKeys.Add(definitionPair.Key))
                    {
                        throw new SchemaParseException($"The definition '{definitionPair.Key}' exists in both the definitions and the $defs containers");
                    }
                    IdentifierString definitionName = CreateIdentifier(definitionPair.Key, $"definition '{definitionPair.Key}'");
                    IJSBPart definitionPart = ParsePartInternal(definitionName, definitionPair.Value, false, true);
                    result.Add(new KeyValuePair<string, IJSBPart>(definitionPair.Key, definitionPart));
                }
            }
            return result;
        }

        private static void AddDefinitions(IJSBPart part, List<KeyValuePair<string, IJSBPart>> definitions)
        {
            foreach (KeyValuePair<string, IJSBPart> definitionPair in definitions)
            {
                if (part.Definitions.ContainsKey(definitionPair.Key))
                {
                    throw new SchemaParseException($"The definition '{definitionPair.Key}' is defined more than once under '{part.Name}'");
                }
                part.Definitions.Add(definitionPair.Key, definitionPair.Value);
            }
        }

        private static JSBSchemaVersion DetectSchemaVersion(JsonObject root)
        {
            string schemaUri = GetStringOrNull(root, "$schema");
            if (schemaUri == null)
            {
                //Without a $schema the historical draft-7 wire format is assumed
                return JSBSchemaVersion.Draft7;
            }
            foreach (JSBSchemaVersion version in Enum.GetValues(typeof(JSBSchemaVersion)))
            {
                if (string.Equals(schemaUri, version.SchemaUri(), StringComparison.OrdinalIgnoreCase)
                    || string.Equals(schemaUri.TrimEnd('#'), version.SchemaUri().TrimEnd('#'), StringComparison.OrdinalIgnoreCase))
                {
                    return version;
                }
            }
            throw new SchemaParseException($"The $schema dialect '{schemaUri}' is not supported. Supported dialects are draft-07, 2019-09 and 2020-12");
        }

        /// <summary>
        /// Resolves the builder part type of a subschema. Type arrays are only
        /// supported in the two element form [type, null] and are resolved as the
        /// base type marked nullable
        /// </summary>
        private static void ResolveType(JsonObject o, IdentifierString name, out string typeString, out bool isNullableType)
        {
            isNullableType = false;
            if (o.TryGetPropertyValue("type", out JsonNode typeNode))
            {
                string directType = AsStringOrNull(typeNode);
                if (directType != null)
                {
                    typeString = directType;
                    return;
                }
                if (typeNode is JsonArray typeArray)
                {
                    string baseType = null;
                    int nullCount = 0;
                    foreach (JsonNode typeEntry in typeArray)
                    {
                        string entry = AsString(typeEntry, $"type of '{name}'");
                        if (entry.Equals("null"))
                        {
                            nullCount++;
                        }
                        else if (baseType == null)
                        {
                            baseType = entry;
                        }
                        else
                        {
                            throw new SchemaParseException($"The type array of '{name}' contains more than one non-null type ({typeNode.ToJsonString()}). The builder parts cannot represent type unions");
                        }
                    }
                    if (baseType == null || nullCount != 1 || typeArray.Count != 2)
                    {
                        throw new SchemaParseException($"The type array of '{name}' ({typeNode.ToJsonString()}) is not supported. Only the two element form [type, null] can be approximated by the builder parts");
                    }
                    isNullableType = true;
                    typeString = baseType;
                    return;
                }
                throw new SchemaParseException($"The type of '{name}' must be a string or a type array but was: {typeNode.ToJsonString()}");
            }

            //No type keyword: infer from the keywords present
            if (HasAnyKey(o, "properties", "required", "additionalProperties"))
            {
                typeString = "object";
            }
            else if (HasAnyKey(o, "items", "prefixItems", "minItems", "maxItems", "uniqueItems"))
            {
                typeString = "array";
            }
            else if (HasAnyKey(o, "format", "minLength", "maxLength", "pattern"))
            {
                typeString = "string";
            }
            else
            {
                throw new SchemaParseException($"The subschema '{name}' cannot be parsed: the builder part type cannot be determined from the keywords present. Only typed subschemas are supported. Offending schema: {o.ToJsonString()}");
            }
        }

        private static bool IsNullableTypeArray(JsonNode subschema)
        {
            JsonObject o = subschema as JsonObject;
            if (o == null || !o.TryGetPropertyValue("type", out JsonNode typeNode))
            {
                return false;
            }
            if (!(typeNode is JsonArray typeArray))
            {
                return false;
            }
            foreach (JsonNode typeEntry in typeArray)
            {
                string entry = AsStringOrNull(typeEntry);
                if (entry != null && entry.Equals("null"))
                {
                    return true;
                }
            }
            return false;
        }

        private static void CheckUnsupportedKeywords(JsonObject o, IdentifierString name)
        {
            foreach (string unsupportedKeyword in UNSUPPORTED_KEYWORDS)
            {
                if (o.ContainsKey(unsupportedKeyword))
                {
                    throw new SchemaParseException($"The subschema '{name}' uses the keyword '{unsupportedKeyword}' which the builder parts cannot represent without changing the meaning of the schema");
                }
            }
        }

        /// <summary>
        /// True when the keyword next to a $ref is an annotation or a definition
        /// container the builder part can represent, or one of the empty
        /// examples or enum arrays every builder part emits
        /// </summary>
        private static bool IsToleratedRefSibling(JsonObject o, string siblingKey)
        {
            if (siblingKey.Equals("title") || siblingKey.Equals("description") || siblingKey.Equals("$comment")
                || siblingKey.Equals("definitions") || siblingKey.Equals("$defs"))
            {
                return true;
            }
            //Builder emitted JSBRef subschemas carry the type string and the
            //iri formats next to the $ref: exactly that combination is accepted
            //so builder output round trips. Any other type or format next to
            //a $ref changes the meaning of the reference so it still throws
            if (siblingKey.Equals("type"))
            {
                string typeValue = AsStringOrNull(o[siblingKey]);
                return typeValue != null && typeValue.Equals("string");
            }
            if (siblingKey.Equals("format"))
            {
                string formatValue = AsStringOrNull(o[siblingKey]);
                return formatValue != null && (formatValue.Equals("iri") || formatValue.Equals("iri-reference"));
            }
            return IsToleratedEmptyAnnotation(o, siblingKey);
        }

        /// <summary>
        /// True for a default, examples or enum sibling that is null or an empty
        /// array, which is how absent values appear in builder emitted schemas
        /// </summary>
        private static bool IsToleratedEmptyAnnotation(JsonObject o, string siblingKey)
        {
            if (!siblingKey.Equals("default") && !siblingKey.Equals("examples") && !siblingKey.Equals("enum"))
            {
                return false;
            }
            if (!o.TryGetPropertyValue(siblingKey, out JsonNode siblingNode) || siblingNode == null)
            {
                return true;
            }
            if (siblingNode is JsonArray siblingArray)
            {
                return siblingArray.Count == 0;
            }
            return false;
        }

        #region JsonNode access helpers

        private static IEnumerable<string> EnumerateKeys(JsonObject o)
        {
            //Snapshot so callers can throw without invalidating enumeration
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, JsonNode> pair in o)
            {
                keys.Add(pair.Key);
            }
            return keys;
        }

        private static bool HasAnyKey(JsonObject o, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (o.ContainsKey(key))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetStringOrNull(JsonObject o, string key)
        {
            if (!o.TryGetPropertyValue(key, out JsonNode node) || node == null)
            {
                return null;
            }
            return AsStringOrNull(node);
        }

        private static string AsStringOrNull(JsonNode node)
        {
            if (node is JsonValue value && value.TryGetValue<string>(out string stringValue))
            {
                return stringValue;
            }
            return null;
        }

        private static string AsString(JsonNode node, string context)
        {
            string result = AsStringOrNull(node);
            if (result == null)
            {
                throw new SchemaParseException($"The {context} must be a string but was: {node?.ToJsonString() ?? "null"}");
            }
            return result;
        }

        private static List<string> GetStringArray(JsonObject o, string key)
        {
            List<string> result = new List<string>();
            if (!o.TryGetPropertyValue(key, out JsonNode node) || node == null)
            {
                return result;
            }
            if (!(node is JsonArray array))
            {
                throw new SchemaParseException($"The {key} must be an array of strings but was: {node.ToJsonString()}");
            }
            foreach (JsonNode entry in array)
            {
                result.Add(AsString(entry, $"{key} entry"));
            }
            return result;
        }

        private static bool? AsBoolOrNull(JsonNode node)
        {
            if (node is JsonValue value && value.TryGetValue<bool>(out bool boolValue))
            {
                return boolValue;
            }
            return null;
        }

        private static JsonNode GetJsonNodeOrNull(JsonObject o, string key)
        {
            if (!o.TryGetPropertyValue(key, out JsonNode node))
            {
                return null;
            }
            return node;
        }

        /// <summary>
        /// The enum (and const turned into a single value enum) as raw JSON
        /// values. Non-scalar const values throw since the scalar builder parts
        /// cannot represent them
        /// </summary>
        private static List<JsonNode> GetSchemaNodeEnums(JsonObject o)
        {
            List<JsonNode> result = new List<JsonNode>();
            if (o.TryGetPropertyValue("enum", out JsonNode enumNode) && enumNode != null)
            {
                if (!(enumNode is JsonArray enumArray))
                {
                    throw new SchemaParseException($"The enum must be an array but was: {enumNode.ToJsonString()}");
                }
                foreach (JsonNode entry in enumArray)
                {
                    result.Add(entry.DeepClone());
                }
            }
            if (o.TryGetPropertyValue("const", out JsonNode constNode) && constNode != null)
            {
                if (constNode is JsonObject || constNode is JsonArray)
                {
                    throw new SchemaParseException($"A const with a non-scalar value ({constNode.ToJsonString()}) cannot be represented by the builder parts");
                }
                result.Add(constNode.DeepClone());
            }
            return result;
        }

        private static List<JsonNode> GetSchemaNodeList(JsonObject o, string key)
        {
            List<JsonNode> result = new List<JsonNode>();
            if (!o.TryGetPropertyValue(key, out JsonNode node) || node == null)
            {
                return result;
            }
            if (!(node is JsonArray array))
            {
                throw new SchemaParseException($"The {key} must be an array but was: {node.ToJsonString()}");
            }
            foreach (JsonNode entry in array)
            {
                result.Add(entry.DeepClone());
            }
            return result;
        }

        private static uint? GetUInt(JsonObject o, string key, string context)
        {
            if (!o.TryGetPropertyValue(key, out JsonNode node) || node == null)
            {
                return null;
            }
            double number = GetNumber(node, context);
            if (number < 0 || number > uint.MaxValue || number != Math.Floor(number))
            {
                throw new SchemaParseException($"The {context} must be a non-negative whole number but was: {node.ToJsonString()}");
            }
            return (uint)number;
        }

        private static long? GetInteger(JsonObject o, string key, string context)
        {
            if (!o.TryGetPropertyValue(key, out JsonNode node) || node == null)
            {
                return null;
            }
            return (long)GetIntegralNumber(node, context);
        }

        private static double? GetNumber(JsonObject o, string key, string context)
        {
            if (!o.TryGetPropertyValue(key, out JsonNode node) || node == null)
            {
                return null;
            }
            return GetNumber(node, context);
        }

        private static double GetNumber(JsonNode node, string context)
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue<long>(out long longValue))
                {
                    return longValue;
                }
                if (value.TryGetValue<decimal>(out decimal decimalValue))
                {
                    return (double)decimalValue;
                }
                if (value.TryGetValue<double>(out double doubleValue))
                {
                    return doubleValue;
                }
            }
            throw new SchemaParseException($"The {context} must be a JSON number but was: {node?.ToJsonString() ?? "null"}");
        }

        private static long GetIntegralNumber(JsonNode node, string context)
        {
            double number = GetNumber(node, context);
            if (number < long.MinValue || number > long.MaxValue || number != Math.Floor(number))
            {
                throw new SchemaParseException($"The {context} must be a whole number in the integer range but was: {node.ToJsonString()}");
            }
            return (long)number;
        }

        #endregion

        #region Name helpers

        /// <summary>
        /// Converts a wire name (a property key, definition key or url segment)
        /// into a builder part name. Characters that are not ascii letters or
        /// numbers are stripped, a leading digit is prefixed with x and the
        /// result is converted to TitleCase. Emitted wire keys diverge from the
        /// source for keys that are not identifier-like after sanitization
        /// </summary>
        /// <summary>
        /// Chooses the part name for an anonymous subschema position: a single
        /// array item, a tuple position or a composition variant. The title is
        /// the authored name of the subschema so it wins over the positional
        /// fallback: builder emitted schemas carry the original part name as the
        /// title so round trips keep the names and authored documents keep the
        /// intent. The title is skipped when it would duplicate a name already
        /// used in the same collection
        /// </summary>
        private static IdentifierString ChooseAnonymousPartName(IdentifierString parentName, string positionalName,
            JsonNode subschema, HashSet<string> usedNames, string context)
        {
            JsonObject subschemaObject = subschema as JsonObject;
            if (subschemaObject != null)
            {
                string title = GetStringOrNull(subschemaObject, "title");
                if (!string.IsNullOrWhiteSpace(title))
                {
                    IdentifierString titleName = CreateIdentifierOrNull(title);
                    if (titleName != null && usedNames.Add(titleName.ToString()))
                    {
                        return titleName;
                    }
                }
            }
            IdentifierString fallbackName = CreateIdentifier(positionalName, context);
            if (!usedNames.Add(fallbackName.ToString()))
            {
                throw new SchemaParseException($"The subschema names of '{parentName}' collide: '{fallbackName}' is used twice");
            }
            return fallbackName;
        }

        private static IdentifierString CreateIdentifier(string raw, string context)
        {
            string cleaned = SanitizeToIdentifierText(raw);
            if (cleaned == null)
            {
                throw new SchemaParseException($"The name '{raw}' found in {context} cannot be converted into a builder part name. Only ascii letters and numbers are usable after sanitization");
            }
            return new IdentifierString(Capitalize(cleaned));
        }

        private static IdentifierString CreateIdentifierOrNull(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }
            string cleaned = SanitizeToIdentifierText(raw);
            if (cleaned == null)
            {
                return null;
            }
            return new IdentifierString(Capitalize(cleaned));
        }

        private static string SanitizeToIdentifierText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }
            StringBuilder stringBuilder = new StringBuilder();
            foreach (char character in raw)
            {
                if ((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9'))
                {
                    stringBuilder.Append(character);
                }
            }
            string cleaned = stringBuilder.ToString();
            if (cleaned.Length == 0)
            {
                return null;
            }
            if (cleaned[0] >= '0' && cleaned[0] <= '9')
            {
                cleaned = "x" + cleaned;
            }
            return cleaned;
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        /// <summary>
        /// The last url segment of an $id stripped of schema file endings, used
        /// as a scheme name fallback when no title is present
        /// </summary>
        private static string IdFileName(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }
            string segment = id;
            int lastSeparator = segment.LastIndexOfAny(new char[] { '/', '\\', '#' });
            if (lastSeparator >= 0 && lastSeparator < segment.Length - 1)
            {
                segment = segment.Substring(lastSeparator + 1);
            }
            if (segment.EndsWith(".schema.json", StringComparison.OrdinalIgnoreCase))
            {
                segment = segment.Substring(0, segment.Length - ".schema.json".Length);
            }
            else if (segment.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                segment = segment.Substring(0, segment.Length - ".json".Length);
            }
            return segment;
        }

        #endregion
    }
}
