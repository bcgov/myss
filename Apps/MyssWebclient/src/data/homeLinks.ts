// Static link content for the Home page. Extracted from the current prod
// MySS home page so the copy/URLs live in one editable place.

export interface ResourceLink {
    label: string;
    href: string;
    /** External links open in a new tab. */
    external?: boolean;
}

export const commonQuestions: ResourceLink[] = [
    {
        label: "What are common eligibility requirements to consider when applying for Income Assistance or Disability Assistance?",
        href: "http://www2.gov.bc.ca/assets/download/EC6CBA242B494D0DBDAA1427B9D61CE2",
        external: true,
    },
    {
        label: "Learn More about My Self Serve",
        href: "http://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/access-services/my-self-serve",
        external: true,
    },
    {
        label: "How do I apply for Employment Insurance?",
        href: "https://www.canada.ca/en/services/benefits/ei/ei-apply-online.html",
        external: true,
    },
    {
        label: "What are my Rights and Responsibilities?",
        href: "http://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/apply-for-assistance/rights-responsibilities",
        external: true,
    },
    {
        label: "Where can I find an office near me?",
        href: "http://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/access-services",
        external: true,
    },
    {
        label: "How can I report fraud?",
        href: "http://www2.gov.bc.ca/gov/content/governments/policies-for-government/bcea-policy-and-procedure-manual/compliance-and-debt-management/referral-for-plms-review-or-investigation",
        external: true,
    },
    {
        label: "How do I submit documents for my application?",
        href: "http://www2.gov.bc.ca/assets/download/67FFD5354CE34D1087D587B12DA806E2",
        external: true,
    },
    {
        label: "What is the definition of a spouse or a marriage-like relationship?",
        href: "https://www2.gov.bc.ca/assets/download/58A8F49FE04B4111863836A7C6DE782D",
        external: true,
    },
    {
        label: "What is the definition of a dependent child?",
        href: "https://www2.gov.bc.ca/assets/download/019EDC1CBB3B4BF9905F248EB588DD82",
        external: true,
    },
    {
        label: "What is the definition of dependant(s)?",
        href: "https://www2.gov.bc.ca/assets/download/44C805CADCE94C2E8191DAD49A7A3DB8",
        external: true,
    },
];

export const otherResources: ResourceLink[] = [
    {
        label: "Employment Planning",
        href: "http://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/employment-planning",
        external: true,
    },
    {
        label: "General Health and Supplements",
        href: "http://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/supplements",
        external: true,
    },
    {
        label: "Support for Young Families",
        href: "http://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/fyp",
        external: true,
    },
    {
        label: "Leaving Assistance",
        href: "http://www2.gov.bc.ca/gov/content/family-social-supports/income-assistance/on-assistance/leaving-assistance",
        external: true,
    },
    {
        label: "BC Employment and Assistance Manual",
        href: "http://www2.gov.bc.ca/gov/content/governments/policies-for-government/bcea-policy-and-procedure-manual",
        external: true,
    },
];

// YouTube video IDs embedded on the prod home page.
export const homeVideos: { id: string; title: string }[] = [
    { id: "rHgD9GOB4nQ", title: "How to use MySS" },
    { id: "Mng2RHEpb-o", title: "How to use MySS in ASL" },
];
