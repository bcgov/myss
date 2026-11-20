import { useMe } from "@/auth/useMe";
import { useSession } from "@/auth/useSession";
import ReviewQueue from "@/widgets/review/ReviewQueue";

/**
 * The worker operations view (MYSS-226): every submitted application, with
 * its request number as the way into the review and the signed-in worker's
 * own files picked out.
 */
export default function ApplicationsPage() {
  const { isAuthenticated, user } = useSession();
  const me = useMe(isAuthenticated, user?.sub);

  return (
    <>
      <h1>Applications</h1>
      <ReviewQueue myIdir={me.data?.idirUsername ?? undefined} />
    </>
  );
}
