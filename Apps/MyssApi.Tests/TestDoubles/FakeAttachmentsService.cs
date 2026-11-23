namespace Myss.Api.Tests.TestDoubles
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Myss.Api.Models;
    using Myss.Api.Services;

    /// <summary>
    /// An <see cref="IAttachmentsService"/> that knows only the files released
    /// to it with <see cref="Release"/>. Uploads and lists are not supported.
    /// </summary>
    public sealed class FakeAttachmentsService : IAttachmentsService
    {
        private readonly Dictionary<Guid, string> _released = [];

        /// <summary>Records a file as uploaded by the owner and released after a clean scan.</summary>
        /// <param name="ownerSubject">The identity subject that uploaded it.</param>
        /// <returns>The file's ID.</returns>
        public Guid Release(string ownerSubject)
        {
            Guid id = Guid.NewGuid();
            _released[id] = ownerSubject;
            return id;
        }

        /// <inheritdoc/>
        public Task<IReadOnlySet<Guid>> FindReleasedAsync(
            IReadOnlyCollection<Guid> ids,
            string ownerSubject,
            CancellationToken cancellationToken)
        {
            IReadOnlySet<Guid> found = ids
                .Where(id => _released.TryGetValue(id, out string? owner) && owner == ownerSubject)
                .ToHashSet();
            return Task.FromResult(found);
        }

        /// <inheritdoc/>
        public Task<AttachmentUploadResult> UploadAsync(
            string fileName,
            string contentType,
            long sizeBytes,
            Stream content,
            CancellationToken cancellationToken,
            Guid? submissionId = null,
            Func<CancellationToken, Task<bool>>? stillAccepting = null) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task<IReadOnlyList<AttachmentResponseModel>> ListForSubmissionAsync(
            Guid submissionId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task<bool> DeleteFromSubmissionAsync(
            Guid submissionId,
            Guid attachmentId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        /// <inheritdoc/>
        public Task<IReadOnlyList<AttachmentResponseModel>> ListOwnAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
