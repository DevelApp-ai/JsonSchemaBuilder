using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// TSQL DDL generator for JSON Schema (Database First approach).
    ///
    /// Maps every registered schema to one SQL file with CREATE TABLE DDL:
    /// - One table per schema, named after the schema and placed in the TSQL
    ///   schema named after the module (created with a guard if missing)
    /// - Object and array properties become child tables with a ParentId
    ///   foreign key back to the owning table (one row per array item)
    /// - AllOf merges its object subschemas into the owning table, first
    ///   property name wins on collisions (same merge as the other generators)
    /// - AnyOf/OneOf have no single column shape and map to NVARCHAR(MAX)
    ///   JSON columns
    /// - References resolve to their underlying part: scalar parts become
    ///   columns of that type, whole object schemas become Id link columns to
    ///   the table of the referenced schema (created by its own SQL file)
    /// - Required parts become NOT NULL, defaults become DEFAULT and enums
    ///   and numeric bounds become CHECK constraints
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
            List<(string fileName, string code)> generatedCode = new List<(string fileName, string code)>();
            foreach (IJsonSchemaDefinition schema in codeGenerator.RegisteredJsonSchemas.Values)
            {
                string fileName = Path.Combine(codeGenerator.ApplicationRoot, schema.Module.ToFilePath, schema.Name + FILE_ENDING);

                TSQL tsql = new TSQL(codeGenerator, schema.Module, schema.Name);
                generatedCode.Add((fileName, tsql.GenerateCode(schema.JsonSchemaBuilderSchema)));
            }
            return generatedCode;
        }

        /// <summary>
        /// Column of a generated table
        /// </summary>
        private class ColumnDefinition
        {
            public string Name { get; set; }
            public string Comment { get; set; }
            public string TypeDeclaration { get; set; }
            public bool IsRequired { get; set; }
            public string DefaultValueSql { get; set; }
            public List<string> Checks { get; } = new List<string>();
        }

        /// <summary>
        /// Generated table. Tables are always built parent first so the
        /// discovery order in the list can create tables as is and must
        /// be dropped in reverse.
        /// </summary>
        private class TableDefinition
        {
            public string Name { get; set; }
            public string Comment { get; set; }
            public TableDefinition Parent { get; set; }
            public List<ColumnDefinition> Columns { get; } = new List<ColumnDefinition>();
        }

        private NamespaceString _startNameSpace;
        private string _moduleName;
        private string _schemaFileName;
        private JSBSchema _rootSchema;
        private CodeGenerator _codeGenerator;
        private List<TableDefinition> _tables = new List<TableDefinition>();

        private TSQL(CodeGenerator codeGenerator, NamespaceString startNameSpace, IdentifierString schemaFileName)
        {
            _codeGenerator = codeGenerator;
            _startNameSpace = startNameSpace;
            _moduleName = startNameSpace.ToString();
            _schemaFileName = schemaFileName.ToString();
        }

        /// <summary>
        /// Generate DDL code from JsonSchemaBuilderSchema
        /// </summary>
        /// <param name="schema"></param>
        /// <returns></returns>
        private string GenerateCode(JSBSchema schema)
        {
            _rootSchema = schema;
            TableDefinition rootTable = CreateTable(_schemaFileName, null, schema.Description);
            if (schema.TopPart != null)
            {
                if (schema.TopPart.PartType == JSBPartType.Object)
                {
                    AddObjectProperties(rootTable, schema.TopPart as JSBObject);
                }
                else
                {
                    //Non object top parts are handled like a property of an encasing
                    //object, which is the same model the other generators use
                    AddProperty(rootTable, schema.TopPart.Name.ToString(), schema.TopPart);
                }
            }
            return Render(schema);
        }

        /// <summary>
        /// Creates a table and adds it to the discovery order (parents before children)
        /// </summary>
        private TableDefinition CreateTable(string tableName, TableDefinition parentTable, string comment)
        {
            TableDefinition tableDefinition = new TableDefinition
            {
                Name = tableName,
                Comment = comment,
                Parent = parentTable
            };
            _tables.Add(tableDefinition);
            return tableDefinition;
        }

        /// <summary>
        /// Adds the properties of an object part as columns/child tables of the table.
        /// When mergedKeys is supplied the AllOf merge semantics apply: the first
        /// property name wins and later subschemas are skipped for that name.
        /// </summary>
        private void AddObjectProperties(TableDefinition table, JSBObject objectPart, HashSet<string> mergedKeys = null)
        {
            foreach (KeyValuePair<IdentifierString, IJSBPart> pair in objectPart.Properties)
            {
                string key = pair.Key.ToString();
                if (mergedKeys != null && !mergedKeys.Add(key))
                {
                    continue;
                }
                AddProperty(table, key, pair.Value);
            }
        }

        /// <summary>
        /// Maps one part to the relational model of the table. For resolved
        /// references the requiredness of the reference property wins over the
        /// requiredness of the underlying part.
        /// </summary>
        private void AddProperty(TableDefinition table, string key, IJSBPart part, bool? isRequiredOverride = null)
        {
            switch (part.PartType)
            {
                case JSBPartType.Object:
                    //Objects become their own child table with a ParentId back to the owner
                    TableDefinition childTable = CreateTable(table.Name + _codeGenerator.TransformToTitleCase(key), table, part.Description);
                    AddObjectProperties(childTable, part as JSBObject);
                    break;
                case JSBPartType.Array:
                    AddArrayProperty(table, key, part as JSBArray);
                    break;
                case JSBPartType.AllOf:
                    AddAllOfProperty(table, key, part as JSBAllOf);
                    break;
                case JSBPartType.AnyOf:
                case JSBPartType.OneOf:
                    //No single column shape exists; the JSON representation is stored
                    AddJsonColumn(table, _codeGenerator.TransformToTitleCase(key), part.Description, isRequiredOverride ?? part.IsRequired);
                    break;
                case JSBPartType.IriReference:
                    AddReferenceProperty(table, key, part as JSBRef);
                    break;
                case JSBPartType.Schema:
                    //A whole schema part resolves through its top part
                    JSBSchema referencedSchema = part as JSBSchema;
                    if (referencedSchema != null && referencedSchema.TopPart != null)
                    {
                        AddProperty(table, key, referencedSchema.TopPart, isRequiredOverride);
                    }
                    break;
                default:
                    AddScalarColumn(table, _codeGenerator.TransformToTitleCase(key), part, isRequiredOverride);
                    break;
            }
        }

        /// <summary>
        /// Arrays become a child table with one row per item
        /// </summary>
        private void AddArrayProperty(TableDefinition table, string key, JSBArray arrayPart)
        {
            if (arrayPart.Items.Count != 1)
            {
                throw new NotImplementedException($"TSQL DDL generation only supports arrays with a single item part ({arrayPart.Name})");
            }
            TableDefinition childTable = CreateTable(table.Name + _codeGenerator.TransformToTitleCase(key), table, arrayPart.Description);
            AddArrayItem(childTable, key, arrayPart.Items[0]);
        }

        /// <summary>
        /// Maps the item part of an array to the child table of the array
        /// </summary>
        private void AddArrayItem(TableDefinition childTable, string key, IJSBPart item, bool? isRequiredOverride = null)
        {
            switch (item.PartType)
            {
                case JSBPartType.Object:
                    AddObjectProperties(childTable, item as JSBObject);
                    break;
                case JSBPartType.AllOf:
                    AddAllOfProperty(childTable, key, item as JSBAllOf);
                    break;
                case JSBPartType.Array:
                    //Arrays of arrays get an anchor table and the inner array becomes its child
                    AddArrayProperty(childTable, item.Name.ToString(), item as JSBArray);
                    break;
                case JSBPartType.AnyOf:
                case JSBPartType.OneOf:
                    AddJsonColumn(childTable, "Value", item.Description, isRequiredOverride ?? item.IsRequired);
                    break;
                case JSBPartType.IriReference:
                    AddReferenceItem(childTable, item as JSBRef);
                    break;
                case JSBPartType.Schema:
                    JSBSchema referencedSchema = item as JSBSchema;
                    if (referencedSchema != null && referencedSchema.TopPart != null)
                    {
                        AddArrayItem(childTable, key, referencedSchema.TopPart, isRequiredOverride);
                    }
                    break;
                default:
                    //Scalar array items are stored in the Value column
                    AddScalarColumn(childTable, "Value", item, isRequiredOverride);
                    break;
            }
        }

        /// <summary>
        /// AllOf merges its object subschemas into the owning table like the other
        /// generators do. If a subschema is not an object the merge is not possible
        /// and the part degrades to a JSON column.
        /// </summary>
        private void AddAllOfProperty(TableDefinition table, string key, JSBAllOf allOfPart)
        {
            bool allSubSchemasAreObjects = allOfPart.Schemas.Count > 0
                && allOfPart.Schemas.All(subSchema => subSchema.PartType == JSBPartType.Object);
            if (allSubSchemasAreObjects)
            {
                HashSet<string> mergedKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (IJSBPart subSchema in allOfPart.Schemas)
                {
                    AddObjectProperties(table, subSchema as JSBObject, mergedKeys);
                }
            }
            else
            {
                AddJsonColumn(table, _codeGenerator.TransformToTitleCase(key), allOfPart.Description, allOfPart.IsRequired);
            }
        }

        /// <summary>
        /// Resolves a reference to its underlying part. Whole file references to
        /// registered schemas also return the schema so the link column can point
        /// at the table that the referenced schema generates in its own file.
        /// </summary>
        private (IJSBPart refPart, IJsonSchemaDefinition wholeSchema) ResolveReference(JSBRef jSBRef)
        {
            string relativeLocalFileWithExpandedDot = ExpandRelativelocalFile(jSBRef.RelativeLocalFile);
            if (string.IsNullOrWhiteSpace(relativeLocalFileWithExpandedDot))
            {
                //Fragment only references resolve against the schema being generated.
                //The registry is keyed by the definition FileName, which is derived from
                //the definition class name and can differ from the internal schema
                //name, so the registered FileName is used when the schema is registered
                if (!_codeGenerator.TryGetRegisteredFileName(_rootSchema, out string registeredFileName))
                {
                    registeredFileName = Path.Combine(_startNameSpace.ToFilePath, _rootSchema.Name + ".schema.json");
                }
                relativeLocalFileWithExpandedDot = registeredFileName;
            }
            string lookupKey = _codeGenerator.TransformToCamelCase(relativeLocalFileWithExpandedDot);
            if (_codeGenerator.RegisteredJsonSchemas.TryGetValue(lookupKey, out IJsonSchemaDefinition registeredSchema))
            {
                if (jSBRef.Fragment.ToLowerInvariant().StartsWith("/definitions/"))
                {
                    //Shared resolution supports nested definition paths like
                    //#/definitions/outer/definitions/inner as well
                    IJSBPart definitionPart = _codeGenerator.LookupReferencedPartFromDefinitions(registeredSchema.JsonSchemaBuilderSchema, jSBRef.Fragment);
                    if (definitionPart != null)
                    {
                        return (definitionPart, null);
                    }
                    return (null, null);
                }
                return (registeredSchema.JsonSchemaBuilderSchema, registeredSchema);
            }
            //Only registered schemas can be mapped to DDL; external schema files on
            //disk have no code generation model to map from
            throw new NotImplementedException($"TSQL DDL generation is only supported for references to registered schemas ({jSBRef.RelativeLocalFile})");
        }

        /// <summary>
        /// Maps a reference property. Scalar parts resolve to a column of that
        /// type; whole object schemas become a link column to the table of the
        /// referenced schema, which is created by its own SQL file (therefore no
        /// foreign key constraint is emitted for it).
        /// </summary>
        private void AddReferenceProperty(TableDefinition table, string key, JSBRef referencePart)
        {
            (IJSBPart refPart, IJsonSchemaDefinition wholeSchema) = ResolveReference(referencePart);
            if (wholeSchema != null)
            {
                JSBSchema referencedSchema = wholeSchema.JsonSchemaBuilderSchema;
                if (referencedSchema.TopPart == null)
                {
                    return;
                }
                if (referencedSchema.TopPart.PartType == JSBPartType.Object)
                {
                    AddLinkColumn(table, _codeGenerator.TransformToTitleCase(key) + "Id", referencePart.Description, wholeSchema);
                    return;
                }
                //The requiredness of the reference property wins over the underlying part
                AddProperty(table, key, referencedSchema.TopPart, referencePart.IsRequired);
                return;
            }
            if (refPart == null)
            {
                throw new CodeGenerationException($"Could not find reference to {referencePart.RelativeLocalFile}");
            }
            AddProperty(table, key, refPart, referencePart.IsRequired);
        }

        /// <summary>
        /// Maps the reference item of an array to the child table of the array
        /// </summary>
        private void AddReferenceItem(TableDefinition childTable, JSBRef referencePart)
        {
            (IJSBPart refPart, IJsonSchemaDefinition wholeSchema) = ResolveReference(referencePart);
            if (wholeSchema != null)
            {
                JSBSchema referencedSchema = wholeSchema.JsonSchemaBuilderSchema;
                if (referencedSchema.TopPart == null)
                {
                    return;
                }
                if (referencedSchema.TopPart.PartType == JSBPartType.Object)
                {
                    AddLinkColumn(childTable, _codeGenerator.TransformToTitleCase(referencePart.Name.ToString()) + "Id", referencePart.Description, wholeSchema);
                    return;
                }
                //The requiredness of the reference property wins over the underlying part
                AddArrayItem(childTable, referencePart.Name.ToString(), referencedSchema.TopPart, referencePart.IsRequired);
                return;
            }
            if (refPart == null)
            {
                throw new CodeGenerationException($"Could not find reference to {referencePart.RelativeLocalFile}");
            }
            AddArrayItem(childTable, referencePart.Name.ToString(), refPart, referencePart.IsRequired);
        }

        /// <summary>
        /// Link column to the table of a referenced schema
        /// </summary>
        private void AddLinkColumn(TableDefinition table, string columnName, string description, IJsonSchemaDefinition wholeSchema)
        {
            table.Columns.Add(new ColumnDefinition
            {
                Name = columnName,
                Comment = description + $" (link to [{wholeSchema.Module}].[{wholeSchema.Name}])",
                TypeDeclaration = "BIGINT",
                IsRequired = false
            });
        }

        /// <summary>
        /// Column holding the JSON representation of a part with no single column shape
        /// </summary>
        private void AddJsonColumn(TableDefinition table, string columnName, string description, bool isRequired)
        {
            table.Columns.Add(new ColumnDefinition
            {
                Name = columnName,
                Comment = description,
                TypeDeclaration = "NVARCHAR(MAX)",
                IsRequired = isRequired
            });
        }

        /// <summary>
        /// Maps a scalar part to a column with type, default and check constraints
        /// </summary>
        private void AddScalarColumn(TableDefinition table, string columnName, IJSBPart part, bool? isRequiredOverride = null)
        {
            ColumnDefinition column = new ColumnDefinition
            {
                Name = columnName,
                Comment = part.Description,
                IsRequired = isRequiredOverride ?? part.IsRequired
            };
            switch (part.PartType)
            {
                case JSBPartType.String:
                case JSBPartType.Email:
                case JSBPartType.PhoneNumber:
                case JSBPartType.Uri:
                case JSBPartType.Base64String:
                case JSBPartType.DataString:
                    JSBString stringPart = part as JSBString;
                    column.TypeDeclaration = MakeStringTypeDeclaration(part.PartType, stringPart.MaxLength);
                    if (!string.IsNullOrEmpty(stringPart.DefaultValue))
                    {
                        column.DefaultValueSql = $"DEFAULT N'{EscapeSqlStringLiteral(stringPart.DefaultValue)}'";
                    }
                    if (stringPart.Enums.Count > 0)
                    {
                        string enumValues = string.Join(", ", stringPart.Enums.Select(enumValue => $"N'{EscapeSqlStringLiteral(enumValue)}'"));
                        column.Checks.Add($"CHECK ([{columnName}] IN ({enumValues}))");
                    }
                    if (stringPart.MinLength > 0)
                    {
                        column.Checks.Add($"CHECK (LEN([{columnName}]) >= {stringPart.MinLength.ToString(CultureInfo.InvariantCulture)})");
                    }
                    break;
                case JSBPartType.Guid:
                    JSBString guidPart = part as JSBString;
                    column.TypeDeclaration = "UNIQUEIDENTIFIER";
                    if (!string.IsNullOrEmpty(guidPart.DefaultValue))
                    {
                        column.DefaultValueSql = $"DEFAULT '{EscapeSqlStringLiteral(guidPart.DefaultValue)}'";
                    }
                    break;
                case JSBPartType.Date:
                    JSBString datePart = part as JSBString;
                    column.TypeDeclaration = "DATE";
                    if (!string.IsNullOrEmpty(datePart.DefaultValue))
                    {
                        column.DefaultValueSql = $"DEFAULT '{EscapeSqlStringLiteral(datePart.DefaultValue)}'";
                    }
                    break;
                case JSBPartType.DateTime:
                    JSBString dateTimePart = part as JSBString;
                    column.TypeDeclaration = "DATETIMEOFFSET";
                    if (!string.IsNullOrEmpty(dateTimePart.DefaultValue))
                    {
                        column.DefaultValueSql = $"DEFAULT '{EscapeSqlStringLiteral(dateTimePart.DefaultValue)}'";
                    }
                    break;
                case JSBPartType.Time:
                    JSBString timePart = part as JSBString;
                    column.TypeDeclaration = "TIME";
                    if (!string.IsNullOrEmpty(timePart.DefaultValue))
                    {
                        column.DefaultValueSql = $"DEFAULT '{EscapeSqlStringLiteral(StripTimeOffset(timePart.DefaultValue))}'";
                    }
                    break;
                case JSBPartType.Integer:
                    JSBInteger integerPart = part as JSBInteger;
                    column.TypeDeclaration = "BIGINT";
                    if (integerPart.DefaultValue.HasValue)
                    {
                        column.DefaultValueSql = $"DEFAULT {integerPart.DefaultValue.Value.ToString(CultureInfo.InvariantCulture)}";
                    }
                    List<long?> integerEnums = integerPart.Enums.Where(enumValue => enumValue.HasValue).ToList();
                    if (integerEnums.Count > 0)
                    {
                        string enumValues = string.Join(", ", integerEnums.Select(enumValue => enumValue.Value.ToString(CultureInfo.InvariantCulture)));
                        column.Checks.Add($"CHECK ([{columnName}] IN ({enumValues}))");
                    }
                    if (integerPart.Minimum.HasValue)
                    {
                        column.Checks.Add($"CHECK ([{columnName}] >= {integerPart.Minimum.Value.ToString(CultureInfo.InvariantCulture)})");
                    }
                    if (integerPart.Maximum.HasValue)
                    {
                        column.Checks.Add($"CHECK ([{columnName}] <= {integerPart.Maximum.Value.ToString(CultureInfo.InvariantCulture)})");
                    }
                    break;
                case JSBPartType.Number:
                    JSBNumber numberPart = part as JSBNumber;
                    column.TypeDeclaration = "FLOAT";
                    if (numberPart.DefaultValue.HasValue)
                    {
                        column.DefaultValueSql = $"DEFAULT {numberPart.DefaultValue.Value.ToString(CultureInfo.InvariantCulture)}";
                    }
                    List<double?> numberEnums = numberPart.Enums.Where(enumValue => enumValue.HasValue).ToList();
                    if (numberEnums.Count > 0)
                    {
                        string enumValues = string.Join(", ", numberEnums.Select(enumValue => enumValue.Value.ToString(CultureInfo.InvariantCulture)));
                        column.Checks.Add($"CHECK ([{columnName}] IN ({enumValues}))");
                    }
                    if (numberPart.Minimum.HasValue)
                    {
                        column.Checks.Add($"CHECK ([{columnName}] >= {numberPart.Minimum.Value.ToString(CultureInfo.InvariantCulture)})");
                    }
                    if (numberPart.Maximum.HasValue)
                    {
                        column.Checks.Add($"CHECK ([{columnName}] <= {numberPart.Maximum.Value.ToString(CultureInfo.InvariantCulture)})");
                    }
                    break;
                case JSBPartType.Boolean:
                    JSBBoolean booleanPart = part as JSBBoolean;
                    column.TypeDeclaration = "BIT";
                    if (booleanPart.DefaultValue.HasValue)
                    {
                        column.DefaultValueSql = booleanPart.DefaultValue.Value ? "DEFAULT 1" : "DEFAULT 0";
                    }
                    if (booleanPart.Enums.Count > 0)
                    {
                        //BIT stores 0/1, a NULL enum member is covered by the nullable column
                        column.Checks.Add($"CHECK ([{columnName}] IN (0, 1))");
                    }
                    break;
                default:
                    throw new NotImplementedException($"TSQL DDL generation for {part.PartType} has not been implemented");
            }
            table.Columns.Add(column);
        }

        /// <summary>
        /// NVARCHAR type declaration with the default length per string format
        /// part. Without MaxLength the part specific defaults are used:
        /// Email 320 (RFC length limit), PhoneNumber 50 and Uri 2048; ordinary
        /// strings without MaxLength are NVARCHAR(MAX).
        /// </summary>
        private string MakeStringTypeDeclaration(JSBPartType partType, uint? maxLength)
        {
            if (maxLength.HasValue)
            {
                return $"NVARCHAR({maxLength.Value.ToString(CultureInfo.InvariantCulture)})";
            }
            switch (partType)
            {
                case JSBPartType.Email:
                    return "NVARCHAR(320)";
                case JSBPartType.PhoneNumber:
                    return "NVARCHAR(50)";
                case JSBPartType.Uri:
                    return "NVARCHAR(2048)";
                default:
                    return "NVARCHAR(MAX)";
            }
        }

        /// <summary>
        /// The Time part carries the default value with an UTC offset that a TIME
        /// column cannot store, so the offset is stripped
        /// </summary>
        private string StripTimeOffset(string timeValue)
        {
            int offsetIndex = timeValue.IndexOf('+');
            int minusIndex = timeValue.IndexOf('-', 1);
            if (minusIndex > -1 && (offsetIndex == -1 || minusIndex < offsetIndex))
            {
                offsetIndex = minusIndex;
            }
            if (offsetIndex > -1)
            {
                return timeValue.Substring(0, offsetIndex);
            }
            return timeValue;
        }

        /// <summary>
        /// Doubles single quotes so the value can be used in a TSQL literal
        /// </summary>
        private string EscapeSqlStringLiteral(string value)
        {
            return value.Replace("'", "''");
        }

        /// <summary>
        /// Expand local file if needed
        /// </summary>
        private string ExpandRelativelocalFile(string relativeLocalFile)
        {
            if (relativeLocalFile.StartsWith("./") || relativeLocalFile.StartsWith(".\\"))
            {
                return Path.Combine(_startNameSpace.ToFilePath, relativeLocalFile.Remove(0, 2));
            }
            else
            {
                return relativeLocalFile;
            }
        }

        #region Rendering

        /// <summary>
        /// Renders the DDL: schema guard, drops in reverse creation order and
        /// creates in creation order so foreign keys always reference an
        /// existing table
        /// </summary>
        private string Render(JSBSchema schema)
        {
            CodeBuilder codeBuilder = new CodeBuilder();
            codeBuilder
                .L("-- ============================================================================")
                .L($"-- Table DDL for schema: {_schemaFileName}")
                .L($"-- Description: {schema.Description}")
                .L($"-- Module: {_moduleName}")
                .L("-- Generated with JsonSchemaBuilder (TSQL DDL, Database First)")
                .L("-- ----------------------------------------------------------------------------")
                .L("-- Storage model:")
                .L("--   One table per schema, named after the schema and placed in the TSQL")
                .L("--   schema named after the module.")
                .L("--   Object and array properties become child tables with a ParentId")
                .L("--   foreign key back to the owning table (one row per array item).")
                .L("--   AllOf merges its object subschemas into the owning table.")
                .L("--   AnyOf/OneOf map to NVARCHAR(MAX) JSON columns.")
                .L("--   References resolve to their underlying part; references to whole")
                .L("--   object schemas become Id link columns to the table of the referenced")
                .L("--   schema, which is created by its own SQL file.")
                .L("-- ============================================================================")
                .EmptyLine()
                .L($"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'{EscapeSqlStringLiteral(_moduleName)}')")
                .L($"    EXEC(N'CREATE SCHEMA [{_moduleName}];');")
                .L("GO")
                .EmptyLine();

            //Drops must run child tables first, so the reverse of the creation order
            codeBuilder.L("-- Drop child tables before their parents (reverse creation order)");
            for (int tableIndex = _tables.Count - 1; tableIndex >= 0; tableIndex--)
            {
                TableDefinition tableDefinition = _tables[tableIndex];
                codeBuilder
                    .L($"IF OBJECT_ID(N'[{_moduleName}].[{tableDefinition.Name}]', N'U') IS NOT NULL")
                    .L($"    DROP TABLE [{_moduleName}].[{tableDefinition.Name}];")
                    .L("GO")
                    .EmptyLine();
            }

            //Creation runs parents first so foreign keys always reference an existing table
            foreach (TableDefinition tableDefinition in _tables)
            {
                RenderCreateTable(codeBuilder, tableDefinition);
            }
            return codeBuilder.Build();
        }

        /// <summary>
        /// Renders one CREATE TABLE statement
        /// </summary>
        private void RenderCreateTable(CodeBuilder codeBuilder, TableDefinition tableDefinition)
        {
            if (!string.IsNullOrWhiteSpace(tableDefinition.Comment))
            {
                codeBuilder.L($"-- {tableDefinition.Comment}");
            }
            codeBuilder
                .L($"CREATE TABLE [{_moduleName}].[{tableDefinition.Name}]")
                .L("(")
                .IndentIncrease()
                .L("[Id] BIGINT IDENTITY(1,1) NOT NULL,");
            if (tableDefinition.Parent != null)
            {
                codeBuilder.L("[ParentId] BIGINT NOT NULL,");
            }
            foreach (ColumnDefinition column in tableDefinition.Columns)
            {
                if (!string.IsNullOrWhiteSpace(column.Comment))
                {
                    codeBuilder.L($"-- {column.Comment}");
                }
                string nullAbility = column.IsRequired ? "NOT NULL" : "NULL";
                string constraints = string.Empty;
                if (!string.IsNullOrEmpty(column.DefaultValueSql))
                {
                    constraints += " " + column.DefaultValueSql;
                }
                foreach (string check in column.Checks)
                {
                    constraints += " " + check;
                }
                codeBuilder.L($"[{column.Name}] {column.TypeDeclaration} {nullAbility}{constraints},");
            }
            bool hasForeignKey = tableDefinition.Parent != null;
            codeBuilder.L($"CONSTRAINT [PK_{tableDefinition.Name}] PRIMARY KEY ([Id])" + (hasForeignKey ? "," : string.Empty));
            if (hasForeignKey)
            {
                codeBuilder.L($"CONSTRAINT [FK_{tableDefinition.Name}] FOREIGN KEY ([ParentId]) REFERENCES [{_moduleName}].[{tableDefinition.Parent.Name}] ([Id])");
            }
            codeBuilder
                .IndentDecrease()
                .L(");")
                .L("GO")
                .EmptyLine();
        }

        #endregion
    }
}
