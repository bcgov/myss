// Introductory copy describing what My Self Serve is (lifted from prod home).
import { Link as BcLink } from "@bcgov/design-system-react-components";
import { paths } from "@/routes/paths";

export default function IntroSection() {
    return (
        <div>
            <p>
                Apply for and manage income and disability assistance for B.C 
                residents online.
            </p>
            <p>
                <b>Need help with My Self Serve?</b><br></br>
                Call us at <a href="tel:+18668660800">1-866-866-0800</a> 
                if you need help applying for or accessing My Self Serve.
            </p>
            <p>
                <b>Not sure if you're eligible for assistance?</b><br></br>
                Check your eligibility without logging in.
            </p>
            <p>
                <BcLink
                    href={paths.eligibilityEstimator}
                    isButton
                    buttonVariant="primary"
                >
                    Check your eligibility
                </BcLink>
            </p>
        </div>
    );
}
