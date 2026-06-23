namespace Myss.Api.Configuration.Addons.Swagger
{
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.OpenApi.Models;

    /// <summary>
    /// Swagger Configuration.
    /// </summary>
    public class SwaggerConfig
    {
        /// <summary>
        /// Gets or sets swagger Info.
        /// </summary>
        public OpenApiInfo? Info { get; set; }

        /// <summary>
        /// Gets or sets RoutePrefix.
        /// </summary>
        public string? RoutePrefix { get; set; }

        /// <summary>
        /// Gets or sets RoutePrefix.
        /// </summary>
        public string? RouteTemplatePrefix { get; set; } = "swagger";

        /// <summary>
        /// Gets or sets BasePath.
        /// </summary>
        public string? BasePath { get; set; }

        /// <summary>
        /// Gets Route Prefix with tailing slash.
        /// </summary>
        public string RoutePrefixWithSlash =>
            string.IsNullOrWhiteSpace(this.RoutePrefix) ? string.Empty : this.RoutePrefix + "/";
    }
}
