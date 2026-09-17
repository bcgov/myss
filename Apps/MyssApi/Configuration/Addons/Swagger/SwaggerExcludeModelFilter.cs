namespace Myss.Api.Configuration.Addons.Swagger
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json.Nodes;
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;

    public class SwaggerExcludeModelFilter : IDocumentFilter, ISchemaFilter
    {
        private static HashSet<string> ExcludedKeys = new();

        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.GetCustomAttribute<SwaggerExcludeAttribute>() != null)
            {
                if (context.Type.FullName is not null)
                {
                    ExcludedKeys.Add(context.Type.FullName);
                }

                return;
            }

            // Microsoft.OpenApi 2 hands filters a read-only interface; only the
            // concrete schema is mutable (a schema reference is not).
            if (schema is not OpenApiSchema concreteSchema)
            {
                return;
            }

            if (concreteSchema.Properties != null)
            {
                var excludedProperties = context
                    .Type.GetProperties()
                    .Where(t => t.GetCustomAttribute<SwaggerExcludeAttribute>() != null);

                foreach (var excludedProperty in excludedProperties)
                {
                    var propertyToRemove = concreteSchema.Properties.Keys.SingleOrDefault(x =>
                        string.Equals(x, excludedProperty.Name, StringComparison.OrdinalIgnoreCase)
                    );

                    if (propertyToRemove != null)
                    {
                        concreteSchema.Properties.Remove(propertyToRemove);
                    }
                }
            }

            var enumType = context.Type.IsEnum
                ? context.Type
                : Nullable.GetUnderlyingType(context.Type);

            if (enumType is { IsEnum: true })
            {
                var enums = new List<JsonNode>();

                foreach (var name in Enum.GetNames(enumType))
                {
                    var value = enumType.GetMember(name)[0];
                    if (!value.GetCustomAttributes<SwaggerExcludeAttribute>().Any())
                    {
                        enums.Add(JsonValue.Create(name));
                    }
                }

                concreteSchema.Enum = enums;
            }
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            IDictionary<string, IOpenApiSchema>? schemas = swaggerDoc.Components?.Schemas;
            if (schemas is null)
            {
                return;
            }

            // Snapshot the keys: entries are removed while iterating.
            foreach (var key in schemas.Keys.ToList())
            {
                if (ExcludedKeys.Any(x => x.EndsWith(key, StringComparison.Ordinal)))
                {
                    schemas.Remove(key);
                }
            }
        }
    }
}
