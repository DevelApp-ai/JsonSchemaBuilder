# JsonSchemaBuilder
Simple Json Schema builder to make it faster to combine in code and to generate code for the schema making it possible to use schema first.

As of version 2.0 the builder runs on [json-everything](https://github.com/gregsdennis/json-everything) (JsonSchema.Net) with [System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json-overview)
instead of Manatee.Json. The default wire format stays draft 7: the same keyword shapes and the `definitions` vocabulary,
so existing schemas and downstream consumers do not change. Building a part returns a `Json.Schema.JsonSchema`; every
part also exposes `AsJsonSchemaBuilder()` so schemas can be composed before conversion. Schema parts register through
`IJSBPart.AsJsonSchema()`, and references must be well formed RFC 3986 uri references since references are resolved
eagerly with System.Uri.

## Schema dialects (draft 7, 2019-09, 2020-12)
The default dialect is draft 7 so existing schemas keep their exact wire format. Later dialects are opt-in per schema
with `JSBSchemaVersion`:

```csharp
JSBSchema schema = new JSBSchema("MySchema", "Description", topPart,
    schemaVersion: JSBSchemaVersion.Draft202012);
```

* Draft7 (default): `definitions` container, tuple form `items`, `http://json-schema.org/draft-07/schema#`
* Draft201909: `$defs` container, tuple form `items` still allowed, `https://json-schema.org/draft/2019-09/schema`
* Draft202012: `$defs` container, tuple form `items` replaced by `prefixItems`, `https://json-schema.org/draft/2020-12/schema`

Individual parts can also be built for a later dialect with `AsJsonSchema(JSBSchemaVersion)` and
`AsJsonSchemaBuilder(JSBSchemaVersion)` so they can be embedded in a schema of that dialect. A `JSBSchema` carries its
own dialect: its `SchemaVersion` wins no matter which overload builds it. Reference fragments resolve leniently
against both `/definitions/` and `/$defs/` containers.

Not yet covered by builder parts (left as TODO): `$anchor`/`$dynamicRef`/`$recursiveRef`, `dependentSchemas`/
`dependentRequired`, `unevaluatedProperties`/`unevaluatedItems`.

Why another code generation tool from Json Schema ? Mainly the ones made are either supporting a too old version of Json Schema or generated code with the wrong structure. I also needed dynamic code generation to make dynamic API based on defined messages for making dynamic MVC controller for a message based system so that an API can be supported on the system for others to interface with.


This has mainly been made to support my [Workflow project](https://github.com/DevelApp-dk/Workflow) which works on schema first principle.

## Schema parts supported
My intention is to support all parts of the official Json Schema Draft 7 and some special formats that are used often

* **AnyOf/OneOf/AllOf**

  Composition parts. AnyOf/OneOf are emitted as JSON Schema composition keywords; AllOf is additionally folded into the owning structure where the target language supports it.
* **Array**
* **Boolean**
* **Date**
* **DateTime**
* **Email**
* **Integer**
* **Number**
* **Object**
* **Schema**

  This is a bit different in that it only wraps one of the other parts as a  top part and otherwise handles definitions as they can only be handled on the schema level
* **String**
* **Time**
* **Uri/UriReference/Iri/IriReference**

  References support RFC 6901 JSON Pointer fragments (`#/definitions/...`, `#/properties/...`,
  `#/allOf/0` and any other pointer into the schema) as well as entire schemas. Fragments that
  resolve to a builder part use that part directly; fragments that resolve deeper into the
  emitted schema json (for example into `properties` or an `allOf` entry) map the raw subschema:
  objects are emitted as a raw class/interface with the schema `required` list driving
  nullability, arrays map to their item type and formats map like their builder part
  counterparts. References that stay inside a `definitions` container keep the historical
  lenient key matching (exact, then TitleCase, then camelCase).

  Limitations: raw enum subschemas map to their base type, raw `allOf`/`anyOf`/`oneOf`
  subschemas are not merged (NotImplemented), arrays without a single item schema are not
  supported, the TSQL generator resolves only references that map to registered builder parts
  (whole schemas and `/definitions/` fragments), and the C# Entities generator has no reference
  support. Remote schemas are downloaded but code is not generated from them.

## Code generation
Code generation is built based on the above schema parts. The generation is done in batch mode

### Supported languages
* C# (serialization classes with a serializer choice: Newtonsoft.Json, System.Text.Json, or Akka.NET Hyperion)
* C# Entities for Code First approach to development
* TypeScript
* PHP
* VB
* Java
* TSQL DDL for Database First approach to development

## TODO
* Builder parts for the later dialect advanced keywords ($anchor/$dynamicRef/$recursiveRef, dependentSchemas/dependentRequired, unevaluatedProperties/unevaluatedItems)
* Remote schema code generation support
