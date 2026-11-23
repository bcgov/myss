import { render } from "vitest-browser-react";
import { describe, expect, it } from "vitest";

import RegistrationReview, {
  type ReviewGroup,
} from "@/widgets/registration/RegistrationReview";

const yourInformation = [
  { key: "firstName", label: "First name", value: "Sam" },
  { key: "lastName", label: "Last name", value: "Rivera" },
];

const groups: ReviewGroup[] = [
  {
    heading: "You, the representative",
    rows: [
      {
        key: "name",
        label: "Your name (authorized representative)",
        value: "Pat Lee",
      },
      {
        key: "documents",
        label: "Power of attorney documents",
        value: ["poa.pdf (412 KB)", "page-2.pdf (1.8 MB)"],
      },
    ],
  },
  { heading: "The person you are representing", rows: yourInformation },
];

describe("RegistrationReview", () => {
  it("draws rows alone as today, with no group headings", async () => {
    const screen = await render(<RegistrationReview rows={yourInformation} />);

    expect(screen.container.querySelectorAll("tbody")).toHaveLength(1);
    expect(screen.container.querySelector('th[scope="rowgroup"]')).toBeNull();
    await expect
      .element(screen.getByRole("rowheader", { name: "First name" }))
      .toBeInTheDocument();
    await expect
      .element(screen.getByRole("row", { name: "First name Sam" }))
      .toBeInTheDocument();
  });

  it("draws a single group as rows alone", async () => {
    const screen = await render(
      <RegistrationReview
        groups={[{ heading: "Your information", rows: yourInformation }]}
      />,
    );

    expect(screen.container.querySelectorAll("tbody")).toHaveLength(1);
    expect(screen.container.querySelector('th[scope="rowgroup"]')).toBeNull();
  });

  it("leads each of several groups with its heading row", async () => {
    const screen = await render(<RegistrationReview groups={groups} />);

    const bodies = screen.container.querySelectorAll("tbody");
    expect(bodies).toHaveLength(2);
    const headings = Array.from(
      screen.container.querySelectorAll('th[scope="rowgroup"]'),
    );
    expect(headings.map((th) => th.textContent)).toEqual([
      "You, the representative",
      "The person you are representing",
    ]);
    expect(headings.every((th) => th.getAttribute("colspan") === "2")).toBe(
      true,
    );
    expect(bodies[0].querySelector("tr")?.textContent).toBe(
      "You, the representative",
    );
  });

  it("lists several values", async () => {
    const screen = await render(<RegistrationReview groups={groups} />);

    const list = screen.getByRole("list");
    await expect.element(list).toBeInTheDocument();
    expect(list.element().querySelectorAll("li")).toHaveLength(2);
    await expect
      .element(screen.getByText("page-2.pdf (1.8 MB)"))
      .toBeInTheDocument();
  });

  it("still gives each value its row heading", async () => {
    const screen = await render(<RegistrationReview groups={groups} />);

    for (const label of [
      "Your name (authorized representative)",
      "Power of attorney documents",
      "First name",
      "Last name",
    ]) {
      await expect
        .element(screen.getByRole("rowheader", { name: label }))
        .toBeInTheDocument();
    }
    await expect
      .element(
        screen.getByRole("row", {
          name: "Your name (authorized representative) Pat Lee",
        }),
      )
      .toBeInTheDocument();
  });
});
