namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Resolves a template by name and returns the template bytes without exposing the source implementation.
    /// </summary>
    public interface ITemplateProvider
    {
        /// <summary>
        /// Reads template bytes for the supplied template name.
        /// </summary>
        /// <param name="templateName">The template name, e.g. "bus-pass.odt".</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The template bytes.</returns>
        Task<byte[]> GetTemplateAsync(string templateName, CancellationToken cancellationToken);
    }
}
