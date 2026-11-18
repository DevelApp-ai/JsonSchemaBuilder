using DevelApp.JsonSchemaBuilder;
using DevelApp.JsonSchemaBuilder.JsonSchemaParts;
using DevelApp.Utility.Model;
using System;
using System.Collections.Generic;

namespace JsonSchemaBuilder.Test.RemoteFixtures
{
    /// <summary>
    /// Fixture schemas for the remote schema pipeline tests. They live in their
    /// own namespace so the load-all authored schemas corpus sweep in
    /// TestSchemaAndCodeGeneration does not register them (they need a fake
    /// downloader and the opt-in to resolve)
    /// </summary>
    public class RemoteOwnerJsonSchema : AbstractJsonSchema
    {
        public override NamespaceString Module { get { return new NamespaceString("RemoteOwner"); } }

        public override string Description { get { return "A schema with a remote reference"; } }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBRef addressRef = new JSBRef("Address", "The remote address", new Uri("https://example.com/Remote/address.schema.json", UriKind.Absolute), isRequired: true);
            JSBObject topPart = new JSBObject("RemoteOwner", "The top part",
                props: new List<IJSBPart>() { addressRef },
                isRequired: true,
                isExpandable: false);
            return new JSBSchema(new IdentifierString("RemoteOwner"), Description, topPart);
        }
    }

    public class FragmentRefSchema : AbstractJsonSchema
    {
        public override NamespaceString Module { get { return new NamespaceString("RemoteOwner"); } }

        public override string Description { get { return "A schema with a remote fragment reference"; } }

        protected override JSBSchema BuildJsonSchema()
        {
            JSBRef zipRef = new JSBRef("Zip", "The zip definition of the remote schema", new Uri("https://example.com/Remote/address.schema.json#/definitions/zip", UriKind.Absolute), isRequired: true);
            JSBObject topPart = new JSBObject("FragmentRefHolder", "The top part",
                props: new List<IJSBPart>() { zipRef },
                isRequired: true,
                isExpandable: false);
            return new JSBSchema(new IdentifierString("FragmentRefHolder"), Description, topPart);
        }
    }
}
