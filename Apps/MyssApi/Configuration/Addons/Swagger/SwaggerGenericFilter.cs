namespace Myss.Api.Configuration.Addons.Swagger
{
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public class SwaggerGenericFilter : ISchemaFilter
    {
        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            var type = context.Type;

            // Microsoft.OpenApi 2 hands filters a read-only interface; only the
            // concrete schema is mutable (a schema reference is not).
            if (!type.IsGenericType || schema is not OpenApiSchema concreteSchema)
            {
                return;
            }

            concreteSchema.Title = $"{type.Name[0..^2]}<{type.GenericTypeArguments[0].Name}>";
        }
    }
}
