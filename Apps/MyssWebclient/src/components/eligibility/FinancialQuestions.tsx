import { NumberField } from "@bcgov/design-system-react-components";

import styles from "./FinancialQuestions.module.css";

interface FinancialQuestionsProps {
    who: "you" | "spouse";
}

const currencyFormat = { style: "currency", currency: "CAD" } as const;

// The four currency questions (income + two vehicle values + other assets),
// worded for either the applicant ("you") or their spouse. Placeholder inputs;
// values are not read or calculated yet.
export default function FinancialQuestions({ who }: FinancialQuestionsProps) {
    const isSpouse = who === "spouse";
    const prefix = isSpouse ? "Partner" : "";

    const incomeLabel = isSpouse
        ? "Spouse's Monthly Income"
        : "Your Monthly Income";
    const transportVehicleLabel = isSpouse
        ? "What is the value of your spouse's vehicle minus any amount owing that is used for day to day transportation needs"
        : "What is the value of your vehicle minus any amount owing that is used for day to day transportation needs";
    const otherVehiclesLabel = isSpouse
        ? "What is the value minus any amount owing of all your spouse's other vehicles?"
        : "What is the value minus any amount owing of all your other vehicles?";
    const otherAssetsLabel = isSpouse
        ? "Spouse's Combined Value of Other Assets (Property, Investments, Cash, or Savings)"
        : "Your Combined Value of Other Assets (Property, Investments, Cash, or Savings)";

    return (
        <div className={styles.fields}>
            <NumberField
                name={`${prefix}MonthlyIncome`}
                label={incomeLabel}
                defaultValue={0}
                minValue={0}
                formatOptions={currencyFormat}
            />
            <NumberField
                name={`${prefix}VehicleValueMinusTransportation`}
                label={transportVehicleLabel}
                defaultValue={0}
                minValue={0}
                formatOptions={currencyFormat}
            />
            <NumberField
                name={`${prefix}VehicleValue`}
                label={otherVehiclesLabel}
                defaultValue={0}
                minValue={0}
                formatOptions={currencyFormat}
            />
            <NumberField
                name={`${prefix}AssetValue`}
                label={otherAssetsLabel}
                defaultValue={0}
                minValue={0}
                formatOptions={currencyFormat}
            />
        </div>
    );
}
