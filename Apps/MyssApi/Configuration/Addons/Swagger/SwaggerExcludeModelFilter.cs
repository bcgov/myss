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

            RemoveExcludedProperties(concreteSchema, context.Type);
            ListIncludedEnumMembers(concreteSchema, context.Type);
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            IDictionary<string, IOpenApiSchema>? schemas = swaggerDoc.Components?.Schemas;
            if (schemas is null)
            {
                return;
            }

            // Snapshot the matching keys: entries are removed while iterating.
            List<string> excludedSchemaKeys = schemas
                .Keys.Where(key => ExcludedKeys.Any(x => x.EndsWith(key, StringComparison.Ordinal)))
                .ToList();

            foreach (var key in excludedSchemaKeys)
            {
                schemas.Remove(key);
            }
        }

        private static void RemoveExcludedProperties(OpenApiSchema schema, Type type)
        {
            if (schema.Properties == null)
            {
                return;
            }

            var excludedProperties = type.GetProperties()
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

        private static void ListIncludedEnumMembers(OpenApiSchema schema, Type type)
        {
            var enumType = type.IsEnum ? type : Nullable.GetUnderlyingType(type);

            if (enumType is not { IsEnum: true })
            {
                return;
            }

            schema.Enum = Enum.GetNames(enumType)
                .Where(name =>
                    !enumType.GetMember(name)[0].GetCustomAttributes<SwaggerExcludeAttribute>().Any()
                )
                .Select(name => (JsonNode)JsonValue.Create(name))
                .ToList();
        }
    }
}
