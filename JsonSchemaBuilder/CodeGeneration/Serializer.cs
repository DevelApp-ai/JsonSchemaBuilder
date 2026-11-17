namespace DevelApp.JsonSchemaBuilder.CodeGeneration
{
    /// <summary>
    /// Serializer that the C# code generator emits its mapping attributes for.
    /// Newtonsoft is the default and produces the same output as before the
    /// serializer choice existed. The serializer choice is only consumed by
    /// Code.CSharp; the other code types ignore it, and Code.CSharp_Entities is
    /// serializer-free by definition.
    /// </summary>
    public enum Serializer
    {
        /// <summary>
        /// Newtonsoft.Json mapping attributes ([JsonProperty] and StringEnumConverter)
        /// </summary>
        Newtonsoft = 0,

        /// <summary>
        /// Microsoft's own System.Text.Json mapping attributes
        /// ([JsonPropertyName] and JsonStringEnumConverter)
        /// </summary>
        SystemTextJson = 1,

        /// <summary>
        /// Akka.NET Hyperion. Hyperion serializes plain POCOs without any mapping
        /// attributes and uses the actual property names on the wire, so this produces
        /// the attribute-free classes
        /// </summary>
        Hyperion = 2
    }
}
