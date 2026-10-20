import { Link } from "react-router";

import SubmissionErrors from "@/components/SubmissionErrors";
import FormSpecWidget from "@/widgets/FormSpecWidget";

const FORM_SPEC_ID = "poc-test-form";

/**
 * Forms tech demo: the Strapi-authored, version-stamped Form.io loop. Listing
 * and reading submissions back are staff calls and live on the admin site.
 */
export default function FormsTechDemo() {
  return (
    <>
      <nav aria-label="Breadcrumb">
        <Link to="/techdemos">← Tech demos</Link>
      </nav>
      <h1>Forms tech demo</h1>
      <FormSpecWidget
        formSpecId={FORM_SPEC_ID}
        renderSubmissionError={(error) => <SubmissionErrors error={error} />}
        showSpecHeading
      />
    </>
  );
}
