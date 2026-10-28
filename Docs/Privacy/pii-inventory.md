# PII inventory

- **Status:** Living document. Started 2026-09-28 with the Application Intake
  applicant slice; seeded with every place the code already held personal
  information at that date, verified against the EF models and the seeded
  form specs.
- **Why it exists:** Handbook Part 7.5. MySS is a system of record for
  applicant data until reviewer Accept, so it must know every field of
  personal information it holds, where, and under which ownership boundary.
  The inventory drives the log-redaction list (Part 4.12), the per-data-class
  retention schedules and purge job (SB-08), and the privacy impact
  assessment.
- **The rule:** adding a field that holds personal information is an
  inventory change, not just a column add. The pull request that adds the
  field adds the row here.

## How to read the table

- **Boundary** is who is authoritative for the data (Part 3.5):
  *MySS pre-acceptance* means MySS owns it until the application is promoted;
  *MySS* means MySS owns it outright (registration, preferences);
  *ICM* means ICM is the system of record and MySS holds a copy or a reference.
- **Retention class** names the schedule the purge job will use once the
  windows are decided (SB-08). Until then the class is a label, not a window.
- **Logging** says how the value is kept out of logs today. Structured logs
  are stdout JSON with no central redaction list yet; the protection is that
  services log identifiers and counts only, plus the `Sin` value type, which
  prints as `[SIN redacted]`.

## Inventory

