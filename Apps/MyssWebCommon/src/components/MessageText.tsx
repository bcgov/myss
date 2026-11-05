import { Fragment, type ReactNode } from "react";

import styles from "./MessageText.module.css";
import { parseMessageText, type InlinePart, type MessageLine } from "./messageTextParser";

type Values = Readonly<Record<string, string>>;

// Every text an editor typed reaches the page as a text node, so nothing in a
// message can become markup. A placeholder with no value stays as typed.
function renderPart(part: InlinePart, values: Values): ReactNode {
  switch (part.kind) {
    case "bold":
      return (
        <strong key={part.at}>
          {part.parts.map((inner) => renderPart(inner, values))}
        </strong>
      );
    case "link": {
      // Email and phone links open the device's own app, not a tab.
      const newTab = /^https:/i.test(part.href);
      return (
        <a
          key={part.at}
          href={part.href}
          target={newTab ? "_blank" : undefined}
          rel={newTab ? "noopener noreferrer" : undefined}
        >
          {part.text}
          {newTab && (
            <span className={styles.visuallyHidden}> (opens in a new tab)</span>
          )}
        </a>
      );
    }
    case "placeholder":
      return (
        <Fragment key={part.at}>
          {Object.hasOwn(values, part.name) ? values[part.name] : `{${part.name}}`}
        </Fragment>
      );
    default:
      return <Fragment key={part.at}>{part.text}</Fragment>;
  }
}

function renderLine(line: MessageLine, values: Values): ReactNode {
  return line.parts.map((part) => renderPart(part, values));
}

/**
 * A form message in its formatting set: paragraphs, line breaks,
 * bold, bulleted lists and links (https links in a new tab), with each
 * placeholder replaced by its value from `values`. Spacing and colours come
 * from the page that shows it.
 */
export default function MessageText({
  text,
  values = {},
}: Readonly<{ text: string; values?: Values }>) {
  return (
    <>
      {parseMessageText(text).map((block) =>
        block.kind === "list" ? (
          <ul key={block.at}>
            {block.items.map((item) => (
              <li key={item.at}>{renderLine(item, values)}</li>
            ))}
          </ul>
        ) : (
          <p key={block.at}>
            {block.lines.map((line, position) => (
              <Fragment key={line.at}>
                {position > 0 && <br />}
                {renderLine(line, values)}
              </Fragment>
            ))}
          </p>
        ),
      )}
    </>
  );
}
