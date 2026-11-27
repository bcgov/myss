import { useState, type ReactNode } from "react";
import { Link as RouterLink } from "react-router";
import {
  Accordion,
  AccordionGroup,
  AlertBanner,
  AlertDialog,
  Button,
  ButtonGroup,
  Checkbox,
  CheckboxGroup,
  DatePicker,
  InlineAlert,
  Link,
  Modal,
  ProgressBar,
  ProgressCircle,
  Radio,
  RadioGroup,
  Select,
  TagGroup,
  TagList,
  TextArea,
  TextField,
} from "@bcgov/design-system-react-components";
import {
  DateInputField,
  type DateEntry,
  type DateEntryPart,
  type DateInputError,
} from "@myss/formio";
import { SubmissionRejectedError } from "@myss/web-common/api";
import {
  AnnouncementBanner,
  StatusPill,
  SubmissionErrors,
} from "@myss/web-common/components";

import Callout from "@/components/Callout";
import styles from "./DesignSystemTechDemo.module.css";

// The design system as the app draws it, on one page, for the designers to
// compare with Figma (MYSS-355). Every component here is the real one: the
// design system's React component under the site theme
// (public/vendor/myss-theme.css), or this app's own wrapper where one exists.
// Nothing here is a copy, so a change to a component or the theme shows up
// here as it does everywhere else.

const OPTIONS = [
  { id: "1", label: "Option 1" },
  { id: "2", label: "Option 2" },
  { id: "3", label: "Option 3" },
  { id: "4", label: "Option 4" },
];

const LOREM =
  "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam,";

const SECTIONS = [
  { id: "tokens", title: "Design tokens" },
  { id: "typography", title: "Typography" },
  { id: "buttons", title: "Buttons and links" },
  { id: "fields", title: "Form fields" },
  { id: "choices", title: "Radio buttons, checkboxes and dropdowns" },
  { id: "accordions", title: "Accordions" },
  { id: "alerts", title: "Alerts, notifications and status" },
  { id: "panels", title: "Cards and information panels" },
  { id: "navigation", title: "Navigation and page headers" },
  { id: "tables", title: "Tables and lists" },
  { id: "dialogs", title: "Modals and dialogs" },
  { id: "states", title: "Loading, empty, error and unavailable states" },
] as const;

type SectionId = (typeof SECTIONS)[number]["id"];

const COLOUR_TOKENS = [
  {
    group: "Text",
    tokens: [
      "--typography-color-primary",
      "--typography-color-secondary",
      "--typography-color-placeholder",
      "--typography-color-disabled",
      "--typography-color-link",
      "--typography-color-danger",
    ],
  },
  {
    group: "Surfaces",
    tokens: [
      "--surface-color-primary-button-default",
      "--surface-color-primary-button-hover",
      "--surface-color-primary-danger-button-default",
      "--surface-color-background-white",
      "--surface-color-background-light-gray",
      "--surface-color-background-light-blue",
      "--surface-color-background-dark-blue",
      "--surface-color-forms-disabled",
    ],
  },
  {
    group: "Borders",
    tokens: [
      "--surface-color-border-default",
      "--surface-color-border-medium",
      "--surface-color-border-dark",
      "--surface-color-border-active",
    ],
  },
  {
    group: "Support (alerts)",
    tokens: [
      "--support-surface-color-info",
      "--support-border-color-info",
      "--support-surface-color-success",
      "--support-border-color-success",
      "--support-surface-color-warning",
      "--support-border-color-warning",
      "--support-surface-color-danger",
      "--support-border-color-danger",
    ],
  },
];

const FONT_SIZE_TOKENS = [
  "--typography-font-size-display",
  "--typography-font-size-h1",
  "--typography-font-size-h2",
  "--typography-font-size-h3",
  "--typography-font-size-h4",
  "--typography-font-size-h5",
  "--typography-font-size-large-body",
  "--typography-font-size-body",
  "--typography-font-size-small-body",
  "--typography-font-size-label",
];

const SPACING_TOKENS = [
  "--layout-margin-hair",
  "--layout-margin-xsmall",
  "--layout-margin-small",
  "--layout-margin-medium",
  "--layout-margin-large",
  "--layout-margin-xlarge",
  "--layout-margin-xxlarge",
  "--layout-margin-xxxlarge",
  "--layout-margin-huge",
];

