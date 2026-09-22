namespace Myss.Api.Configuration.Addons.Swagger
{
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Microsoft.AspNetCore.Mvc.ApiExplorer;
    using Microsoft.OpenApi;
    using Swashbuckle.AspNetCore.SwaggerGen;

    /// <summary>
    /// Represents the Swagger/Swashbuckle operation filter used to document the implicit API version parameter.
    /// </summary>
    /// <remarks>
    /// This <see cref="IOperationFilter"/> is only required due to bugs in the <see cref="SwaggerGenerator"/>.
    /// Once they are fixed and published, this class can be removed.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public sealed class OperationFilter : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the specified operation using the given context.
        /// </summary>
        /// <param name="operation">The operation to apply the filter to.</param>
        /// <param name="context">The current operation filter context.</param>
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters != null)
            {
                foreach (IOpenApiParameter openApiParameter in operation.Parameters)
                {
                    // Microsoft.OpenApi 2 exposes parameters through a read-only
                    // interface; only the concrete type is mutable. A reference
                    // to a shared component parameter is left as declared.
                    if (openApiParameter is not OpenApiParameter parameter)
                    {
                        continue;
                    }

                    ApiParameterDescription description =
                        context.ApiDescription.ParameterDescriptions.First(p =>
                            p.Name == parameter.Name
                        );
                    ApiParameterRouteInfo? routeInfo = description.RouteInfo;

                    parameter.Description ??= description.ModelMetadata?.Description;

                    if (routeInfo == null)
                    {
                        continue;
                    }

                    parameter.Required |= !routeInfo.IsOptional;
                }
            }
        }
    }
}
