namespace Myss.Api.Tests.Configuration
{
    using System.Text.Json.Nodes;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.OpenApi;
    using Myss.Api.Configuration.Addons.Swagger;
    using Swashbuckle.AspNetCore.SwaggerGen;

    /// <summary>
    /// Tests for <see cref="SwaggerExcludeModelFilter"/>: what it hides from the
    /// generated OpenAPI document, and that it leaves everything else alone.
    /// </summary>
    public class SwaggerExcludeModelFilterTests
    {
        private enum Colour
        {
            Red,
            Green,
        }

        [Fact]
        public void SchemaFilter_ListsEnumMembersByName()
        {
            var schema = new OpenApiSchema();

            new SwaggerExcludeModelFilter().Apply(schema, ContextFor(typeof(Colour)));

            Assert.Equal(["Red", "Green"], EnumNames(schema));
        }

        [Fact]
        public void SchemaFilter_ListsEnumMembersByName_ForANullableEnum()
        {
            var schema = new OpenApiSchema();

            new SwaggerExcludeModelFilter().Apply(schema, ContextFor(typeof(Colour?)));

            Assert.Equal(["Red", "Green"], EnumNames(schema));
        }

        [Fact]
        public void SchemaFilter_LeavesPropertiesOfAnOrdinaryModelUntouched()
        {
            var schema = new OpenApiSchema
            {
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["name"] = new OpenApiSchema(),
                    ["count"] = new OpenApiSchema(),
                },
            };

            new SwaggerExcludeModelFilter().Apply(schema, ContextFor(typeof(VisibleModel)));

            Assert.Equal(["count", "name"], schema.Properties.Keys.Order());
            Assert.Null(schema.Enum);
        }

        [Fact]
        public void SchemaFilter_LeavesASchemaWithoutPropertiesUntouched()
        {
            var schema = new OpenApiSchema { Properties = null };

            new SwaggerExcludeModelFilter().Apply(schema, ContextFor(typeof(VisibleModel)));

            Assert.Null(schema.Properties);
            Assert.Null(schema.Enum);
        }

        [Fact]
        public void SchemaFilter_IgnoresASchemaReference()
        {
            // Only a concrete schema is mutable in Microsoft.OpenApi 2; a reference
            // must pass through without being touched, even for an enum type.
            var reference = new OpenApiSchemaReference("Colour");

            Exception? thrown = Record.Exception(
                () => new SwaggerExcludeModelFilter().Apply(reference, ContextFor(typeof(Colour))));

            Assert.Null(thrown);
        }

        [Fact]
        public void DocumentFilter_RemovesSchemasOfExcludedTypes_AndKeepsTheRest()
        {
            var filter = new SwaggerExcludeModelFilter();
            string hiddenKey = typeof(HiddenModel).FullName!;
            string visibleKey = typeof(VisibleModel).FullName!;

            // The schema pass records the excluded type; the document pass removes it.
            filter.Apply(new OpenApiSchema(), ContextFor(typeof(HiddenModel)));

            var document = new OpenApiDocument
            {
                Components = new OpenApiComponents
                {
                    Schemas = new Dictionary<string, IOpenApiSchema>
                    {
                        [hiddenKey] = new OpenApiSchema(),
                        [visibleKey] = new OpenApiSchema(),
                    },
                },
            };

            filter.Apply(document, DocumentContext());

            Assert.Equal([visibleKey], document.Components.Schemas.Keys);
        }

        [Fact]
        public void SchemaFilter_DoesNotModifyTheSchemaOfAnExcludedType()
        {
            var schema = new OpenApiSchema
            {
                Properties = new Dictionary<string, IOpenApiSchema> { ["secret"] = new OpenApiSchema() },
            };

            new SwaggerExcludeModelFilter().Apply(schema, ContextFor(typeof(HiddenModel)));

            Assert.Equal(["secret"], schema.Properties.Keys);
        }

        [Fact]
        public void DocumentFilter_ToleratesADocumentWithoutComponents()
        {
            var document = new OpenApiDocument { Components = null };

            Exception? thrown = Record.Exception(
                () => new SwaggerExcludeModelFilter().Apply(document, DocumentContext()));

            Assert.Null(thrown);
        }

        [Fact]
        public void DocumentFilter_ToleratesComponentsWithoutSchemas()
        {
            var document = new OpenApiDocument { Components = new OpenApiComponents { Schemas = null } };

            Exception? thrown = Record.Exception(
                () => new SwaggerExcludeModelFilter().Apply(document, DocumentContext()));

            Assert.Null(thrown);
            Assert.Null(document.Components.Schemas);
        }

        private static List<string> EnumNames(OpenApiSchema schema) =>
            schema.Enum!.Select(node => ((JsonValue)node).GetValue<string>()).ToList();

        // The filters read only the context's Type; no schema generator is involved.
        private static SchemaFilterContext ContextFor(Type type) =>
            new(type, schemaGenerator: null!, new SchemaRepository());

        private static DocumentFilterContext DocumentContext() =>
            new(Array.Empty<ApiDescription>(), schemaGenerator: null!, new SchemaRepository());

        // The models below are only ever reflected over (typeof), never instantiated.
        [SwaggerExclude]
        private abstract class HiddenModel
        {
            public string? Secret { get; set; }
        }

        private abstract class VisibleModel
        {
            public string? Name { get; set; }

            public int Count { get; set; }
        }
    }
}
