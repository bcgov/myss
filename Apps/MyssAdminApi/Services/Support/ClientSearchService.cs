namespace Myss.AdminApi.Support
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Myss.Common.Auth;
    using Myss.Data;

    /// <summary>Client search over the forms schema's profile table (MYSS-275).</summary>
    public class ClientSearchService : IClientSearchService
    {
        /// <summary>The most clients one search returns; a worker narrows the search past it.</summary>
        public const int MaxResults = 50;

        private readonly FormsDbContext _dbContext;
        private readonly ILogger<ClientSearchService> _logger;
        private readonly string? _bcServicesCardIdp;

        /// <summary>Initializes a new instance of the <see cref="ClientSearchService"/> class.</summary>
        /// <param name="dbContext">Injected forms db context.</param>
        /// <param name="configuration">Configuration, for the BC Services Card broker's alias.</param>
        /// <param name="logger">Injected logger.</param>
        public ClientSearchService(
            FormsDbContext dbContext,
            IConfiguration configuration,
            ILogger<ClientSearchService> logger)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            _dbContext = dbContext;
            _logger = logger;

            // The alias RoleCalculator recognises a BC Services Card sign-in by.
            _bcServicesCardIdp = configuration[RoleCalculationClaimsTransformation.BcServicesCardIdpKey]
                ?? configuration["Oidc:ClientId"];
        }

        /// <inheritdoc/>
        public async Task<ClientSearchOutcome> SearchAsync(ClientSearchCriteria criteria, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            string? firstName = Term(criteria.FirstName);
            string? lastName = Term(criteria.LastName);
            string? bceidUsername = Term(criteria.BceidUsername);
            string? sinInput = Term(criteria.Sin);

            if (firstName is null && lastName is null && bceidUsername is null && sinInput is null)
            {
                return new ClientSearchOutcome(null, ClientSearchKeywords.CriteriaRequired);
            }

            string? sin = null;
            if (sinInput is not null)
            {
                sin = Digits(sinInput);
                if (sin.Length != 9 || sinInput.Any(c => !char.IsDigit(c) && c != ' ' && c != '-'))
                {
                    return new ClientSearchOutcome(null, ClientSearchKeywords.SinInvalid);
                }
            }

            IQueryable<MyssUserProfile> query = Filter(
                _dbContext.MyssUserProfiles.AsNoTracking(), firstName, lastName, bceidUsername, sin);

            var rows = await query
                .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
                .ThenBy(p => p.Id)
                .Take(MaxResults + 1)
                .Select(p => new
                {
                    p.Id,
                    p.FirstName,
                    p.LastName,
                    p.BceidUsername,
                    p.Sin,
                    p.DateOfBirth,
                    p.Subject,
                    p.Origin,
                })
                .ToListAsync(cancellationToken);

            List<ClientSummaryModel> clients = rows
                .Take(MaxResults)
                .Select(r => new ClientSummaryModel
                {
                    Id = r.Id,
                    FirstName = r.FirstName,
                    LastName = r.LastName,
                    BceidUsername = r.BceidUsername,
                    SinLastThree = LastThree(r.Sin),
                    DateOfBirth = r.DateOfBirth,
                    SignInType = SignInType(r.Subject, r.Origin),
                })
                .ToList();

            // Which fields were used and how many matched; never the values.
            _logger.LogInformation(
                "Client search by {Fields} found {Count} client(s){More}",
                UsedFields(firstName, lastName, sin, bceidUsername),
                clients.Count,
                rows.Count > MaxResults ? " (more not returned)" : string.Empty);

            return new ClientSearchOutcome(
                new ClientSearchResultModel { Clients = clients, HasMore = rows.Count > MaxResults },
                null);
        }

        /// <summary>Narrows the query by each term given; every one has to match.</summary>
        private static IQueryable<MyssUserProfile> Filter(
            IQueryable<MyssUserProfile> query,
            string? firstName,
            string? lastName,
            string? bceidUsername,
            string? sin)
        {
            if (firstName is not null)
            {
                query = query.Where(p => p.FirstName.ToLower().Contains(firstName));
            }

            if (lastName is not null)
            {
                query = query.Where(p => p.LastName.ToLower().Contains(lastName));
            }

            if (bceidUsername is not null)
            {
                query = query.Where(p => p.BceidUsername != null && p.BceidUsername.ToLower().Contains(bceidUsername));
            }

            if (sin is not null)
            {
                // A registration stores the SIN as entered, so the punctuation
                // a citizen typed is dropped before comparing.
                query = query.Where(p => p.Sin.Replace(" ", string.Empty).Replace("-", string.Empty) == sin);
            }

            return query;
        }

        /// <summary>The names of the fields searched by, for the log; never their values.</summary>
        private static string UsedFields(string? firstName, string? lastName, string? sin, string? bceidUsername) =>
            string.Join(
                ',',
                new[]
                {
                    firstName is null ? null : "firstName",
                    lastName is null ? null : "lastName",
                    sin is null ? null : "sin",
                    bceidUsername is null ? null : "bceidUsername",
                }.Where(f => f is not null));

        private static string? Term(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

        private static string Digits(string value) => new(value.Where(char.IsDigit).ToArray());

        private static string? LastThree(string sin)
        {
            string digits = Digits(sin);
            return digits.Length >= 3 ? digits[^3..] : null;
        }

        /// <summary>
        /// A worker override account by its origin; otherwise the broker alias CSS
        /// appends to the subject (<c>{id}@bceidbasic</c>, <c>{id}@{client id}</c>
        /// for BC Services Card). Null when the subject carries neither.
        /// </summary>
        private string? SignInType(string subject, string origin)
        {
            if (string.Equals(origin, ProfileOrigins.WorkerOverride, StringComparison.Ordinal))
            {
                return ClientSignInTypes.WorkerOverride;
            }

            int at = subject.LastIndexOf('@');
            if (at < 0)
            {
                return null;
            }

            string idp = subject[(at + 1)..];
            if (idp is IdentityProviders.BceidBasic or IdentityProviders.BceidBusiness or IdentityProviders.BceidBoth)
            {
                return ClientSignInTypes.Bceid;
            }

            return string.Equals(idp, _bcServicesCardIdp, StringComparison.OrdinalIgnoreCase)
                ? ClientSignInTypes.BcServicesCard
                : null;
        }
    }
}
