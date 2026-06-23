namespace Myss.Api.Configuration.Addons.Swagger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.OpenApi.Any;
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public class SwaggerExcludeModelFilter : IDocumentFilter, ISchemaFilter
    {
        private static HashSet<string> ExcludedKeys = new();

        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.GetCustomAttribute<SwaggerExcludeAttribute>() != null)
            {
                if (context.Type.FullName is not null)
                {
                    ExcludedKeys.Add(context.Type.FullName);
                }

                return;
            }

            if (schema.Properties != null)
            {
                var excludedProperties = context
                    .Type.GetProperties()
                    .Where(t => t.GetCustomAttribute<SwaggerExcludeAttribute>() != null);

                foreach (var excludedProperty in excludedProperties)
                {
                    var propertyToRemove = schema.Properties.Keys.SingleOrDefault(x =>
                        string.Equals(x, excludedProperty.Name, StringComparison.OrdinalIgnoreCase)
                    );

                    if (propertyToRemove != null)
                    {
                        schema.Properties.Remove(propertyToRemove);
                    }
                }
            }

            var enumType = context.Type.IsEnum
                ? context.Type
                : Nullable.GetUnderlyingType(context.Type);

            if (enumType is { IsEnum: true })
            {
                var enums = new List<IOpenApiAny>();

                foreach (var name in Enum.GetNames(enumType))
                {
                    var value = enumType.GetMember(name)[0];
                    if (!value.GetCustomAttributes<SwaggerExcludeAttribute>().Any())
                    {
                        enums.Add(new OpenApiString(name));
                    }
                }

                schema.Enum = enums;
            }
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            foreach (var key in swaggerDoc.Components.Schemas.Keys)
            {
                if (ExcludedKeys.Any(x => x.EndsWith(key, StringComparison.Ordinal)))
                {
                    swaggerDoc.Components.Schemas.Remove(key);
                }
            }
        }
    }
}
