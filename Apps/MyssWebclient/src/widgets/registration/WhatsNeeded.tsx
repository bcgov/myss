import styles from "./WhatsNeeded.module.css";

/** What to have ready before creating an account, at the top of the questions page. */
export default function WhatsNeeded() {
  return (
    <section className={styles.needed} aria-labelledby="whats-needed-heading">
      <h2 id="whats-needed-heading" className={styles.heading}>
        What’s needed
      </h2>
      <ul className={styles.list}>
        <li>An email address</li>
        <li>Your Social Insurance Number (SIN)</li>
        <li>Your spouse’s information, if you have a spouse</li>
        <li>
          If you receive assistance from the ministry now: details that connect
          your open case to your MySS account. These are the amount of your last
          assistance payment, your Personal Health Number, the expiry date of
          your driver’s licence (if you have one), the last address the ministry
          has for you, and how you receive your payments.
        </li>
      </ul>
      <p className={styles.paragraph}>
        You can upload identification and the documents the ministry asks for
        while you complete your application, or after you submit it. After you
        submit your Application for Assistance, you can also fax or mail them to
        the ministry, or bring them to a ministry office.
      </p>
      <p className={styles.paragraph}>
        If you cannot provide this information, or you need help with your
        application, call <a href="tel:+18668660800">1-866-866-0800</a>.
      </p>
    </section>
  );
}
