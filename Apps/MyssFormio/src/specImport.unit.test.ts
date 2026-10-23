import { describe, expect, it } from "vitest";

import {
  checkSpec,
  EXAMPLE_SPEC,
  formatSpecJson,
  locateJsonError,
  locateKey,
  parseSpecJson,
} from "./specImport";

const submit = { type: "button", key: "submit", action: "submit", input: true };

function spec(...components: object[]) {
  return { display: "form", components: [...components, submit] };
}

function textfield(key: string, extra: object = {}) {
  return { type: "textfield", key, label: key, input: true, ...extra };
}

/** The problem messages for a spec with one extra component, without a current version. */
function problemsFor(component: object, current?: unknown): string[] {
  return checkSpec(spec(component), current).problems.map(
    (problem) => problem.message,
  );
}

describe("EXAMPLE_SPEC", () => {
  it("has no problems, no warnings and three questions", () => {
    const result = checkSpec(EXAMPLE_SPEC);

    expect(result.problems).toEqual([]);
    expect(result.warnings).toEqual([]);
    expect(result.removedQuestions).toEqual([]);
    expect(result.questionCount).toBe(3);
  });
});

describe("parseSpecJson", () => {
  it("reports empty text as empty", () => {
    expect(parseSpecJson("  \n")).toEqual({ state: "empty" });
  });

  it("parses valid JSON", () => {
    expect(parseSpecJson('{ "components": [] }')).toEqual({
      state: "parsed",
      value: { components: [] },
    });
  });

  it("reports invalid JSON with a line and column when the engine gives one", () => {
    // A missing comma: V8 reports where it is.
    const result = parseSpecJson('{\n  "a": 1\n  "b": 2\n}');

    expect(result).toEqual({
      state: "invalid",
      problem: {
        message: "The JSON is not valid at line 3, column 3.",
        line: 3,
        column: 3,
      },
    });
  });

  it("reports invalid JSON without a position when the engine gives none", () => {
    // An unexpected token: V8's message quotes the text but gives no position.
    const result = parseSpecJson('{\n  "components": [\n  }\n}');

    expect(result).toEqual({
      state: "invalid",
      problem: { message: "The JSON is not valid." },
    });
  });
});

describe("locateJsonError", () => {
  const text = '{\n  "a": 1,\n  oops\n}';

  it("reads the line and column Chromium gives", () => {
    expect(
      locateJsonError(
        "Expected ',' or '}' after property value in JSON at position 13 (line 3 column 3)",
        text,
      ),
    ).toEqual({ line: 3, column: 3 });
  });

  it("reads the line and column Firefox gives", () => {
    expect(
      locateJsonError(
        "JSON.parse: unexpected character at line 3 column 3 of the JSON data",
        text,
      ),
    ).toEqual({ line: 3, column: 3 });
  });

  it("counts a position out in the text", () => {
    expect(
      locateJsonError("Unexpected token o in JSON at position 14", text),
    ).toEqual({
      line: 3,
      column: 3,
    });
  });

  it("ignores a location quoted from the text itself", () => {
    expect(
      locateJsonError(
        "Unexpected token x, 'at line 2 column 5 at position 7' is not valid JSON",
        text,
      ),
    ).toBeUndefined();
  });

  it("gives nothing for a message without a location", () => {
    expect(
      locateJsonError("Unexpected end of JSON input", text),
    ).toBeUndefined();
  });
});

describe("checkSpec: shape", () => {
  it.each([
    [[], 'The JSON must be an object with a "components" list.'],
    [{ display: "form" }, 'The JSON has no "components" list.'],
    [{ components: [] }, 'The "components" list is empty.'],
    [
      { components: [submit], controller: "alert(1)" },
      'The JSON has a top-level "controller", which is not allowed. Only "display" and "components" are.',
    ],
    [
      { components: [submit], module: "x" },
      'The JSON has a top-level "module", which is not allowed. Only "display" and "components" are.',
    ],
    [
      {
        components: [submit],
        settings: { pdf: { src: "https://example.com" } },
      },
      'The JSON has a top-level "settings", which is not allowed. Only "display" and "components" are.',
    ],
    [
      { display: "pdf", components: [submit] },
      'The display "pdf" is not allowed. Use "form".',
    ],
  ])("refuses %j", (value, message) => {
    expect(checkSpec(value).problems).toEqual([{ message }]);
  });
});

