using DevelApp.JsonSchemaBuilder;
using System;
using System.Collections.Generic;
using Xunit;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using System.Linq;
using DevelApp.JsonSchemaBuilder.CodeGeneration;
using System.IO;

namespace JsonSchemaBuilder.Test
{
    public class TestSchemaAndCodeGeneration
    {
        [Fact]
        public void BuildFromAssembly()
        {
            string pathString = ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar;
            string assemblyPath = GetType().Assembly.Location;
            string pathStringExpanded = Path.GetFullPath(pathString, assemblyPath);
            string jsonSchemaApplicationRoot = Path.Combine(pathStringExpanded, "JsonSchema");
            LoadAllJsonSchemaBuildersAndWriteSchemasToFile(jsonSchemaApplicationRoot);
            string applicationRoot = Path.Combine(pathStringExpanded, "CSharp");
            LoadAllJsonSchemaBuildersAndGenerateCSharpCodeToFile(applicationRoot, jsonSchemaApplicationRoot);
        }

        [Fact]
        public void BuildFromAssemblyEntities()
        {
            string pathString = ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar;
            string assemblyPath = GetType().Assembly.Location;
            string pathStringExpanded = Path.GetFullPath(pathString, assemblyPath);
            string jsonSchemaApplicationRoot = Path.Combine(pathStringExpanded, "JsonSchema");
            LoadAllJsonSchemaBuildersAndWriteSchemasToFile(jsonSchemaApplicationRoot);
            string applicationRoot = Path.Combine(pathStringExpanded, "CSharpEntities");
            LoadAllJsonSchemaBuildersAndGenerateCSharpEntitiesCodeToFile(applicationRoot, jsonSchemaApplicationRoot);
        }

        [Fact]
        public void BuildFromAssemblyTypeScript()
        {
            string pathString = ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar;
            string assemblyPath = GetType().Assembly.Location;
            string pathStringExpanded = Path.GetFullPath(pathString, assemblyPath);
            string jsonSchemaApplicationRoot = Path.Combine(pathStringExpanded, "JsonSchema");
            LoadAllJsonSchemaBuildersAndWriteSchemasToFile(jsonSchemaApplicationRoot);
            string applicationRoot = Path.Combine(pathStringExpanded, "TypeScript");
            LoadAllJsonSchemaBuildersAndGenerateTypeScriptCodeToFile(applicationRoot, jsonSchemaApplicationRoot);
        }

        [Fact]
        public void BuildFromAssemblyPHP()
        {
            string pathString = ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar;
            string assemblyPath = GetType().Assembly.Location;
            string pathStringExpanded = Path.GetFullPath(pathString, assemblyPath);
            string jsonSchemaApplicationRoot = Path.Combine(pathStringExpanded, "JsonSchema");
            LoadAllJsonSchemaBuildersAndWriteSchemasToFile(jsonSchemaApplicationRoot);
            string applicationRoot = Path.Combine(pathStringExpanded, "PHP");
            LoadAllJsonSchemaBuildersAndGeneratePHPCodeToFile(applicationRoot, jsonSchemaApplicationRoot);
        }

        [Fact]
        public void BuildFromAssemblyVB()
        {
            string pathString = ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar;
            string assemblyPath = GetType().Assembly.Location;
            string pathStringExpanded = Path.GetFullPath(pathString, assemblyPath);
            string jsonSchemaApplicationRoot = Path.Combine(pathStringExpanded, "JsonSchema");
            LoadAllJsonSchemaBuildersAndWriteSchemasToFile(jsonSchemaApplicationRoot);
            string applicationRoot = Path.Combine(pathStringExpanded, "VB");
            LoadAllJsonSchemaBuildersAndGenerateVBCodeToFile(applicationRoot, jsonSchemaApplicationRoot);
        }

        [Fact]
        public void BuildFromAssemblyJava()
        {
            string pathString = ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar;
            string assemblyPath = GetType().Assembly.Location;
            string pathStringExpanded = Path.GetFullPath(pathString, assemblyPath);
            string jsonSchemaApplicationRoot = Path.Combine(pathStringExpanded, "JsonSchema");
            LoadAllJsonSchemaBuildersAndWriteSchemasToFile(jsonSchemaApplicationRoot);
            string applicationRoot = Path.Combine(pathStringExpanded, "Java");
            LoadAllJsonSchemaBuildersAndGenerateJavaCodeToFile(applicationRoot, jsonSchemaApplicationRoot);
        }

        [Fact]
        public void BuildFromAssemblyTSQL()
        {
            string pathString = ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar;
            string assemblyPath = GetType().Assembly.Location;
            string pathStringExpanded = Path.GetFullPath(pathString, assemblyPath);
            string jsonSchemaApplicationRoot = Path.Combine(pathStringExpanded, "JsonSchema");
            LoadAllJsonSchemaBuildersAndWriteSchemasToFile(jsonSchemaApplicationRoot);
            string applicationRoot = Path.Combine(pathStringExpanded, "TSQL");
            LoadAllJsonSchemaBuildersAndGenerateTSQLCodeToFile(applicationRoot, jsonSchemaApplicationRoot);
        }

