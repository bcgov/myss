namespace Myss.Api.Configuration.Addons.Swagger
{
    using Asp.Versioning;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Swashbuckle.AspNetCore.Swagger;
    using Swashbuckle.AspNetCore.SwaggerGen;
    using Swashbuckle.AspNetCore.SwaggerUI;

    /// <summary>
    /// Service Collection(IServiceCollection) Extensions.
    /// </summary>
    public static class SwaggerServices
    {
        /// <summary>
        /// Add AddVersionedApiExplorer and AddApiVersioning middlewares.
        /// </summary>
        /// <param name="services">services.</param>
        /// <returns>IServiceCollection.</returns>
        public static IServiceCollection AddApiVersionWithExplorer(this IServiceCollection services)
        {
            services
                .AddApiVersioning(options =>
                {
                    options.AssumeDefaultVersionWhenUnspecified = true;
                    options.ReportApiVersions = true;
                    options.DefaultApiVersion = new ApiVersion(1, 0);
                })
                .AddApiExplorer(options =>
                {
                    options.GroupNameFormat = "'v'VVV";
                    options.SubstituteApiVersionInUrl = true;
                });

            return services;
        }

        /// <summary>
        /// Add swagger services.
        /// </summary>
        /// <param name="services"><see cref="IServiceCollection"/>/>.</param>
        /// <returns>IServiceCollection.</returns>
        public static IServiceCollection AddSwaggerOptions(this IServiceCollection services)
        {
            return services
                .AddTransient<IConfigureOptions<SwaggerOptions>, ConfigureSwaggerOptions>()
                .AddTransient<IConfigureOptions<SwaggerUIOptions>, ConfigureSwaggerUiOptions>()
                .AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerGenOptions>();
        }
    }
}
