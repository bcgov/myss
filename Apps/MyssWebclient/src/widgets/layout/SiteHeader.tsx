// Production BC Gov header markup (logo + ministry text + accessibility skip
// links). Styling comes from the global bootstrap + myss-theme CSS.
export default function SiteHeader() {
    return (
        <div role="banner" className="bcgov-title">
            <div id="header-main">
                <div className="container">
                    <div id="header-main-row1" className="row">
                        <div className="col-xs-6 col-sm-6 col-md-5 col-lg-6 header-main-left">
                            <div id="logo">
                                <a href="http://www2.gov.bc.ca/" className="pull-left">
                                    <img
                                        src="/images/gov3_bc_logo.png"
                                        alt="Government of BC"
                                        title="Government of B.C."
                                    />
                                </a>
                                <p
                                    className="pull-left hidden-xs"
                                    style={{
                                        fontSize: "1em",
                                        lineHeight: "1em",
                                        paddingTop: "12px",
                                    }}
                                >
                                    Ministry of
                                    <br />
                                    Social Development
                                    <br />
                                    and Poverty Reduction
                                </p>
                            </div>

                            <div id="access">
                                <ul>
                                    <li aria-label="Keyboard Tab Skip">
                                        <a
                                            href="#main-content-anchor"
                                            aria-label="Skip to main content"
                                        >
                                            Skip to main content
                                        </a>
                                    </li>
                                    <li aria-label="Keyboard Tab Skip">
                                        <a
                                            href="http://www2.gov.bc.ca/gov/content/home/accessibility"
                                            aria-label="Accessibility Statement"
                                        >
                                            Accessibility Statement
                                        </a>
                                    </li>
                                </ul>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
