namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The answer to a PIN reset request (MYSS-36): the email is on its way.
    /// </summary>
    public class PinResetRequestedModel
    {
        /// <summary>Gets or sets when the emailed link stops working.</summary>
        public DateTimeOffset ExpiresAt { get; set; }
    }

    /// <summary>
    /// The body of a PIN reset from the emailed link. All three are checked by
    /// the service, so a mistake comes back as a refusal or a field error.
    /// </summary>
    public class ResetPinRequestModel
    {
        /// <summary>Gets or sets the token from the link.</summary>
        public string? Token { get; set; }

        /// <summary>Gets or sets the new 4-digit PIN.</summary>
        public string? NewPin { get; set; }

        /// <summary>Gets or sets the new PIN typed a second time.</summary>
        public string? ConfirmPin { get; set; }
    }

    /// <summary>
    /// How a PIN reset operation ended.
    /// </summary>
    public enum PinResetOutcome
    {
        /// <summary>The email was sent, or the PIN was reset.</summary>
        Ok,

        /// <summary>The caller has no registered profile.</summary>
        ProfileRequired,

        /// <summary>The caller's sign-in does not use a PIN.</summary>
        PinNotAvailable,

        /// <summary>The email could not be sent; nothing the citizen did wrong.</summary>
        EmailNotSent,

        /// <summary>A reset email was sent too recently; <see cref="PinResetResultModel.RetryAt"/> says when to ask again.</summary>
        TooSoon,

        /// <summary>The link is unknown, already used, or expired.</summary>
        LinkInvalid,

        /// <summary>The new PIN failed validation; <see cref="PinResetResultModel.Errors"/> is set.</summary>
        Invalid,
    }

    /// <summary>
    /// The result of a PIN reset operation, one shape for every outcome.
    /// </summary>
    public sealed class PinResetResultModel
    {
        private PinResetResultModel(
            PinResetOutcome outcome,
            DateTimeOffset? expiresAt = null,
            IReadOnlyList<ValidationErrorModel>? errors = null,
            DateTimeOffset? retryAt = null)
        {
            Outcome = outcome;
            ExpiresAt = expiresAt;
            Errors = errors ?? [];
            RetryAt = retryAt;
        }

        /// <summary>Gets how the operation ended.</summary>
        public PinResetOutcome Outcome { get; }

        /// <summary>Gets when the emailed link expires, after a request that sent one.</summary>
        public DateTimeOffset? ExpiresAt { get; }

        /// <summary>Gets the validation failures when the outcome is <see cref="PinResetOutcome.Invalid"/>.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; }

        /// <summary>Gets when another request will be accepted, when the outcome is <see cref="PinResetOutcome.TooSoon"/>.</summary>
        public DateTimeOffset? RetryAt { get; }

        /// <summary>Creates the success of a request: the email was accepted for sending.</summary>
        /// <param name="expiresAt">When the link expires.</param>
        /// <returns>The result.</returns>
        public static PinResetResultModel Sent(DateTimeOffset expiresAt) => new(PinResetOutcome.Ok, expiresAt: expiresAt);

        /// <summary>Creates the success of a reset.</summary>
        /// <returns>The result.</returns>
        public static PinResetResultModel Reset() => new(PinResetOutcome.Ok);

        /// <summary>Creates a refusal, or a failure with nothing more to say.</summary>
        /// <param name="outcome">Which.</param>
        /// <returns>The result.</returns>
        public static PinResetResultModel Refused(PinResetOutcome outcome) => new(outcome);

        /// <summary>Creates a refusal inside the cooldown after the last reset email.</summary>
        /// <param name="retryAt">When another request will be accepted.</param>
        /// <returns>The result.</returns>
        public static PinResetResultModel TooSoon(DateTimeOffset retryAt) =>
            new(PinResetOutcome.TooSoon, retryAt: retryAt);

        /// <summary>Creates a validation failure.</summary>
        /// <param name="errors">Every failure found.</param>
        /// <returns>The result.</returns>
        public static PinResetResultModel Invalid(IReadOnlyList<ValidationErrorModel> errors) =>
            new(PinResetOutcome.Invalid, errors: errors);
    }
}
