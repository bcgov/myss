# Accessibility

> **Status:** commitment — WCAG 2.1 AA is a Definition-of-Done item for
> every UI story. Practices current as of 2026-09-09; automated axe-style
> checks are not yet wired into CI (manual passes carry that weight
> today).

WCAG 2.1 AA is a hard commitment for MySS, not a compliance checkbox. A
social-assistance portal serves a population with a higher-than-average
incidence of disability and assistive-technology use — for many users,
accessibility is the difference between getting assistance and not.
Accessibility is a Definition-of-Done item for every story that touches
UI: **automated checks plus the relevant manual WCAG 2.1 AA checks**.
Automated tools (axe-style) catch perhaps a third of issues; the manual
pass catches the rest.

A regression in an accessible behaviour is a **defect, not a tweak**.

## The patterns, and the failures they prevent

| Pattern                   | What to do                                                                                                                                          | The failure it prevents                                     |
| ------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------- |
| **Focus management**      | Move focus deliberately on route/step change and into opened dialogs; never trap it except in a modal, and release on close.                        | Keyboard and screen-reader users lost after a transition.   |
| **Error summaries**       | On a failed submit, render a summary at the top linking to each errored field, and associate each message with its field.                           | Errors a screen-reader user cannot find or associate.       |
| **Keyboard flows**        | Every interaction works without a mouse, in a logical tab order — wizards, review panes, and lists included.                                        | Users unable to complete at all; worker efficiency tanking. |
| **Dynamic forms**         | Announce conditional sections appearing/disappearing; label every input; group related fields with fieldset/legend.                                 | The Form.io-rendered forms becoming unusable non-visually.  |
| **Contrast & sizing**     | Meet AA contrast for text and UI components; support reflow and 200% zoom; never convey meaning by colour alone (status pills carry text + colour). | Low-vision and colour-blind users excluded.                 |
| **Accessible challenges** | Any bot-protection is an accessible challenge, never an image CAPTCHA.                                                                              | An applicant locked out at the very first step.             |

## The footgun: breaking what BCDS gives for free

`@bcgov/design-system-react-components` is accessible by default; that
property is easy to destroy downstream:

- A wrapper that drops an ARIA attribute or swallows keyboard handling.
- A `div` with `onClick` where a `button` belongs.
- A colour override that fails contrast, or a focus ring removed
  "because it looks busy" — focus styles come from design tokens and
  stay.
- A custom Form.io component that renders a CSS look-alike instead of
  the real shared component (see the two-render-worlds contract in
  [designprinciples.md](designprinciples.md)).

Using accessible primitives correctly is still our job — the library
only makes it _possible_ to be accessible cheaply.

## How this shows up in code and tests

- **Drive tests by accessible role, never DOM name.** Form.io
  randomizes input names per render; the browser suites already select
  radios and buttons by role and label. Code that only works when
  addressed by DOM internals is code a screen reader cannot address
  either.
- **Test the keyboard path** when touching any interactive component:
  tab order, Enter/Space activation, Escape out of overlays.
- **Sensitive flows deserve extra care in their messaging**: sign-in
  back-off after repeated failures, the file-rejected (virus-scan)
  message, and any challenge or break-glass flow — these are moments
  where a user is already stressed, and the message must be perceivable
  and understandable for everyone.

## Process

Bake the manual WCAG 2.1 AA checklist into design review and the sprint
demo, not just the test phase — the cheapest accessibility fix is the
one made in the design, the most expensive is the one found in a
pre-launch audit. Demoing a keyboard-only and screen-reader pass of a
new journey at sprint review keeps the practice visible.
