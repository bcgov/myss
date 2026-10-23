namespace Myss.AdminApi.Providers
{
    /// <summary>
    /// A form's version numbers, read together so that a versioned save decides on one view of them.
    /// </summary>
    /// <param name="LatestVersion">The highest version, draft or published; 0 when the form does not exist.</param>
    /// <param name="InProgressDraftVersion">The unpublished draft above the latest published version, or null when there is none.</param>
    public sealed record FormSpecVersionState(int LatestVersion, int? InProgressDraftVersion);
}
