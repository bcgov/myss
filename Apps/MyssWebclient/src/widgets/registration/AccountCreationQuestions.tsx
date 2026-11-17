import {
  InlineAlert,
  Radio,
  RadioGroup,
  TextField,
} from "@bcgov/design-system-react-components";
import { PhoneInput } from "@myss/formio";

import styles from "./AccountCreationQuestions.module.css";
import type { AttachmentPayload } from "@/api/attachments";
import {
  ACCOUNT_CREATION_FIELDS as F,
  type AccountContext,
  type AccountContextValue,
  type AccountCreationAnswers,
  type AccountDocument,
  asksForAgency,
  asksForPowerOfAttorney,
  contextFromValue,
  type DocumentField,
  HELPER_ANSWERS,
  HELPER_QUESTION,
  HR3189_URL,
  isRepresentative,
  REPRESENTATIVE_INFO,
} from "@/lib/accountContext";
import DocumentUpload from "@/widgets/registration/DocumentUpload";

interface AccountCreationQuestionsProps {
  answers: AccountCreationAnswers;
  /** The contexts to choose from, or null when the context is fixed. */
  contexts: readonly AccountContext[] | null;
  /** Keep files in the tab before sign-in, or upload them after it. */
  documentMode: "keep" | "upload";
  /** Whether this tab can keep files while the citizen signs in. */
  canKeep: boolean;
  /** Messages keyed by field. */
  errors: Readonly<Record<string, string>>;
  onChange: (change: Partial<AccountCreationAnswers>) => void;
  onKeepFile: (field: DocumentField, file: File) => Promise<void>;
  onUploaded: (field: DocumentField, attachment: AttachmentPayload) => void;
  onRemoveFile: (field: DocumentField, document: AccountDocument) => void;
}

type QuestionProps = Omit<AccountCreationQuestionsProps, "contexts">;

function Documents({
  field,
  ...props
}: QuestionProps & { field: DocumentField }) {
  return (
    <DocumentUpload
      field={field}
      mode={props.documentMode}
      canKeep={props.canKeep}
      documents={props.answers[field]}
      error={props.errors[field]}
      onKeep={(file) => props.onKeepFile(field, file)}
      onUploaded={(attachment) => props.onUploaded(field, attachment)}
      onRemove={(document) => props.onRemoveFile(field, document)}
    />
  );
}

function HelperQuestions(props: QuestionProps) {
  const { answers, errors, onChange } = props;
  return (
    <div className={styles.followUps}>
      <RadioGroup
        id={F.helperAuthorized}
        label={HELPER_QUESTION}
        value={answers.helperAuthorized}
        onChange={(value) =>
          onChange({ helperAuthorized: value as "yes" | "no" })
        }
        validationBehavior="aria"
        isInvalid={Boolean(errors[F.helperAuthorized])}
        errorMessage={errors[F.helperAuthorized]}
      >
        {HELPER_ANSWERS.map((answer) => (
          <Radio key={answer.value} value={answer.value}>
            {answer.label}
          </Radio>
        ))}
      </RadioGroup>
      {answers.helperAuthorized === "yes" && (
        <>
          <p className={styles.paragraph}>
            Please fill out a{" "}
            <a href={HR3189_URL} target="_blank" rel="noopener noreferrer">
              Consent to Disclosure of Information
              <span className={styles.visuallyHidden}>
                {" "}
                (PDF, opens in a new tab)
              </span>
            </a>{" "}
            document and attach it below.
          </p>
          <Documents field={F.consentDocuments} {...props} />
        </>
      )}
    </div>
  );
}

function textFieldProps(
  field: string,
  value: string,
  errors: Readonly<Record<string, string>>,
) {
  return {
    id: field,
    value,
    validationBehavior: "aria" as const,
    isInvalid: Boolean(errors[field]),
    errorMessage: errors[field],
    className: styles.field,
  };
}

function RepresentativeQuestions(
  props: QuestionProps & { context: AccountContextValue },
) {
  const { answers, errors, onChange, context } = props;
  return (
    <>
      <div className={styles.followUps}>
        <h3 className={styles.subheading}>
          Enter your information as the authorized representative
        </h3>
        <TextField
          label="First name"
          isRequired
          autoComplete="given-name"
          onChange={(value) => onChange({ representativeFirstName: value })}
          {...textFieldProps(
            F.representativeFirstName,
            answers.representativeFirstName,
            errors,
          )}
        />
        <TextField
          label="Last name"
          isRequired
          autoComplete="family-name"
          onChange={(value) => onChange({ representativeLastName: value })}
          {...textFieldProps(
            F.representativeLastName,
            answers.representativeLastName,
            errors,
          )}
        />
        <PhoneInput
          label="Phone number"
          description="(999) 999-9999"
          type="tel"
          isRequired
          autoComplete="tel"
          onChange={(value) => onChange({ representativePhone: value })}
          {...textFieldProps(
            F.representativePhone,
            answers.representativePhone,
            errors,
          )}
        />
        {asksForAgency(context) && (
          <TextField
            label="Agency (optional)"
            autoComplete="organization"
            onChange={(value) => onChange({ representativeAgency: value })}
            {...textFieldProps(
              F.representativeAgency,
              answers.representativeAgency,
              errors,
            )}
          />
        )}
        {asksForPowerOfAttorney(context) && (
          <>
            <p className={styles.paragraph}>
              Attach Power of Attorney documents below.
            </p>
            <Documents field={F.powerOfAttorneyDocuments} {...props} />
          </>
        )}
      </div>
      <InlineAlert variant="info" title={REPRESENTATIVE_INFO} />
    </>
  );
}

/**
 * Who the account is for, then the chosen context's own questions under it,
 * as the current MySS asks them. The same questions are shown before sign-in
 * and, when they must be asked again, after it.
 */
export default function AccountCreationQuestions({
  contexts,
  ...props
}: AccountCreationQuestionsProps) {
  const { answers, errors, onChange } = props;
  const context = answers.context;

  return (
    <section
      className={styles.questions}
      aria-labelledby="account-context-heading"
    >
      <h2 id="account-context-heading" tabIndex={-1} className={styles.heading}>
        Creating a My Self Serve account
      </h2>
      {contexts ? (
        <RadioGroup
          id={F.context}
          aria-labelledby="account-context-heading"
          value={context}
          onChange={(value) =>
            onChange({ context: value as AccountContextValue })
          }
          validationBehavior="aria"
          isInvalid={Boolean(errors[F.context])}
          errorMessage={errors[F.context]}
        >
          {contexts.map((option) => (
            <Radio key={option.value} value={option.value}>
              {option.label}
            </Radio>
          ))}
        </RadioGroup>
      ) : (
        <div className={styles.answer}>
          <p className={styles.paragraph}>
            <strong>Your answer: </strong>
            {contextFromValue(context)?.label}
          </p>
          <p className={styles.hint}>
            To change this answer, select Cancel and start again.
          </p>
        </div>
      )}
      {context === "withHelper" && <HelperQuestions {...props} />}
      {context && isRepresentative(context) && (
        <RepresentativeQuestions {...props} context={context} />
      )}
    </section>
  );
}
