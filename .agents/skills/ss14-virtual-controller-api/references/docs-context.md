# Docs Context (VirtualController API)

## The role of docs for the API

1. Docs are needed for concepts and general rules, but do not replace code.
2. Record API facts based on the current implementation of controllers and physics system.
3. If there is a conflict between docs and runtime code, choose runtime code.

## Relevant pages

1. `robust-toolbox/transform/physics.md` — date `2023-09-21`.
What to take: the general idea of the physics pipeline and the place of VirtualControllers.
Limitation: Outdated page, do not use as a source for the exact API contract.

2. `ss14-by-example/prediction-guide.md` — date `2026-02-04`.
What to take: prediction practices, expectations for repeated runs, typical mispredict traps.
Limitation: examples from the guide still need to be checked against current movement systems.

3. `ss14-by-example/basic-networking-and-you.md` — date `2024-05-31`.
What to take: dirty/component state rules for shared/predicted API.
Limitation: This is a basic networking layer, not a complete reference to movement controllers.

4. `general-development/codebase-info/conventions.md` — date `2026-01-19`.
What to take: rules for reusable API, dirty/field-delta, shared naming conventions.
Restriction: Use as style-guideline, not runtime behavior.

## Application practice

1. First select the method by `fresh-pattern-catalog.md`.
2. Then check the docs only to clarify the intent.
3. If the docs suggest an old pattern, move it to `rejected-snippets.md`, not to the working rules.
