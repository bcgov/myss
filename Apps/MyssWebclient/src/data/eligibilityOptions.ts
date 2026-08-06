// Options and copy for the Eligibility Estimator form, mirroring the current
// prod page (https://myselfserve.gov.bc.ca/EligibilityEstimator).

export interface RadioOption {
    value: string;
    label: string;
}

export const relationshipStatusOptions: RadioOption[] = [
    { value: "single", label: "Single and Never Married" },
    { value: "married", label: "Married" },
    { value: "marriagelike", label: "Marriage-Like Relationship" },
    { value: "divorced", label: "Divorced" },
    { value: "separated", label: "Separated" },
    { value: "widowed", label: "Widowed" },
];

// Relationship values that reveal the spouse/partner sections.
export const partneredStatuses = ["married", "marriagelike"];