describe("checkSpec: component types", () => {
  it("refuses a type outside the palette, naming the key", () => {
    const [problem] = checkSpec(
      spec({ type: "datagrid", key: "children", input: true }),
    ).problems;

    expect(problem).toEqual({
      key: "children",
      message: '"children": the type "datagrid" is not allowed.',
    });
  });

  it("suggests radio for bcgovRadio in a new form", () => {
    expect(problemsFor({ type: "bcgovRadio", key: "q", input: true })).toEqual([
      '"q": use "radio" instead of "bcgovRadio". It looks the same to citizens.',
    ]);
  });

  it("refuses phoneNumber and bcgovAddressAutocomplete in a new form", () => {
    expect(
      problemsFor({ type: "phoneNumber", key: "phone", input: true }),
    ).toHaveLength(1);
    expect(
      problemsFor({
        type: "bcgovAddressAutocomplete",
        key: "address",
        input: true,
      }),
    ).toHaveLength(1);
  });

  it("allows the types the current version already uses, and no others", () => {
    const current = spec(
      { type: "bcgovRadio", key: "q", input: true },
      { type: "phoneNumber", key: "phone", input: true },
      { type: "bcgovAddressAutocomplete", key: "address", input: true },
    );

    expect(checkSpec(current, current).problems).toEqual([]);
    expect(
      problemsFor({ type: "datagrid", key: "children", input: true }, current),
    ).toEqual(['"children": the type "datagrid" is not allowed.']);
  });

  it("refuses a component without a type", () => {
    expect(problemsFor({ key: "untyped", input: true })).toEqual([
      '"untyped": it has no "type".',
    ]);
  });
});

describe("checkSpec: script properties", () => {
  it.each(["calculateValue", "customConditional", "customDefaultValue"])(
    "refuses %s",
    (property) => {
      expect(problemsFor(textfield("q", { [property]: "value = 1;" }))).toEqual(
        [`"q": "${property}" runs code and is not allowed.`],
      );
    },
  );

  it("refuses validate.custom", () => {
    expect(
      problemsFor(textfield("q", { validate: { custom: "valid = true;" } })),
    ).toEqual(['"q": "validate.custom" runs code and is not allowed.']);
  });

  it("ignores script properties that are empty", () => {
    expect(
      problemsFor(
        textfield("q", { calculateValue: "", validate: { custom: "" } }),
      ),
    ).toEqual([]);
  });
});

describe("checkSpec: logic", () => {
  // The bus pass form's Email logic: required when email is the preferred contact.
  const dataOnly = [
    {
      name: "Required when email is the preferred method of communication",
      trigger: {
        type: "simple",
        simple: { show: true, when: "contact", eq: "email" },
      },
      actions: [
        {
          name: "Require",
          type: "property",
          property: {
            label: "Required",
            value: "validate.required",
            type: "boolean",
          },
          state: true,
        },
      ],
    },
  ];

  it("allows data-only logic", () => {
    expect(
      checkSpec(
        spec(textfield("contact"), textfield("email", { logic: dataOnly })),
      ).problems,
    ).toEqual([]);
  });

  it("refuses a javascript trigger", () => {
    const logic = [
      {
        trigger: { type: "javascript", javascript: "result = true;" },
        actions: [],
      },
    ];

    expect(problemsFor(textfield("q", { logic }))).toEqual([
      '"q": logic with the trigger "javascript" is not allowed. Only "simple" and "json" triggers are.',
    ]);
  });

  it.each(["value", "customAction", "mergeComponentSchema"])(
    "refuses a %s action",
    (type) => {
      const logic = [
        { trigger: { type: "simple", simple: {} }, actions: [{ type }] },
      ];

      expect(problemsFor(textfield("q", { logic }))).toEqual([
        `"q": the logic action "${type}" is not allowed. Only "property" actions are.`,
      ]);
    },
  );

  it("refuses a property action that sets a script property", () => {
    const logic = [
      {
        trigger: { type: "json", json: { "==": [1, 1] } },
        actions: [
          {
            type: "property",
            property: { value: "validate.custom" },
            state: "valid = true;",
          },
        ],
      },
    ];

    expect(problemsFor(textfield("q", { logic }))).toEqual([
      '"q": a logic action sets "validate.custom", which runs code.',
    ]);
  });
});

describe("checkSpec: data sources and buttons", () => {
  it.each(["custom", "url"])("refuses a select with dataSrc %s", (dataSrc) => {
    expect(
      problemsFor({ type: "select", key: "topic", input: true, dataSrc }),
    ).toEqual([
      `"topic": the list source "${dataSrc}" is not allowed. Use fixed values.`,
    ]);
  });

  it("refuses a list source on any component, such as a radio", () => {
    expect(
      problemsFor({
        type: "radio",
        key: "choice",
        input: true,
        dataSrc: "url",
        data: { url: "https://example.com/options" },
      }),
    ).toEqual([
      '"choice": the list source "url" is not allowed. Use fixed values.',
    ]);
  });

  it("allows a select with fixed values", () => {
    expect(
      problemsFor({
        type: "select",
        key: "topic",
        input: true,
        data: { values: [] },
      }),
    ).toEqual([]);
    expect(
      problemsFor({
        type: "select",
        key: "topic",
        input: true,
        dataSrc: "values",
      }),
    ).toEqual([]);
  });

  it.each(["custom", "url"])("refuses a button with action %s", (action) => {
    expect(
      problemsFor({ type: "button", key: "other", action, input: true }),
    ).toEqual([`"other": the button action "${action}" is not allowed.`]);
  });
});

