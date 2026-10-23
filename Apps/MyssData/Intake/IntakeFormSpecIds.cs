namespace Myss.Data.Intake
{
    /// <summary>
    /// The form IDs stored on <see cref="ApplicationAnswers.FormSpecId"/>. Shared so that
    /// the citizen API, which writes the rows, and the staff API, which reviews them,
    /// agree on which rows belong to which form.
    /// </summary>
    public static class IntakeFormSpecIds
    {
        /// <summary>The income assistance application, the form staff review.</summary>
        public const string IncomeAssistance = "income-assistance-poc";

        /// <summary>The ID the product team publishes for Application for Assistance.</summary>
        public const string ApplicationForAssistance = "application-for-assistance";
    }
}
