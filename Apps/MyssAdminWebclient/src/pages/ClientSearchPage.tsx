import ClientSearch from "@/widgets/support/ClientSearch";
import pageStyles from "./Page.module.css";

/**
 * Support View: search for clients (MYSS-275), so a worker can validate a
 * client's personal information.
 */
export default function ClientSearchPage() {
  return (
    <div className={pageStyles.page}>
      <h1>Search for clients</h1>
      <p>
        Search by any one or more fields. Names and the BCeID user name match on
        any part of the name.
      </p>
      <ClientSearch />
    </div>
  );
}
