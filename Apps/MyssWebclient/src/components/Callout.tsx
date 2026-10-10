import type { ComponentProps, ReactNode } from "react";
import { Callout as BcdsCallout } from "@bcgov/design-system-react-components";

import styles from "./Callout.module.css";

// A tinted box that sets content apart from the page: a heading and any
// content nested under it. Wraps the design system's Callout, which drops its
// own title, description and buttons as soon as it is given children, and
// draws its title as a <span>. This keeps both: the title is a real heading
// (so it shows in the page outline and takes the site heading styles) and
// anything can be nested below it.

type HeadingLevel = 2 | 3 | 4 | 5 | 6;

type CalloutProps = Readonly<{
  /** Shown as a heading above the content. Omit for a callout without one. */
  title?: ReactNode;
  /** Heading level for the title, to fit the page outline. Defaults to 2. */
  headingLevel?: HeadingLevel;
  /** Design system colour scheme. Defaults to the component's light grey. */
  variant?: ComponentProps<typeof BcdsCallout>["variant"];
  /** Hides the coloured bar down the left edge. */
  isLeftBorderDisabled?: boolean;
  children?: ReactNode;
}>;

export default function Callout({
  title,
  headingLevel = 2,
  variant,
  isLeftBorderDisabled,
  children,
}: CalloutProps) {
  const Heading = `h${headingLevel}` as const;
  // The wrapper only carries the border width (see Callout.module.css): the
  // design system component replaces its own classes with any it is given.
  return (
    <div className={styles.callout}>
      <BcdsCallout
        variant={variant}
        isLeftBorderDisabled={isLeftBorderDisabled}
      >
        <div className={styles.content}>
          {title && <Heading className={styles.title}>{title}</Heading>}
          {children}
        </div>
      </BcdsCallout>
    </div>
  );
}
