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
| First, middle and last name (draft working copy) | `intake.application_answers.answers` (jsonb) | MySS pre-acceptance | abandoned draft; denied application | Intake service logs ids and counts only | Overwritten on every save while the application is a draft; not read once submitted. |
| First, middle and last name (submitted snapshot) | `platform.event.payload` on `Submitted` events, `answers` | MySS pre-acceptance | denied application | Never logged | The log is append-only. The handbook's purge example deletes answer rows and never event rows, so this needs payload redaction or crypto-shredding before any purge can be complete. Open item. |
| Keycloak subject (`sub`) | `intake.application_answers.owner_subject`; `platform.event.actor` as `applicant:{sub}` | MySS | with the row or event it identifies | Appears in logs as an identifier only where an id is needed | Pseudonymous; resolves to a person only through Keycloak. Same class as the attachments owner column. |
| Worker IDIR username | `platform.event.actor` as `worker:{idir}` on `ReviewStarted`, `Accepted` and `Denied` events | MySS | with the event (the audit trail of the decision) | Logged with the action and application id | Staff identifier, not citizen data, but personal information all the same. Retained as long as the decision it records. |
| Registration profile: first name, last name, date of birth, email | `forms.myss_user_profiles` | MySS | account lifetime | Not logged | Written by the registration form submission. |
| Registration profile: SIN | `forms.myss_user_profiles.sin` | MySS | account lifetime | Not logged | **Stored as entered**, in a 20-character column, straight from the form answers. Handbook Part 7.5 requires salted-hashed and never in clear. Pre-existing gap; needs its own ticket. |
| Keycloak subject | `forms.myss_user_profiles.subject` | MySS | account lifetime | Identifier only | Unique per profile. |
| Account phone numbers and their types | `forms.myss_user_phones.number`, `.type` | MySS | account lifetime | Not logged; the account service logs the profile id and a count only | Entered on Account Info (MYSS-271), ten digits each, one per type. Replaced as a whole list on every save. Not sent to ICM yet; when it is, ICM becomes the system of record. |
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
