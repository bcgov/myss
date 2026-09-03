import { Link } from "@bcgov/design-system-react-components";

const applicationProcessUrl =
    "http://www2.gov.bc.ca/assets/download/240B1495B3C3497F8153C6D1EC6429B3";

export default function HowToApply() {
    return (
        <div>
            <h4>How to apply for or return to assistance</h4>
            <Link href={applicationProcessUrl} size="large">
                B.C. Government information on the application process
            </Link>
        </div>
    );
}