const RADIUS_TOKENS = [
  "--layout-border-radius-small",
  "--layout-border-radius-medium",
  "--layout-border-radius-large",
];

const BORDER_WIDTH_TOKENS = [
  "--layout-border-width-small",
  "--layout-border-width-medium",
  "--layout-border-width-large",
];

// The widths the app's own stylesheets switch layout at, in rem at the 16px
// root. The design tokens have none, and the app's stylesheets still write
// them as 576px, 767px and 991px.
const BREAKPOINTS = [
  { width: "36rem", use: "Phone: page padding narrows" },
  {
    width: "47.9375rem",
    use: "Phone layout: the menu moves to the header, text sizes step down",
  },
  { width: "61.9375rem", use: "Tablet: the form page column narrows" },
];

export default function DesignSystemTechDemo() {
  return (
    <div className={styles.page}>
      <nav aria-label="Breadcrumb">
        <RouterLink to="/techdemos">← Tech demos</RouterLink>
      </nav>
      <h1>MySS design system</h1>
      <p className={styles.lead}>
        Every component the citizen site is built with, drawn by the same code
        and styles the site uses. Compare it with Figma.
      </p>

      <nav aria-labelledby="contents-heading" className={styles.contents}>
        <h2 id="contents-heading" className={styles.contentsHeading}>
          On this page
        </h2>
        <ol>
          {SECTIONS.map((section) => (
            <li key={section.id}>
              <a href={`#${section.id}`}>{section.title}</a>
            </li>
          ))}
        </ol>
      </nav>

      <TokensSection />
      <TypographySection />
      <ButtonsSection />
      <FieldsSection />
      <ChoicesSection />
      <AccordionsSection />
      <AlertsSection />
      <PanelsSection />
      <NavigationSection />
      <TablesSection />
      <DialogsSection />
      <StatesSection />
    </div>
  );
}

function Section({
  id,
  children,
}: Readonly<{ id: SectionId; children: ReactNode }>) {
  const title = SECTIONS.find((section) => section.id === id)?.title;
  return (
    <section
      id={id}
      aria-labelledby={`${id}-heading`}
      className={styles.section}
    >
      <h2 id={`${id}-heading`}>{title}</h2>
      {children}
    </section>
  );
}

/** One example under a small heading naming it, as designers label frames. */
function Example({
  title,
  children,
}: Readonly<{ title: string; children: ReactNode }>) {
  return (
    <div className={styles.example}>
      <h3 className={styles.exampleHeading}>{title}</h3>
      {children}
    </div>
  );
}

function TokensSection() {
  return (
    <Section id="tokens">
      <p>
        From <code>@bcgov/design-tokens</code>. Code uses the name; the value
        shown is what the browser resolves.
      </p>
      {COLOUR_TOKENS.map(({ group, tokens }) => (
        <Example key={group} title={`Colour: ${group}`}>
          <ul className={styles.swatches}>
            {tokens.map((token) => (
              <li key={token} className={styles.swatch}>
                <span
                  className={styles.swatchColour}
                  style={{ background: `var(${token})` }}
                />
                <code>{token}</code>
                <TokenValue token={token} />
              </li>
            ))}
          </ul>
        </Example>
      ))}
      <Example title="Font sizes">
        <ul className={styles.tokenList}>
          {FONT_SIZE_TOKENS.map((token) => (
            <li key={token}>
              <span style={{ fontSize: `var(${token})` }}>Aa</span>{" "}
              <code>{token}</code> <TokenValue token={token} />
            </li>
          ))}
        </ul>
      </Example>
      <Example title="Spacing (margins; paddings share the scale)">
        <ul className={styles.tokenList}>
          {SPACING_TOKENS.map((token) => (
            <li key={token} className={styles.spacingRow}>
              <span
                className={styles.spacingBar}
                style={{ width: `var(${token})` }}
              />
              <code>{token}</code> <TokenValue token={token} />
            </li>
          ))}
        </ul>
      </Example>
      <Example title="Border radius and width">
        <ul className={styles.boxes}>
          {RADIUS_TOKENS.map((token) => (
            <li key={token}>
              <span
                className={styles.box}
                style={{ borderRadius: `var(${token})` }}
              />
              <code>{token}</code> <TokenValue token={token} />
            </li>
          ))}
          {BORDER_WIDTH_TOKENS.map((token) => (
            <li key={token}>
              <span
                className={styles.box}
                style={{ borderWidth: `var(${token})` }}
              />
              <code>{token}</code> <TokenValue token={token} />
            </li>
          ))}
        </ul>
      </Example>
      <Example title="Responsive breakpoints">
        <ul>
          {BREAKPOINTS.map((breakpoint) => (
            <li key={breakpoint.width}>
              <strong>{breakpoint.width}</strong> and narrower: {breakpoint.use}
            </li>
          ))}
        </ul>
      </Example>
    </Section>
  );
}

