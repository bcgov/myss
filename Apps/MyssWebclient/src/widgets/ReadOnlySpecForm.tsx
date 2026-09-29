import { Form } from "@formio/react";
import type {
  FormType,
  JSON as FormioJson,
} from "@formio/react/lib/components/Form";
import { useMemo } from "react";

import "@formio/js/dist/formio.form.min.css";
import "@/widgets/FormSpecWidget.css";

// A Form.io spec rendered read-only over a set of answers: how a submitted
// application is shown, to the applicant and to a reviewing worker alike. The
// spec's buttons are stripped rather than disabled, because a submitted form
// must expose no submit control at all, and Form.io's read-only mode only
// disables one.

/**
 * The spec without its buttons, recursively, so a button nested in a panel or
 * a column goes too.
 */
function withoutButtons(spec: FormType): FormType {
  type Node = { type?: string; components?: Node[] };
  const strip = (components: Node[]): Node[] =>
    components
      .filter((component) => component.type !== "button")
      .map((component) =>
        Array.isArray(component.components)
          ? { ...component, components: strip(component.components) }
          : component,
      );
  return {
    ...spec,
    components: strip(spec.components as Node[]) as FormType["components"],
  };
}

const READ_ONLY_OPTIONS = { readOnly: true, noAlerts: true };

export default function ReadOnlySpecForm({
  spec,
  answers,
}: {
  spec: FormType;
  answers: { [key: string]: FormioJson };
}) {
  const readOnlySpec = useMemo(() => withoutButtons(spec), [spec]);
  const submission = useMemo(() => ({ data: answers }), [answers]);

  return (
    <Form
      src={readOnlySpec}
      submission={submission}
      options={READ_ONLY_OPTIONS}
    />
  );
}
