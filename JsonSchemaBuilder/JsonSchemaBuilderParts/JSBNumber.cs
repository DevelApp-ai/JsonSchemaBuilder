using DevelApp.JsonSchemaBuilder.Exceptions;
using DevelApp.Utility.Model;
using Json.Schema;
using System.Collections.Generic;

namespace DevelApp.JsonSchemaBuilder.JsonSchemaParts
{
    using JsonSchemaBuilder = Json.Schema.JsonSchemaBuilder;
    /// <summary>
    /// Convenience number definition in Json Schema.
    /// </summary>
    public class JSBNumber : AbstractJSBPart<double?>
    {
        public JSBNumber(IdentifierString objectName, string description, double? minimum = null, double? maximum = null, 
            double? multipleOf = null, double? defaultValue = null, List<double?> examples = null,
            List<double?> enums = null, bool isRequired = false)
            : base(objectName, description, isRequired, defaultValue: defaultValue, examples: examples, enums: enums)
        {
            if (minimum.HasValue && defaultValue.HasValue)
            {
                if (defaultValue.Value < minimum.Value)
                {
                    throw new JsonSchemaBuilderException($"The default value ({defaultValue}) supplied is below the minimum ({minimum}) supplied");
                }
            }
            if (maximum.HasValue && defaultValue.HasValue)
            {
                if (defaultValue.Value > maximum.Value)
                {
                    throw new JsonSchemaBuilderException($"The default value ({defaultValue}) supplied is above the maximum ({maximum}) supplied");
                }
            }
            if (multipleOf.HasValue && defaultValue.HasValue)
            {
                if (defaultValue.Value % multipleOf.Value != 0)
                {
                    throw new JsonSchemaBuilderException($"The default value ({defaultValue}) supplied is not a multiple of multipleOf ({multipleOf}) supplied");
                }
            }

            MultipleOf = multipleOf;
            Minimum = minimum;
            Maximum = maximum;
        }

        public double? MultipleOf { get; }

        public double? Minimum { get; }
        public double? Maximum { get; }

        public override JSBPartType PartType
        {
            get
            {
                return JSBPartType.Number;
            }
        }

        public override JsonSchemaBuilder AsJsonSchemaBuilder(JSBSchemaVersion schemaVersion)
        {
            JsonSchemaBuilder builder = InitialJsonSchema(schemaVersion)
                .Type(SchemaValueType.Number);

            if (MultipleOf.HasValue)
            {
                builder = builder.MultipleOf((decimal)MultipleOf.Value);
            }
            if (Minimum.HasValue)
            {
                builder = builder.Minimum((decimal)Minimum.Value);
            }
            if (Maximum.HasValue)
            {
                builder = builder.Maximum((decimal)Maximum.Value);
            }
            return builder;
        }
    }
}
