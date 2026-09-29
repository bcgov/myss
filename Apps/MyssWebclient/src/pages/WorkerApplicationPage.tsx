import { useParams } from "react-router";

import ReviewApplication from "@/widgets/review/ReviewApplication";

/**
 * One submitted application opened by a worker: read what the applicant
 * submitted and move it through Under Review to Accepted or Denied.
 */
export default function WorkerApplicationPage() {
  const { id } = useParams<{ id: string }>();

  return (
    <>
      <h1>Application review</h1>
      <ReviewApplication applicationId={id!} />
    </>
  );
}
