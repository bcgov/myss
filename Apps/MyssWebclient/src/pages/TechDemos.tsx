import { Link } from "react-router";

import useDemo from "@/hooks/useDemo";

export default function TechDemos() {
  const { data, error, isPending } = useDemo();

  return (
    <>
      <h1>Tech demo - My Self Serve</h1>
      {isPending && <p>Loading…</p>}
      {error && <p>An error has occurred: {error.message}</p>}
      {data && (
        <dl>
          <h3>From Backend:</h3>
          <dt>First:</dt>
          <dd>{data.foo}</dd>
          <dt>Second:</dt>
          <dd>{data.bar}</dd>
        </dl>
      )}
      <h3>Tech demos</h3>
      <ul>
        <li>
          <Link to="/techdemos/forms">
            Forms - Strapi-authored Form.io loop
          </Link>
        </li>
        <li>
          <Link to="/techdemos/attachments">
            Attachments - scanned upload to object storage
          </Link>
        </li>
      </ul>
    </>
  );
}
