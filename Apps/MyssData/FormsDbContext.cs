namespace Myss.Data
{
    using Microsoft.EntityFrameworkCore;
    using Myss.FormSpec.Domain;

    /// <summary>
    /// EF Core context for the forms module. Owns the "forms" schema only,
    /// per the schema-per-module rule.
    /// </summary>
    public class FormsDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FormsDbContext"/> class.
        /// </summary>
        /// <param name="options">Injected context options.</param>
        public FormsDbContext(DbContextOptions<FormsDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets the form submissions set.
        /// </summary>
        public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

        /// <summary>
        /// Gets the MySS user profiles set.
        /// </summary>
        public DbSet<MyssUserProfile> MyssUserProfiles => Set<MyssUserProfile>();

        /// <summary>
        /// Gets the bus pass dispatch event log.
        /// </summary>
        public DbSet<BusPassDispatchEvent> BusPassDispatchEvents => Set<BusPassDispatchEvent>();

        /// <summary>
        /// Gets the email addresses being proven during registration (MYSS-295),
        /// one per signed-in identity, before any profile exists.
        /// </summary>
        public DbSet<RegistrationEmailVerification> RegistrationEmailVerifications => Set<RegistrationEmailVerification>();

        /// <summary>
        /// Gets the representatives who made accounts for someone else, one per profile at most.
        /// </summary>
        public DbSet<AccountRepresentative> AccountRepresentatives => Set<AccountRepresentative>();

        /// <summary>
        /// Gets the uploaded files linked to accounts at registration.
        /// </summary>
        public DbSet<AccountDocument> AccountDocuments => Set<AccountDocument>();

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("forms");

            var submission = modelBuilder.Entity<FormSubmission>();
            submission.ToTable("form_submissions");
            submission.HasKey(s => s.Id);
            submission.Property(s => s.Id).HasColumnName("id");
            submission.Property(s => s.FormSpecId).HasColumnName("form_spec_id").IsRequired();
            submission.Property(s => s.FormSpecVersion).HasColumnName("form_spec_version");
            submission.Property(s => s.Answers).HasColumnName("answers").HasColumnType("jsonb");
            submission.Property(s => s.SubmittedAt).HasColumnName("submitted_at");
            submission.HasIndex(s => new { s.FormSpecId, s.FormSpecVersion });

            var profile = modelBuilder.Entity<MyssUserProfile>();
            profile.ToTable("myss_user_profiles");
            profile.HasKey(p => p.Id);
            profile.Property(p => p.Id).HasColumnName("id");
            profile.Property(p => p.Subject).HasColumnName("subject").HasMaxLength(255).IsRequired();
            profile.Property(p => p.FirstName).HasColumnName("first_name").HasMaxLength(200).IsRequired();
            profile.Property(p => p.LastName).HasColumnName("last_name").HasMaxLength(200).IsRequired();
            profile.Property(p => p.DateOfBirth).HasColumnName("date_of_birth").IsRequired();
            profile.Property(p => p.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            profile.Property(p => p.Sin).HasColumnName("sin").HasMaxLength(20);
            profile.Property(p => p.Phone).HasColumnName("phone").HasMaxLength(10);
            profile.Property(p => p.Gender).HasColumnName("gender").HasMaxLength(32);
            profile.Property(p => p.CreatedAt).HasColumnName("created_at");
            profile.Property(p => p.UpdatedAt).HasColumnName("updated_at");
            profile.HasIndex(p => p.Subject).IsUnique();
            profile.Property(p => p.MonthlyReportReminder)
                .HasColumnName("monthly_report_reminder")
                .HasDefaultValue(false);

            // The column takes the data dictionary's name (MYSS-258). 256 leaves
            // room for a later hash format; today's is 84 characters.
            // The PIN columns are concurrency tokens: a save that read them
            // before another request changed them fails rather than overwrites
            // (AccountService.SavePinAsync tries again from a fresh read).
            profile.Property(p => p.PinHash).HasColumnName("taapcd_aae_passcode").HasMaxLength(256).IsConcurrencyToken();
            profile.Property(p => p.PinFailedAttempts).HasColumnName("pin_failed_attempts").IsConcurrencyToken();
            profile.Property(p => p.PinLockedUntil).HasColumnName("pin_locked_until").IsConcurrencyToken();

            // The PIN reset token (MYSS-36): stored as its hash, so a read of
            // the table does not hand out working reset links. A concurrency
            // token like the PIN columns, so two uses of one link cannot both
            // land. Indexed because the anonymous reset finds the profile by it.
            profile.Property(p => p.PinResetTokenHash).HasColumnName("pin_reset_token_hash").HasMaxLength(64).IsConcurrencyToken();
            profile.Property(p => p.PinResetRequestedAt).HasColumnName("pin_reset_requested_at");
            profile.HasIndex(p => p.PinResetTokenHash).IsUnique();

            // Email verification (MYSS-305), on the same pattern: the token as
            // its hash, a concurrency token, indexed for the anonymous lookup.
            profile.Property(p => p.EmailVerifiedAt).HasColumnName("email_verified_at");
            profile.Property(p => p.PendingEmail).HasColumnName("pending_email").HasMaxLength(320);
            profile.Property(p => p.EmailVerificationTokenHash)
                .HasColumnName("email_verification_token_hash")
                .HasMaxLength(64)
                .IsConcurrencyToken();
            profile.Property(p => p.EmailVerificationRequestedAt).HasColumnName("email_verification_requested_at");
            profile.Property(p => p.IcmContactMatchedAt).HasColumnName("icm_contact_matched_at");

            // Worker override accounts (MYSS-321). Every existing row is a
            // citizen's own registration, which the default records.
            profile.Property(p => p.Origin)
                .HasColumnName("origin")
                .HasMaxLength(32)
                .HasDefaultValue(ProfileOrigins.Registration)
                .IsRequired();
            profile.Property(p => p.CreatedByIdir).HasColumnName("created_by_idir").HasMaxLength(64);
            profile.HasIndex(p => p.EmailVerificationTokenHash).IsUnique();

            // Support View client search (MYSS-275): the BCeID user name a
            // worker can search by, taken from the citizen's sign-in token.
            profile.Property(p => p.BceidUsername).HasColumnName("bceid_username").HasMaxLength(255);

            // Who the account is for. The representative and the linked
            // documents have tables of their own, deleted with the profile.
            profile.Property(p => p.AccountCreationContext)
                .HasColumnName("account_creation_context")
                .HasConversion<string>()
                .HasMaxLength(32);
            profile.Property(p => p.HelperAuthorized).HasColumnName("helper_authorized");
            profile.HasOne(p => p.Representative)
                .WithOne()
                .HasForeignKey<AccountRepresentative>(r => r.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            profile.HasMany(p => p.Documents)
                .WithOne()
                .HasForeignKey(d => d.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Registration's email proof (MYSS-295): keyed by the identity
            // subject, because no profile exists yet. The token as its hash and
            // a concurrency token, as on the profile.
            var registrationEmail = modelBuilder.Entity<RegistrationEmailVerification>();
            registrationEmail.ToTable("registration_email_verifications");
            registrationEmail.HasKey(r => r.Subject);
            registrationEmail.Property(r => r.Subject).HasColumnName("subject").HasMaxLength(255);
            registrationEmail.Property(r => r.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            registrationEmail.Property(r => r.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsConcurrencyToken();
            registrationEmail.Property(r => r.RequestedAt).HasColumnName("requested_at");
            registrationEmail.Property(r => r.VerifiedAt).HasColumnName("verified_at");
            registrationEmail.HasIndex(r => r.TokenHash).IsUnique();
            profile.HasMany(p => p.Phones)
                .WithOne()
                .HasForeignKey(p => p.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            var phone = modelBuilder.Entity<MyssUserPhone>();
            phone.ToTable("myss_user_phones");
            phone.HasKey(p => p.Id);
            phone.Property(p => p.Id).HasColumnName("id");
            phone.Property(p => p.ProfileId).HasColumnName("profile_id");
            phone.Property(p => p.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16);
            phone.Property(p => p.Number).HasColumnName("number").HasMaxLength(10).IsRequired();
            phone.Property(p => p.Position).HasColumnName("position");

            // One number per type: ICM holds one home, cell, work and message
            // number per contact, so the list stays something it can take.
            phone.HasIndex(p => new { p.ProfileId, p.Type }).IsUnique();

            var representative = modelBuilder.Entity<AccountRepresentative>();
            representative.ToTable("account_representatives");
            representative.HasKey(r => r.Id);
            representative.Property(r => r.Id).HasColumnName("id");
            representative.Property(r => r.ProfileId).HasColumnName("profile_id");
            representative.Property(r => r.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(32);
            representative.Property(r => r.FirstName).HasColumnName("first_name").HasMaxLength(200).IsRequired();
            representative.Property(r => r.LastName).HasColumnName("last_name").HasMaxLength(200).IsRequired();
            representative.Property(r => r.Phone).HasColumnName("phone").HasMaxLength(10).IsRequired();
            representative.Property(r => r.Agency).HasColumnName("agency").HasMaxLength(200);
            representative.Property(r => r.CreatedAt).HasColumnName("created_at");
            representative.HasIndex(r => r.ProfileId).IsUnique();

            var document = modelBuilder.Entity<AccountDocument>();
            document.ToTable("account_documents");
            document.HasKey(d => d.Id);
            document.Property(d => d.Id).HasColumnName("id");
            document.Property(d => d.ProfileId).HasColumnName("profile_id");
            document.Property(d => d.AttachmentId).HasColumnName("attachment_id");
            document.Property(d => d.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(32);
            document.Property(d => d.AttachedAt).HasColumnName("attached_at");
            document.HasIndex(d => d.ProfileId);

            // A file is linked to one account at most.
            document.HasIndex(d => d.AttachmentId).IsUnique();

            var dispatchEvent = modelBuilder.Entity<BusPassDispatchEvent>();
            dispatchEvent.ToTable("bus_pass_dispatch_events");
            dispatchEvent.HasKey(e => e.Id);
            dispatchEvent.Property(e => e.Id).HasColumnName("id");
            dispatchEvent.Property(e => e.SubmissionId).HasColumnName("submission_id");
            dispatchEvent.Property(e => e.AttemptId).HasColumnName("attempt_id");
            dispatchEvent.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(16);
            dispatchEvent.Property(e => e.OccurredAt).HasColumnName("occurred_at");
            dispatchEvent.Property(e => e.RequestId).HasColumnName("request_id").HasMaxLength(128);
            dispatchEvent.Property(e => e.ReferenceNumber).HasColumnName("reference_number").HasMaxLength(64);
            dispatchEvent.Property(e => e.ErrorCode).HasColumnName("error_code").HasMaxLength(64);
            dispatchEvent.Property(e => e.ErrorMessage).HasColumnName("error_message").HasMaxLength(1024);
            dispatchEvent.HasOne<FormSubmission>()
                .WithMany()
                .HasForeignKey(e => e.SubmissionId)
                .OnDelete(DeleteBehavior.Restrict);

            // Two indexes on the same column need distinct model names, or EF
            // treats the second HasIndex as a reconfiguration of the first.
            dispatchEvent.HasIndex(e => e.SubmissionId, "ix_bus_pass_dispatch_events_submission_id");
            dispatchEvent.HasIndex(e => e.AttemptId, "ix_bus_pass_dispatch_events_attempt_id");

            // At most one accepted hand-off per submission: ICM has no
            // idempotency key, so this is the last line against sending a
            // request twice.
            dispatchEvent.HasIndex(e => e.SubmissionId, "ux_bus_pass_dispatch_events_accepted")
                .IsUnique()
                .HasFilter("type = 'Accepted'");
        }
    }
}
