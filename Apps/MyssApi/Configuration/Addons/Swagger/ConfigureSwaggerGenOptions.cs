namespace Myss.Api.Configuration.Addons.Swagger
{
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using Asp.Versioning.ApiExplorer;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;

    /// <inheritdoc/>
    /// <summary>
    /// Implementation of IConfigureOptions for Swagger Options.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ConfigureSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IApiVersionDescriptionProvider provider;
        private readonly SwaggerConfig config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureSwaggerGenOptions"/> class.
        /// </summary>
        /// <param name="versionDescriptionProvider">IApiVersionDescriptionProvider.</param>
        /// <param name="swaggerConfiguration">App Settings for Swagger.</param>
        public ConfigureSwaggerGenOptions(
            IApiVersionDescriptionProvider versionDescriptionProvider,
            IOptions<SwaggerConfig> swaggerConfiguration
        )
        {
            Debug.Assert(
                versionDescriptionProvider != null,
                "The versionDescriptionProvider parameter cannot be null."
            );
            Debug.Assert(swaggerConfiguration != null, "The swagger parameter cannot be null.");

            this.provider = versionDescriptionProvider;
            this.config = swaggerConfiguration.Value;
        }

        /// <inheritdoc/>
        public void Configure(SwaggerGenOptions options)
        {
            options.OperationFilter<OperationFilter>();
            options.IgnoreObsoleteActions();
            options.IgnoreObsoleteProperties();

            options.AddSecurityDefinition(
                "bearer",
                new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description =
                        "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Type = SecuritySchemeType.Http,
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Scheme = "bearer",
                }
            );

            this.AddSwaggerDocumentForEachDiscoveredApiVersion(options);
        }

        private void AddSwaggerDocumentForEachDiscoveredApiVersion(SwaggerGenOptions options)
        {
            foreach (ApiVersionDescription description in this.provider.ApiVersionDescriptions)
            {
                this.config.Info!.Version = description.ApiVersion.ToString();

                if (description.IsDeprecated)
                {
                    this.config.Info.Description = $"{this.config.Info.Description} - DEPRECATED";
                }

                options.SwaggerDoc(description.GroupName, this.config.Info);
            }
        }
    }
}
