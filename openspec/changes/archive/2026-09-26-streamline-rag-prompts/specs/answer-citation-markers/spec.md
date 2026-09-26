## MODIFIED Requirements

### Requirement: Model-emitted citation markers

The book-RAG answer prompt SHALL instruct the model, exactly once and in the final user message
after the numbered excerpts, to support factual clinical claims by appending the supporting excerpt
number(s) in square brackets (e.g. `[1]` or `[2][4]`), while preserving the existing continuous-prose
style (no lists, no markdown, answering in the question's language). The instruction SHALL tell the
model to cite only excerpts that support the specific claim and never to invent a number not present
in the excerpts. Conversation history sent with the prompt SHALL NOT contain `[n]` markers from
prior answers.

#### Scenario: Cited claim

- **WHEN** the model states a claim supported by excerpt 2
- **THEN** it appends `[2]` after that claim, within flowing prose

#### Scenario: Prose style preserved

- **WHEN** an answer is generated with citation markers enabled
- **THEN** it is still continuous prose with no bullet lists, numbered lists, headings, or bold /
  italic markdown

#### Scenario: Language preserved

- **WHEN** the question is in Bulgarian
- **THEN** the answer is in Bulgarian with the same bracketed `[n]` markers

#### Scenario: No stale markers in history

- **WHEN** a follow-up question is asked after an answer that contained `[1]` and `[3]`
- **THEN** the history turn sent to the model contains that answer without the `[1]` and `[3]`
  markers