/** A token's value as the browser resolves it, so a designer can check it. */
function TokenValue({ token }: Readonly<{ token: string }>) {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(token)
    .trim();
  return <span className={styles.tokenValue}>{value}</span>;
}

function TypographySection() {
  const inlineLink = <a href="#typography">Inline link example</a>;
  return (
    <Section id="typography">
      <p>
        Headers 2 to 6 below are real headings, so they take the site&apos;s
        heading styles; a page has one H1, so the H1 sample is drawn with the H1
        type token. Set your browser narrower than 47.9375rem to see the phone
        sizes.
      </p>
      <div className={styles.specimen}>
        <p className={styles.specimenH1}>Page title H1</p>
        <p>
          {LOREM} {inlineLink}
        </p>
        <h2>Header 2</h2>
        <p>
          {LOREM} {inlineLink}
        </p>
        <h3>Header 3</h3>
        <p>
          {LOREM} {inlineLink}
        </p>
        <h4>Header 4</h4>
        <p>
          {LOREM} {inlineLink}
        </p>
        <h5>Header 5</h5>
        <p>
          {LOREM} {inlineLink}
        </p>
        <h6>Header 6</h6>
        <p>
          {LOREM} {inlineLink}
        </p>
        <p className={styles.largeText}>
          Large text example. {LOREM} quis nostrud exercitatio. {inlineLink}
        </p>
        <p className={styles.smallText}>
          Small text example. {LOREM} {inlineLink}
        </p>
      </div>
    </Section>
  );
}

function ButtonsSection() {
  return (
    <Section id="buttons">
      <Example title="Variants">
        <ButtonGroup>
          <Button variant="primary">Primary button</Button>
          <Button variant="secondary">Secondary button</Button>
          <Button variant="tertiary">Tertiary button</Button>
          <Button variant="link">Link button</Button>
        </ButtonGroup>
      </Example>
      <Example title="Danger">
        <ButtonGroup>
          <Button variant="primary" danger>
            Delete
          </Button>
          <Button variant="secondary" danger>
            Delete
          </Button>
        </ButtonGroup>
      </Example>
      <Example title="Disabled">
        <ButtonGroup>
          <Button variant="primary" isDisabled>
            Primary button
          </Button>
          <Button variant="secondary" isDisabled>
            Secondary button
          </Button>
          <Button variant="tertiary" isDisabled>
            Tertiary button
          </Button>
        </ButtonGroup>
      </Example>
      <Example title="Sizes">
        <ButtonGroup>
          <Button size="large">Large</Button>
          <Button size="medium">Medium</Button>
          <Button size="small">Small</Button>
          <Button size="xsmall">Extra small</Button>
        </ButtonGroup>
      </Example>
      <Example title="Links">
        <ul className={styles.stack}>
          <li>
            <Link href="#buttons">Design system link</Link>
          </li>
          <li>
            <Link href="#buttons" size="small">
              Small link
            </Link>
          </li>
          <li>
            <Link href="#buttons" danger>
              Danger link
            </Link>
          </li>
          <li>
            <Link href="#buttons" isButton buttonVariant="primary">
              Link drawn as a button
            </Link>
          </li>
          <li>
            Running text with an <a href="#buttons">inline link</a> in it.
          </li>
        </ul>
      </Example>
    </Section>
  );
}

