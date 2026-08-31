namespace Myss.Api.Providers
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Reads embedded ODT templates from the assembly so callers do not know whether the source is on-disk or embedded.
    /// </summary>
    public class EmbeddedTemplateProvider : ITemplateProvider
    {
        private const string TemplateRootNamespace = "Myss.Api.Templates";

        public async Task<byte[]> GetTemplateAsync(string templateName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                throw new ArgumentException("Template name is required.", nameof(templateName));
            }

            string resourceName = $"{TemplateRootNamespace}.{templateName.TrimStart('/').Replace('/', '.')}";
            Assembly assembly = Assembly.GetExecutingAssembly();

            await using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                throw new InvalidOperationException(
                    $"Embedded template '{resourceName}' was not found.");
            }

            await using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
    }
}