describe("checkSpec: interpolation, multiple values and keys", () => {
  it("finds {{ in a nested content component", () => {
    const panel = {
      type: "panel",
      key: "outer",
      components: [
        { type: "content", key: "note", html: "<p>Hello {{ data.name }}</p>" },
      ],
    };

    expect(problemsFor(panel)).toEqual([
      '"note": text containing "{{" or "{%" runs code and is not allowed.',
    ]);
  });

  it("finds {% in content HTML", () => {
    expect(
      problemsFor({
        type: "content",
        key: "note",
        html: "<p>{% print(1) %}</p>",
      }),
    ).toEqual([
      '"note": text containing "{{" or "{%" runs code and is not allowed.',
    ]);
  });

  it("refuses multiple values", () => {
    expect(problemsFor(textfield("q", { multiple: true }))).toEqual([
      '"q": "multiple" makes the answer a list, which the form cannot accept.',
    ]);
  });

  it("refuses a duplicate key", () => {
    const result = checkSpec(spec(textfield("dupe"), textfield("dupe")));

    expect(result.problems).toEqual([
      {
        key: "dupe",
        message:
          '"dupe" is used as the key of more than one field. Each key must be unique.',
      },
    ]);
  });

  it("refuses a condition on a key that is not in the form, naming the conditional field", () => {
    const result = checkSpec(
      spec(
        textfield("q", {
          conditional: { show: true, when: "missing", eq: "x" },
        }),
      ),
    );

    expect(result.problems).toEqual([
      {
        key: "q",
        message:
          '"q": its condition refers to "missing", which is not in the form.',
      },
    ]);
  });

  it("allows a JSON-logic condition", () => {
    const conditional = { json: { "===": [{ var: "data.a" }, "yes"] } };

    expect(
      checkSpec(spec(textfield("a"), textfield("b", { conditional }))).problems,
    ).toEqual([]);
  });
});

describe("checkSpec: warnings, counts and removed questions", () => {
  it("warns, without blocking, when there is no Submit button", () => {
    const result = checkSpec({ components: [textfield("q")] });

    expect(result.problems).toEqual([]);
    expect(result.warnings).toEqual([
      "There is no Submit button, so citizens could not send this form.",
    ]);
  });

  it("counts questions, not buttons or content", () => {
    const result = checkSpec(
      spec(textfield("a"), {
        type: "content",
        key: "intro",
        html: "<p>Hi</p>",
      }),
    );

    expect(result.questionCount).toBe(1);
  });

  it("counts a component of an answer type as a question even without input", () => {
    // The bus pass form's radios and checkboxes leave `input` out; Form.io fills it in.
    const current = spec(
      { type: "radio", key: "category", values: [] },
      { type: "checkbox", key: "acknowledged" },
      { type: "textfield", key: "notAQuestion", input: false },
    );

    expect(checkSpec(current).questionCount).toBe(2);
    expect(
      checkSpec(spec(textfield("other")), current).removedQuestions,
    ).toEqual(["category", "acknowledged"]);
  });

  it("lists questions of the current version that the pasted spec lacks", () => {
    const current = spec(textfield("a"), textfield("b"), {
      type: "content",
      key: "intro",
      html: "",
    });
    const pasted = spec(textfield("a"), textfield("c"));

    expect(checkSpec(pasted, current).removedQuestions).toEqual(["b"]);
  });

  it("lists no removed questions without a current version", () => {
    expect(checkSpec(spec(textfield("a"))).removedQuestions).toEqual([]);
  });
});

describe("locateKey and formatSpecJson", () => {
  it("finds a component's key on its line", () => {
    const text = formatSpecJson(
      JSON.stringify(spec(textfield("first"), textfield("second"))),
    )!;
    const position = locateKey(text, "second");

    expect(position).toBeDefined();
    expect(text.split("\n")[position!.line - 1]).toContain('"key": "second"');
  });

  it("gives nothing for a key that is not there", () => {
    expect(locateKey('{ "key": "a" }', "b")).toBeUndefined();
  });

  it("re-indents single-line JSON, and gives null for invalid JSON", () => {
    expect(formatSpecJson('{"a":[1]}')).toBe('{\n  "a": [\n    1\n  ]\n}');
    expect(formatSpecJson("{")).toBeNull();
  });
});