function FieldsSection() {
  return (
    <Section id="fields">
      <p>
        Tab through the fields to see the focus style; hover for the hover
        border. Forms from Strapi draw these same components through Form.io.
      </p>
      <div className={styles.fields}>
        <Example title="Text input">
          <TextField label="Input label" defaultValue="Example text" />
        </Example>
        <Example title="Text input, required with help text">
          <TextField
            label="Input label"
            description="Help text that explains what to enter."
            isRequired
          />
        </Example>
        <Example title="Text input, error">
          <TextField
            label="Input label"
            defaultValue="Not valid"
            isInvalid
            errorMessage="Enter a valid value."
          />
        </Example>
        <Example title="Text input, disabled">
          <TextField
            label="Input label"
            defaultValue="Example text"
            isDisabled
          />
        </Example>
        <Example title="Text area">
          <TextArea
            label="Label"
            defaultValue="Autosize based on content lines"
          />
        </Example>
        <Example title="Text area, error">
          <TextArea
            label="Label"
            isInvalid
            errorMessage="Tell us a little more."
          />
        </Example>
        <Example title="Date input">
          <DateInputDemo />
        </Example>
        <Example title="Date input, error">
          <DateInputDemo
            initial={{ month: "2", day: "31", year: "2026" }}
            errors={[{ message: "Enter a real date.", parts: ["day"] }]}
          />
        </Example>
        <Example title="Calendar">
          <DatePicker label="Calendar" />
        </Example>
        <Example title="Error summary (after a refused submit)">
          <ErrorSummaryDemo />
        </Example>
      </div>
    </Section>
  );
}

function DateInputDemo({
  initial = { month: "", day: "", year: "" },
  errors = [],
}: Readonly<{ initial?: DateEntry; errors?: readonly DateInputError[] }>) {
  const [entry, setEntry] = useState<DateEntry>(initial);
  return (
    <DateInputField
      label="Date input"
      isRequired={false}
      isDisabled={false}
      name="demoDate"
      entry={entry}
      isoValue=""
      errors={errors}
      onChange={(part: DateEntryPart, text: string) => {
        setEntry((previous) => ({ ...previous, [part]: text }));
      }}
    />
  );
}

// Shown on request: the summary takes focus when it appears, as it does after
// a refused submit, which would pull the page there on load.
function ErrorSummaryDemo() {
  const [isShown, setIsShown] = useState(false);
  if (!isShown) {
    return (
      <Button variant="secondary" onPress={() => setIsShown(true)}>
        Show the error summary
      </Button>
    );
  }
  return (
    <SubmissionErrors
      error={
        new SubmissionRejectedError(422, [
          {
            field: "firstName",
            keyword: "FORM.FIELD.REQUIRED",
            message: "Enter your first name.",
          },
          {
            field: "email",
            keyword: "FORM.EMAIL.INVALID",
            message: "Enter an email address like name@example.com.",
          },
        ])
      }
    />
  );
}

function ChoicesSection() {
  return (
    <Section id="choices">
      <div className={styles.fields}>
        <Example title="Dropdown">
          <Select
            label="Dropdown label"
            placeholder="Select an option..."
            items={OPTIONS}
          />
        </Example>
        <Example title="Dropdown, help text and error">
          <Select
            label="Dropdown label"
            placeholder="Select an option..."
            description="Help text for the dropdown."
            items={OPTIONS}
            isInvalid
            errorMessage="Select an option."
          />
        </Example>
        <Example title="Dropdown, disabled">
          <Select
            label="Dropdown label"
            placeholder="Select an option..."
            items={OPTIONS}
            isDisabled
          />
        </Example>
        <Example title="Radio group">
          <RadioGroup label="Radio group label" isRequired>
            <Radio value="1">Radio label</Radio>
            <Radio value="2">Radio label</Radio>
            <Radio value="3">Radio label</Radio>
          </RadioGroup>
        </Example>
        <Example title="Radio group, error">
          <RadioGroup
            label="Radio group label"
            isInvalid
            errorMessage="Select an answer."
          >
            <Radio value="1">Radio label</Radio>
            <Radio value="2">Radio label</Radio>
          </RadioGroup>
        </Example>
        <Example title="Radio group, disabled with a selection">
          <RadioGroup label="Radio group label" defaultValue="1" isDisabled>
            <Radio value="1">Radio label</Radio>
            <Radio value="2">Radio label</Radio>
          </RadioGroup>
        </Example>
        <Example title="Checkbox group">
          <CheckboxGroup label="Label" isRequired>
            <Checkbox value="1">Label</Checkbox>
            <Checkbox value="2">Label</Checkbox>
            <Checkbox value="3">Label</Checkbox>
          </CheckboxGroup>
        </Example>
        <Example title="Checkbox group, error">
          <CheckboxGroup
            label="Label"
            isInvalid
            errorMessage="Select at least one."
          >
            <Checkbox value="1">Label</Checkbox>
            <Checkbox value="2">Label</Checkbox>
          </CheckboxGroup>
        </Example>
        <Example title="Checkbox, selected and disabled">
          <CheckboxGroup label="Label" defaultValue={["1"]} isDisabled>
            <Checkbox value="1">Label</Checkbox>
            <Checkbox value="2">Label</Checkbox>
          </CheckboxGroup>
        </Example>
      </div>
    </Section>
  );
}

