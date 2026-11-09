# Evaluation baseline findings

**Type:** project (state of quality, 2026-11-04)

Pinned baseline: run `a7c83bb3` (qwen3:8b, 8,192 context, temperature 0, 34 cases). It scored 31/34 passed, fact
recall 80%, unsafe claims 0, abstention 10/10, faithfulness 97%, Hit@5 100%, fact coverage 75%, and mean latency
13.8 s. A full run takes about 18 minutes. Failing: G6, E3, E6.

## Findings

- **Abstention gate never fires.** Reranker scores cannot separate invented diseases from real ones: N1 scored
  9.03, second highest of 20, and N2 7.92, above three real questions, so no `MinAnswerScore` value works. The
  reranker is English ms-marco on Bulgarian text. N1's refusal in the baseline came from the LLM, not the gate.
- **English query translation is weak.** qwen3:8b produced "отитис медиалин", "парациетамолен отров", and turned
  bronchiolitis into "Пневмония". The search query was identical in both runs.
- **Retrieval was not repeatable (fixed and verified).** RRF scores depend only on ranks, so ties are routine,
  and Qdrant returns tied points in a varying order; cutting at `topK` 5 let ties pick the chunks.
  `QdrantVectorStore.TakeTopFused` fetches every fused point and cuts by score, then point id. Two runs after the
  fix matched on all 20 queries, excerpts, rankings, answers and verdicts, so any change between runs now comes
  from the change under test. The tie-break is arbitrary, not a quality gain.
- **Hit@5 is lenient.** It reads 100% while fact coverage is 82%, because relevance matches topic only.
- **Books use Latin eponyms** ("болест на Kawasaki"), so relevance terms and search need both scripts.
- **Citation page numbers were 0–0** on every excerpt inspected.

## Refusal guard and name search (deployed 2026-11-04)

`ConditionNameGuard` refuses when a condition named in the question (`синдром на X`, `X disease`) is absent from
the excerpts, with outcome `ConditionNotInExcerpts`. `FindBookSpellings` adds the books' Latin spelling to the
search query from the BM25 vocabulary, and `QueryPreparation.Shorten` stops keyword-list search queries.

- **Why it matches by sound:** questions use Cyrillic but books mostly use Latin (`Wilson` 53 chunks, `Уилсън` 3).
  Latin names get English, German and French readings, and `ъ` matches any vowel.
- **Two strictness levels, on purpose:** the guard is loose so real diseases pass. The spelling lookup is near-exact,
  because a loose lookup turns a fake name into a real word, retrieves that word and then satisfies the guard.
  Offline it resolved 37 of 44 real names and hit 2 of 30 unseen fakes, both true transliterations.
- **qwen3:8b cannot do this.** Asked for Latin spellings it transliterates (`Gudpaschur`, `Goshe`). A rewritten
  translation prompt was not clearly better either, so the prompt stayed.
- **Deliberate limits:** any name part passes, first-word English names are unchecked, and invented drugs are out of
  scope. N9 and N10 refuse only because the model does.
- **Open:** the `illnesses` and `illness_aliases` tables are empty, so dictionary query expansion does nothing.
  R3 passes with both reference facts absent from the excerpts, so the model answered from its own knowledge.
  Translation errors remain (E3 `отитис медиалин`, E6 `Манажмънт`), and refusals are always in English.

## qwen3:14b comparison (run b3dcde7b, 2026-11-04, not pinned)

Judged by `qwen3:8b`, same settings as baseline `a7c83bb3`. Decision pending, recommendation is to stay on 8B.

- **Headline numbers mislead.** 24 of 33 scored, but 5 questions hit the 5-minute Ollama `HttpClient` timeout
  (G1, G4, G9, G11, R4) because 14B does not fit in 8 GB of VRAM. Compare per question, not by summary.
- **Like for like, 18 answer questions:** passes 15 vs 14, facts from excerpts 29 vs 33 of 48, lost in retrieval
  8 vs 5, lost in generation 3 vs 4, mean latency about 15 s vs 153 s.
- **The gain comes from query translation, not answer writing.** 14B fixed E3 and E6 and sharpened E4 and E5, but
  broke E2 (`вдъхновителен стихо`) and left the G12 follow-up unrewritten, so it gave a non-answer.
