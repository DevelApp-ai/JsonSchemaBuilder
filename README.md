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
  support. Remote references are downloaded and fully parsed with the opt-in pinned flow
  described under Remote schema code generation below.

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

## Parsing schema documents
`JSBSchemaParser.ParseDocument` turns a schema document given as JSON into the builder part
tree, and `ParsePart` parses a single subschema. Both `definitions` and `$defs` containers are
accepted on every level, parsed definitions keep the wire keys they were authored with so
fragment references into them resolve, and the document dialect is detected from `$schema`.

The parser represents what the builder parts can express, so keywords the parts cannot carry
fail LOUD instead of silently changing the meaning of the schema: `patternProperties`,
`propertyNames`, `if`/`then`/`else`, `not`, `contains`, `dependencies`/`dependentSchemas`/
`dependentRequired`, `minProperties`/`maxProperties`, `exclusiveMin/Max`, `unevaluated*`,
`additionalItems`, `$dynamicRef`/`$recursiveRef`, non scalar `const`, boolean schemas,
compositions combined with other body keywords and `$ref` with constraint siblings.

Rounding behavior to know about:
* a type array of `[type, null]` is approximated as the base type marked optional (the parts
  cannot express required but nullable)
* typed formats (`date`, `time`, `date-time`, `email`, `uuid`, `uri`, `byte`) map to their
  typed parts only when unconstrained. A carried default is mapped into the typed part only
  when the part can re-emit it unchanged: string defaults must match the native pattern and
  `date`/`time`/`date-time` defaults must be the exact canonical emission (UTC offset),
  otherwise the value stays a plain `JSBString` that carries format, pattern and default
  wire-faithfully
* anonymous positions (array items, tuple positions, composition variants) take their part
  name from the subschema `title` when present (builder emitted schemas round trip their part
  names this way); without a title a stable positional name is generated
* wire keys that are not identifier like are sanitized for the part name, so the re-emitted
  document diverges from the source for exotic keys
* emitted schemas carry a `description` on every part, so descriptions default to a
  generated text when the source subschema has none

## Remote schema code generation
Remote references are downloaded, parsed into the SAME builder part tree and code generated
from them like local schemas. Because a remote download silently changes what the generated
code means, both are opt-in on the `CodeGenerator`:

* `EnableRemoteSchemas` must be set to `true` (default `false`; a remote reference without the
  opt-in fails with an instructive exception)
* `RemoteSchemaDownloader` (default `HttpSchemaDownloader`) performs the downloads and can be
  replaced for testing

The first opt-in download writes a pin file next to the mirrored schema file recording the url
and the SHA-256 of the downloaded content. Every later run downloads again (the flow expects
network) and verifies the content against the pin: a hash mismatch is a HARD failure so
upstream changes can never silently alter the generated code. To accept a changed remote
schema delete the pin file and the mirror, then re-run. Remote schemas that reference further
remote schemas resolve to a fixpoint, and an unparsable remote schema fails loud with the url
in the message.

## TODO
* Builder parts for the later dialect advanced keywords ($anchor/$dynamicRef/$recursiveRef, dependentSchemas/dependentRequired, unevaluatedProperties/unevaluatedItems)