function AccordionsSection() {
  return (
    <Section id="accordions">
      <div className={styles.fields}>
        <AccordionGroup>
          <Accordion id="closed" label="Accordion label">
            <p>A description of the panel text</p>
          </Accordion>
          <Accordion id="open" label="Accordion label" defaultExpanded>
            <p>A description of the panel text</p>
          </Accordion>
        </AccordionGroup>
      </div>
    </Section>
  );
}

const TAG_COLOURS = [
  "bc-blue",
  "bc-gold",
  "blue",
  "green",
  "yellow",
  "red",
  "gray",
  "dark",
] as const;

function AlertsSection() {
  return (
    <Section id="alerts">
      <Example title="Inline alerts">
        <div className={styles.stack}>
          <InlineAlert
            variant="info"
            title="Information"
            description="Something the citizen should know."
          />
          <InlineAlert
            variant="success"
            title="Success"
            description="Your request has been submitted."
          />
          <InlineAlert
            variant="warning"
            title="Warning"
            description="Check this before you continue."
          />
          <InlineAlert
            variant="danger"
            title="Error"
            description="Something went wrong. Try again."
          />
          <InlineAlert
            variant="info"
            title="Closeable, with buttons"
            description="A closeable alert can carry its own actions."
            isCloseable
            buttons={<Button size="small">Action</Button>}
          />
        </div>
      </Example>
      <Example title="Alert banners (full width, top of page)">
        <div className={styles.stack}>
          <AlertBanner variant="info">An information banner.</AlertBanner>
          <AlertBanner variant="success">A success banner.</AlertBanner>
          <AlertBanner variant="warning">A warning banner.</AlertBanner>
          <AlertBanner variant="danger">A danger banner.</AlertBanner>
        </div>
      </Example>
      <Example title="Announcement (site-wide, under the header)">
        <AnnouncementBanner>
          Need help? Call us at 1-866-866-0800.
        </AnnouncementBanner>
      </Example>
      <Example title="Application status">
        <ul className={styles.inline}>
          {[
            "DRAFT",
            "SUBMITTED",
            "UNDER_REVIEW",
            "RETURNED",
            "ACCEPTED",
            "DENIED",
            "WITHDRAWN",
          ].map((status) => (
            <li key={status}>
              <StatusPill status={status} />
            </li>
          ))}
        </ul>
      </Example>
      <Example title="Tags">
        <TagGroup aria-label="Tag colours">
          <TagList
            items={TAG_COLOURS.map((color) => ({
              id: color,
              textValue: color,
              color,
            }))}
          />
        </TagGroup>
      </Example>
    </Section>
  );
}

function PanelsSection() {
  return (
    <Section id="panels">
      <Example title="Callouts">
        <div className={styles.stack}>
          <Callout title="Light grey (default)" headingLevel={4}>
            <p>Sets content apart from the page.</p>
          </Callout>
          <Callout title="Light blue" headingLevel={4} variant="lightBlue">
            <p>Sets content apart from the page.</p>
          </Callout>
          <Callout title="Light gold" headingLevel={4} variant="lightGold">
            <p>Sets content apart from the page.</p>
          </Callout>
          <Callout
            title="Without the left bar"
            headingLevel={4}
            isLeftBorderDisabled
          >
            <p>Sets content apart from the page.</p>
          </Callout>
        </div>
      </Example>
    </Section>
  );
}

