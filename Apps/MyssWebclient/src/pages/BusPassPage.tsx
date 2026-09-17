import { Link } from "react-router";

import BusPassForm from "@/widgets/BusPassForm";

/**
 * The public BC Bus Pass request page: the form and, after submit, the
 * outcome. Nothing else. This page needs no sign-in, so it must not list or
 * link to stored submissions; the outcome the citizen just received is the
 * only record shown.
 */
export default function BusPassPage() {
    return (
        <>
            <nav aria-label="Breadcrumb">
                <Link to="/">← Home</Link>
            </nav>
            <BusPassForm />
        </>
    );
}
