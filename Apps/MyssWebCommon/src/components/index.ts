// Page pieces both webclients render the same way.

export { default as AnnouncementBanner } from "./AnnouncementBanner";
export { default as AuthCallbackPage } from "./AuthCallbackPage";
export { default as DraftApplicationForm } from "./DraftApplicationForm";
export type {
    DraftAnswersInput,
    DraftApplication,
    DraftFormMessages,
} from "./DraftApplicationForm";
export { default as MessageText } from "./MessageText";
export { messageShowsPlaceholder } from "./messageTextParser";
export { default as StatusPill } from "./StatusPill";
export { default as SubmissionErrors } from "./SubmissionErrors";
export { useClientValidation } from "./useClientValidation";
