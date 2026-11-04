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

## Book term lookup (written 2026-11-04, not yet measured in a run)

`BookTermLookup` adds the books' Bulgarian term for each medical word in an English question, because the model
invents words (`отитис медиалин`) and swaps concepts (bronchiolitis became `Пневмония`). English words are rewritten by
shared Greek and Latin rules and prefix-matched against the BM25 vocabulary. It never runs on Bulgarian questions.

- **Offline:** 60 of 67 held-out terms (the 67 were not used for tuning, 9 scored misses were right matches), and none
  of 8 invented drug names matched. The expected stems for those lists were written by Claude, not a physician.
- **Limit:** words with no shared root (`poisoning`, `barking`) are unreachable. A glossary would need physician review.

Related: `mem:project/evaluation-harness`.
