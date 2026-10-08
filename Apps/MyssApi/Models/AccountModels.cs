namespace Myss.Api.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// What Account Info shows the citizen (MYSS-271): case details, which come
    /// from ICM and MIS, beside the contact details MySS holds itself.
    /// </summary>
    public class AccountModel
    {
        /// <summary>Gets or sets the ICM case number, or null for a citizen with no case.</summary>
        public string? CaseNumber { get; set; }

        /// <summary>Gets or sets the client's full name, from the registration profile.</summary>
        public required string ClientName { get; set; }

        /// <summary>Gets or sets the names of the other people on the case. Empty when there are none.</summary>
        public required IReadOnlyList<string> FamilyMembers { get; set; }

        /// <summary>Gets or sets the email address from registration. Read-only here.</summary>
        public required string Email { get; set; }

        /// <summary>Gets or sets the citizen's phone numbers, in their chosen order.</summary>
        public required IReadOnlyList<AccountPhoneModel> Phones { get; set; }

        /// <summary>Gets or sets the mailing address, one line per entry. Empty when unknown.</summary>
        public required IReadOnlyList<string> MailingAddressLines { get; set; }

        /// <summary>Gets or sets a value indicating whether the citizen wants the monthly report reminder.</summary>
        public bool MonthlyReportReminder { get; set; }

        /// <summary>Gets or sets whether the citizen has a PIN, or does not use one (MYSS-258).</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<AccountPinStatus>))]
        public AccountPinStatus PinStatus { get; set; }
    }

    /// <summary>
    /// Where the citizen stands with the PIN, which decides what Account Info offers.
    /// </summary>
    public enum AccountPinStatus
    {
        /// <summary>The sign-in does not use a PIN: only Basic BCeID does.</summary>
        NotApplicable,

        /// <summary>A Basic BCeID citizen with no PIN yet, registered before PINs were asked for.</summary>
        NotSet,

        /// <summary>A PIN is set; changing it needs the current one.</summary>
        Set,
    }

    /// <summary>
    /// A stored phone number.
    /// </summary>
    public class AccountPhoneModel
    {
        /// <summary>Gets or sets the ten digits, formatting stripped.</summary>
        public required string Number { get; set; }

        /// <summary>Gets or sets which kind of number it is.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<AccountPhoneType>))]
        public AccountPhoneType Type { get; set; }
    }

    /// <summary>
    /// The kinds of phone number ICM keeps for a contact.
    /// </summary>
    public enum AccountPhoneType
    {
        /// <summary>A home number.</summary>
        Home,

        /// <summary>A mobile number.</summary>
        Cell,

        /// <summary>A work number.</summary>
        Work,

        /// <summary>A number where a message can be left.</summary>
        Message,
    }

    /// <summary>
    /// The body of a phone update: the whole list as the citizen left it, which
    /// replaces the stored one. An empty list removes every number.
    /// </summary>
    public class UpdatePhonesRequestModel
    {
        /// <summary>Gets or sets the phone numbers, in order.</summary>
        public required IReadOnlyList<PhoneInputModel> Phones { get; set; }
    }

    /// <summary>
    /// A phone number as entered. Both values are checked by the service, so a
    /// mistake comes back as a field error rather than a binding failure.
    /// </summary>
    public class PhoneInputModel
    {
        /// <summary>Gets or sets the number as typed, punctuation allowed.</summary>
        public string? Number { get; set; }

        /// <summary>Gets or sets the type: Home, Cell, Work or Message.</summary>
        public string? Type { get; set; }
    }

    /// <summary>
    /// The body of a notification preference update.
    /// </summary>
    public class UpdateNotificationPreferencesRequestModel
    {
        /// <summary>
        /// Gets or sets a value indicating whether the citizen wants the monthly report reminder.
        /// Required: a body without it is refused instead of read as false, which would turn the
        /// reminder off without the citizen asking.
        /// </summary>
        public required bool MonthlyReportReminder { get; set; }
    }

    /// <summary>
    /// The body of a PIN save: a change when a PIN is set, otherwise its
    /// creation, in which case <see cref="CurrentPin"/> is ignored. All three are
    /// checked by the service, so a mistake comes back as a field error.
    /// </summary>
    public class SavePinRequestModel
    {
        /// <summary>Gets or sets the PIN the citizen has now. Required to change one.</summary>
        public string? CurrentPin { get; set; }

        /// <summary>Gets or sets the new 4-digit PIN.</summary>
        public string? NewPin { get; set; }

        /// <summary>Gets or sets the new PIN typed a second time.</summary>
        public string? ConfirmPin { get; set; }
    }

    /// <summary>
    /// How an account operation ended.
    /// </summary>
    public enum AccountOutcome
    {
        /// <summary>The operation succeeded; <see cref="AccountResultModel.Account"/> is set.</summary>
        Ok,

        /// <summary>The caller has no registered profile.</summary>
        ProfileRequired,

        /// <summary>The input failed validation; <see cref="AccountResultModel.Errors"/> is set.</summary>
        Invalid,

        /// <summary>Too many wrong PINs; <see cref="AccountResultModel.LockedUntil"/> says until when.</summary>
        PinLocked,

        /// <summary>The caller's sign-in does not use a PIN.</summary>
        PinNotAvailable,
    }

    /// <summary>
    /// The result of an account operation, one shape for every outcome.
    /// </summary>
    public sealed class AccountResultModel
    {
        private AccountResultModel(
            AccountOutcome outcome,
            AccountModel? account = null,
            IReadOnlyList<ValidationErrorModel>? errors = null,
            DateTimeOffset? lockedUntil = null)
        {
            Outcome = outcome;
            Account = account;
            Errors = errors ?? [];
            LockedUntil = lockedUntil;
        }

        /// <summary>Gets how the operation ended.</summary>
        public AccountOutcome Outcome { get; }

        /// <summary>Gets the account on success.</summary>
        public AccountModel? Account { get; }

        /// <summary>Gets the validation failures when the outcome is <see cref="AccountOutcome.Invalid"/>.</summary>
        public IReadOnlyList<ValidationErrorModel> Errors { get; }

        /// <summary>Gets when the PIN lockout ends, when the outcome is <see cref="AccountOutcome.PinLocked"/>.</summary>
        public DateTimeOffset? LockedUntil { get; }

        /// <summary>Creates a success.</summary>
        /// <param name="account">The account as it now stands.</param>
        /// <returns>The result.</returns>
        public static AccountResultModel Ok(AccountModel account) => new(AccountOutcome.Ok, account);

        /// <summary>Creates a refusal for a caller with no profile.</summary>
        /// <returns>The result.</returns>
        public static AccountResultModel ProfileRequired() => new(AccountOutcome.ProfileRequired);

        /// <summary>Creates a validation failure.</summary>
        /// <param name="errors">Every failure found.</param>
        /// <returns>The result.</returns>
        public static AccountResultModel Invalid(IReadOnlyList<ValidationErrorModel> errors) =>
            new(AccountOutcome.Invalid, errors: errors);

        /// <summary>Creates a refusal while the PIN is locked.</summary>
        /// <param name="lockedUntil">When the lockout ends.</param>
        /// <returns>The result.</returns>
        public static AccountResultModel PinLocked(DateTimeOffset lockedUntil) =>
            new(AccountOutcome.PinLocked, lockedUntil: lockedUntil);

        /// <summary>Creates a refusal for a caller whose sign-in does not use a PIN.</summary>
        /// <returns>The result.</returns>
        public static AccountResultModel PinNotAvailable() => new(AccountOutcome.PinNotAvailable);
    }
}