- **The judge gave a false pass in the pinned baseline.** On R3 the excerpts only list Wiskott-Aldrich by name, yet
  8B's answer from memory scored 7 of 7 supported. A similar 14B answer scored 0 of 8. Treat faithfulness as
  an estimate, and read baseline R3 as a failure.
- **G12 row bug:** an unjudged case keeps `Passed: true`, although the summary counts it as inconclusive.

## Book term lookup (measured in run 777066ad, 2026-11-04, not pinned)

`BookTermLookup` adds the books' Bulgarian term for each medical word in an English question, because the model
invents words (`отитис медиалин`) and swaps concepts (bronchiolitis became `Пневмония`). English words are rewritten by
shared Greek and Latin rules and prefix-matched against the BM25 vocabulary. It never runs on Bulgarian questions.

- **Offline:** 60 of 67 held-out terms (the 67 were not used for tuning, 9 scored misses were right matches), and none
  of 8 invented drug names matched. The expected stems for those lists were written by Claude, not a physician.
- **Limit:** words with no shared root (`poisoning`, `barking`) are unreachable. A glossary would need physician review.

Run `777066ad` against `a7c83bb3`: 33 vs 31 of 34, Hit@1 92% vs 83%, nDCG 91% vs 83%, fact coverage 84% vs 75%,
lost in retrieval 5 vs 8, lost in generation 6 vs 5. E3 and E6 now pass, and all Bulgarian answers are unchanged.
The run took 53 minutes because the DnD book extractor shared Ollama, so its latency is not comparable.

**Passing does not mean correct.** Reading E3 and E6 against the excerpts found statements the judge marked supported:

- **E3:** amoxicillin/clavulanate as first line for otitis media cites excerpts 1 and 5, which never mention it.
  The list comes from excerpt 2, a passage on an infection of the ear skin. Excerpt 3 only calls amoxicillin widely
  used.
- **E6:** "within 15 hours" for NAC. The book says effective up to hour 8, and unreliable after hour 15.
- **E6:** "even therapeutic doses" of paracetamol causing liver damage. That sentence is about phenacetin hemolysis.

All E6 doses matched the book. Trust retrieval metrics, but not faithfulness, until the judge is fixed.

Related: `mem:project/evaluation-harness`.

## Two-step claim judge (`quote-then-check-v1`)

The old one-call claim judge kept 15 of 15 true statements but caught only 6 of 16 false ones, all 6 being
altered numbers. Faithfulness scores from before this judge are therefore inflated (97-99%).

The new judge copies evidence from the excerpts, then checks each fact against that evidence alone. Code confirms
that the quotes exist, compares numbers, and decides citations from where the quote really is. Measured with
the shipped C# code on `qwen3:8b`: 13 of 15 true kept, 15 of 16 false caught, about 14 s per statement with the
GPU shared, or about an hour for the 256 sentences in a run.

Limits of that measurement:

- Labels are mine, not a physician's, and the prompt was tuned on this same set of 31 statements.
- Remaining miss: R3.4. Wrong rejections: E6.3 and E2.3 (evidence search stopped short).
- Bulgarian: 14 of 20 passed. G6.3, G6.6 and G6.9 look wrongly rejected (OCR damage, cut-off quotes).
- Expect faithfulness to under-read somewhat, so treat it as a strict lower bound.
- Required facts and forbidden claims still use the old batch `CheckStatementsAsync`, which was not re-measured.

Design lessons: an example reply with an empty list gets copied, so show filled examples. A translation step
cost time and did not help. Alternating prompts loses Ollama's cached excerpts, so run all searches first.

### First full run with the two-step judge

Run `c266155d` (not pinned): all 34 answers identical to `777066ad`, so every change comes from the judge.
Faithfulness 69% (was 99%), passed 22 of 34 (was 33), 61 minutes, 2 unjudged (G1, G8).

- I read 28 of the 72 rejections, chosen because they looked suspicious: roughly a third were wrong. The usual
  cause is evidence that stops short of the sentence, or synonyms it cannot link (gamma globulin and IVIG).
