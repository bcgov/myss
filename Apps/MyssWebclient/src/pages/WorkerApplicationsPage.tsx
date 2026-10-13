import ReviewQueue from "@/widgets/review/ReviewQueue";

/**
 * The worker operations view (MYSS-226): every submitted Income Assistance
 * application, with its request number as the way into the review.
 */
export default function WorkerApplicationsPage() {
  return (
    <>
      <h1>Applications</h1>
      <ReviewQueue />
    </>
  );
}
