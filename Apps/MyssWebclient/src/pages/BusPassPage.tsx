import { Link } from "react-router";

import BusPassForm from "@/widgets/BusPassForm";

export default function BusPassPage() {
  return (
    <div className="form-page">
      <nav aria-label="Breadcrumb">
        <Link to="/">← Home</Link>
      </nav>
      {/* The form's own title is the page's h1 (spec v7), so none here. */}
      <BusPassForm />
    </div>
  );
}
