namespace Myss.Api.Configuration.Addons.Swagger
{
    using Microsoft.Extensions.Options;
    using Swashbuckle.AspNetCore.Swagger;

    /// <inheritdoc cref="SwaggerOptions"/>
    public sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerOptions>
    {
        private readonly SwaggerConfig config;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigureSwaggerOptions"/> class.
        /// </summary>
        /// <param name="settings">settings.</param>
        public ConfigureSwaggerOptions(IOptions<SwaggerConfig> settings)
        {
            this.config = settings.Value;
        }

        /// <inheritdoc/>
        public void Configure(SwaggerOptions options)
        {
            options.RouteTemplate =
                this.config.RouteTemplatePrefix + "/{documentName}/swagger.json";
            if (!string.IsNullOrEmpty(this.config.BasePath))
            {
                options.PreSerializeFilters.Add(
                    (swaggerDoc, httpReq) =>
                        swaggerDoc.Servers = [
                            new()
                            {
                                Url =
                                    $"{httpReq.Scheme}://{httpReq.Host.Value}{this.config.BasePath}",
                            },
                        ]
                );
            }
        }
    }
}
