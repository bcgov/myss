namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;
    using Myss.FormSpec;

    /// <summary>
    /// The body of an email change on Account Info (MYSS-305). Checked by the
    /// service, so a mistake comes back as a field error on <c>email</c>.
    /// </summary>
    public class ChangeEmailRequestModel
    {
        /// <summary>Gets or sets the new address as typed.</summary>
        public string? Email { get; set; }
    }

    /// <summary>
    /// The body of a verification from the emailed link.
    /// </summary>
    public class VerifyEmailRequestModel
    {
        /// <summary>Gets or sets the token from the link.</summary>
        public string? Token { get; set; }
    }

    /// <summary>
    /// How an email verification operation ended.
    /// </summary>
    public enum EmailVerificationOutcome
    {
        /// <summary>Done: the link was sent, the change cancelled, or the address verified.</summary>
        Ok,

        /// <summary>The caller has no registered profile.</summary>
        ProfileRequired,

        /// <summary>The address failed validation; <see cref="EmailVerificationResultModel.Errors"/> is set.</summary>
        Invalid,

        /// <summary>A link was sent too recently; <see cref="EmailVerificationResultModel.RetryAt"/> says when to ask again.</summary>
        TooSoon,

        /// <summary>The email could not be sent; nothing the citizen did wrong.</summary>
        EmailNotSent,

        /// <summary>A resend was asked for with no address waiting to be verified.</summary>
        NothingToVerify,

        /// <summary>The link is unknown, already used, replaced, or expired.</summary>
        LinkInvalid,
    }

    /// <summary>
    /// The result of an email verification operation, one shape for every outcome.
    /// </summary>
    public sealed class EmailVerificationResultModel
    {
        private EmailVerificationResultModel(
            EmailVerificationOutcome outcome,
            IReadOnlyList<ValidationErrorModel>? errors = null,
            DateTimeOffset? retryAt = null)
        {
            Outcome = outcome;
            Errors = errors ?? [];
            RetryAt = retryAt;
        }

        /// <summary>Gets how the operation ended.</summary>
        public EmailVerificationOutcome Outcome { get; }

        /// <summary>Gets the validation failures when the outcome is <see cref="EmailVerificationOutcome.Invalid"/>.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; }

        /// <summary>Gets when another link may be sent, when the outcome is <see cref="EmailVerificationOutcome.TooSoon"/>.</summary>
        public DateTimeOffset? RetryAt { get; }

        /// <summary>Gets the success.</summary>
        public static EmailVerificationResultModel Done { get; } = new(EmailVerificationOutcome.Ok);

        /// <summary>Creates a refusal, or a failure with nothing more to say.</summary>
        /// <param name="outcome">Which.</param>
        /// <returns>The result.</returns>
        public static EmailVerificationResultModel Refused(EmailVerificationOutcome outcome) => new(outcome);

        /// <summary>Creates a refusal inside the cooldown after the last link.</summary>
        /// <param name="retryAt">When another link may be sent.</param>
        /// <returns>The result.</returns>
        public static EmailVerificationResultModel TooSoon(DateTimeOffset retryAt) =>
            new(EmailVerificationOutcome.TooSoon, retryAt: retryAt);

        /// <summary>Creates a validation failure.</summary>
        /// <param name="errors">Every failure found.</param>
        /// <returns>The result.</returns>
        public static EmailVerificationResultModel Invalid(IReadOnlyList<ValidationErrorModel> errors) =>
            new(EmailVerificationOutcome.Invalid, errors: errors);
    }
}
