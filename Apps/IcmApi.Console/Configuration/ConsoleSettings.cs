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

        /// <summary>Gets a value indicating whether this run reads or creates prospects.</summary>
        public bool IsProspectMode =>
            string.Equals(Mode, "prospect", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether this run creates a bus pass SR directly, without the workflow.</summary>
        public bool IsDirectMode =>
            string.Equals(Mode, "direct", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether this run sends one raw request.</summary>
        public bool IsRawMode =>
            string.Equals(Mode, "raw", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether this run reads or creates activities.</summary>
        public bool IsActivityMode =>
            string.Equals(Mode, "activity", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets a value indicating whether this run reads, or links, the contacts on a service request.</summary>
        public bool IsSrContactMode =>
            string.Equals(Mode, "srcontact", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets or sets the search to run.</summary>
        public QuerySettings Query { get; set; } = new();

        /// <summary>Gets or sets the contact search for <c>contact</c> mode.</summary>
        public ContactSettings Contact { get; set; } = new();

        /// <summary>Gets or sets the case lookup for <c>case</c> mode.</summary>
        public CaseSettings Case { get; set; } = new();

        /// <summary>Gets or sets what to fetch in <c>describe</c> mode.</summary>
        public DescribeSettings Describe { get; set; } = new();

        /// <summary>Gets or sets the prospect read, or create, for <c>prospect</c> mode.</summary>
        public ProspectSettings Prospect { get; set; } = new();

        /// <summary>Gets or sets the direct SR + prospect create for <c>direct</c> mode.</summary>
        public DirectSettings Direct { get; set; } = new();

        /// <summary>Gets or sets the request for <c>raw</c> mode.</summary>
        public RawSettings Raw { get; set; } = new();

        /// <summary>Gets or sets the activity read, or create, for <c>activity</c> mode.</summary>
        public ActivitySettings Activity { get; set; } = new();

        /// <summary>Gets or sets the contact read, or link, for <c>srcontact</c> mode.</summary>
        public SrContactSettings SrContact { get; set; } = new();

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
                && !IsDescribeMode
                && !IsProspectMode
                && !IsDirectMode
                && !IsRawMode
                && !IsActivityMode
                && !IsSrContactMode)
            {
                found.Add($"Mode must be 'query', 'buspass', 'contact', 'case', 'prospect', 'activity', 'srcontact', 'direct', 'raw' or 'describe' (it is '{Mode}').");
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

            ValidateMode(found);

            problems = found.Count == 0 ? null : found;
            return found.Count == 0;
        }

        // The mode-specific checks, kept out of TryValidate so it stays readable.
        private void ValidateMode(List<string> found)
        {
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

            if (IsProspectMode)
            {
                Prospect.Validate(found);
            }

            if (IsDirectMode)
            {
                Direct.Validate(found, Prospect.Create);
            }

            if (IsActivityMode)
            {
                Activity.Validate(found);
            }

            if (IsSrContactMode)
            {
                Require(found, "SrContact:ServiceRequestId", SrContact.ServiceRequestId);
            }

            if (IsRawMode)
            {
                Require(found, "Raw:Path", Raw.Path);
                if (!HttpMethod.Parse(Raw.Method).Method.Equals(Raw.Method, StringComparison.OrdinalIgnoreCase))
                {
                    found.Add($"Raw:Method '{Raw.Method}' is not an HTTP method.");
                }
            }
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

    /// <summary>The prospect read, or create, to run. Mirrors <see cref="Icm.Api.Models.ProspectQuery"/> and <see cref="Icm.Api.Models.ProspectInput"/>.</summary>
    /// <remarks>
    /// <see cref="ServiceRequestId"/> reads the prospects on that service request (the
    /// child path, the one MEASURED to work); <see cref="Id"/> reads one row on the
    /// standalone path; otherwise the search criteria are used, ANDed, on the standalone
    /// path. <see cref="Create"/>, when enabled, creates a row instead — a write to ICM.
    /// </remarks>
    public class ProspectSettings
    {
        private string? _serviceRequestId;
        private string? _id;
        private string? _prospectiveContactUId;
        private string? _source;
        private string? _viewMode;

        // Blank means "not set" for every criterion: a --Prospect:Id= on the command line
        // (the way to cancel a committed value) binds as "", which must not become a
        // blank criterion the library then refuses.

        /// <summary>Gets or sets the service request row id whose prospects to read.</summary>
        public string? ServiceRequestId { get => _serviceRequestId; set => _serviceRequestId = NullIfBlank(value); }

        /// <summary>Gets or sets a prospect row id to read on the standalone path.</summary>
        public string? Id { get => _id; set => _id = NullIfBlank(value); }

        /// <summary>Gets or sets the prospective contact UId to search for.</summary>
        public string? ProspectiveContactUId { get => _prospectiveContactUId; set => _prospectiveContactUId = NullIfBlank(value); }

        /// <summary>Gets or sets the source to search for.</summary>
        public string? Source { get => _source; set => _source = NullIfBlank(value); }

        /// <summary>Gets or sets the records per page of a search.</summary>
        public int PageSize { get; set; } = 5;

        /// <summary>Gets or sets the zero-based index of the first record a search returns.</summary>
        public int StartRowNum { get; set; }

        /// <summary>Gets or sets the Siebel visibility mode. Null or blank is the library's default, <c>Organization</c>.</summary>
        public string? ViewMode { get => _viewMode; set => _viewMode = NullIfBlank(value); }

        /// <summary>Gets or sets a value indicating whether to print the values. On by default: SIT holds test data.</summary>
        public bool ShowValues { get; set; } = true;

        /// <summary>Gets or sets the create to run instead of a read, when <see cref="ProspectCreateSettings.Enabled"/>.</summary>
        public ProspectCreateSettings Create { get; set; } = new();

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

        /// <summary>Gets the names of the criteria that are set, for the run header.</summary>
        public IReadOnlyList<string> CriteriaSet =>
        [
            .. new (string Name, bool IsSet)[]
            {
                (nameof(ServiceRequestId), ServiceRequestId is not null),
                (nameof(Id), Id is not null),
                (nameof(ProspectiveContactUId), ProspectiveContactUId is not null),
                (nameof(Source), Source is not null),
            }.Where(criterion => criterion.IsSet).Select(criterion => criterion.Name),
        ];

        /// <summary>Adds what is wrong with these settings to <paramref name="problems"/>.</summary>
        /// <param name="problems">The list to add to.</param>
        public void Validate(IList<string> problems)
        {
            ArgumentNullException.ThrowIfNull(problems);

            if (Create.Enabled)
            {
                Create.Validate(problems);
                return;
            }

            if (CriteriaSet.Count == 0)
            {
                problems.Add(
                    "Prospect mode needs a service request (--Prospect:ServiceRequestId=1-53CJXK6), a row id "
                    + "(--Prospect:Id=…) or a search criterion — or --Prospect:Create:Enabled=true to create one.");
            }

            if (PageSize is < 1 or > 100)
            {
                problems.Add($"Prospect:PageSize must be between 1 and 100 (it is {PageSize}).");
            }

            if (StartRowNum < 0)
            {
                problems.Add($"Prospect:StartRowNum cannot be negative (it is {StartRowNum}).");
            }
        }

        /// <summary>Converts these settings into the standalone search the library takes.</summary>
        /// <returns>The query.</returns>
        public Models.ProspectQuery ToQuery() =>
            new()
            {
                Id = Id,
                ServiceRequestId = ServiceRequestId,
                ProspectiveContactUId = ProspectiveContactUId,
                Source = Source,
                PageSize = PageSize,
                StartRowNum = StartRowNum,
                ViewMode = ViewMode,
                IncludeTotalCount = true,
            };

        /// <summary>Converts these settings into the options a read by key takes.</summary>
        /// <returns>The read options.</returns>
        public Models.ProspectReadOptions ToReadOptions() => new() { ViewMode = ViewMode };
    }

    /// <summary>The prospect to create in <c>prospect</c> mode. Mirrors <see cref="Icm.Api.Models.ProspectInput"/>.</summary>
    /// <remarks>
    /// Synthetic on purpose, like the bus pass settings: enabling it writes a row to the
    /// target ICM — a PUT on <c>Prospects/SRProspects</c> keyed by the placeholder
    /// <c>Id</c> <c>NULL</c>, the shape ICM's REST Data API guidance prescribes (MEASURED
    /// 2026-09-29). The run then reads the service request's prospects back and says
    /// whether the new row is listed.
    /// </remarks>
    public class ProspectCreateSettings
    {
        /// <summary>Gets or sets a value indicating whether to create rather than read. Off by default: it writes.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets the row id of the service request the prospect belongs to. Required.</summary>
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets the first name. Required.</summary>
        public string? FirstName { get; set; }

        /// <summary>Gets or sets the middle name.</summary>
        public string? MiddleName { get; set; }

        /// <summary>Gets or sets the last name. Required.</summary>
        public string? LastName { get; set; }

        /// <summary>Gets or sets the birth date, as <c>yyyy-MM-dd</c>.</summary>
        public DateOnly? BirthDate { get; set; }

        /// <summary>Gets or sets the gender, in ICM's vocabulary (<c>M/F</c>).</summary>
        public string? Gender { get; set; }

        /// <summary>Gets or sets the SIN.</summary>
        public string? Sin { get; set; }

        /// <summary>Gets or sets whether the row is the applicant (<c>Applicant Flag</c>).</summary>
        public bool? IsApplicant { get; set; }

        /// <summary>
        /// Gets or sets <c>Prospective Contact UId</c>. Equal to the row id on every
        /// workflow-created row, which makes it the likely user key a standalone create
        /// needs (<c>SBL-EAI-04397</c> without one). <c>auto</c> sends a unique
        /// <c>MYSS-{timestamp}</c> value.
        /// </summary>
        public string? ProspectiveContactUId { get; set; }

        /// <summary>Gets or sets the address purpose: <c>Residence/Mailing</c>, <c>Residence</c> or <c>Mailing</c>.</summary>
        public string? Purpose { get; set; }

        /// <summary>Gets or sets the unit number.</summary>
        public string? UnitNumber { get; set; }

        /// <summary>Gets or sets the street address.</summary>
        public string? StreetAddress { get; set; }

        /// <summary>Gets or sets the city.</summary>
        public string? City { get; set; }

        /// <summary>Gets or sets the province (<c>State</c>).</summary>
        public string? Province { get; set; }

        /// <summary>Gets or sets the postal code.</summary>
        public string? PostalCode { get; set; }

        /// <summary>Gets or sets the email address.</summary>
        public string? EmailAddress { get; set; }

        /// <summary>Gets or sets the home phone.</summary>
        public string? HomePhone { get; set; }

        /// <summary>Gets or sets the cell phone.</summary>
        public string? CellularPhone { get; set; }

        /// <summary>Gets or sets the work phone.</summary>
        public string? WorkPhone { get; set; }

        /// <summary>Gets or sets the alternate phone (the old form's leave-message consent).</summary>
        public string? AlternatePhone { get; set; }

        /// <summary>Gets or sets the preferred communication method, e.g. <c>Home Phone</c>.</summary>
        public string? PreferredCommunicationMethod { get; set; }

        /// <summary>Gets or sets the comment.</summary>
        public string? Comment { get; set; }

        /// <summary>Gets or sets the source.</summary>
        public string? Source { get; set; }

        /// <summary>Gets or sets the source name.</summary>
        public string? SourceName { get; set; }

        /// <summary>Gets or sets the source type.</summary>
        public string? SourceType { get; set; }

        /// <summary>Adds what is wrong with these settings to <paramref name="problems"/>.</summary>
        /// <param name="problems">The list to add to.</param>
        public void Validate(IList<string> problems)
        {
            ArgumentNullException.ThrowIfNull(problems);

            if (string.IsNullOrWhiteSpace(ServiceRequestId))
            {
                problems.Add("Prospect:Create:ServiceRequestId is required: the service request the row belongs to.");
            }

            // ICM's field list (ADO 8249) marks these four required.
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName)
                || BirthDate is null || string.IsNullOrWhiteSpace(Gender))
            {
                problems.Add("Prospect:Create:FirstName, LastName, BirthDate and Gender (M/F: Man/Boy, Woman/Girl, Non-Binary, Unknown) are required.");
            }
        }

        /// <summary>Converts these settings into the input the library takes.</summary>
        /// <returns>The input.</returns>
        public Models.ProspectInput ToInput() =>
            new()
            {
                // Blank means "not set" for every text field, so a --Prospect:Create:X=
                // on the command line drops a committed value instead of sending "".
                ServiceRequestId = N(ServiceRequestId),
                FirstName = N(FirstName),
                MiddleName = N(MiddleName),
                LastName = N(LastName),
                BirthDate = BirthDate,
                Gender = N(Gender),
                Sin = N(Sin),
                IsApplicant = IsApplicant,
                ProspectiveContactUId = string.Equals(ProspectiveContactUId, "auto", StringComparison.OrdinalIgnoreCase)
                    ? $"MYSS-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                    : N(ProspectiveContactUId),
                Purpose = N(Purpose),
                UnitNumber = N(UnitNumber),
                StreetAddress = N(StreetAddress),
                City = N(City),
                Province = N(Province),
                PostalCode = N(PostalCode),
                EmailAddress = N(EmailAddress),
                HomePhone = N(HomePhone),
                CellularPhone = N(CellularPhone),
                WorkPhone = N(WorkPhone),
                AlternatePhone = N(AlternatePhone),
                PreferredCommunicationMethod = N(PreferredCommunicationMethod),
                Comment = N(Comment),
                Source = N(Source),
                SourceName = N(SourceName),
                SourceType = N(SourceType),
            };

        private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The direct bus pass create of <c>direct</c> mode: the SR to file, with the <c>Prospect:Create</c> row nested.</summary>
    /// <remarks>
    /// The SR values default to what the workflow files for a clean Application (MEASURED
    /// SIT2 SR <c>1-53CJXK6</c>, 2026-09-29): Bus Pass / Application / One Address, Ready,
    /// 3-Standard, Web, the Provincial Services Contact Centre, organization MHSD. A run
    /// creates a service request in the target ICM.
    /// </remarks>
    public class DirectSettings
    {
        /// <summary>Gets or sets the service request to create.</summary>
        public DirectServiceRequestSettings ServiceRequest { get; set; } = new();

        /// <summary>Gets or sets a value indicating whether to nest the <c>Prospect:Create</c> row in the SR. On by default.</summary>
        public bool IncludeProspect { get; set; } = true;

        /// <summary>
        /// Gets or sets how the prospect reaches ICM: <c>nested</c> (inside the create's
        /// body) or <c>update</c> (the SR is created bare, then updated by key with the row
        /// nested — for when the create refuses a hierarchy). Ignored without
        /// <see cref="IncludeProspect"/>.
        /// </summary>
        public string ProspectVia { get; set; } = "nested";

        /// <summary>Gets a value indicating whether the prospect is added by a follow-up update.</summary>
        public bool ProspectByUpdate => IncludeProspect && string.Equals(ProspectVia, "update", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets a value indicating whether to create in stages: the SR with its type
        /// only, then the sub type, then the sub sub type, then the prospect, each a PUT by
        /// key — because a create carrying the sub types answers <c>SBL-EAI-04401</c> (the
        /// sub type is validated against a picklist its parent has not yet constrained,
        /// MEASURED SIT2 2026-09-29 in either body order).
        /// </summary>
        public bool Staged { get; set; }

        /// <summary>Gets or sets the workflow-created SR whose children are read beside the new SR's, or null to skip.</summary>
        public string? ReferenceServiceRequestId { get; set; }

        /// <summary>Gets or sets how long to wait after the create before reading the children back.</summary>
        public int SettleSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets the JSON name the nested prospect rows are sent under when
        /// <see cref="RawBody"/> is on. The read side calls the child <c>SRProspects</c>;
        /// what the write side calls it is the experiment.
        /// </summary>
        public string ChildName { get; set; } = "SRProspects";

        /// <summary>
        /// Gets or sets a value indicating whether to POST a hand-built body (the same
        /// fields, under <see cref="ChildName"/>) instead of going through the library —
        /// for varying the body's shape without a code change.
        /// </summary>
        public bool RawBody { get; set; }

        /// <summary>
        /// Gets or sets which names the raw body uses for the fields the two vocabularies
        /// spell differently: <c>live</c> (the gateway's read names — <c>Type</c>,
        /// <c>Service Request Number</c>, <c>Are Any Of The Family Members Indigenous</c>)
        /// or <c>document</c> (<c>SR Type</c>, <c>SR Number</c>, <c>ICMCPU Aborginal</c>).
        /// </summary>
        public string Vocabulary { get; set; } = "document";

        /// <summary>Gets a value indicating whether the raw body uses the gateway's read names.</summary>
        public bool LiveVocabulary => string.Equals(Vocabulary, "live", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the <c>Id</c> sent on each nested prospect row in a raw body. The
        /// child integration component needs a user key of its own (<c>SBL-EAI-04397</c>
        /// for 'Prospects', MEASURED 2026-09-29, once the parent passed); Siebel allocates
        /// the real row id itself.
        /// </summary>
        public string? ChildId { get; set; }

        /// <summary>Gets or sets a <c>Service Request Id</c> to send on each nested prospect row in a raw body, as a user-key candidate.</summary>
        public string? ChildServiceRequestId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a raw body sends the child list even
        /// when it is empty. A bare parent is "Invalid input" (<c>SBL-EAI-50243</c>) or has
        /// no user key, while a parent with a child list gets its row id allocated by
        /// Siebel (MEASURED 2026-09-29); an empty list may be the way to create the SR alone.
        /// </summary>
        public bool EmptyChildList { get; set; }

        /// <summary>
        /// Gets or sets an existing service request's row id to attach the prospect to
        /// instead of creating an SR: a raw PUT on that key carrying only the child list
        /// (a POST is a strict insert — <c>SBL-EAI-04383</c> on an existing Id, MEASURED
        /// 2026-09-29).
        /// </summary>
        public string? AttachTo { get; set; }

        /// <summary>Gets or sets the Siebel visibility mode for the read-back. Service requests need <c>Organization</c>.</summary>
        public string? ViewMode { get; set; } = "Organization";

        /// <summary>Adds what is wrong with these settings to <paramref name="problems"/>.</summary>
        /// <param name="problems">The list to add to.</param>
        /// <param name="prospect">The prospect that would be nested.</param>
        public void Validate(IList<string> problems, ProspectCreateSettings prospect)
        {
            ArgumentNullException.ThrowIfNull(problems);
            ArgumentNullException.ThrowIfNull(prospect);

            if (string.IsNullOrWhiteSpace(ServiceRequest.Type) || string.IsNullOrWhiteSpace(ServiceRequest.CommMethod)
                || string.IsNullOrWhiteSpace(ServiceRequest.Status) || string.IsNullOrWhiteSpace(ServiceRequest.Priority)
                || string.IsNullOrWhiteSpace(ServiceRequest.ServiceOffice))
            {
                problems.Add("Direct:ServiceRequest needs Type, CommMethod, Status, Priority and ServiceOffice (ICM's required fields).");
            }

            if (IncludeProspect)
            {
                prospect.Validate(problems);
            }

            if (SettleSeconds is < 0 or > 120)
            {
                problems.Add($"Direct:SettleSeconds must be between 0 and 120 (it is {SettleSeconds}).");
            }

            if (!string.Equals(ProspectVia, "nested", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(ProspectVia, "update", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"Direct:ProspectVia must be 'nested' or 'update' (it is '{ProspectVia}').");
            }
        }

        /// <summary>The follow-up update that adds the prospect when <see cref="ProspectByUpdate"/>.</summary>
        /// <param name="prospect">The prospect to nest.</param>
        /// <returns>The input, carrying only the nested row.</returns>
        public static Models.ServiceRequestInput ToProspectUpdate(ProspectCreateSettings prospect)
        {
            ArgumentNullException.ThrowIfNull(prospect);
            Models.ProspectInput nested = prospect.ToInput();
            nested.ServiceRequestId = null;
            return new Models.ServiceRequestInput { Prospects = [nested] };
        }

        /// <summary>Converts these settings into the write the library takes.</summary>
        /// <param name="prospect">The prospect to nest, when <see cref="IncludeProspect"/>.</param>
        /// <returns>The input.</returns>
        public Models.ServiceRequestInput ToInput(ProspectCreateSettings prospect)
        {
            ArgumentNullException.ThrowIfNull(prospect);

            Models.ProspectInput? nested = null;
            if (IncludeProspect && !ProspectByUpdate)
            {
                // The nested row's parent is the enclosing SR, so its own SR id stays null
                // whatever Prospect:Create:ServiceRequestId says.
                nested = prospect.ToInput();
                nested.ServiceRequestId = null;
            }

            return new Models.ServiceRequestInput
            {
                Type = ServiceRequest.Type,
                SRSubType = Staged ? null : ServiceRequest.SubType,
                SRSubSubType = Staged ? null : ServiceRequest.SubSubType,
                Status = ServiceRequest.Status,
                Priority = ServiceRequest.Priority,
                CommMethod = ServiceRequest.CommMethod,
                ServiceOffice = ServiceRequest.ServiceOffice,
                PrimaryOrganizationId = ServiceRequest.PrimaryOrganizationId,
                PrimaryContactId = ServiceRequest.PrimaryContactId,
                AreAnyOfTheFamilyMembersIndigenous = ServiceRequest.Indigenous,
                Memo = ServiceRequest.Memo,
                IntegrationId = DirectServiceRequestSettings.Resolve(ServiceRequest.IntegrationId),
                ServiceRequestNumber = DirectServiceRequestSettings.Resolve(ServiceRequest.ServiceRequestNumber),
                Id = string.Equals(ServiceRequest.Id, "auto", StringComparison.OrdinalIgnoreCase)
                    ? $"MYSS-{DateTime.UtcNow:yyMMddHHmm}"
                    : DirectServiceRequestSettings.Resolve(ServiceRequest.Id),
                Prospects = nested is null ? null : [nested],
            };
        }
    }

    /// <summary>The service request fields of a direct create.</summary>
    public class DirectServiceRequestSettings
    {
        /// <summary>Gets or sets <c>Type</c>, e.g. <c>Bus Pass</c>.</summary>
        public string? Type { get; set; } = "Bus Pass";

        /// <summary>Gets or sets <c>SR Sub Type</c>, e.g. <c>Application</c>.</summary>
        public string? SubType { get; set; } = "Application";

        /// <summary>Gets or sets <c>SR Sub Sub Type</c>, e.g. <c>One Address</c>.</summary>
        public string? SubSubType { get; set; } = "One Address";

        /// <summary>Gets or sets <c>Status</c>, e.g. <c>Ready</c>.</summary>
        public string? Status { get; set; } = "Ready";

        /// <summary>Gets or sets <c>Priority</c>, e.g. <c>3-Standard</c>.</summary>
        public string? Priority { get; set; } = "3-Standard";

        /// <summary>Gets or sets <c>Comm Method</c>, e.g. <c>Web</c>.</summary>
        public string? CommMethod { get; set; } = "Web";

        /// <summary>Gets or sets <c>Service Office</c>.</summary>
        public string? ServiceOffice { get; set; } = "055 - PROVINCIAL SERVICES CONTACT CENTRE";

        /// <summary>Gets or sets <c>Primary Organization Id</c>; <c>1-5ZF8</c> is MHSD.</summary>
        public string? PrimaryOrganizationId { get; set; } = "1-5ZF8";

        /// <summary>Gets or sets <c>Primary Contact Id</c>, the matched contact, or null for an unmatched application.</summary>
        public string? PrimaryContactId { get; set; }

        /// <summary>Gets or sets <c>Are Any Of The Family Members Indigenous</c>. <c>TBD</c>, what the workflow's SRs read, is not a value the write picklist accepts (<c>SBL-DAT-00225</c>, MEASURED 2026-09-29), so none by default.</summary>
        public string? Indigenous { get; set; }

        /// <summary>Gets or sets <c>Memo</c>.</summary>
        public string? Memo { get; set; }

        /// <summary>
        /// Gets or sets <c>Integration Id</c>. A bare POST answers <c>SBL-EAI-04397</c> (no
        /// usable user key, MEASURED 2026-09-29), as the workflow's upsert did until it was
        /// given caller-supplied keys; <c>auto</c> sends a unique <c>MYSS-{timestamp}</c>.
        /// </summary>
        public string? IntegrationId { get; set; }

        /// <summary>Gets or sets <c>Service Request Number</c>; <c>auto</c> sends a unique <c>MYSS-{timestamp}</c>. Normally Siebel's to assign.</summary>
        public string? ServiceRequestNumber { get; set; }

        /// <summary>
        /// Gets or sets <c>Id</c>; <c>auto</c> sends a unique 15-character <c>MYSS-…</c>
        /// value (a Siebel row id is at most 15). The one user key ICM has named
        /// (2026-09-29); whether Siebel keeps it as the row id is the experiment.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>Resolves <c>auto</c> to a unique value.</summary>
        /// <param name="value">The configured value.</param>
        /// <returns>The value to send, or null.</returns>
        public static string? Resolve(string? value) =>
            string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase)
                ? $"MYSS-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                : string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The activity read, or create, to run. Mirrors <see cref="Icm.Api.Models.ActivityQuery"/> and <see cref="Icm.Api.Models.ActivityInput"/>.</summary>
    /// <remarks>
    /// <see cref="ServiceRequestId"/> reads the plans and activities on that service request
    /// — the events on it; <see cref="Id"/> reads one activity; otherwise the criteria are
    /// searched, ANDed. <see cref="Create"/>, when enabled, creates one instead — a write.
    /// </remarks>
    public class ActivitySettings
    {
        private string? _serviceRequestId;
        private string? _id;
        private string? _caseId;
        private string? _planName;
        private string? _status;
        private string? _type;
        private string? _viewMode;

        /// <summary>Gets or sets the service request row id whose plans and activities to read.</summary>
        public string? ServiceRequestId { get => _serviceRequestId; set => _serviceRequestId = N(value); }

        /// <summary>Gets or sets an activity row id to read.</summary>
        public string? Id { get => _id; set => _id = N(value); }

        /// <summary>Gets or sets the case row id to search for.</summary>
        public string? CaseId { get => _caseId; set => _caseId = N(value); }

        /// <summary>Gets or sets the plan name to search for, e.g. <c>Bus Pass - New Intake</c>.</summary>
        public string? PlanName { get => _planName; set => _planName = N(value); }

        /// <summary>Gets or sets the status to search for.</summary>
        public string? Status { get => _status; set => _status = N(value); }

        /// <summary>Gets or sets the type to search for.</summary>
        public string? Type { get => _type; set => _type = N(value); }

        /// <summary>Gets or sets the records per page of a search.</summary>
        public int PageSize { get; set; } = 10;

        /// <summary>Gets or sets the zero-based index of the first record a search returns.</summary>
        public int StartRowNum { get; set; }

        /// <summary>Gets or sets the Siebel visibility mode. Null or blank is the library's default, <c>Organization</c>.</summary>
        public string? ViewMode { get => _viewMode; set => _viewMode = N(value); }

        /// <summary>Gets or sets a value indicating whether to print every field of each row, or one summary line each.</summary>
        public bool ShowValues { get; set; }

        /// <summary>Gets or sets how long a create waits before reading the service request back.</summary>
        public int SettleSeconds { get; set; } = 5;

        /// <summary>Gets or sets the create to run instead of a read, when <see cref="ActivityCreateSettings.Enabled"/>.</summary>
        public ActivityCreateSettings Create { get; set; } = new();

        /// <summary>Gets the names of the criteria that are set, for the run header.</summary>
        public IReadOnlyList<string> CriteriaSet =>
        [
            .. new (string Name, bool IsSet)[]
            {
                (nameof(ServiceRequestId), ServiceRequestId is not null),
                (nameof(Id), Id is not null),
                (nameof(CaseId), CaseId is not null),
                (nameof(PlanName), PlanName is not null),
                (nameof(Status), Status is not null),
                (nameof(Type), Type is not null),
            }.Where(criterion => criterion.IsSet).Select(criterion => criterion.Name),
        ];

        /// <summary>Adds what is wrong with these settings to <paramref name="problems"/>.</summary>
        /// <param name="problems">The list to add to.</param>
        public void Validate(IList<string> problems)
        {
            ArgumentNullException.ThrowIfNull(problems);

            if (Create.Enabled)
            {
                // Checked before the create: an invalid delay found afterwards would skip the
                // read-back, and a retry would file a duplicate.
                if (SettleSeconds is < 0 or > 120)
                {
                    problems.Add($"Activity:SettleSeconds must be between 0 and 120 (it is {SettleSeconds}).");
                }

                Create.Validate(problems);
                return;
            }

            if (CriteriaSet.Count == 0)
            {
                problems.Add(
                    "Activity mode needs a service request (--Activity:ServiceRequestId=1-53CJXO6), a row id (--Activity:Id=…) "
                    + "or a search criterion — or --Activity:Create:Enabled=true to create one.");
            }

            if (PageSize is < 1 or > 100)
            {
                problems.Add($"Activity:PageSize must be between 1 and 100 (it is {PageSize}).");
            }
        }

        /// <summary>Converts these settings into the search the library takes.</summary>
        /// <returns>The query.</returns>
        public Models.ActivityQuery ToQuery() =>
            new()
            {
                Id = Id, ServiceRequestId = ServiceRequestId, CaseId = CaseId, PlanName = PlanName, Status = Status, Type = Type,
                PageSize = PageSize, StartRowNum = StartRowNum, ViewMode = ViewMode, IncludeTotalCount = true,
            };

        /// <summary>Converts these settings into the options a read takes.</summary>
        /// <returns>The read options.</returns>
        public Models.ActivityReadOptions ToReadOptions() => new() { ViewMode = ViewMode };

        private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The activity to create in <c>activity</c> mode. Mirrors <see cref="Icm.Api.Models.ActivityInput"/>.</summary>
    /// <remarks>
    /// Defaults to the workflow's step 01 of "Bus Pass - New Intake", field for field
    /// (MEASURED SIT2 2026-09-29), so a run reproduces on a directly created SR the event
    /// the workflow files — bar the plan it would hang off, which the REST API cannot create.
    /// Enabling it writes a row to the target ICM.
    /// </remarks>
    public class ActivityCreateSettings
    {
        /// <summary>Gets or sets a value indicating whether to create rather than read. Off by default: it writes.</summary>
        public bool Enabled { get; set; }

        /// <summary>Gets or sets the row id of the service request to profile the activity to. Required.</summary>
        public string? ServiceRequestId { get; set; }

        /// <summary>Gets or sets <c>Type</c>.</summary>
        public string? Type { get; set; } = "Task";

        /// <summary>Gets or sets <c>Action By</c>.</summary>
        public string? ActionBy { get; set; } = "Staff";

        /// <summary>Gets or sets <c>Action Code</c>.</summary>
        public string? ActionCode { get; set; } = "Validate Address and Promote Prospect";

        /// <summary>Gets or sets <c>Description</c>.</summary>
        public string? Description { get; set; } =
            "Validate if the SIN is present for the contact, if not then update SIN; validate address; and promote prospect if applicable.";

        /// <summary>Gets or sets <c>Status</c>.</summary>
        public string? Status { get; set; } = "Open";

        /// <summary>Gets or sets <c>Priority</c>.</summary>
        public string? Priority { get; set; } = "3-Standard";

        /// <summary>Gets or sets <c>Sequence</c>.</summary>
        public string? Sequence { get; set; } = "01";

        /// <summary>Gets or sets <c>Service Office</c>.</summary>
        public string? ServiceOffice { get; set; } = "055 - PROVINCIAL SERVICES CONTACT CENTRE";

        /// <summary>Gets or sets <c>ICM Created By Office</c>.</summary>
        public string? CreatedByOffice { get; set; } = "Siebel Administration";

        /// <summary>Gets or sets <c>Primary Owned By</c>, the login to assign to; null leaves it to ICM (the trusted user).</summary>
        public string? PrimaryOwnedBy { get; set; }

        /// <summary>Gets or sets <c>Activity Template Id</c>, a plan instance to hang the step off, when there is one.</summary>
        public string? ActivityPlanId { get; set; }

        /// <summary>Gets or sets how many days from now the activity is due.</summary>
        public int DueInDays { get; set; } = 30;

        /// <summary>Adds what is wrong with these settings to <paramref name="problems"/>.</summary>
        /// <param name="problems">The list to add to.</param>
        public void Validate(IList<string> problems)
        {
            ArgumentNullException.ThrowIfNull(problems);

            if (string.IsNullOrWhiteSpace(ServiceRequestId))
            {
                problems.Add("Activity:Create:ServiceRequestId is required: the service request to profile the activity to.");
            }

            if (string.IsNullOrWhiteSpace(Type) || string.IsNullOrWhiteSpace(ActionBy))
            {
                problems.Add("Activity:Create:Type and Activity:Create:ActionBy are required.");
            }
        }

        /// <summary>Converts these settings into the input the library takes.</summary>
        /// <returns>The input.</returns>
        public Models.ActivityInput ToInput()
        {
            DateTime now = DateTime.Now;
            return new Models.ActivityInput
            {
                ServiceRequestId = ServiceRequestId,
                Type = Type,
                ActionBy = ActionBy,
                ActionCode = N(ActionCode),
                Description = N(Description),
                Status = N(Status),
                Priority = N(Priority),
                Sequence = N(Sequence),
                ServiceOffice = N(ServiceOffice),
                CreatedByOffice = N(CreatedByOffice),
                PrimaryOwnedBy = N(PrimaryOwnedBy),
                ActivityPlanId = N(ActivityPlanId),
                IsDone = false,
                Planned = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0),
                Due = now.Date.AddDays(DueInDays),
            };
        }

        private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The service request whose contacts <c>srcontact</c> mode reads, and the contact it links first, if any.</summary>
    public class SrContactSettings
    {
        private string? _serviceRequestId;
        private string? _contactId;
        private string? _relationship;
        private string? _viewMode;

        /// <summary>Gets or sets the service request's row id. Required.</summary>
        public string? ServiceRequestId { get => _serviceRequestId; set => _serviceRequestId = N(value); }

        /// <summary>Gets or sets the contact's row id to link (the <c>Id</c> a contact search prints); null reads only. <b>Writes.</b></summary>
        public string? ContactId { get => _contactId; set => _contactId = N(value); }

        /// <summary>Gets or sets <c>SR Relationship</c>; null sends <c>Key player</c>.</summary>
        public string? Relationship { get => _relationship; set => _relationship = N(value); }

        /// <summary>Gets or sets whether the contact is the SR's primary; null sends true.</summary>
        public bool? IsPrimary { get; set; }

        /// <summary>Gets or sets the Siebel visibility mode; null is <c>Organization</c>.</summary>
        public string? ViewMode { get => _viewMode; set => _viewMode = N(value); }

        /// <summary>Gets or sets a value indicating whether to print every field of each contact.</summary>
        public bool ShowValues { get; set; }

        /// <summary>Converts these settings into the link the library takes.</summary>
        /// <returns>The link.</returns>
        public Models.ServiceRequestContactLink ToLink() =>
            new() { ContactId = ContactId, Relationship = Relationship, IsPrimary = IsPrimary };

        private static string? N(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>The one request <c>raw</c> mode sends.</summary>
    public class RawSettings
    {
        /// <summary>Gets or sets the HTTP method. A POST or PUT writes.</summary>
        public string Method { get; set; } = "GET";

        /// <summary>Gets or sets the path under the ICM base URL, e.g. <c>data/Prospects/SRProspects/</c>.</summary>
        public string? Path { get; set; }

        /// <summary>Gets or sets a file holding the JSON body.</summary>
        public string? BodyFile { get; set; }

        /// <summary>Gets or sets the JSON body inline, when there is no file.</summary>
        public string? Body { get; set; }
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
