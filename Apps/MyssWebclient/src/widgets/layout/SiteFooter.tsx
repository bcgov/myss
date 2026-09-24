// Production BC Gov footer markup (admin links row). Styling comes from the
// global bootstrap + myss-theme CSS.
const adminLinks = [
    { label: "Home", href: "http://www2.gov.bc.ca/gov/content/home", target: "_self", ariaLabel: "BC Homepage" },
    { label: "Disclaimer", href: "http://www2.gov.bc.ca/gov/content/home/disclaimer", target: "_self" },
    { label: "Privacy", href: "http://www2.gov.bc.ca/gov/content/home/privacy", target: "_blank" },
    { label: "Terms of Use", href: "https://myselfserve.gov.bc.ca/Terms", target: "_blank" },
    { label: "Accessibility", href: "http://www2.gov.bc.ca/gov/content/home/accessibility", target: "_self" },
    { label: "Copyright", href: "http://www2.gov.bc.ca/gov/content/home/copyright", target: "_self" },
];

export default function SiteFooter() {
    return (
        <div id="footer" role="contentinfo">
            <div id="footerAdminSection">
                <div id="footerAdminLinksContainer" className="container">
                    <div id="footerAdminLinks" className="row">
                        <ul className="inline">
                            {adminLinks.map((link) => (
                                <li key={link.label}>
                                    <a
                                        href={link.href}
                                        target={link.target}
                                        aria-label={link.ariaLabel}
                                    >
                                        {link.label}
                                    </a>
                                </li>
                            ))}
                        </ul>
                    </div>
                </div>
            </div>
        </div>
    );
}
