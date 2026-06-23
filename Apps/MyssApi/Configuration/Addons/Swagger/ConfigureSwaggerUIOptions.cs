namespace Myss.Api.Configuration.Addons.Swagger
{
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using Asp.Versioning.ApiExplorer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.Options;
    using Swashbuckle.AspNetCore.SwaggerUI;

    /// <inheritdoc cref="SwaggerUIOptions"/>
    public sealed class ConfigureSwaggerUiOptions : IConfigureOptions<SwaggerUIOptions>
    {
        private readonly IApiVersionDescriptionProvider provider;
        private readonly SwaggerConfig config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureSwaggerUiOptions"/> class.
        /// </summary>
        /// <param name="versionDescriptionProvider">versionDescriptionProvider.</param>
        /// <param name="settings">settings.</param>
        public ConfigureSwaggerUiOptions(
            IApiVersionDescriptionProvider versionDescriptionProvider,
            IOptions<SwaggerConfig> settings
        )
        {
#pragma warning disable S3236
            Debug.Assert(
                versionDescriptionProvider != null,
                "The versionDescriptionProvider parameter cannot be null."
            );
            Debug.Assert(settings != null, "The settings parameter cannot be null.");
#pragma warning restore S3236
            this.provider = versionDescriptionProvider;
            this.config = settings.Value;
        }

        /// <summary>
        /// Configure.
        /// </summary>
        /// <param name="options">options.</param>
        public void Configure(SwaggerUIOptions options)
        {
            this.provider.ApiVersionDescriptions.ToList()
                .ForEach(description =>
                {
                    options.SwaggerEndpoint(
                        $"/{this.config.RoutePrefixWithSlash}{description.GroupName}/swagger.json",
                        description.GroupName.ToUpperInvariant()
                    );
                });
        }
    }
}