        [Fact]
        public void TestCSharpEntitiesCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new CompositionTestJsonSchema());
            codeGenerator.Register(new StringEnumAsTopPartJsonSchema());
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.CSharp_Entities);

            (string fileName, string code) compositionTest = generated.FirstOrDefault(g => g.fileName.EndsWith("CompositionTest.cs"));
            Assert.False(string.IsNullOrEmpty(compositionTest.code), "Expected generated entities code for CompositionTest.cs");
            //Entity classes keep the class and property structure of the serialization classes
            Assert.Contains("public partial class CompositionTest", compositionTest.code);
            Assert.Contains("public partial class Combined", compositionTest.code);
            Assert.Contains("public Combined CombinedProp { get; set; }", compositionTest.code);
            Assert.Contains("public object AnyChoice { get; set; }", compositionTest.code);
            //Serialization specifics are removed for Code First entities
            Assert.DoesNotContain("JsonProperty", compositionTest.code);
            Assert.DoesNotContain("Newtonsoft", compositionTest.code);
            Assert.DoesNotContain("Ardalis", compositionTest.code);

            (string fileName, string code) stringEnum = generated.FirstOrDefault(g => g.fileName.EndsWith("StringEnumAsTopPart.cs"));
            Assert.False(string.IsNullOrEmpty(stringEnum.code), "Expected generated entities code for StringEnumAsTopPart.cs");
            //Enums are kept as plain C# enums without the JsonConverter attribute
            Assert.Contains("public enum MyTopPartStringEnum", stringEnum.code);
            Assert.Contains("public MyTopPartStringEnum MyTopPartString { get; set; }", stringEnum.code);
            Assert.DoesNotContain("JsonConverter", stringEnum.code);
            Assert.DoesNotContain("JsonProperty", stringEnum.code);
        }

        [Fact]
        public void TestCompositionCSharpCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new CompositionTestJsonSchema());
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.CSharp);

            (string fileName, string code) compositionTest = generated.FirstOrDefault(g => g.fileName.EndsWith("CompositionTest.cs"));
            Assert.False(string.IsNullOrEmpty(compositionTest.code), "Expected generated code for CompositionTest.cs");

            //AllOf conversion merges the properties of all object subschemas into one class
            Assert.Contains("public partial class Combined", compositionTest.code);
            Assert.Contains("public string Name { get; set; }", compositionTest.code);
            Assert.Contains("public long? Count { get; set; }", compositionTest.code);
            //The AllOf part itself becomes a property pointing at the merged class
            Assert.Contains("public Combined CombinedProp { get; set; }", compositionTest.code);
            //AnyOf and OneOf map to object since C# has no union type
            Assert.Contains("public object AnyChoice { get; set; }", compositionTest.code);
            Assert.Contains("public object OneChoice { get; set; }", compositionTest.code);
            //An array of AllOf parts uses the merged class as item type
            Assert.Contains("List<Combined>", compositionTest.code);

            //A property name present in several subschemas is only emitted once
            int sharedValueCount = 0;
            int indexOfSharedValue = compositionTest.code.IndexOf("public string SharedValue { get; set; }", StringComparison.Ordinal);
            while (indexOfSharedValue != -1)
            {
                sharedValueCount += 1;
                indexOfSharedValue = compositionTest.code.IndexOf("public string SharedValue { get; set; }", indexOfSharedValue + 1, StringComparison.Ordinal);
            }
            Assert.Equal(1, sharedValueCount);
        }

        [Fact]
        public void TestTypeScriptCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new CompositionTestJsonSchema());
            codeGenerator.Register(new StringEnumAsTopPartJsonSchema());
            codeGenerator.Register(new WorkflowDefinitionJsonSchema());
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.TypeScript);

            (string fileName, string code) compositionTest = generated.FirstOrDefault(g => g.fileName.EndsWith("CompositionTest.ts"));
            Assert.False(string.IsNullOrEmpty(compositionTest.code), "Expected generated TypeScript code for CompositionTest.ts");

            //AllOf merges the properties of all object subschemas into one interface
            Assert.Contains("export interface CompositionTest", compositionTest.code);
            Assert.Contains("export interface Combined", compositionTest.code);
            Assert.Contains("combined?: Combined;", compositionTest.code);
            //AnyOf and OneOf map to object since the subschema choice cannot be expressed
            Assert.Contains("anyChoice?: object;", compositionTest.code);
            Assert.Contains("oneChoice?: object;", compositionTest.code);
            //An array of AllOf parts uses the merged interface as item type
            Assert.Contains("combinedArray?: Combined[];", compositionTest.code);
            Assert.Contains("name: string;", compositionTest.code);
            Assert.Contains("count?: number;", compositionTest.code);
            //A property name present in several subschemas is only emitted once
            int sharedValueCount = 0;
            int indexOfSharedValue = compositionTest.code.IndexOf("sharedValue: string;", StringComparison.Ordinal);
            while (indexOfSharedValue != -1)
            {
                sharedValueCount += 1;
                indexOfSharedValue = compositionTest.code.IndexOf("sharedValue: string;", indexOfSharedValue + 1, StringComparison.Ordinal);
            }
            Assert.Equal(1, sharedValueCount);

            (string fileName, string code) stringEnum = generated.FirstOrDefault(g => g.fileName.EndsWith("StringEnumAsTopPart.ts"));
            Assert.False(string.IsNullOrEmpty(stringEnum.code), "Expected generated TypeScript code for StringEnumAsTopPart.ts");
            //Enums on value parts become string literal union types
            Assert.Contains("export type MyTopPartStringEnum = 'Monster' | 'item' | 'Nusense';", stringEnum.code);
            Assert.Contains("myTopPartString?: MyTopPartStringEnum;", stringEnum.code);

            (string fileName, string code) workflowDefinition = generated.FirstOrDefault(g => g.fileName.EndsWith("WorkflowDefinition.ts"));
            Assert.False(string.IsNullOrEmpty(workflowDefinition.code), "Expected generated TypeScript code for WorkflowDefinition.ts");
            //Objects in arrays are emitted as their own interfaces
            Assert.Contains("export interface WorkflowDefinition", workflowDefinition.code);
            Assert.Contains("nodes: Node[];", workflowDefinition.code);
            Assert.Contains("edges: Edge[];", workflowDefinition.code);
            Assert.Contains("export interface Node", workflowDefinition.code);
            Assert.Contains("export interface Edge", workflowDefinition.code);
        }

        [Fact]
        public void TestPHPCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new CompositionTestJsonSchema());
            codeGenerator.Register(new StringEnumAsTopPartJsonSchema());
            codeGenerator.Register(new WorkflowDefinitionJsonSchema());
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.PHP);

            (string fileName, string code) compositionTest = generated.FirstOrDefault(g => g.fileName.EndsWith("CompositionTest.php"));
            Assert.False(string.IsNullOrEmpty(compositionTest.code), "Expected generated PHP code for CompositionTest.php");

            //AllOf merges the properties of all object subschemas into one class
            Assert.Contains("<?php", compositionTest.code);
            Assert.Contains("declare(strict_types=1);", compositionTest.code);
            Assert.Contains("namespace Default;", compositionTest.code);
            Assert.Contains("class CompositionTest", compositionTest.code);
            Assert.Contains("class Combined", compositionTest.code);
            Assert.Contains("public ?Combined $combined;", compositionTest.code);
            //AnyOf and OneOf map to object since the subschema choice cannot be expressed
            Assert.Contains("public ?object $anyChoice;", compositionTest.code);
            Assert.Contains("public ?object $oneChoice;", compositionTest.code);
            //An array of AllOf parts uses the merged class as docblock item type
            Assert.Contains("public ?array $combinedArray;", compositionTest.code);
            Assert.Contains("@var Combined[]|null", compositionTest.code);
            Assert.Contains("public string $name;", compositionTest.code);
            Assert.Contains("public ?int $count;", compositionTest.code);
            //A property name present in several subschemas is only emitted once
            int sharedValueCount = 0;
            int indexOfSharedValue = compositionTest.code.IndexOf("public string $sharedValue;", StringComparison.Ordinal);
            while (indexOfSharedValue != -1)
            {
                sharedValueCount += 1;
                indexOfSharedValue = compositionTest.code.IndexOf("public string $sharedValue;", indexOfSharedValue + 1, StringComparison.Ordinal);
            }
            Assert.Equal(1, sharedValueCount);

            (string fileName, string code) stringEnum = generated.FirstOrDefault(g => g.fileName.EndsWith("StringEnumAsTopPart.php"));
            Assert.False(string.IsNullOrEmpty(stringEnum.code), "Expected generated PHP code for StringEnumAsTopPart.php");
            //Enums on string parts become string backed enums (PHP 8.1)
            Assert.Contains("namespace Funny\\Onion;", stringEnum.code);
            Assert.Contains("enum MyTopPartStringEnum: string", stringEnum.code);
            Assert.Contains("case Monster = 'Monster';", stringEnum.code);
            Assert.Contains("case item = 'item';", stringEnum.code);
            Assert.Contains("case Nusense = 'Nusense';", stringEnum.code);
            Assert.Contains("public ?MyTopPartStringEnum $myTopPartString;", stringEnum.code);

            (string fileName, string code) workflowDefinition = generated.FirstOrDefault(g => g.fileName.EndsWith("WorkflowDefinition.php"));
            Assert.False(string.IsNullOrEmpty(workflowDefinition.code), "Expected generated PHP code for WorkflowDefinition.php");
            //Objects in arrays are emitted as their own classes
            Assert.Contains("class WorkflowDefinition", workflowDefinition.code);
            Assert.Contains("@var Node[]", workflowDefinition.code);
            Assert.Contains("public array $nodes;", workflowDefinition.code);
            Assert.Contains("public array $edges;", workflowDefinition.code);
            Assert.Contains("class Node", workflowDefinition.code);
            Assert.Contains("class Edge", workflowDefinition.code);
        }

        [Fact]
        public void TestVBCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new CompositionTestJsonSchema());
            codeGenerator.Register(new StringEnumAsTopPartJsonSchema());
            codeGenerator.Register(new WorkflowDefinitionJsonSchema());
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.VB);

            (string fileName, string code) compositionTest = generated.FirstOrDefault(g => g.fileName.EndsWith("CompositionTest.vb"));
            Assert.False(string.IsNullOrEmpty(compositionTest.code), "Expected generated VB code for CompositionTest.vb");

            //AllOf merges the properties of all object subschemas into one class
            Assert.Contains("Imports Newtonsoft.Json", compositionTest.code);
            //VB reserved word namespace segments are escaped with brackets
            Assert.Contains("Namespace [Default]", compositionTest.code);
            Assert.Contains("Partial Public Class CompositionTest", compositionTest.code);
            Assert.Contains("Partial Public Class Combined", compositionTest.code);
            //The AllOf part itself becomes a property pointing at the merged class
            Assert.Contains("Public Property CombinedProp As Combined", compositionTest.code);
            //AnyOf and OneOf map to object since VB has no union type
            Assert.Contains("Public Property AnyChoice As Object", compositionTest.code);
            Assert.Contains("Public Property OneChoice As Object", compositionTest.code);
            //An array of AllOf parts uses the merged class as item type
            Assert.Contains("Public Property CombinedArray As List(Of Combined)", compositionTest.code);
            Assert.Contains("Public Property Name As String", compositionTest.code);
            //Value types are nullable when the part is not required
            Assert.Contains("Public Property Count As Long?", compositionTest.code);
            //A property name present in several subschemas is only emitted once
            int sharedValueCount = 0;
            int indexOfSharedValue = compositionTest.code.IndexOf("Public Property SharedValue As String", StringComparison.Ordinal);
            while (indexOfSharedValue != -1)
            {
                sharedValueCount += 1;
                indexOfSharedValue = compositionTest.code.IndexOf("Public Property SharedValue As String", indexOfSharedValue + 1, StringComparison.Ordinal);
            }
            Assert.Equal(1, sharedValueCount);

            (string fileName, string code) stringEnum = generated.FirstOrDefault(g => g.fileName.EndsWith("StringEnumAsTopPart.vb"));
            Assert.False(string.IsNullOrEmpty(stringEnum.code), "Expected generated VB code for StringEnumAsTopPart.vb");
            //Enums on string parts become VB enums serialized with StringEnumConverter
            Assert.Contains("Namespace Funny.Onion", stringEnum.code);
            Assert.Contains("Public Enum MyTopPartStringEnum", stringEnum.code);
            Assert.Contains("Monster", stringEnum.code);
            Assert.Contains("Item", stringEnum.code);
            Assert.Contains("Nusense", stringEnum.code);
            Assert.Contains("<JsonProperty(\"myTopPartString\"), JsonConverter(GetType(StringEnumConverter))>", stringEnum.code);
            Assert.Contains("Public Property MyTopPartString As MyTopPartStringEnum", stringEnum.code);

            (string fileName, string code) workflowDefinition = generated.FirstOrDefault(g => g.fileName.EndsWith("WorkflowDefinition.vb"));
            Assert.False(string.IsNullOrEmpty(workflowDefinition.code), "Expected generated VB code for WorkflowDefinition.vb");
            //Objects in arrays are emitted as their own classes
            Assert.Contains("Partial Public Class WorkflowDefinition", workflowDefinition.code);
            Assert.Contains("Public Property Nodes As List(Of Node)", workflowDefinition.code);
            Assert.Contains("Public Property Edges As List(Of Edge)", workflowDefinition.code);
            Assert.Contains("Partial Public Class Node", workflowDefinition.code);
            Assert.Contains("Partial Public Class Edge", workflowDefinition.code);
        }

        [Fact]
        public void TestJavaCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new CompositionTestJsonSchema());
            codeGenerator.Register(new StringEnumAsTopPartJsonSchema());
            codeGenerator.Register(new WorkflowDefinitionJsonSchema());
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.Java);

            (string fileName, string code) compositionTest = generated.FirstOrDefault(g => g.fileName.EndsWith("CompositionTest.java"));
            Assert.False(string.IsNullOrEmpty(compositionTest.code), "Expected generated Java code for CompositionTest.java");

            //The package is derived from the module; Java reserved word segments get a trailing underscore
            Assert.Contains("package default_;", compositionTest.code);
            Assert.Contains("import com.fasterxml.jackson.annotation.JsonProperty;", compositionTest.code);
            //Java has no partial classes: the first class of a file is top level, the rest are static nested classes
            Assert.Contains("public class CompositionTest", compositionTest.code);
            Assert.Contains("public static class Combined", compositionTest.code);
            //The AllOf part itself becomes a property pointing at the merged class
            Assert.Contains("@JsonProperty(\"combined\")", compositionTest.code);
            Assert.Contains("private Combined combinedProp;", compositionTest.code);
            Assert.Contains("public Combined getCombinedProp()", compositionTest.code);
            //AnyOf and OneOf map to Object since Java has no union type
            Assert.Contains("private Object anyChoice;", compositionTest.code);
            Assert.Contains("private Object oneChoice;", compositionTest.code);
            //An array of AllOf parts uses the merged class as item type
            Assert.Contains("private List<Combined> combinedArray = new ArrayList<>();", compositionTest.code);
            Assert.Contains("private String name;", compositionTest.code);
            //Boxed value types are used since the part is not required
            Assert.Contains("private Long count;", compositionTest.code);
            //A property name present in several subschemas is only emitted once
            int sharedValueCount = 0;
            int indexOfSharedValue = compositionTest.code.IndexOf("private String sharedValue;", StringComparison.Ordinal);
            while (indexOfSharedValue != -1)
            {
                sharedValueCount += 1;
                indexOfSharedValue = compositionTest.code.IndexOf("private String sharedValue;", indexOfSharedValue + 1, StringComparison.Ordinal);
            }
            Assert.Equal(1, sharedValueCount);

            (string fileName, string code) stringEnum = generated.FirstOrDefault(g => g.fileName.EndsWith("StringEnumAsTopPart.java"));
            Assert.False(string.IsNullOrEmpty(stringEnum.code), "Expected generated Java code for StringEnumAsTopPart.java");
            //Enums on string parts become Java enums whose @JsonValue matches the raw schema value
            Assert.Contains("package funny.onion;", stringEnum.code);
            Assert.Contains("public enum MyTopPartStringEnum", stringEnum.code);
            Assert.Contains("Monster(\"Monster\"),", stringEnum.code);
            Assert.Contains("Item(\"item\"),", stringEnum.code);
            Assert.Contains("Nusense(\"Nusense\");", stringEnum.code);
            Assert.Contains("@JsonValue", stringEnum.code);
            Assert.Contains("private MyTopPartStringEnum myTopPartString;", stringEnum.code);

            (string fileName, string code) workflowDefinition = generated.FirstOrDefault(g => g.fileName.EndsWith("WorkflowDefinition.java"));
            Assert.False(string.IsNullOrEmpty(workflowDefinition.code), "Expected generated Java code for WorkflowDefinition.java");
            //Objects in arrays are emitted as static nested classes
            Assert.Contains("public class WorkflowDefinition", workflowDefinition.code);
            Assert.Contains("private List<Node> nodes = new ArrayList<>();", workflowDefinition.code);
            Assert.Contains("private List<Edge> edges = new ArrayList<>();", workflowDefinition.code);
            Assert.Contains("public static class Node", workflowDefinition.code);
            Assert.Contains("public static class Edge", workflowDefinition.code);
        }

        [Fact]
        public void TestTSQLCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new CompositionTestJsonSchema());
            codeGenerator.Register(new WorkflowDefinitionJsonSchema());
            codeGenerator.Register(new StringEnumAsTopPartJsonSchema());
            codeGenerator.Register(new FtpInformationJsonSchema());
            List<(string fileName, string code)> generated = codeGenerator.Generate(Code.TSQL_DDL);

            (string fileName, string code) compositionTest = generated.FirstOrDefault(g => g.fileName.EndsWith("CompositionTest.sql"));
            Assert.False(string.IsNullOrEmpty(compositionTest.code), "Expected generated TSQL DDL for CompositionTest.sql");

            //The module becomes a TSQL schema created with a guard if missing
            Assert.Contains("IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Default')", compositionTest.code);
            //AllOf merges the properties of all object subschemas into the owning table
            Assert.Contains("CREATE TABLE [Default].[CompositionTest]", compositionTest.code);
            Assert.Contains("[Name] NVARCHAR(100) NOT NULL", compositionTest.code);
            Assert.Contains("[Count] BIGINT NULL", compositionTest.code);
            //A property name present in several subschemas is only emitted once per merge,
            //so it occurs exactly twice: in the owning table and in the CombinedArray child table
            int sharedValueCount = 0;
            int indexOfSharedValue = compositionTest.code.IndexOf("[SharedValue] NVARCHAR(MAX) NOT NULL", StringComparison.Ordinal);
            while (indexOfSharedValue != -1)
            {
                sharedValueCount += 1;
                indexOfSharedValue = compositionTest.code.IndexOf("[SharedValue] NVARCHAR(MAX) NOT NULL", indexOfSharedValue + 1, StringComparison.Ordinal);
            }
            Assert.Equal(2, sharedValueCount);
            //AnyOf and OneOf have no single column shape and map to a JSON column
            Assert.Contains("[AnyChoice] NVARCHAR(MAX) NULL", compositionTest.code);
            Assert.Contains("[OneChoice] NVARCHAR(MAX) NULL", compositionTest.code);
            //An array of AllOf parts becomes a child table with the merged columns
            Assert.Contains("CREATE TABLE [Default].[CompositionTestCombinedArray]", compositionTest.code);
            Assert.Contains("[ParentId] BIGINT NOT NULL", compositionTest.code);
            Assert.Contains("CONSTRAINT [FK_CompositionTestCombinedArray] FOREIGN KEY ([ParentId]) REFERENCES [Default].[CompositionTest] ([Id])", compositionTest.code);
            //Child tables are dropped before their parents
            Assert.Contains("DROP TABLE [Default].[CompositionTestCombinedArray];", compositionTest.code);

            (string fileName, string code) workflowDefinition = generated.FirstOrDefault(g => g.fileName.EndsWith("WorkflowDefinition.sql"));
            Assert.False(string.IsNullOrEmpty(workflowDefinition.code), "Expected generated TSQL DDL for WorkflowDefinition.sql");
            //Objects in arrays become child tables with one row per item and a ParentId back to the owner
            Assert.Contains("CREATE TABLE [Default].[WorkflowDefinition]", workflowDefinition.code);
            Assert.Contains("[Name] NVARCHAR(100) NOT NULL", workflowDefinition.code);
            Assert.Contains("CREATE TABLE [Default].[WorkflowDefinitionNodes]", workflowDefinition.code);
            Assert.Contains("[NodeKey] NVARCHAR(100) NULL", workflowDefinition.code);
            Assert.Contains("CREATE TABLE [Default].[WorkflowDefinitionEdges]", workflowDefinition.code);
            Assert.Contains("[FromNodeKey] NVARCHAR(100) NOT NULL", workflowDefinition.code);
            Assert.Contains("CONSTRAINT [FK_WorkflowDefinitionNodes] FOREIGN KEY ([ParentId]) REFERENCES [Default].[WorkflowDefinition] ([Id])", workflowDefinition.code);

            (string fileName, string code) stringEnum = generated.FirstOrDefault(g => g.fileName.EndsWith("StringEnumAsTopPart.sql"));
            Assert.False(string.IsNullOrEmpty(stringEnum.code), "Expected generated TSQL DDL for StringEnumAsTopPart.sql");
            //String enums become a CHECK constraint on the column with the raw enum values
            Assert.Contains("CREATE TABLE [Funny.Onion].[StringEnumAsTopPart]", stringEnum.code);
            Assert.Contains("[MyTopPartString] NVARCHAR(MAX) NULL CHECK ([MyTopPartString] IN (N'Monster', N'item', N'Nusense'))", stringEnum.code);

            (string fileName, string code) ftpInformation = generated.FirstOrDefault(g => g.fileName.EndsWith("FtpInformation.sql"));
            Assert.False(string.IsNullOrEmpty(ftpInformation.code), "Expected generated TSQL DDL for FtpInformation.sql");
            //The requiredness of the reference property wins over the referenced definition part
            Assert.Contains("CREATE TABLE [Funny.Onion].[FtpInformation]", ftpInformation.code);
            Assert.Contains("[FtpType] NVARCHAR(MAX) NOT NULL CHECK ([FtpType] IN (N'Auto', N'FTP', N'FTPS'))", ftpInformation.code);
            Assert.Contains("[SourceFileName] NVARCHAR(MAX) NOT NULL", ftpInformation.code);
        }

        [Fact]
        public void TestDataStringCodeGeneration()
        {
            CodeGenerator codeGenerator = new CodeGenerator("TestApplicationRoot", "TestJsonSchemaApplicationRoot");
            codeGenerator.Register(new DataStringAsTopPartJsonSchema());

            List<(string fileName, string code)> csharpCode = codeGenerator.Generate(Code.CSharp);
            (string fileName, string code) dataStringCSharp = csharpCode.Single(g => g.fileName.EndsWith("DataStringAsTopPart.cs"));
            //CSharp maps the DataString part to the DataString value object with its limitations
            Assert.Contains("public DataString MyTopPartDataString", dataStringCSharp.code);
            Assert.Contains("new DataString(\"abc123\")", dataStringCSharp.code);

            List<(string fileName, string code)> entitiesCode = codeGenerator.Generate(Code.CSharp_Entities);
            (string fileName, string code) dataStringEntities = entitiesCode.Single(g => g.fileName.EndsWith("DataStringAsTopPart.cs"));
            //Entities keep the DataString value object, only the serialization specifics are removed
            Assert.Contains("public DataString MyTopPartDataString", dataStringEntities.code);
            Assert.DoesNotContain("[JsonProperty", dataStringEntities.code);

            List<(string fileName, string code)> typeScriptCode = codeGenerator.Generate(Code.TypeScript);
            (string fileName, string code) dataStringTypeScript = typeScriptCode.Single(g => g.fileName.EndsWith("DataStringAsTopPart.ts"));
            Assert.Contains("myTopPartDataString?: string", dataStringTypeScript.code);

            List<(string fileName, string code)> phpCode = codeGenerator.Generate(Code.PHP);
            (string fileName, string code) dataStringPhp = phpCode.Single(g => g.fileName.EndsWith("DataStringAsTopPart.php"));
            Assert.Contains("public ?string $myTopPartDataString", dataStringPhp.code);

            List<(string fileName, string code)> vbCode = codeGenerator.Generate(Code.VB);
            (string fileName, string code) dataStringVb = vbCode.Single(g => g.fileName.EndsWith("DataStringAsTopPart.vb"));
            Assert.Contains("Public Property MyTopPartDataString As DataString", dataStringVb.code);

            List<(string fileName, string code)> javaCode = codeGenerator.Generate(Code.Java);
            (string fileName, string code) dataStringJava = javaCode.Single(g => g.fileName.EndsWith("DataStringAsTopPart.java"));
            Assert.Contains("private String myTopPartDataString", dataStringJava.code);

            List<(string fileName, string code)> tsqlCode = codeGenerator.Generate(Code.TSQL_DDL);
            (string fileName, string code) dataStringTsql = tsqlCode.Single(g => g.fileName.EndsWith("DataStringAsTopPart.sql"));
            //The limitations become column size, CHECK constraints and DEFAULT
            Assert.Contains("[MyTopPartDataString] NVARCHAR(10) NULL DEFAULT N'abc123'", dataStringTsql.code);
            Assert.Contains("CHECK (LEN([MyTopPartDataString]) >= 3)", dataStringTsql.code);
        }


        #region Assembly load all schemas and write

        private static void LoadAllJsonSchemaBuildersAndGenerateCSharpCodeToFile(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(applicationRoot))
            {
                Directory.Delete(applicationRoot, true);
            }
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                codeGenerator.Register(jsonSchema);
            }
            codeGenerator.GenerateToFile(Code.CSharp);
        }

        private static void LoadAllJsonSchemaBuildersAndGenerateCSharpEntitiesCodeToFile(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(applicationRoot))
            {
                Directory.Delete(applicationRoot, true);
            }
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                codeGenerator.Register(jsonSchema);
            }
            codeGenerator.GenerateToFile(Code.CSharp_Entities);
        }

        private static void LoadAllJsonSchemaBuildersAndGenerateTypeScriptCodeToFile(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(applicationRoot))
            {
                Directory.Delete(applicationRoot, true);
            }
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                codeGenerator.Register(jsonSchema);
            }
            codeGenerator.GenerateToFile(Code.TypeScript);
        }

        private static void LoadAllJsonSchemaBuildersAndGeneratePHPCodeToFile(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(applicationRoot))
            {
                Directory.Delete(applicationRoot, true);
            }
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                codeGenerator.Register(jsonSchema);
            }
            codeGenerator.GenerateToFile(Code.PHP);
        }

        private static void LoadAllJsonSchemaBuildersAndGenerateVBCodeToFile(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(applicationRoot))
            {
                Directory.Delete(applicationRoot, true);
            }
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                codeGenerator.Register(jsonSchema);
            }
            codeGenerator.GenerateToFile(Code.VB);
        }

        private static void LoadAllJsonSchemaBuildersAndGenerateJavaCodeToFile(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(applicationRoot))
            {
                Directory.Delete(applicationRoot, true);
            }
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                codeGenerator.Register(jsonSchema);
            }
            codeGenerator.GenerateToFile(Code.Java);
        }

        private static void LoadAllJsonSchemaBuildersAndGenerateTSQLCodeToFile(string applicationRoot, string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(applicationRoot))
            {
                Directory.Delete(applicationRoot, true);
            }
            CodeGenerator codeGenerator = new CodeGenerator(applicationRoot, jsonSchemaApplicationRoot);
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                codeGenerator.Register(jsonSchema);
            }
            codeGenerator.GenerateToFile(Code.TSQL_DDL);
        }

        private static void LoadAllJsonSchemaBuildersAndWriteSchemasToFile(string jsonSchemaApplicationRoot)
        {
            if (Directory.Exists(jsonSchemaApplicationRoot))
            {
                Directory.Delete(jsonSchemaApplicationRoot, true);
            }
            foreach (Type codeDefinedType in GetInterfaceTypes(typeof(IJsonSchemaDefinition)))
            {
                IJsonSchemaDefinition jsonSchema = GetJsonSchemaInstance(codeDefinedType);
                jsonSchema.WriteSchemaToFile(jsonSchemaApplicationRoot);
            }
        }

        private static IEnumerable<Type> GetInterfaceTypes(Type interfaceType)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(p => interfaceType.IsAssignableFrom(p) && p.IsClass && !p.IsAbstract);
        }

        private static IJsonSchemaDefinition GetJsonSchemaInstance(Type jsonSchemaType)
        {
            //Create dataowner instance
            IJsonSchemaDefinition jsonSchemaInstance = (IJsonSchemaDefinition)Activator.CreateInstance(jsonSchemaType);

            return jsonSchemaInstance;
        }

        #endregion
    }

    public class DataOwnerDefinitionJsonSchema : AbstractJsonSchema
    {
        protected override JSBSchema BuildJsonSchema()
        {
            List<IJSBPart> wfProps = new List<IJSBPart>();
            wfProps.Add(new JSBString("Name", "Returns the DataOwner unique name", maxLength: 100, isRequired: true));
            wfProps.Add(new JSBString("Version", "Returns the version of the DataOwner", isRequired: true));
            wfProps.Add(new JSBArray("ModuleDefinitions", "Returns the embedded module definitions", items: new List<IJSBPart>() {
             new JSBRef("ModuleDefinition", "Definition of an module", iriReference: new Uri("./ModuleDefinition.schema.json", UriKind.Relative))}, isRequired: true));
            JSBObject objectPart = new JSBObject("DataOwnerDefinition", "The easy definition of the schema of a dataOwner", props: wfProps);

            return new JSBSchema("DataOwnerDefinition", Description, topPart: objectPart);
        }

        public override string Description
        {
            get
            {
                return "The easy definition of the schema of a dataOwner";
            }
        }

        public override NamespaceString Module
        {
            get
            {
                return "Default";
            }
        }
    }

    public class ModuleDefinitionJsonSchema : AbstractJsonSchema
    {
        protected override JSBSchema BuildJsonSchema()
        {
            List<IJSBPart> wfProps = new List<IJSBPart>();
            wfProps.Add(new JSBString("Name", "Returns the Module unique name", maxLength: 100, isRequired: true));
            wfProps.Add(new JSBString("Version", "Returns the version of the Module", isRequired: true));
            wfProps.Add(new JSBArray("WorkflowDefinitions", "Returns the embedded workflow definitions", items: new List<IJSBPart>() {
             new JSBRef("WorkflowDefinition", "Definition of an workflow", iriReference: new Uri("./WorkflowDefinition.schema.json", UriKind.Relative))}, isRequired: true));
            JSBObject objectPart = new JSBObject("ModuleDefinition", "The easy definition of the schema of a module", props: wfProps);

            return new JSBSchema("ModuleDefinition", Description, topPart: objectPart);
        }

        public override string Description
        {
            get
            {
                return "The easy definition of the schema of a module";
            }
        }

        public override NamespaceString Module
        {
            get
            {
                return "Default";
            }
        }
    }

    public class WorkflowDefinitionJsonSchema : AbstractJsonSchema
    {
        protected override JSBSchema BuildJsonSchema()
        {
            List<IJSBPart> nProps = new List<IJSBPart>();
            nProps.Add(new JSBString("NodeKey", "NodeKey of the node", maxLength: 100));
            nProps.Add(new JSBString("Description", "Description of the node to make it more readable and understandable"));
            nProps.Add(new JSBString("BehaviorKey", "Key of the behavior to use for lookup", maxLength: 100));
            nProps.Add(new JSBString("BehaviorVersion", "Version of the behavior"));
            nProps.Add(new JSBString("BehaviorConfiguration", "Configuration of the instance of behavior"));
            nProps.Add(new JSBString("DataJsonSchemaModuleKey", "ModuleKey for looking up jsonschema for data", maxLength: 100));
            nProps.Add(new JSBString("DataJsonSchemaKey", "Key for looking up jsonschema for data", maxLength: 100));

            List<IJSBPart> eProps = new List<IJSBPart>();
            eProps.Add(new JSBString("Description", "Description of the edge"));
            eProps.Add(new JSBString("FromNodeKey", "The nodekey of the node that the edge comes from", maxLength: 100, isRequired: true));
            eProps.Add(new JSBString("ToNodeKey", "The nodekey of the node that the edge goes to", maxLength: 100, isRequired: true));

            List<IJSBPart> wfProps = new List<IJSBPart>();
            wfProps.Add(new JSBString("Name", "The version number of the workflow", maxLength: 100, isRequired: true));
            wfProps.Add(new JSBString("Version", "The version number of the workflow", isRequired: true));
            wfProps.Add(new JSBArray("Nodes", "Nodes of the workflow", items: new List<IJSBPart>() {
             new JSBObject("Node", "Definition of an node", nProps)}, isRequired: true));
            wfProps.Add(new JSBArray("Edges", "Edges of the workflow", items: new List<IJSBPart>() {
             new JSBObject("Edge", "Definition of an edge", eProps)}, isRequired: true));
            JSBObject objectPart = new JSBObject("WorkflowDefinition", "The easy definition of the schema of a workflow", props: wfProps);

            return new JSBSchema("WorkflowDefinition", Description, topPart: objectPart);
        }


        public override string Description
        {
            get
            {
                return "The easy definition of the schema of a workflow";
            }
        }

        public override NamespaceString Module
        {
            get
            {
                return "Default";
            }
        }
    }

    public class FtpInformationJsonSchema : AbstractJsonSchema
    {
        protected override JSBSchema BuildJsonSchema()
        {
            List<IJSBPart> definitions = new List<IJSBPart>();
            definitions.Add(new JSBString("FtpType", "Type of FTP", enums: new List<string>() { "Auto", "FTP", "FTPS" }));
            List<IJSBPart> properties = new List<IJSBPart>();
            properties.Add(new JSBString("SourceFileName", "File to move or copy", isRequired: true));
            properties.Add(new JSBString("SourceDirectory", "Location of the file to move or copy", isRequired: true));
            properties.Add(new JSBString("DestinationFileName", "File after move or copy. If filled out a rename will be done under the move or copy operation"));
            properties.Add(new JSBString("DestinationDirectory", "Location of the file should end after move or copy", isRequired: true));
            properties.Add(new JSBString("FtpUser", "User registered on the server", isRequired: true));
            properties.Add(new JSBString("FtpPassword", "Password for the user registered on the server", isRequired: true));
            properties.Add(new JSBString("FtpServer", "Ftp host server", isRequired: true));
            properties.Add(new JSBString("FtpFolderOnServer", "Ftp folder on server if needed"));
            properties.Add(new JSBRef("FtpType", "Type of FTP", iriReference: new Uri("#/definitions/ftpType", UriKind.Relative), isRequired: true));

            JSBObject jsonObject = new JSBObject("FtpInformation", "Data For Copy and Move file operations on FTP", props: properties);
            return new JSBSchema("FtpInformation", Description, topPart: jsonObject, defs: definitions);
        }

        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Data For FTP operations";
            }
        }
    }

    public class UriReferenceAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test uri reference as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            if (Uri.TryCreate("./dateAsTopPart", UriKind.RelativeOrAbsolute, out Uri uri))
            {

                JSBRef uriReferencePart = new JSBRef("MyTopPartUriReference", "TopPart", iriReference: uri);

                return new JSBSchema("UriReferencesATopPart", Description, topPart: uriReferencePart);
            }
            else
            {
                throw new Exception("Uri not valid");
            }
        }
    }

    public class TimeAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test time as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBTime timePart = new JSBTime("MyTopPartTime", "TopPart", defaultValue: new DateTime(2020, 1, 1, 15, 30, 48, 765));

            return new JSBSchema("TimeAsATopPart", Description, topPart: timePart);
        }
    }

    public class StringAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test string as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBString stringPart = new JSBString("MyTopPartString", "TopPart");

            return new JSBSchema("StringAsATopPart", Description, topPart: stringPart);
        }
    }

    public class StringEnumAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test string enum as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBString stringPart = new JSBString("MyTopPartString", "TopPart", enums: new List<string> { "Monster", "item", "Nusense" });

            return new JSBSchema("StringEnumAsATopPart", Description, topPart: stringPart);
        }
    }

    public class ObjectAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test object as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBBoolean booleanPart = new JSBBoolean("BooleanPart", "BooleanPart for testing", isRequired: true);
            JSBInteger integerPart = new JSBInteger("IntegerPart", "IntegerPart for testing");

            List<IJSBPart> properties = new List<IJSBPart>();
            properties.Add(booleanPart);
            properties.Add(integerPart);

            JSBObject objectPart = new JSBObject("MyTopPartObject", "TopPart", props: properties);

            return new JSBSchema("ObjectAsATopPart", Description, topPart: objectPart);
        }
    }

    public class EmailAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test email as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBEmail emailPart = new JSBEmail("MyTopPartEmail", "TopPart", defaultValue: "riger@support.com");

            return new JSBSchema("EmailAsATopPart", Description, topPart: emailPart);
        }
    }

    public class DataStringAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test data string as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBDataString dataStringPart = new JSBDataString("MyTopPartDataString", "TopPart", defaultValue: "abc123",
                minLength: 3, maxLength: 10, pattern: "^[a-z0-9]+$");

            return new JSBSchema("DataStringAsATopPart", Description, topPart: dataStringPart);
        }
    }

    public class DateTimeAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test datetime as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBDateTime dateTimePart = new JSBDateTime("MyTopPartDateTime", "TopPart", defaultValue: new DateTime(2015, 05, 16));

            return new JSBSchema("DateTimeAsATopPart", Description, topPart: dateTimePart);
        }
    }

    public class DateAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test date as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBDate datePart = new JSBDate("MyTopPartDate", "TopPart", defaultValue: new DateTime(2015, 05, 16));

            return new JSBSchema("DateAsATopPart", Description, topPart: datePart);
        }
    }

    public class ArrayAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test string as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            List<IJSBPart> items = new List<IJSBPart>();

            items.Add(new JSBInteger("SwanNumber", "Swans are relevant in the world"));

            JSBArray arrayPart = new JSBArray("MyTopPartArray", "TopPart", items);

            return new JSBSchema("ArrayAsATopPart", Description, topPart: arrayPart);
        }
    }

    public class ArrayAsTopPartRefChildJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test array as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            List<IJSBPart> items = new List<IJSBPart>();
            if (Uri.TryCreate("./dateAsTopPart", UriKind.RelativeOrAbsolute, out Uri uri))
            {
                items.Add(new JSBRef("ArrayUriReference", "Array of UriReference", iriReference: uri));

                JSBArray arrayPart = new JSBArray("MyTopPartArray", "TopPart", items);

                return new JSBSchema("ArrayAsATopPart", Description, topPart: arrayPart);
            }
            else
            {
                throw new Exception("Uri not valid");
            }
        }
    }

    public class ArrayOfObjectsAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test object as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBBoolean booleanPart = new JSBBoolean("BooleanPart", "BooleanPart for testing", isRequired: true);
            JSBInteger integerPart = new JSBInteger("IntegerPart", "IntegerPart for testing");

            List<IJSBPart> properties = new List<IJSBPart>();
            properties.Add(booleanPart);
            properties.Add(integerPart);

            JSBObject objectPart = new JSBObject("ObjectInAnArray", "ObjectInAnArray is fun", props: properties);

            List<IJSBPart> items = new List<IJSBPart>();
            items.Add(objectPart);
            JSBArray arrayPart = new JSBArray("MyTopPartArray", "TopPart", items);

            return new JSBSchema("ArrayOfObjectsAsATopPart", Description, topPart: arrayPart);
        }
    }

    public class ArrayOfArrayOfObjectsAsTopPartJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module
        {
            get
            {
                return "Funny.Onion";
            }
        }

        public override string Description
        {
            get
            {
                return "Used to test array of arry of object as a top part";
            }
        }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBBoolean booleanPart = new JSBBoolean("BooleanPart", "BooleanPart for testing", isRequired: true);
            JSBInteger integerPart = new JSBInteger("IntegerPart", "IntegerPart for testing");

            List<IJSBPart> properties = new List<IJSBPart>();
            properties.Add(booleanPart);
            properties.Add(integerPart);

            JSBObject objectPart = new JSBObject("ObjectInAnArrayOfAnArray", "ObjectInAnArrayOfAnArray is fun", props: properties);

            List<IJSBPart> innerItems = new List<IJSBPart>();
            innerItems.Add(objectPart);
            JSBArray innerArrayPart = new JSBArray("InnerArray", "InnerArrayPart", innerItems);

            List<IJSBPart> items = new List<IJSBPart>();
            items.Add(innerArrayPart);
            JSBArray arrayPart = new JSBArray("MyTopPartArrayOfArray", "TopPart", items);

            return new JSBSchema("ArrayOfObjectsAsATopPart", Description, topPart: arrayPart);
        }
    }

    public class CompositionTestJsonSchema : AbstractJsonSchema
    {
        protected override JSBSchema BuildJsonSchema()
        {
            List<IJSBPart> subAProps = new List<IJSBPart>();
            subAProps.Add(new JSBString("Name", "Name of the first subschema", maxLength: 100, isRequired: true));
            subAProps.Add(new JSBString("SharedValue", "Only emitted once in the merged class", isRequired: true));
            JSBObject subA = new JSBObject("SubA", "First subschema", props: subAProps);

            List<IJSBPart> subBProps = new List<IJSBPart>();
            subBProps.Add(new JSBInteger("Count", "Count of the second subschema"));
            subBProps.Add(new JSBString("SharedValue", "Same property name as in SubA", isRequired: true));
            JSBObject subB = new JSBObject("SubB", "Second subschema", props: subBProps);

            JSBAllOf combined = new JSBAllOf("Combined", "AllOf conversion merges SubA and SubB into one class", new List<IJSBPart>() { subA, subB });
            JSBAnyOf anyChoice = new JSBAnyOf("AnyChoice", "Value validates against at least one subschema", new List<IJSBPart>() { subA, subB });
            JSBOneOf oneChoice = new JSBOneOf("OneChoice", "Value validates against exactly one subschema", new List<IJSBPart>() { subA, subB });
            JSBArray arrayOfAllOf = new JSBArray("CombinedArray", "Array of merged AllOf classes", new List<IJSBPart>() { combined });

            List<IJSBPart> props = new List<IJSBPart>();
            props.Add(combined);
            props.Add(anyChoice);
            props.Add(oneChoice);
            props.Add(arrayOfAllOf);
            JSBObject objectPart = new JSBObject("CompositionTest", "Schema with composition parts", props: props);

            return new JSBSchema("CompositionTest", Description, topPart: objectPart);
        }

        public override string Description
        {
            get
            {
                return "Schema with AnyOf, OneOf and AllOf composition parts for testing code generation";
            }
        }

        public override NamespaceString Module
        {
            get
            {
                return "Default";
            }
        }
    }
}