| Data | Location | Boundary | Retention class | Logging | Notes |
| --- | --- | --- | --- | --- | --- |
| First, middle and last name (draft working copy) | `intake.application_answers.answers` (jsonb) | MySS pre-acceptance | abandoned draft; denied application | Intake service logs ids and counts only | Overwritten on every save while the application is a draft or returned for more information; otherwise not read once submitted. |
| First, middle and last name (submitted snapshot) | `platform.event.payload` on `Submitted` and `Resubmitted` events, `answers` | MySS pre-acceptance | denied application | Never logged | The log is append-only. The handbook's purge example deletes answer rows and never event rows, so this needs payload redaction or crypto-shredding before any purge can be complete. Open item. |
| Product-authored Application for Assistance answers (draft and submitted) | `intake.application_answers.answers` (jsonb) and `platform.event.payload` on `Submitted`, `answers`, for `application-for-assistance` | MySS pre-acceptance | abandoned draft; submitted application | Intake logs IDs, form ID and validation error counts only; never logs answer values | The product team defines the fields in Strapi; inventory each personal field once the form is authored before publication. Submission snapshots in the append-only event log need the same redaction/crypto-shredding strategy as the POC. AFA is not worker-accessible yet. |
| Keycloak subject (`sub`) | `intake.application_answers.owner_subject`; `platform.event.actor` as `applicant:{sub}` | MySS | with the row or event it identifies | Appears in logs as an identifier only where an id is needed | Pseudonymous; resolves to a person only through Keycloak. Same class as the attachments owner column. |
| Worker IDIR username | `platform.event.actor` as `worker:{idir}` on every worker event (`PickedUp`, `Released`, `NoteAdded`, `MoreInfoRequested`, `Escalated`, `Accepted`, `Denied`, and the first slice's `ReviewStarted`) | MySS | with the event (the audit trail of the decision) | Logged with the action and application id | Staff identifier, not citizen data, but personal information all the same. Retained as long as the decision it records. Shown to other workers as the assignee and in the history; never to the applicant. |
| Internal review notes (free text) | `platform.event.payload` on `NoteAdded` events, `text` | MySS pre-acceptance | denied application | Never logged | Workers only; never returned by an applicant endpoint. Free text, so it can hold anything a worker types about the applicant. Same append-only caveat as the submitted snapshot: a complete purge needs payload redaction. Open item. |
| Registration profile: first name, last name, date of birth, email | `forms.myss_user_profiles` | MySS | account lifetime | Not logged | Written by the registration form submission. |
| Registration profile: phone number, gender | `forms.myss_user_profiles.phone`, `.gender` | MySS | account lifetime | Not logged | Asked from registration v4. Phone stored as ten digits; gender as the form's option value (`man`, `nonBinary`, `woman`). Null for profiles registered on earlier versions. The webclient prefills both from the BC Services Card identity token when it carries them; the value stored is still the one submitted. |
| Registration profile: SIN | `forms.myss_user_profiles.sin` | MySS | account lifetime | Not logged | **Stored as entered**, in a 20-character column, straight from the form answers. Handbook Part 7.5 requires salted-hashed and never in clear. Pre-existing gap; needs its own ticket. |
| Keycloak subject | `forms.myss_user_profiles.subject` | MySS | account lifetime | Identifier only | Unique per profile. |
| Account phone numbers and their types | `forms.myss_user_phones.number`, `.type` | MySS | account lifetime | Not logged; the account service logs the profile id and a count only | Entered on Account Info (MYSS-271), ten digits each, one per type. Replaced as a whole list on every save. Not sent to ICM yet; when it is, ICM becomes the system of record. |
| PIN (electronic signature credential) | `forms.myss_user_profiles.taapcd_aae_passcode`, with `.pin_failed_attempts` and `.pin_locked_until` | MySS only; never sent to ICM (RULE-IDA-04) | account lifetime | Never logged; the account service logs the profile id and the outcome only | Basic BCeID only (MYSS-258). **Salted hash only** (PBKDF2, ASP.NET Core Identity's `PasswordHasher` format); the PIN itself is never stored or returned. Set at registration outside the form answers, so it is not in `form_submissions`; changed on Account Info with the current PIN, or reset without it from an emailed single-use link (MYSS-36), which also clears a lockout. A 4-digit PIN has 10,000 values, so the hash slows an offline guess but does not prevent it: protect the column as a credential. Three wrong current PINs in a row lock Change PIN for 15 minutes. |
| PIN reset token | `forms.myss_user_profiles.pin_reset_token_hash`, with `.pin_reset_requested_at` | MySS | until used or replaced | Never logged; the reset service logs the profile id and the outcome only | MYSS-36. A credential, not personal information about the citizen, listed because it opens their account's PIN. **SHA-256 hash only**; the token itself (a random GUID) exists only in the emailed link, in its URL fragment, which the browser never sends to a server. Nulled when used, replaced by a new request, and refused after `Account:PinReset:TokenLifetimeMinutes` (60 by default). An expired hash stays in the row until the next request or reset. |
| Pending email address | `forms.myss_user_profiles.pending_email`, with `.email_verified_at` | MySS | until verified, cancelled or replaced | Not logged; the verification service logs the profile id and the outcome only | MYSS-305. An address the citizen asked to change to on Account Info, or gave on registering again, held until its emailed link is followed; the verified address in `email` stays in use until then. Only set while `email` is verified. Nulled when verified (it then becomes `email`) or cancelled. `email_verified_at` records when `email` was proven; the migration marked every profile from before verification as verified at its `created_at`. |
| Email verification token | `forms.myss_user_profiles.email_verification_token_hash`, with `.email_verification_requested_at` | MySS | until used or replaced | Never logged | MYSS-305. Same handling as the PIN reset token: **SHA-256 hash only**, the token itself (a random GUID) exists only in the emailed link's URL fragment. Nulled when used or when a pending change is cancelled, replaced by a resend or a new change, and refused after `Account:EmailVerification:TokenLifetimeMinutes` (1440 by default). |
| Registration email address and its verification token, before the profile exists | `forms.registration_email_verifications.email`, `.token_hash`, `.requested_at`, `.verified_at`, keyed by `.subject` | MySS | until the registration completes (deleted with it), or at most a link lifetime plus an hour when abandoned | Never logged; the verification service logs that a link was sent or followed, without the address or the subject | MYSS-295. Registration Step 2 proves the Step 1 address by an emailed link before the account is created. One row per signed-in identity; the token is a **SHA-256 hash only**, as for the other emailed links. Deleted on every completed registration, whatever the EmailVerification flag. A proof only counts within a link lifetime of being made (`Account:EmailVerification:TokenLifetimeMinutes`, 1440 by default: the registration session, AC6), and `RegistrationEmailPurge` deletes rows older than that every hour, so an abandoned registration's address does not stay. |
| Email addresses, sent to BC Gov Notify | Leave MySS in the PIN reset email (MYSS-36), the email verification email (MYSS-305: the registration address, or the new address on a change) and the change notice (MYSS-305: the address being replaced). The recipient address and any link go to Notify's `notifysimple/email` API | MySS; Notify holds it as a processor | Notify's own delivery-record retention | Never logged by MySS; the provider logs Notify's `notifyId` and status, and keeps Notify's refusal text out of logs because it can echo the address | No name or other detail goes in the email. Notify keeps per-recipient delivery records (`notification_request`), so the address is also held there; its retention is Notify's, to be confirmed for the privacy impact assessment. |
| Monthly report reminder preference | `forms.myss_user_profiles.monthly_report_reminder` | MySS | account lifetime | Not logged | A yes/no preference (MYSS-271). Stored only; nothing sends the reminder yet. |
| Registration answers, including the SIN in clear | `forms.form_submissions.answers` for `registration` | MySS | account lifetime | Not logged | The registration submission is stored verbatim as well as being copied into the profile. Same SIN gap as above, second copy. |
| Bus pass answers: names, date of birth, address, phone, email, bus pass account number, SIN where the category asks for it | `forms.form_submissions.answers` for `bc-bus-pass` | ICM after dispatch; MySS keeps the copy | service request copy | Not logged; bus pass service logs ids, outcome and codes only | Anonymous route, so no owner subject. The generated PDF is rebuilt from these answers on demand. SIN in clear where present. |
| Bus pass dispatch: ICM reference number and ICM error text | `forms.bus_pass_dispatch_events.reference_number`, `.error_message` | ICM | service request copy | Reference numbers are logged; error text is not | ICM's free-text error may echo request content; treat as potentially personal. |
| POC test form answers | `forms.form_submissions.answers` for `poc-test-form` | MySS | development data | Not logged | Test form; the SIN field there validates a synthetic value. |
| Attachment metadata: original file name, content type, size, scan signature | `attachments.attachments` | MySS pre-acceptance | attachment after promotion | Ids, sizes and content types are logged; file names are not | A file name chosen by a citizen can itself be personal. |
| Attachment owner and object key | `attachments.attachments.owner_subject`, `.storage_key`; the object key in the MinIO bucket | MySS pre-acceptance | attachment after promotion | Identifier only | The object key embeds a sanitised copy of the owner subject. |
| Attachment content | MinIO bucket objects | MySS pre-acceptance | attachment after promotion | Never logged | Released only after a clean scan; ownership checked on every fetch. |
| Correlation id | `platform.event.request_id`; `forms.bus_pass_dispatch_events.request_id` | n/a | n/a | Logged by design | Not personal information. Listed so nobody wonders. |

Not persisted and therefore not listed: the eligibility estimator runs entirely
in the browser; Keycloak token claims are read per request and only the
subject is stored; nothing is written to ICM by the intake slice.

## Open items

- Retention windows for every class above are owed (SB-08).
- Purge or redaction of event payloads that hold personal information, so a
  purge of a denied application is complete (crypto-shredding or payload
  nulling; design follow-up).
- SIN at rest in the registration profile and in the stored registration and
  bus pass answers: salted-hashed per Part 7.5, never in clear.
- A central log-redaction list (Part 4.12) driven by this table, replacing
  per-service discipline.
