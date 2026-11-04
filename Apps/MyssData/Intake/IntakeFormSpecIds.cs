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

        /// <summary>
        /// The simplified Application for Assistance a worker submits for a client
        /// who cannot submit their own (MYSS-321): the client's demographics only.
        /// Unlike the citizen's AFA, it goes to the worker queue.
        /// </summary>
        public const string WorkerOverrideAfa = "worker-override-afa";
    }
}
