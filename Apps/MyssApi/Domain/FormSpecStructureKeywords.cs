namespace Myss.Api.Domain
{
    using System.Collections.Generic;

    /// <summary>
    /// Stable keywords for form-spec <em>structure</em> failures — the fast-path
    /// counterpart to <see cref="ValidationKeywords"/> (which is about submitted
    /// answers). These deliberately mirror the keywords in MyssContent's
    /// <c>src/lib/form-spec-rules.ts</c> (<c>FormSpecRule</c>), which is the
    /// authoritative publish-time gate: MyssApi's <c>ValidateSpecStructure</c> is
    /// only a convenience that catches the cheap structural mistakes early, so it
    /// speaks the same keyword vocabulary. If a keyword changes there, change it
    /// here together.
    /// </summary>
    public static class FormSpecStructureKeywords
    {
        /// <summary>The <c>spec</c> string could not be parsed as JSON.</summary>
        public const string SpecUnparseable = "FORMSPEC.SPEC.UNPARSEABLE";

        /// <summary>The <c>spec</c> is valid JSON but not an object.</summary>
        public const string SpecNotAnObject = "FORMSPEC.SPEC.NOT_AN_OBJECT";

        /// <summary>The spec has no <c>components</c> array.</summary>
        public const string ComponentsMissing = "FORMSPEC.COMPONENTS.MISSING";

        /// <summary>The spec's <c>components</c> array is empty.</summary>
        public const string ComponentsEmpty = "FORMSPEC.COMPONENTS.EMPTY";

        /// <summary>A component has no non-empty <c>key</c>.</summary>
        public const string ComponentKeyMissing = "FORMSPEC.COMPONENT_KEY.MISSING";

        /// <summary>Two components share a <c>key</c>.</summary>
        public const string ComponentKeyDuplicate = "FORMSPEC.COMPONENT_KEY.DUPLICATE";

        /// <summary>A <c>conditional.when</c> names a component key that does not exist.</summary>
        public const string ConditionalUnknownField = "FORMSPEC.CONDITIONAL.UNKNOWN_FIELD";

        /// <summary>The submitted version is not exactly the next in sequence.</summary>
        public const string VersionNotNext = "FORMSPEC.VERSION.NOT_NEXT";

        /// <summary>An attempt was made to change a published (immutable) version.</summary>
        public const string PublishedImmutable = "FORMSPEC.PUBLISHED.IMMUTABLE";

        /// <summary>There is no in-progress draft to publish for the form.</summary>
        public const string NothingToPublish = "FORMSPEC.PUBLISH.NO_DRAFT";

        /// <summary>The content engine refused the write (its lifecycle is the authoritative gate).</summary>
        public const string StrapiRefused = "FORMSPEC.STRAPI.REFUSED";

        /// <summary>
        /// The authoritative lifecycle vocabulary (the <c>FormSpecRule</c> values in
        /// MyssContent's <c>form-spec-rules.ts</c>). A Strapi 400 is trusted as a
        /// lifecycle refusal only when it carries at least one of these; unknown
        /// keywords are dropped, and our own <see cref="NothingToPublish"/> /
        /// <see cref="StrapiRefused"/> are deliberately not part of Strapi's set.
        /// </summary>
        public static readonly IReadOnlySet<string> LifecycleKeywords = new HashSet<string>(System.StringComparer.Ordinal)
        {
            SpecUnparseable,
            SpecNotAnObject,
            ComponentsMissing,
            ComponentsEmpty,
            ComponentKeyMissing,
            ComponentKeyDuplicate,
            ConditionalUnknownField,
            VersionNotNext,
            PublishedImmutable,
        };
    }
}
