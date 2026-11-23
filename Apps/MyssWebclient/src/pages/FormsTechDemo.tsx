import { Link, useSearchParams } from "react-router";

import { SubmissionErrors } from "@myss/web-common/components";
import FormSpecWidget from "@/widgets/FormSpecWidget";

const DEFAULT_FORM_SPEC_ID = "poc-test-form";
// The id goes into the API path with the user's token, so only plain slugs.
const FORM_SPEC_ID_PATTERN = /^[a-z0-9][a-z0-9-]*$/;

/**
 * Forms tech demo: the Strapi-authored, version-stamped Form.io loop. Listing
 * and reading submissions back are staff calls and live on the admin site.
 * `?formSpecId=` renders another published form.
 */
export default function FormsTechDemo() {
  const [searchParams] = useSearchParams();
  const requested = searchParams.get("formSpecId") ?? "";
  const formSpecId = FORM_SPEC_ID_PATTERN.test(requested)
    ? requested
    : DEFAULT_FORM_SPEC_ID;

  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to="/techdemos">← Tech demos</Link>
      </nav>
      <h1>Forms tech demo</h1>
      <FormSpecWidget
        key={formSpecId}
        formSpecId={formSpecId}
        renderSubmissionError={(error) => <SubmissionErrors error={error} />}
        showSpecHeading
      />
    </>
  );
}