function NavigationSection() {
  return (
    <Section id="navigation">
      <p>
        The header, the menu on the left and the footer around this page are the
        site&apos;s own. On a phone the menu moves into the header. This page is
        in no menu, so no item is marked as current.
      </p>
    </Section>
  );
}

function TablesSection() {
  return (
    <Section id="tables">
      <Example title="Table">
        <div className={styles.tableScroll}>
          <table className={styles.table}>
            <caption>Service requests</caption>
            <thead>
              <tr>
                <th scope="col">Reference</th>
                <th scope="col">Request</th>
                <th scope="col">Status</th>
                <th scope="col">Updated</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td>IA-11111111</td>
                <td>Application for assistance</td>
                <td>
                  <StatusPill status="DRAFT" />
                </td>
                <td>2026-09-28</td>
              </tr>
              <tr>
                <td>IA-22222222</td>
                <td>Application for assistance</td>
                <td>
                  <StatusPill status="SUBMITTED" />
                </td>
                <td>2026-09-29</td>
              </tr>
            </tbody>
          </table>
        </div>
      </Example>
      <Example title="Lists">
        <div className={styles.lists}>
          <ul>
            <li>Unordered list item</li>
            <li>Unordered list item</li>
          </ul>
          <ol>
            <li>Ordered list item</li>
            <li>Ordered list item</li>
          </ol>
          <dl className={styles.summaryList}>
            <dt>Name</dt>
            <dd>Alex Example</dd>
            <dt>Email</dt>
            <dd>alex@example.com</dd>
          </dl>
        </div>
      </Example>
    </Section>
  );
}

const DIALOG_VARIANTS = [
  "info",
  "confirmation",
  "warning",
  "error",
  "destructive",
] as const;

function DialogsSection() {
  const [variant, setVariant] = useState<(typeof DIALOG_VARIANTS)[number]>();
  return (
    <Section id="dialogs">
      <p>Each button opens the dialog; Escape or Cancel closes it.</p>
      <ButtonGroup>
        {DIALOG_VARIANTS.map((name) => (
          <Button
            key={name}
            variant="secondary"
            onPress={() => setVariant(name)}
          >
            Open {name} dialog
          </Button>
        ))}
      </ButtonGroup>
      <Modal
        isOpen={variant !== undefined}
        onOpenChange={(open) => {
          if (!open) setVariant(undefined);
        }}
        isDismissable
      >
        <AlertDialog
          variant={variant}
          title="Dialog title"
          // The design system does not name the dialog by its title.
          aria-label="Dialog title"
          buttons={
            <>
              <Button
                danger={variant === "destructive"}
                onPress={() => setVariant(undefined)}
              >
                Confirm
              </Button>
              <Button
                variant="secondary"
                autoFocus
                onPress={() => setVariant(undefined)}
              >
                Cancel
              </Button>
            </>
          }
        >
          <p>The text of the dialog, which explains what confirming does.</p>
        </AlertDialog>
      </Modal>
    </Section>
  );
}

function StatesSection() {
  return (
    <Section id="states">
      <Example title="Loading: spinner">
        <ProgressCircle aria-label="Loading" isIndeterminate />
      </Example>
      <Example title="Loading: progress bar">
        <div className={styles.fields}>
          <ProgressBar aria-label="Uploading" value={60} />
        </div>
      </Example>
      <Example title="Loading: text, as pages show it today">
        <output>Loading…</output>
      </Example>
      <Example title="Empty">
        <p>You have no service requests.</p>
      </Example>
      <Example title="Error">
        <InlineAlert
          variant="danger"
          title="We could not load your service requests"
          description="Try again in a few minutes."
        />
      </Example>
      <Example title="Unavailable (a section still being built)">
        <div className={styles.frame}>
          <p className={styles.specimenH1}>Messages</p>
          <p>This feature is under development. Please check back later.</p>
        </div>
      </Example>
    </Section>
  );
}
