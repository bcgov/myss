namespace Myss.Api.Tests.Configuration
{
    using Microsoft.OpenApi;
    using Myss.Api.Configuration.Addons.Swagger;
    using Swashbuckle.AspNetCore.SwaggerGen;

    /// <summary>
    /// Tests for <see cref="SwaggerGenericFilter"/>, which gives a closed generic
    /// model a readable schema title.
    /// </summary>
    public class SwaggerGenericFilterTests
    {
        [Fact]
        public void TitlesAClosedGenericType_WithItsTypeArgument()
        {
            var schema = new OpenApiSchema();

            new SwaggerGenericFilter().Apply(schema, ContextFor(typeof(List<string>)));

            Assert.Equal("List<String>", schema.Title);
        }

        [Fact]
        public void LeavesANonGenericTypeUntitled()
        {
            var schema = new OpenApiSchema();

            new SwaggerGenericFilter().Apply(schema, ContextFor(typeof(string)));

            Assert.Null(schema.Title);
        }

        [Fact]
        public void IgnoresASchemaReference_ForAGenericType()
        {
            // Only a concrete schema is mutable in Microsoft.OpenApi 2.
            var reference = new OpenApiSchemaReference("ListOfString");

            Exception? thrown = Record.Exception(
                () => new SwaggerGenericFilter().Apply(reference, ContextFor(typeof(List<string>))));

            Assert.Null(thrown);
        }

        // The filter reads only the context's Type; no schema generator is involved.
        private static SchemaFilterContext ContextFor(Type type) =>
            new(type, schemaGenerator: null!, new SchemaRepository());
    }
}
