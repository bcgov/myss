namespace Myss.Common.Content
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Where the Strapi readers (<see cref="StrapiFormSpecProvider"/>,
    /// <see cref="EligibilityRateTableReader"/>) read from, and with which token.
    /// <para>
    /// Each API registers one: MyssApi reads with its read-only <c>Strapi:ApiToken</c>,
    /// MyssAdminApi with <c>Strapi:AdminApiToken</c>, the only Strapi token it holds
    /// (it also grants find and findOne). The token is deliberately not defaulted: an
    /// unset value means anonymous reads, which Strapi's Public role refuses, so a
    /// missing token fails loudly as a 403 rather than quietly.
    /// </para>
    /// </summary>
    public sealed class StrapiReadOptions
    {
        /// <summary>Gets the content engine's base address.</summary>
        public required Uri BaseAddress { get; init; }

        /// <summary>Gets the API token, or null when none is configured.</summary>
        public string? ApiToken { get; init; }

        /// <summary>Gets the setting the token was read from, named in log messages.</summary>
        public required string TokenSettingName { get; init; }

        /// <summary>
        /// Reads <c>Strapi:BaseUrl</c> (default <c>http://localhost:1337</c>) and the token
        /// from <paramref name="tokenSettingName"/>.
        /// </summary>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="tokenSettingName">The setting holding this API's Strapi token.</param>
        /// <returns>The options.</returns>
        public static StrapiReadOptions FromConfiguration(IConfiguration configuration, string tokenSettingName)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            string? token = configuration.GetValue<string>(tokenSettingName);
            return new StrapiReadOptions
            {
                BaseAddress = new Uri(configuration.GetValue<string>("Strapi:BaseUrl") ?? "http://localhost:1337"),
                ApiToken = string.IsNullOrWhiteSpace(token) ? null : token.Trim(),
                TokenSettingName = tokenSettingName,
            };
        }

        /// <summary>Points a client at the content engine and attaches the token, when there is one.</summary>
        /// <param name="httpClient">The client to configure.</param>
        public void ApplyTo(HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(httpClient);

            httpClient.BaseAddress = this.BaseAddress;
            if (this.ApiToken is not null)
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.ApiToken);
            }
        }
    }
}
