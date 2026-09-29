namespace Icm.Api.ConsoleApp.Configuration
{
    using System.Diagnostics.CodeAnalysis;

    /// <summary>Everything appsettings.json supplies.</summary>
    public class ConsoleSettings
    {
        /// <summary>Gets or sets the ICM connection and credentials.</summary>
        public IcmSettings Icm { get; set; } = new();

        /// <summary>
        /// Gets or sets what to run: <c>query</c> (the Service Request search this tool
        /// started as) or <c>buspass</c> (submit a bus pass request through the workflow,
        /// then read the created service request back — a hand-run integration test),
        /// <c>contact</c> (search contacts by id, name, SIN, PHN, birth date…), <c>case</c>
        /// (look a case up by row id, case number or key player and dump it with the people
        /// on it), or <c>describe</c> (fetch a resource's OpenAPI document from ICM's
        /// <c>…/describe</c> endpoint and save it).
        /// </summary>
        public string Mode { get; set; } = "query";

        /// <summary>Gets a value indicating whether this run submits a bus pass.</summary>
        public bool IsBusPassMode =>
            string.Equals(Mode, "buspass", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether this run searches contacts.</summary>
        public bool IsContactMode =>
            string.Equals(Mode, "contact", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether this run looks up a case.</summary>
        public bool IsCaseMode =>
            string.Equals(Mode, "case", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether this run fetches a describe document.</summary>
        public bool IsDescribeMode =>
            string.Equals(Mode, "describe", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets or sets the search to run.</summary>
        public QuerySettings Query { get; set; } = new();

        /// <summary>Gets or sets the contact search for <c>contact</c> mode.</summary>
        public ContactSettings Contact { get; set; } = new();

        /// <summary>Gets or sets the case lookup for <c>case</c> mode.</summary>
        public CaseSettings Case { get; set; } = new();

        /// <summary>Gets or sets what to fetch in <c>describe</c> mode.</summary>
        public DescribeSettings Describe { get; set; } = new();

        /// <summary>Gets or sets the bus pass submission for <c>buspass</c> mode.</summary>
        public BusPassSettings BusPass { get; set; } = new();

        /// <summary>
        /// Gets or sets how much of each record to print: <c>full</c> (every selected
        /// field), <c>summary</c> (one line per record), or <c>raw</c> (full, plus the
        /// untouched wire traffic — outgoing request bodies and response bodies).
        /// </summary>
        public string Output { get; set; } = "full";

        /// <summary>
        /// Checks the settings are usable, so a run fails on the configuration rather than
        /// on a confusing error from ICM twenty seconds later.
        /// </summary>
        /// <param name="problems">What is wrong, one line each. Empty when the settings are fine.</param>
        /// <returns>True when the settings are usable.</returns>
        public bool TryValidate([NotNullWhen(false)] out IReadOnlyList<string>? problems)
        {
            List<string> found = [];

            Require(found, "Icm:BaseUrl", Icm.BaseUrl, absolute: true);

            // Either the composed pair or the explicit override, not neither.
            if (Icm.Auth.IsTokenUrlOverridden)
            {
                Require(found, "Icm:Auth:TokenUrl", Icm.Auth.TokenUrl, absolute: true);
            }
            else
            {
                Require(found, "Icm:Auth:BaseUrl", Icm.Auth.BaseUrl, absolute: true);
                Require(found, "Icm:Auth:Realm", Icm.Auth.Realm);
            }

            Require(found, "Icm:Auth:ClientId", Icm.Auth.ClientId);
            Require(found, "Icm:Auth:ClientSecret", Icm.Auth.ClientSecret);

            if (Icm.TimeoutSeconds <= 0)
            {
                found.Add("Icm:TimeoutSeconds must be greater than zero.");
            }

            // The spec's own bounds. Catching them here turns a 400 from ICM into a
            // sentence that says which setting to change.
            if (Query.PageSize is < 1 or > 100)
            {
                found.Add($"Query:PageSize must be between 1 and 100 (it is {Query.PageSize}).");
            }

            if (Query.StartRowNum < 0)
            {
                found.Add($"Query:StartRowNum cannot be negative (it is {Query.StartRowNum}).");
            }

            if (!string.Equals(Mode, "query", StringComparison.OrdinalIgnoreCase)
                && !IsBusPassMode
                && !IsContactMode
                && !IsCaseMode
                && !IsDescribeMode)
            {
                found.Add($"Mode must be 'query', 'buspass', 'contact', 'case' or 'describe' (it is '{Mode}').");
            }

            // A typo here would otherwise be silently treated as `full` — for a
            // diagnostic tool, quietly not printing what was asked for is the worst
            // possible failure mode.
            if (!string.Equals(Output, "full", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Output, "summary", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Output, "raw", StringComparison.OrdinalIgnoreCase))
            {
                found.Add($"Output must be 'full', 'summary' or 'raw' (it is '{Output}').");
            }

            if (IsBusPassMode)
            {
                BusPass.Validate(found);
            }

            if (IsContactMode)
            {
                Contact.Validate(found);
            }

            if (IsCaseMode)
            {
                Case.Validate(found);
            }

            if (IsDescribeMode)
            {
                Require(found, "Describe:Resource", Describe.Resource);
            }

            problems = found.Count == 0 ? null : found;
            return found.Count == 0;
        }

        private static void Require(List<string> problems, string key, string? value, bool absolute = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                problems.Add($"{key} is not set.");
                return;
            }

            // The placeholders shipped in appsettings.json. Saying so explicitly beats
            // letting "<replace-me>" travel all the way to an authorization server.
            if (value.Contains("<replace-me", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"{key} is still the placeholder from appsettings.json.");
                return;
            }

            if (absolute && !Uri.TryCreate(value, UriKind.Absolute, out _))
            {
                problems.Add($"{key} is not an absolute URL ('{value}').");
            }
        }
    }

    /// <summary>ICM connection details and the credentials to reach it with.</summary>
    public class IcmSettings
    {
        /// <summary>Gets or sets the ICM base URL, including the version prefix.</summary>
        /// <remarks>
        /// A string rather than a <see cref="Uri"/> on purpose: bound as a
        /// <see cref="Uri"/>, the placeholder shipped in appsettings.json would throw
        /// inside the configuration binder before validation ever runs, and the message
        /// would be about type conversion rather than about the setting that needs filling
        /// in. <see cref="ConsoleSettings.TryValidate"/> checks it instead.
        /// </remarks>
        [SuppressMessage(
            "Design",
            "CA1056:URI-like properties should not be strings",
            Justification = "Validated by TryValidate so a placeholder reports as a setting, not a binder error.")]
        public string? BaseUrl { get; set; }

        /// <summary>Gets or sets how long to wait for ICM before giving up.</summary>
        public int TimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the ICM user every call acts as, sent as
        /// <c>X-ICM-TrustedUserName</c>. Null or empty sends no header.
        /// </summary>
        /// <remarks>
        /// Not required to build a request, so it is not validated — but ICM will refuse
        /// calls that need it, which is a clearer signal than a settings error would be.
        /// </remarks>
        public string? TrustedUserName { get; set; }

        /// <summary>Gets or sets the client-credentials details.</summary>
        public AuthSettings Auth { get; set; } = new();
    }

    /// <summary>The client-credentials grant details.</summary>
    /// <remarks>
    /// The token endpoint is composed from <see cref="BaseUrl"/> and <see cref="Realm"/>
    /// rather than configured whole, so the realm — the setting most likely to be wrong,
    /// and the one that produces an <c>invalid_client</c> indistinguishable from a bad
    /// secret — is a word in a committed file rather than a path segment buried in a URL
    /// somebody put in their secret store. Neither is sensitive; only the secret is.
    /// </remarks>
    public class AuthSettings
    {
        /// <summary>
        /// Gets or sets the Keycloak base URL — the server, plus <c>/auth</c> on
        /// deployments that still use it (BC Gov's loginproxy does; Keycloak 17 and later
        /// drop it by default).
        /// </summary>
        [SuppressMessage(
            "Design",
            "CA1056:URI-like properties should not be strings",
            Justification = "Validated by TryValidate so a placeholder reports as a setting, not a binder error.")]
        public string? BaseUrl { get; set; }

        /// <summary>Gets or sets the realm the client is registered in, e.g. <c>standard</c>.</summary>
        public string? Realm { get; set; }

        /// <summary>
        /// Gets or sets the full token endpoint, overriding <see cref="BaseUrl"/> and
        /// <see cref="Realm"/> when set. For an authorization server that is not Keycloak,
        /// or one with an unusual path.
        /// </summary>
        [SuppressMessage(
            "Design",
            "CA1056:URI-like properties should not be strings",
            Justification = "Validated by TryValidate so a placeholder reports as a setting, not a binder error.")]
        public string? TokenUrl { get; set; }

        /// <summary>Gets a value indicating whether <see cref="TokenUrl"/> is overriding the composed URL.</summary>
        public bool IsTokenUrlOverridden => !string.IsNullOrWhiteSpace(TokenUrl);

        /// <summary>
        /// Gets the token endpoint to call: <see cref="TokenUrl"/> when set, otherwise
        /// <c>{BaseUrl}/realms/{Realm}/protocol/openid-connect/token</c>.
        /// </summary>
        /// <returns>The endpoint, or null when there is not enough configuration to build one.</returns>
        [SuppressMessage(
            "Design",
            "CA1055:URI-like return values should not be strings",
            Justification = "Returns whatever configuration holds, placeholder and all, so TryValidate can report it as a setting rather than throwing inside a Uri constructor.")]
        public string? ResolveTokenUrl()
        {
            if (IsTokenUrlOverridden)
            {
                return TokenUrl;
            }

            if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(Realm))
            {
                return null;
            }

            return $"{BaseUrl.TrimEnd('/')}/realms/{Realm.Trim('/')}/protocol/openid-connect/token";
        }

        /// <summary>Gets or sets the client identifier.</summary>
        public string? ClientId { get; set; }

        /// <summary>Gets or sets the client secret. Never commit a real one.</summary>
        public string? ClientSecret { get; set; }

        /// <summary>Gets or sets the scopes to request. Empty asks for none.</summary>
        public IList<string> Scopes { get; } = [];
    }

    /// <summary>The contact search to run. Mirrors <see cref="Icm.Api.Models.ContactQuery"/>.</summary>
    /// <remarks>
    /// Every criterion identifies a person, so none has a committed value: pass them on
    /// the command line (<c>--Contact:LastName=… --Contact:BirthDate=1950-01-31</c>) or keep
    /// them in the user-secret store.
    /// </remarks>
    public class ContactSettings
    {
        /// <summary>Gets or sets the ICM row id to match.</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the person number to match.</summary>
        public string? PersonId { get; set; }

        /// <summary>Gets or sets the integration id to match.</summary>
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets the BC Services Card DID to match. Quote it: base64 may contain <c>+ / =</c>.</summary>
        public string? BcServicesCardDid { get; set; }

        /// <summary>Gets or sets the SIN to match: nine digits, no spaces.</summary>
        public string? Sin { get; set; }

        /// <summary>Gets or sets the PHN to match.</summary>
        public string? Phn { get; set; }

        /// <summary>Gets or sets the first name to match.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the middle name to match.</summary>
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets the last name to match.</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets how names are compared: <c>Exact</c>, <c>StartsWith</c> or <c>Contains</c>.</summary>
        public string NameMatch { get; set; } = "Exact";

        /// <summary>Gets or sets the birth date to match, as <c>yyyy-MM-dd</c>.</summary>
        public DateOnly? BirthDate { get; set; }

        /// <summary>Gets or sets the email to match.</summary>
        public string? Email { get; set; }

        /// <summary>Gets or sets the cell phone to match.</summary>
        public string? CellPhone { get; set; }

        /// <summary>Gets or sets the home phone to match.</summary>
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets the work phone to match.</summary>
        public string? WorkPhone { get; set; }

        /// <summary>Gets or sets the message phone to match.</summary>
        public string? MessagePhone { get; set; }

        /// <summary>Gets or sets the records per page.</summary>
        public int PageSize { get; set; } = 5;

        /// <summary>Gets or sets the zero-based index of the first record to return.</summary>
        public int StartRowNum { get; set; }

        /// <summary>Gets or sets the Siebel visibility mode, or null for ICM's default.</summary>
        public string? ViewMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to print the contacts' values. Off by
        /// default: the run then reports which fields came back filled, which answers
        /// "did the search and the mapping work" without putting people's names, birth
        /// dates and phone numbers in terminal scrollback.
        /// </summary>
        public bool ShowValues { get; set; }

        /// <summary>Gets the names of the criteria that are set — names only, for the run header.</summary>
        public IReadOnlyList<string> CriteriaSet =>
        [
            .. new (string Name, bool IsSet)[]
            {
                (nameof(Id), Id is not null),
                (nameof(PersonId), PersonId is not null),
                (nameof(IntegrationId), IntegrationId is not null),
                (nameof(BcServicesCardDid), BcServicesCardDid is not null),
                (nameof(Sin), Sin is not null),
                (nameof(Phn), Phn is not null),
                (nameof(FirstName), FirstName is not null),
                (nameof(MiddleName), MiddleName is not null),
                (nameof(LastName), LastName is not null),
                (nameof(BirthDate), BirthDate is not null),
                (nameof(Email), Email is not null),
                (nameof(CellPhone), CellPhone is not null),
                (nameof(HomePhone), HomePhone is not null),
                (nameof(WorkPhone), WorkPhone is not null),
                (nameof(MessagePhone), MessagePhone is not null),
            }.Where(criterion => criterion.IsSet).Select(criterion => criterion.Name),
        ];

        /// <summary>Adds what is wrong with these settings to <paramref name="problems"/>.</summary>
        /// <param name="problems">The list to add to.</param>
        public void Validate(IList<string> problems)
        {
            ArgumentNullException.ThrowIfNull(problems);

            if (CriteriaSet.Count == 0)
            {
                problems.Add(
                    "Contact mode needs at least one criterion, e.g. --Contact:LastName=… "
                    + "--Contact:BirthDate=1950-01-31, or --Contact:BcServicesCardDid='…'.");
            }

            if (!Enum.TryParse<Models.ContactNameMatch>(NameMatch, ignoreCase: true, out _))
            {
                problems.Add(
                    $"Contact:NameMatch '{NameMatch}' is not one of "
                    + $"{string.Join(", ", Enum.GetNames<Models.ContactNameMatch>())}.");
            }

            if (PageSize is < 1 or > 100)
            {
                problems.Add($"Contact:PageSize must be between 1 and 100 (it is {PageSize}).");
            }

            if (StartRowNum < 0)
            {
                problems.Add($"Contact:StartRowNum cannot be negative (it is {StartRowNum}).");
            }
        }

        /// <summary>Converts these settings into the query the library takes.</summary>
        /// <returns>The query.</returns>
        public Models.ContactQuery ToQuery() =>
            new()
            {
                Id = Id,
                PersonId = PersonId,
                IntegrationId = IntegrationId,
                BcServicesCardDid = BcServicesCardDid,
                Sin = Sin,
                Phn = Phn,
                FirstName = FirstName,
                MiddleName = MiddleName,
                LastName = LastName,
                NameMatch = Enum.Parse<Models.ContactNameMatch>(NameMatch, ignoreCase: true),
                BirthDate = BirthDate,
                Email = Email,
                CellPhone = CellPhone,
                HomePhone = HomePhone,
                WorkPhone = WorkPhone,
                MessagePhone = MessagePhone,
                PageSize = PageSize,
                StartRowNum = StartRowNum,
                ViewMode = string.IsNullOrWhiteSpace(ViewMode) ? null : ViewMode,
                IncludeTotalCount = true,
            };
    }

    /// <summary>The case lookup to run. Mirrors <see cref="Icm.Api.Models.CaseQuery"/>.</summary>
    /// <remarks>
    /// With <see cref="Id"/> set the case is read by key; otherwise the other criteria are
    /// searched, ANDed. Either way each case found is printed with the people on it.
    /// </remarks>
    public class CaseSettings
    {
        /// <summary>Gets or sets the case's ICM row id to read directly (the <c>case_key</c>).</summary>
        public string? Id { get; set; }

        /// <summary>Gets or sets the case number to search for (<c>Case Num</c>), e.g. <c>1-11077140770</c>.</summary>
        public string? CaseNumber { get; set; }

        /// <summary>Gets or sets the key player's contact row id to search for — the <c>Id</c> a contact search prints.</summary>
        public string? ContactId { get; set; }

        /// <summary>Gets or sets the key player's person number to search for — the <c>PersonId</c> a contact search prints.</summary>
        public string? PersonId { get; set; }

        /// <summary>Gets or sets the key player's integration id to search for.</summary>
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets the status to search for, e.g. <c>Open</c>.</summary>
        public string? Status { get; set; }

        /// <summary>Gets or sets the program to search for, e.g. <c>Employment and Assistance</c>.</summary>
        public string? Type { get; set; }

        /// <summary>Gets or sets the records per page.</summary>
        public int PageSize { get; set; } = 5;

        /// <summary>Gets or sets the zero-based index of the first record to return.</summary>
        public int StartRowNum { get; set; }

        /// <summary>
        /// Gets or sets the Siebel visibility mode. Null or blank is the library's default,
        /// <c>Manager</c> — MEASURED the one that sees cases; <c>Organization</c> does not.
        /// </summary>
        public string? ViewMode { get; set; }

        /// <summary>Gets or sets a value indicating whether to read each case's contacts too.</summary>
        public bool IncludeContacts { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to print the values. On by default: SIT1
        /// and SIT2 hold test data. Set false against anything else to print presence only.
        /// </summary>
        public bool ShowValues { get; set; } = true;

        /// <summary>Gets the names of the criteria that are set, for the run header.</summary>
        public IReadOnlyList<string> CriteriaSet =>
        [
            .. new (string Name, bool IsSet)[]
            {
                (nameof(Id), Id is not null),
                (nameof(CaseNumber), CaseNumber is not null),
                (nameof(ContactId), ContactId is not null),
                (nameof(PersonId), PersonId is not null),
                (nameof(IntegrationId), IntegrationId is not null),
                (nameof(Status), Status is not null),
                (nameof(Type), Type is not null),
            }.Where(criterion => criterion.IsSet).Select(criterion => criterion.Name),
        ];

        /// <summary>Adds what is wrong with these settings to <paramref name="problems"/>.</summary>
        /// <param name="problems">The list to add to.</param>
        public void Validate(IList<string> problems)
        {
            ArgumentNullException.ThrowIfNull(problems);

            if (CriteriaSet.Count == 0)
            {
                problems.Add(
                    "Case mode needs a row id or a criterion, e.g. --Case:Id=1-5371KIQ, "
                    + "--Case:CaseNumber=1-11077140770 or --Case:ContactId=1-532MU4J.");
            }

            if (PageSize is < 1 or > 100)
            {
                problems.Add($"Case:PageSize must be between 1 and 100 (it is {PageSize}).");
            }

            if (StartRowNum < 0)
            {
                problems.Add($"Case:StartRowNum cannot be negative (it is {StartRowNum}).");
            }
        }

        /// <summary>Converts these settings into the search the library takes.</summary>
        /// <returns>The query.</returns>
        public Models.CaseQuery ToQuery() =>
            new()
            {
                Id = Id,
                CaseNumber = CaseNumber,
                KeyPlayerContactId = ContactId,
                KeyPlayerPersonId = PersonId,
                KeyPlayerIntegrationId = IntegrationId,
                Status = Status,
                Type = Type,
                PageSize = PageSize,
                StartRowNum = StartRowNum,
                ViewMode = ViewMode,
                IncludeTotalCount = true,
            };

        /// <summary>Converts these settings into the options a read by key takes.</summary>
        /// <returns>The read options.</returns>
        public Models.CaseReadOptions ToReadOptions() => new() { ViewMode = ViewMode };
    }

    /// <summary>What <c>describe</c> mode fetches, and where it puts it.</summary>
    public class DescribeSettings
    {
        /// <summary>
        /// Gets or sets the resource to describe, as the two path segments after
        /// <c>data/</c> — <c>ServiceRequest/ServiceRequest</c>, <c>ICMContact/ICMContact</c>.
        /// </summary>
        public string? Resource { get; set; }

        /// <summary>
        /// Gets or sets the file to write the document to, or null to print it. A file is
        /// the point of the mode — <c>docs/integration/</c> keeps these documents — and it
        /// also keeps a large document out of the terminal.
        /// </summary>
        public string? OutputFile { get; set; }
    }

    /// <summary>The search to run. Mirrors <see cref="Icm.Api.Models.ServiceRequestQuery"/>.</summary>
    public class QuerySettings
    {
        /// <summary>Gets or sets the Siebel search expression.</summary>
        public string? SearchSpec { get; set; }

        /// <summary>
        /// Gets or sets an ICM row id to read directly after the search, or null to skip
        /// that step.
        /// </summary>
        /// <remarks>
        /// This is the <b>Row #</b> from Siebel's "About Record" dialog, not the SR # shown
        /// in the list. The SR # is not a field this business component exposes — asking
        /// for it by name is rejected outright — so the row id is the only handle on a
        /// specific record.
        /// </remarks>
        public string? ServiceRequestKey { get; set; }

        /// <summary>Gets or sets the Siebel sort expression.</summary>
        public string? SortSpec { get; set; }

        /// <summary>Gets or sets the fields to return. Empty returns them all.</summary>
        public IList<string> Fields { get; } = [];

        /// <summary>Gets or sets the child business components to link.</summary>
        public string? ChildLinks { get; set; }

        /// <summary>
        /// Gets or sets a child business component to read after the single-record
        /// read — <c>SRProspects</c>, say — or null to skip it. The child rows are
        /// fetched raw (<c>…/{key}/{child}/</c>) and printed as ICM sent them, because
        /// the library has no child-collection support yet.
        /// </summary>
        public string? ChildCollection { get; set; }

        /// <summary>Gets or sets the records per page.</summary>
        public int PageSize { get; set; } = 5;

        /// <summary>Gets or sets the first record to return.</summary>
        public int StartRowNum { get; set; }

        /// <summary>Gets or sets the Siebel visibility mode.</summary>
        public string? ViewMode { get; set; }

        /// <summary>Gets or sets a value indicating whether to ask ICM for the total count.</summary>
        public bool? IncludeTotalCount { get; set; }

        /// <summary>Gets or sets a value indicating whether ICM should omit empty fields.</summary>
        public bool? ExcludeEmptyFields { get; set; }

        /// <summary>Converts these settings into the query the library takes.</summary>
        /// <returns>The query.</returns>
        public Models.ServiceRequestQuery ToQuery() =>
            new()
            {
                SearchSpec = NullIfBlank(SearchSpec),
                SortSpec = NullIfBlank(SortSpec),
                Fields = Fields.Count == 0 ? null : Fields,
                ChildLinks = NullIfBlank(ChildLinks),
                PageSize = PageSize,
                StartRowNum = StartRowNum,
                ViewMode = NullIfBlank(ViewMode),
                IncludeTotalCount = IncludeTotalCount,
                ExcludeEmptyFields = ExcludeEmptyFields,
            };

        /// <summary>
        /// Converts these settings into the options a single-record read takes — the
        /// subset that still applies once the record is named by key.
        /// </summary>
        /// <returns>The read options.</returns>
        public Models.ServiceRequestReadOptions ToReadOptions() =>
            new()
            {
                Fields = Fields.Count == 0 ? null : Fields,
                ChildLinks = NullIfBlank(ChildLinks),
                ViewMode = NullIfBlank(ViewMode),
                ExcludeEmptyFields = ExcludeEmptyFields,
            };

        // Configuration turns an absent JSON value into null and an empty one into "".
        // Both mean "leave it off the request", so they are flattened here rather than
        // being sent as an empty parameter that overrides an ICM default with nothing.
        private static string? NullIfBlank(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