- A rough corrected figure is about 80%, not a measurement. Treat 69% as a lower bound.
- Only 5 supported sentences had a wrong citation; the 68% citation accuracy mostly repeats the rejections.
- `MinFaithfulness` 0.7 was set under the inflated judge and now fails 9 questions on this alone.
- One failed call loses a whole question's judging, and transport errors are not retried.
- Lost-in-retrieval and lost-in-generation moved by 1 on identical answers, which is the old fact check's noise.

### First full run with the re-check judge

Run `8a314a1e` (`quote-check-recheck-v2`, not pinned): all 34 answers identical to `c266155d` and `777066ad`.
Faithfulness 77%, citation accuracy 76%, passed 24 of 34, 4 sentences unjudged, about 78 minutes.

- The re-check passed 17 sentences and changed no other verdict. Reading all 17: 13 fair, 2 weak (G10.2, G10.3),
  2 wrong (R3.1, G6.1). These are my readings, not a physician's.
- R3.1 and G6.1 were rejected in the probe. The check step answered yes on bundled facts the evidence does not
  state, so this is an 8B model limit, not a code bug. G10.2 changed verdict on replay: borderline calls wobble.
- G8's broken streams repeat because of JSON mode: on replay the model fell into a run of newlines and the stream
  ended without `done` (2 of 3 sentences). HTTP status stayed 200, and Ollama logged nothing.
- The same prompts without JSON mode finished 3 of 3, so an identical retry cannot help at temperature 0.
  Tried in `quote-check-recheck-v3`: the retry drops JSON mode and a stray closing brace is tolerated.
- Run `e53ce786` (v3) gave the same verdict on every sentence as `8a314a1e`, so the in-app judge is repeatable.
  My replay script's verdicts differed from the app's, not run to run. G8 still had 3 unjudged sentences.
- Real cause: the excerpts hold ASCII double quotes ("ръка, крак и уста"). Copying them word for word breaks the
  JSON string; in JSON mode the model cannot write one, so it loops on blank lines. 34 excerpts in 19 questions
  have them. Candidate fix: show the judge typographic quotes instead.
- `e53ce786` (v3) was pinned as baseline. Run `d568495f` (v4, typographic quotes) judged every sentence:
  passed 24 of 34, faithfulness 76%, citation accuracy 76%, 0 unjudged. G8 now fails on all 7 sentences (4 of 7).
  9 verdicts moved (5 up, 4 down), only in questions whose excerpts changed. R3.1 is now rejected.
- Runs take 80 to 98 minutes because `qwen3:8b` sits 17% on CPU: PCC sets `OLLAMA_NUM_PARALLEL=2` (KV cache
  reserved twice) and the D&D app keeps `mxbai-embed-large` loaded.
- 2026-11-09: `d568495f` (v4) pinned as baseline. PCC now sets `OLLAMA_NUM_PARALLEL=1` (PCC repo, uncommitted
  there): `qwen3:8b` at 12,288 context is 5.9 GB and 100% GPU, about 47 tokens/s. The first call after an Ollama
  restart took about 3 minutes to load.
- The user confirmed the physician has reviewed the golden set and it is fine; no re-run is needed for that.
  The evaluation harness is finished, and `d568495f` is the baseline. Do not raise the review as pending again.
- G8 now fails on 4 of its 7 sentences, so that verdict rests on partial judging.

### Context engineering

- Moving `qwen3:8b` fully onto the GPU changed 16 answers whose excerpts were identical. Each setup repeats
  itself exactly, but answers made on one setup cannot be compared word for word with the other.
- Run `d1b8b73e` drops reference lists and book indexes from the excerpts (`ReferenceMatter`). Only G6, G9, E2
  and E3 got different excerpts, and none lost a relevant one. Passed 23 of 34, faithfulness 77%, excerpt
  precision 82%, about 38 minutes. Pinned as baseline on 2026-11-09.
- The differential-diagnosis few-shot example was the same Kawasaki case as G5 and E5, and the model copied it
  (E5: 8 of 10 sentences unsupported). Run `1c7a13ab` uses an adult pulmonary embolism example instead. The
  other 31 answers were identical to `d1b8b73e`, so the GPU setup is repeatable. E5 went to 6 of 10 supported
  with 3 of 3 facts. G5 stayed at 3 of 6, and G10 lost one fact. Faithfulness 78%, passed 23 of 34.
- Keep few-shot examples off topics the golden set or the books cover, or the model answers from the example.
