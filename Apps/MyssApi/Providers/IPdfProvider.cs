namespace Myss.Api.Providers
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for rendering PDFs from document templates through an external service.
    /// </summary>
    public interface IPdfProvider
    {
        /// <summary>
        /// Renders an ODT template as a PDF.
        /// </summary>
        /// <param name="odtTemplate">The ODT template bytes.</param>
        /// <param name="data">The substitution data for the template.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The generated PDF bytes.</returns>
        Task<byte[]> GenerateFromOdtAsync(
            byte[] odtTemplate,
            object data,
            CancellationToken cancellationToken);
    }
}