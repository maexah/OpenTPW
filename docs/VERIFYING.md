# Verifying

Every rule here is a postmortem of an instrument that **agreed with the author while being wrong** — a
test, a grep, a control run, a harness, a decode or a screenshot that returned a confident answer about
something it had never actually examined. They are numbered as they were written, because other pages
cite them by number ("see rule 75"). Read the group that matches your symptom, not the whole file.

**What this file records about itself.** At least four of these lessons were written down and then
broken anyway — twice by the person who had just written them. Rule 5 recurred the same day it was
recorded; rule 10's swallowed exit code was repeated inside rule 38 with the note "which is already on
this list and I did it anyway"; rule 81's counting shortcut was taken again with rule 81 already on the
page; rule 75 recurred as rule 84, four more times, two of them the same afternoon. **Prose did not stop
any of them.** The lessons that stopped recurring became mechanisms — a test that fails the suite, a
`default` case that records the unhandled value, a runtime report that says so on the console. If you
find yourself breaking a rule you have already read, the fix is not to read it again: build the check.

## Start here

The ones that have bitten more than once.

- **3** — check that the control is a control; a difference of exactly zero is a smell, not a result.
- **5** — a measure that saturates everywhere distinguishes nothing; hold the confound constant.
- **11** — a literal-substring filter is blind to the formatting of the text it searches.
- **43** — never let a check's failure mode be an empty string; pair "nothing found" with a positive control.
- **75** — coverage is decided by fixtures as much as by assertions.
- **81** — a check whose alphabet is also the content's syntax will invent defects.
- **84** — a test that a state was reached says nothing about whether anything happens in it.

## Before you trust a measurement

- **1** — For any gesture with more than one key or step, drive **every** ordering (press orders, release
  orders, both in one frame) with a real pause between steps, and say in the result which orderings were
  covered. "Ctrl+C does nothing" should read "Ctrl+C with the letter released first does nothing".
- **2** — An instrument that reads the same state as the thing under test cannot falsify it. Verify with a
  number computed by a **different** path, and require the two to agree.
- **4** — Check the predicate can be FALSE: run it against a case you know should fail and confirm it says
  so. Assert on a value the system actually prints, not on a word you expect to find in the log.
- **5** — If every candidate region scores the same, the metric is measuring the frame, not the thing. When
  a confound moves everything, do not pick a better threshold — compare two runs that **both** carry the
  confound and differ only in the thing under test, with a control patch that must score zero.
- **6** — A harness must be able to tell its own reply from the surrounding log. Match on a prefix nothing
  else in the stream uses **and** on a marker only that command's answer can carry.
- **29** — A lookup that can fail must report its failure count in the same breath as its results. If
  "unresolved" is not on the summary line, the summary is a claim about an unknown fraction of the corpus.
- **30** — State the unit the **payoff** is measured in before quoting a number, and check the count you
  have is that unit. A large count in the wrong unit is more persuasive than a small one in the right unit.
- **34** — When three hypotheses are each soundly refuted and the fault persists, the error is in the KIND
  of explanation. Build an instrument that partitions the space rather than one that tests the next
  variant, check that instrument's own preconditions before trusting its silence, and ask anyone who holds
  ground truth.
- **38** — Never pipe a run you intend to keep through a truncating filter; redirect to a file and query the
  file. Label every row with something unique (a path, not a basename), or two rows are not comparable.
- **39** — An offset scan over a memory REGION scans every struct type in that region and hands them all
  back as the type you had in mind. Bound the scan to code already established to operate on the type, put
  a known-answer field in as a control, and prefer a site where the code **names** the field. A broad range
  check barely discriminates against binary data — a small integer read as a float is a denormal that
  passes any 0..100 test.
- **45** — A warning or error count is only the project's count when every project was compiled. Use
  `--no-incremental` for any number that gets reported or committed.
- **52** — Never assert on the endpoints of something periodic: sampling at a multiple of the cycle shows a
  still park at any speed, rewarding stutter and punishing correctness. Sample every step and assert over
  the whole series.
- **53** — Parity is not period. To pin a rate, assert the **interval** between events; evenness,
  divisibility and "it changed at least N times" are implied by the right answer and by infinitely many
  wrong ones.
- **54** — "The first one" is not a choice of subject, it is the absence of one. State what property the
  subject must have for the measurement to mean anything and select on that, or measure the population.
- **55** — Simplify an expression containing a bias and a rounding term **symbolically** before believing
  what it looks like; corrections that cancel are invisible to reading. Then ask what values the input can
  actually take — an operation ill-conditioned only on a measure-zero set is fine until the caller can
  produce nothing else.
- **56** — An invariant over differences is blind to a constant offset, a scale or a mirror: it proves
  consistency, never correctness. After one passes, say what it cannot see, and use an independent anchor
  for the absolute or record the gap.
- **59** — Read measured values back and ask whether the magnitude is sensible for the thing you believe it
  is, BEFORE writing the prose that commits you. Where a name and a value disagree, the value is evidence
  and the name is a label somebody chose.
- **69** — Decide in advance what result would count as support and what would count as refutation; if some
  plausible outcome is consistent with both, the instrument does not discriminate and must not be reported
  as though it did. Zeros, empty sets and "no errors" are the usual offenders.
- **85** — Before filtering a census, print the DISTINCT values of the field you are filtering on and count
  the rows dropped. Never write "never observed" without the sampling interval beside it; where the engine
  offers pause/step, use it, so "I did not see it" becomes something that can be false.
- **86** — **A pause that stops the world may stop the thing you are measuring**, and rule 85's advice to
  use it needs this beside it. OpenTPW's debug `pause` sets `Time.Paused`, and `Time.Delta` then reads
  **nought** — so anything driven by it, an animation or an ease or a timed transition, freezes and never
  completes, with nothing in the log to say why. That is indistinguishable from a feature that does not
  work; it cost two runs in one afternoon on a gate that was fine. Before concluding "it never fires", ask
  what your own control did to the clock the subject runs on. Where the engine offers `step <n>`, advance
  it deliberately — and note that stepping advances **everything** on that clock, camera and ocean and sky
  included, not only your subject.
- **87** — **A capture is of one SIDE of the subject.** A whole run of "the doors never moved" frames was
  taken of the back of the island, where there is no gate at all: the lobby camera orbits, so the framing
  was whichever angle the orbit had reached when the shot was taken. Every pixel difference measured was
  real, and none of it was the thing being measured. Before differencing frames, confirm the subject is
  *in* them — name the feature you expect to see and find it by eye once — and pin the viewpoint
  explicitly instead of letting a settle land where it likes. A difference that rises and then falls, or
  that sits no higher than a control taken elsewhere in the frame, is usually the scene and not the
  subject.

## Before you believe an absence

- **17** — A property of the CORPUS is not an invariant of the ENGINE. Before turning a measured range into
  a check, find the instruction that enforces it; if none does, carry the value through. Say "no shipped
  file does X", not "X cannot happen".
- **21** — "Nothing writes X" is only ever "nothing in [a,b) writes X" — write the range down beside the
  finding. A subsystem's own edge is exactly where the interesting writer lives, and a function that guards
  on its own subsystem being up is a tell that its callers are outside.
- **33** — A negative result is a statement about the instrument's reach, never about the data. Say which
  encodings and which ranges were swept, and ask what a positive answer would look like in units you did
  **not** sweep. Prefer reading the code that WRITES a field over scanning bytes for it: the writer names
  the type and the units for free.
- **36** — Before believing an absence, prove the search could have found a presence by searching for
  something you know is there, the same way. A four-character tag has two spellings and the file has only
  one of them. When your finding would REFUTE an existing record, re-read that record in full first —
  including the sentence above the table.
- **43** — Never let a check's failure mode be an empty string: print a count, and make "no result" look
  different from "zero". Check a tool exists before building a verdict on it, especially inside `|| true`.
  Anchor any search for a short or comma-formatted number (`6,384` lives inside `16,384`), pair every
  "nothing found" with a positive control, and read the surrounding lines before acting on a hit. Never
  infer a command's success from a pipeline containing it — test its own `$?` or `PIPESTATUS`.
- **79** — A loop that COLLECTS must run its whole budget; only a loop that WAITS for a condition may exit
  early. A truncated measurement reads exactly like a finding, and the other numbers in the line truncate
  with it. Where you can foresee that an instrument might manufacture the result, write that prediction
  down before looking.
- **102** — **A reader that answers "there is no such memory" can be wrong about it, and that answer reads
  as structural rather than as a failure.** Inside Ghidra's `run_python`, `memory.getBytes`, `api.getBytes`
  **and** `memory.getBlock` all denied `0x00700f94` existed — `getBlock` returning `None`, which is not "I
  could not read this" but "there is nothing here" — while the `read_memory` tool read it at once and the
  executable on disk agreed byte for byte (`.rdata`, file offset `0x002ff794`). `list_segments` compounded
  it by printing **PE section headers instead of Ghidra memory blocks**, so the address sat inside a range
  the listing called initialized while the reader said no block existed. On the strength of that I was one
  step from recording "the delegated probes fabricated these constants" as a finding; they had not, and all
  eleven floats they reported were correct. **Before disbelieving a number, read it by a second code path —
  and for anything load-bearing, read it out of the file the program came from**, which needs no analysis
  tool and so cannot share a bug with one. A negative from a single reader is rule 33's "statement about the
  instrument's reach" even when the reader phrases it as a fact about the data.
- **103** — **A derived statistic must not be segmented by the very quantity under test, or the verdict
  inverts.** Measuring whether a guest's drawn position slides between simulation steps, I split the run
  into thing ticks by watching the interpolation fraction wrap from high to low. That works perfectly on
  the interpolating build and is meaningless on the control, whose fraction never moves: the wrap never
  fires, the whole run collapses into **one** segment, and "distinct positions within one tick" comes
  back as the whole-run figure — about seventy — against the good build's thirty-one. **The broken build
  would have scored twenty times better than the fixed one**, from a harness that was correct on the
  build I happened to write it against. Segment by something the change cannot touch: here the tick's own
  248 ms period, taken off the timestamps, which needs nothing the subject prints. The tell to look for is
  a control whose score is *higher* than the treatment's on a metric that should floor it.

## Controls and mutations

- **3** — Assert that the manipulation took effect before trusting any comparison, and prefer a two-sided
  control: one thing that must change and one that must not.

  > A gadget was "proved" undrawn by grabbing the screen corner with the HUD up and again with it hidden by
  > `key F2`, then diffing. **`key` is not a debug console command** — the game answered `unknown command`
  > and carried on, so the two frames were the *same frame*, the difference was 0.00, and the harness
  > printed **"NOTHING DREW THERE"** about a gadget that was drawing correctly, date readout and all. The
  > crop was independently wrong too: it was computed by centring the virtual screen when the panel is
  > bottom-left anchored, so it sat well off the gadget. Either error alone produces the same false verdict.
  > This is worse than a weak test — it did not fail to find a bug, it *manufactured* one, and a zero
  > difference reads as a confident negative. Read the console's reply; work the crop out the way the
  > interface **anchors**; make the fixed probe report INCONCLUSIVE when the thing that must change did not.

- **9** — Pinning a variable only helps if it is the variable that moved: when a control fails twice,
  suspect the predicate, not the threshold, and grep your own source for the confound — it is often already
  written down in a comment you read that session. An unexplained extreme (0.00 or 100.00) must be
  explained or distrusted; and before recording a discrepancy in work that is already finished, look twice
  and say which pixels, because an instrument that manufactures evidence is worse than one that finds none.
- **26** — Restore from a copy, never from version control: `git checkout --` restores HEAD, which only
  coincides with "what was there a moment ago" while the work is committed. Commit before running a
  control, keep an md5 before/after, and compare it against the file as it was.
- **48** — Deliberately include a mutation you expect to SURVIVE, and write down that you expect it to. A
  surviving mutation names an unpinned claim, and the loudest claims in a comment are the most likely to be
  unpinned. When one shows something is untestable, ask *why* before writing tests around the outside —
  usually the unit takes more than it needs (a pool where one member would do) and shrinking the parameter
  makes the test possible. Then re-run: a pin is not a pin until the mutation it was written for fails.
- **50** — Never let either side of a control substitution be empty; to remove code, replace it with a
  distinctive marker comment. Wrap mutate/measure/restore so the restore runs even when the measurement
  throws, verify the md5 after **every** control, and when a run dies part way ask "what is on disk now?"
  before asking what it measured.
- **65** — Evaluate every guard in front of a band edge at the same inputs before asserting on it; a band
  can be unreachable at the chosen inputs, and no test of it can pass. A control has to land somewhere
  DIFFERENT from the thing it controls, or it discriminates nothing.
- **66** — Give the two sides of an "these two lookups agree" assertion **different** not-found fallbacks,
  or a lookup that fails on both sides passes while proving nothing.
- **77** — To classify an unknown, put it beside one case that certainly DOES and one that certainly does
  NOT, and read all three together. Pick the controls BEFORE looking at the unknown, so they cannot be
  chosen to fit the answer you want; a single listing invites you to read your hypothesis into it.

## Predictions

- **22** — Predict by **code path**, not by test name: corpus sweeps and any test that holds a clock still
  are the usual extra callers. An over-shooting prediction is as much a miss as an under-shooting one, and
  every unexpected failure needs explaining rather than banking as bonus confirmation. Write the prediction
  down before running, every time.
- **23** — A test has two kinds of assertion and they fail under different conditions: the ones that pin the
  behaviour, and the anti-vacuity guards that prove the test is not empty. Predict both — a guard firing
  under a deliberately broken build is evidence the test discriminates. Enumerate by code touched, not by
  symptom imagined.
- **41** — Derive the prediction from the **mechanism**, not from the tidy case the mechanism usually
  produces; if the mechanism is periodic, compute the expectation from the window actually observed. Order
  assertions cheap-and-broad first, strict-and-exact last, because a wrong strict assert masks every check
  behind it — and ask whether the instrument or the system is wrong before touching the system.
- **47** — A prediction is confirmed when the REASON is confirmed, not when the number matches. Write
  predictions so the mechanism is checkable separately from the outcome, and spend one line checking the
  stated cause actually occurred.
- **58** — Once a prediction is confirmed, ask whether the confirming assertion could have passed with the
  feature **removed**; be more suspicious, not less, when a low-confidence prediction comes back green. Pin
  exact measured non-default values — "the value is zero" predicts nothing about a zero-defaulting type.
  For a change with a unit half and a wiring half, say which half each test exercises. Treat a predicted
  failure that never arrives as seriously as an unexpected one: it is evidence the code is not being
  reached.
- **61** — When a predicted failure is masked by an unrelated one, re-check the prediction **on the code**
  after clearing the mask, not on the next run. Record a risk as two claims — the location and the mechanism
  — so a half-right call cannot be banked as a whole one.

## Greps, searches and filters

- **11** — Normalise before matching: strip markdown emphasis, strip known line prefixes, and search whole
  files rather than clipped context windows. Prove the filter matches something before trusting that it
  matched nothing, and when two halves of one instrument disagree, the instrument is broken — do not pick
  the convenient half.
- **24** — Ask the runner, not the source: `dotnet test --list-tests` enumerates what will execute, a grep
  counts what somebody wrote. A negative grep only rules out what you thought to name. A delegate
  contradicting a measured baseline is making a claim about **your** measurement — re-measure.
- **25** — `strings` does not know where a string begins: the first character of anything it prints may
  belong to the binary in front of it, so a plausible extension at the END is no evidence the START is
  clean. Use the reader that walks the archive's own table.
- **63** — When the strings being searched for are DATA rather than patterns, use a fixed-string match or a
  real join — never interpolate into a regex, where `[0]` becomes a character class. Treat "fewer results
  than expected" as a reason to check the instrument, and beware a guard that skips an empty lookup: it
  converts a broken query into a clean pass. When a check looks for a marker, exclude the region that
  DOCUMENTS the marker.
- **74** — A grep cannot confirm a symbol you have just written: exclude files you touched this session, and
  read what each hit actually is — a doc comment and your own new line are zero independent sources, not
  two. Before correcting any file from something quoted in your context, re-read the file itself: context
  holds a photograph, the disk holds the thing.
- **76** — When a tool takes a PATTERN (suffix, glob, wildcard), first ask how many things it matched and
  name the single member explicitly. A duplicate in a list that cannot contain duplicates is a structural
  contradiction, not a curiosity. When a parser the project already trusts disagrees with a hand-rolled
  scan, test the parser's answer first.
- **81** — Before trusting a textual check, ask what ELSE in the corpus spells the thing you are matching.
  If the answer is "plenty", the count is not the check and you must walk the structure. A bare struct
  offset is not a search key — constrain the shape (array, stride, region, instruction class) and always
  plant a known-answer control in the same query.

  > The memory files mark live claims with `>>>` and `<<<`, and the documented check is to **walk** the
  > markers and pair them. Counting them per file instead flagged a file as unbalanced: the "stray" was
  > `List<Func<string, IEnumerable<string>>>` — three closing angle brackets of nested C# generics. The file
  > was fine and had not been touched. Taking the same shortcut again later flagged three files, of which
  > walking cleared two — one's "stray" being the paragraph that **quotes** the markers. The pairing walk is
  > ten lines of Python and one call; the counting version cannot tell a marker from `IEnumerable<string>>>`
  > and never will. When a health check flags a file you did not touch, suspect the instrument.

- **83** — A folder is not a corpus. Name the enumeration's boundary inside the claim itself ("of the
  scripts in `rides/`"), because a scoped claim later read as universal is indistinguishable from a wrong
  one — and when a corpus is small enough to sweep whole, sweep it whole. Two themes may ship the same
  filename with different contents.

## Decoding a struct, an offset or a format

- **13** — An archive member is addressed **without** the archive's extension. Derive a path from the walk
  (`GetDirectories`/`GetFiles`) rather than writing one out, and when a path fails, read the resolver before
  editing the path: one guess is a mistake, two in a row is a method.
- **14** — A table that checked out does not vouch for the table beside it; shared provenance is why two
  tables sit together, not evidence both survived the same version drift. Check the value RANGE against the
  dispatcher's own bounds test before reading a single handler, and when a source is right once and wrong
  once, downgrade the SOURCE. A measured value is evidence about the input that was measured and nothing
  beside it — and when a transplanted number turns out wrong, ask what the number was FOR before
  overwriting it with the right one.
- **15** — If the tree already parses the format, use that parser: a substring scan over binary data yields
  false entries indistinguishable from true ones. Grep the project's own notes before re-deriving anything
  from the binary — the note often carries both the answer and the warning about the trap next to it. Re-run
  a delegate's own command when its finding is load-bearing: cited evidence can be wrong while the
  conclusion is right. For "has this been pushed", ask `git ls-remote <remote>`, never a tracking ref.
- **19** — A ladder or coverage count nominates a candidate; the handler decides it. A row can be worthless
  three ways the count cannot see: the instruction **parks**, it **faults**, or it needs a subsystem that is
  not there. Ask of every candidate what it dereferences and who guarantees that pointer — where a handler
  does not test before dereferencing, the precondition is supplied by a world you do not have. Write the
  warning inside the instrument, not only in a note.
- **20** — A block's address range is not its behaviour: handlers inside one dispatcher share tails freely,
  and cases delegate. Follow every exit — jump targets and fall-through — until you reach a `RET`, and ask
  what is REACHABLE from each case rather than what it calls directly; compute it, do not eyeball it. The
  right answer by the wrong method is still luck.
- **28** — Before parsing extracted bytes, check the extraction: a format full of floats and offsets
  containing **zero bytes over 0x7f** has been through a text decoder. Read a tool's usage for a binary mode
  before piping binary through it, and when your instrument contradicts something the shipping product does
  routinely, suspect the instrument.
- **31** — When the codebase already reads a field, read it the way the codebase does before inferring an
  offset from a decompile — and treat "the values are suspiciously equal to the index" as a failed read, not
  a finding.
- **32** — An unaligned scan over a record of floats WILL produce plausible hits, because half of a float is
  a small number. Never accept a candidate offset on range alone: require it to vary the way ground truth
  varies, and check whether it lies two bytes inside a field you have already named. Write the negative
  branch into the instrument before running it.
- **35** — When a format stores an index, the format does not define what it indexes. Find the code that
  BUILDS the list and ask whether anything is inserted, reordered, capped or skipped before the numbering is
  fixed. Every count and trailer stays green through this class of error, because they all check the file's
  numbers against each other.
- **37** — A function mentioning X and calling Y is not evidence that Y acts on X. Decompile the call site
  and name the actual parameter before claiming it. Before dumping an address as a table, ask which
  **section** it is in and what defines it — a pointer passed to a parameter typed `code *` is code, and a
  dump full of `90` runs and `c3` bytes is instructions, not a sparse table.
- **42** — Convert a nested block's whole offset table from block coordinates to record coordinates in ONE
  step, then check the last entry lands exactly on the block's end. An outside harness reads at the
  harness's offsets and the reader at the reader's, so pinning harness-measured numbers says nothing about
  the reader — always include at least one assertion tying fields the reader reads to EACH OTHER.
- **51** — Never hand a Ghidra helper's `addr()` an integer literal: it takes the integer's decimal digits
  as hex. Use `api.toAddr(0x...)` or a string. Generally: when a helper accepts a type it cannot distinguish
  from another, an unmapped address is a **lucky** failure and a mapped one is a silent wrong answer.
- **71** — "The fields are alphabetical" really means "alphabetical as first written, then appended to". Use
  the sort rule to NAME an unnamed field, never to predict where one sits — read the serialiser's call
  order. A field that breaks the sort was probably added later, so it is a signal. And placing a field is
  not naming it: reason about the mechanism, then read the values before choosing a name.
- **73** — When decoding a packed or offset field, do not look for the reading that seems tidy: find an
  observable that succeeds under one reading and fails under the other, and check the cases you are
  reasoning from are not the ones blind to the difference. Prefer a test whose failure mode is *the feature
  visibly not working*. When a decode is corrected, hunt every place that encoded the old one.
- **80** — When a decode turns on a struct offset, FIRST grep every note and field table for that offset in
  hex. A serialiser naming its own fields outranks any inference from surrounding code, and a log string
  names an OUTCOME, not the field that decides it. If you find yourself deriving a neighbour you already
  know, stop and look up the whole table.

## Tests and fixtures

- **16** — Run a new or changed test class ALONE as well as in the suite; a full green run cannot show an
  order dependency. Build fixtures as the real format, not as the least the reader tolerates, or the fixture
  inherits every dependency of the diagnostic path it trips. Read the SKIPPED count, never just "Passed!".
- **27** — A constant in a hand-built fixture is not a number, it is bytes going through a reader: check it
  against the width, sign and tag the format declares. Any test driving something that can stop, park or
  finish early must ASSERT that state before asserting behaviour — all three failure modes look identical to
  "the behaviour under test did not happen". Prefer a value the format obviously admits over the largest one
  that feels safe.
- **44** — When a factory's job is to WIRE something up, the test must ask **the built object** the
  question. Watch for the shape: `var thing = Factory.For(...); Assert.IsNotNull( thing );` followed by calls
  that never mention `thing` again — that is a test of the underlying static.
- **46** — Rigour about the fields is not rigour about the claim. After a join, name the thing that would be
  MISSING if the claim were false and check that specific thing is present. The danger sign is a test whose
  assertions are all metadata — counts, lengths, indices, flags — while its name promises the payload.
- **49** — Before writing a guard, say out loud what its FAILING would mean. If the answer is "the two things
  agree", it is a corroboration written backwards, not a guard. Guards that arrive as a set of three deserve
  this test individually.
- **62** — A swap has three parts — the new mechanism, the thing that consumes it, and the crutch you are
  removing — and the risk lives in the second. Before deleting the crutch, follow the value by name from
  where it is produced to the line that reads it; if you cannot point at that line, it is not wired. Be
  suspicious of the pleasure in "this retires a departure".
- **64** — A test count in a commit message is a claim about that commit STANDING ALONE, which the
  full-suite run never checks. Take the figures from that commit's own worktree build, or write the message
  without them and add them after.
- **67** — Where behaviour is randomised, accumulate observations across the run — step, observe, collect —
  and assert over the collection; sampling the final state asserts a property of the seed. The tell is an
  assertion reading "at least one of them is currently X". A flaky test is worse than a missing one.
- **68** — When restoring saved state that includes a TIMESTAMP, ask what clock it was taken against and
  whether yours starts from the same place. Assert **position, not state**: a thing handed a destination it
  never moves toward satisfies any state check. Two clocks in one codebase is a units bug waiting to happen
  — name which one a field is denominated in — and look for the engine's own handling of the problem before
  inventing one.
- **75** — When you add a feature behind an OPTIONAL dependency (a nullable park, catalogue or service), read
  which constructor the existing tests call and whether that dependency is present in them.

  > A ride arm shipped green and did not work at all: the behaviour switch had **no case** for the new
  > state, so a guest who chose a ride stood where they decided, playing a walk animation, for ever. It was
  > invisible for a precise reason — every behaviour test in the tree builds the behaviour through its
  > two-fact constructor, which constructs the chooser with a **null park**. With no park the chooser can
  > never return a candidate, so the ride arm never fired in a single test. The arm was unreachable from the
  > entire suite, so its absence could not fail anything, and a stale comment in the same file said the ride
  > "cannot be taken yet" — true only because of that same blindness. **Coverage is decided by fixtures as
  > much as by assertions.** Before trusting green, name the exact construction path that reaches the new
  > code and confirm one test takes it; have that test assert the state is **LEFT**, not merely entered. Where
  > the existing fixtures cannot reach it, build the real thing rather than widening an old assertion.

- **84** — After ANY new state, arm or enum branch, diff what is SET against what is HANDLED.

  > A behaviour switch declared more states than it handled, and the unhandled ones froze a guest where they
  > stood — including two introduced by the very commits reported as "the ride loop closed, measured in a
  > live park". That measurement collected the *set of states any guest was seen in*, saw the riding state
  > in it, and called that the loop working. **It proved a guest could be put into the state; nothing asked
  > what the state then did.** It was found by launching the game, not by the suite. The check is
  > mechanical: grep the `SetState(` sites against the `case` labels, three lines of shell. Better, make it a
  > TEST, so the next one fails the suite instead of waiting for someone to play. When a switch has no
  > top-level `default`, an unhandled value is silent by construction — no log, no throw, nothing to notice —
  > which is why it survives a green suite indefinitely.

## Shell and harness traps

- **7** — `sys.exit()` inside a `finally:` block **replaces whatever exception was in flight**, leaving a
  bare exit code and nothing to debug from. Catch `BaseException` before the `finally` and print it. Before
  trusting a harness's numbers, ask what it would print if the thing under test were broken and what it
  would print if the harness were broken; those must differ.
- **10** — `cmd | tail -40; echo "exit=$?"` reports **tail's** status, not the command's. Redirect to a file
  and capture `$?` from the command itself, or the exit code is decoration.
- **12** — Every "must be" banner needs a comparison that can exit non-zero; a banner beside untested output
  reads exactly like a passing check and is worse than no check, because it buys confidence. Do not trust
  `set -e` to enforce a precondition. `git add` is all-or-nothing across its pathspecs, so one stale path
  stages **none** of them — exactly what a rename produces. Make the label and the predicate the same thing,
  and when a check fires, suspect the check before the file.
- **89** — **A behaviour change can delete the instrument you verify with, and the loss looks like the
  feature being dead.** The lobby's `Lobby audio:` line was the passive observable the attract camera was
  proved with — 2 island changes in 60s, 7 in 180s. Making all four parks sound at once moved that log
  behind an early return, correctly, because the island on show no longer changes what is playing; the
  next run reported **zero** changes and read exactly like a camera that had stopped flying. Before
  changing code, ask what you last verified it with and whether this edit silences it. Prefer an
  observable the subject cannot switch off — a pure getter on the console, a counter in a state reply —
  over a log line emitted from the branch you are editing.
- **88** — **An instrument that writes as well as reads measures your own interference.** A debug command
  that looks like a query can be a setter: OpenTPW's `island` runs `DebugSelect( (int)Argument( 1 ) )` and
  that argument falls back to **0**, so polling a bare `island` every couple of seconds to see which island
  was current would have *selected* island 0 each time and then reported what it had just set. Read the
  handler before polling anything in a loop; prefer a line the subject logs for itself, which cannot
  perturb it; and where a getter and a setter share a name, assume the setter.

## Delegates and commissioned work

- **8** — A confident negative from an instrument that has never been shown to produce a positive is not
  evidence. Read the `<failures>` block of a task result before believing its `<result>`, and remember an
  aggregation can die silently while every agent behind it still ran and still cost.
- **18** — Read every agent's output before acting on any of it; a critic stage exists precisely because the
  verifiers share blind spots, and skipping it keeps the cost and discards the benefit. The summary is not
  the result — a truncated tool result is a reason to open the full file. Save a commissioned verification
  somewhere durable before the session that paid for it ends.

## Memory, documentation and deletion

- **40** — In a multi-branch repo, a file's content is a branch artifact: "the page doesn't say that" is a
  statement about your checkout. Ask which branch OWNS the material by measuring (`git log master..<branch>
  -- <path>` for every branch), read that version, and inventory every link against the headings that exist
  on the branch you will commit to.
- **57** — When a fact changes, grep for the OLD claim and edit every sentence that states it; the edit is
  the update, the banner is only an announcement. Past-tense rather than delete where a line records what a
  measurement found on a day. Then read the whole block back in place — a substring search cannot tell a
  live claim from a quotation of a dead one.
- **60** — A correction is not automatically more accurate than the thing it replaces. Run the measurement
  the NEW sentence asserts before writing it, and treat a plan's explanation of why something is wrong as a
  hypothesis with the same standing as the thing it corrects — especially when you wrote the plan.
- **70** — A deletion instruction is a claim about a file, and the file is the better witness: grep proves
  nothing is *referenced*, only reading says whether it is *wanted*. Where a file documents its own reason to
  exist, raise it rather than deciding silently — and once the owner hands the decision back, judge it on
  whether the stated reason still holds.
- **72** — Before deleting a type, grep its NAME rather than calls to it: `new X` and `X(` miss every
  `<see cref>`, `<inheritdoc>` and prose mention, and a dangling cref moves the warning baseline. Fix them in
  the same commit, rewrite any sentence whose meaning depended on the thing existing, and say in a surviving
  file that it was removed.
- **78** — A comment and the code it describes are ONE edit: write the code first and the comment second,
  never the reverse. Before a batch of documentation edits that announce new behaviour, list the behaviours
  announced and check each against a line of code you actually wrote. Never treat "the comment says so" as
  evidence the code does so.
- **82** — Doc comments rot in one predictable shape — "X is not built" outliving X — and in bulk, because
  the commit that builds X never opens the files explaining its absence. Sweep for the phrasings only an
  absence can use (`not built`, `nothing drives`, `no consumer`, `does not exist yet`, `until .* exists`),
  but note the worst cases use none of them: a stale COUNT reads as precision, not as a claim. Write
  comments that cannot rot by dating the negative and naming the *reason* rather than the *state*.
- **90** — **Measure the CONTROL FLOOR before quoting any pixel percentage.** Two frames of a running park
  with *nothing done between them* differ by **2.54–2.60%**: guests walk, flags move, water animates. A
  whole-frame percentage below that floor is not weak evidence, it is *no* evidence — and a session was
  spent treating 1.42%, 1.04% and 0.15% as results when all three were smaller than doing nothing at all.
  Shoot a before/before pair first, then a before/after pair, and report both. Above the floor, a scalar
  still cannot say *what* changed: build a DIFFERENCE IMAGE and look at the SHAPE. Ten path cells are a
  connected block; a guest is a scattered blob. Shape discriminates where a number cannot.
- **91** — **Never grab a frame while the game is paused.** `pause` plus `step <n>` gives the renderer a
  frame budget, and once it is spent the game stops presenting entirely — so a grab returns the picture
  from *before* the work while the console cheerfully reports the new state. It is not black and it is not
  obviously wrong; it is plausible, which is what makes it dangerous. Worse, a "wait until two consecutive
  grabs agree" check *passes instantly and perfectly* on a frozen renderer: it cannot tell "settled" from
  "not drawing", so that check is worse than none. Determinism and screenshots are mutually exclusive here.
- **92** — **Aim at the built park, and photograph something already there first.** Ten new cells out on
  empty terrain at zoom 80 are a few pixels near the horizon, which is what several "nothing rendered"
  captures were actually showing. Before concluding a feature does not draw, take a CONTROL SHOT of
  something known to exist — the shipped avenue at cell (47,21) renders the gate, the rides, the guests and
  the river. If the control is missing, the harness is at fault; if the control is there and the new thing
  is not, only then is it the feature. Nine explanations were offered before that one-frame control was
  tried, and every one of them was wrong.
- **93** — **Check a threshold against the region's REACHABLE maximum before believing a failure.** A
  control that is transparent by design can only ever change the fraction of its rectangle its drawn
  parts cover. The entry-price spinner is 424x173 and its frame mesh resolved to nothing, so only two
  60x61 buttons and a short number ever painted: 17,633 of 73,352 square units, a **ceiling of 24.0%**.
  It was judged against a 20% threshold — a bar just under its own ceiling — and "failed" twice at
  12.2% while the buttons inside it were changing **92.0%** and **93.6%** against a 0.0% floor. Compute
  what the region *can* do before deciding what it *did*, and judge a mostly-empty control BY ITS PARTS.
- **94** — **Drain the reply queue before reading a reply.** A harness that pumps output into a queue
  and then searches it for the next matching line will happily hand back a reply to an EARLIER command.
  This reported `click: the interface took (727,113)` for a click sent to `(100,609)` — coordinates
  never sent, from a tab click five steps earlier — which reads exactly like a button being missed. The
  button had worked. Flush, then send, then wait.
- **95** — **Every numeric check can pass while the display is wrong. Look at the picture, every
  time.** Four defects in one session cleared every region check, every gap counter and a green suite:
  a column heading 160px clear of its column; a tab strip that vanished with the list it hung off,
  stranding the screen; a kind-to-label table transposed, drawing guards under "Entertainers'
  Happiness"; and a list rebuilt every frame. The measurements were all real and all beside the point —
  each was caught by opening the screenshot. A region check proves *something changed there*; only the
  image proves it is the right thing. Budget for looking, not just for measuring.
- **96** — **`pkill -f PATTERN` kills the shell running it** whenever the pattern appears in that
  shell's own command line. `pkill -f "OpenTPW.dll"` inside a script that mentions `OpenTPW.dll`
  matched itself and died at its first line, taking the mutation check with it — and an earlier
  `pgrep -af "OpenTPW.dll"` had reported "game still alive" about *itself*, which is the same trap
  reading as evidence. Match on the executable instead (`pgrep -x dotnet`), so the pattern cannot
  describe the shell. Generally: **any process query whose pattern is also in your own command line
  is self-referential**, and it fails in the direction of looking like a result.
- **97** — **A control window can land inside a genuine silence of the subject, and that is a reading,
  not a broken instrument.** A park's music is *replayed, not looped* — about 8.5 s of arrangement then
  a ten-second wait — so a park's mix is digitally silent for more than half of every cycle. A 3 s
  music-only floor fell in a gap and read −999 dBFS, which is indistinguishable from an audio device
  that never opened. Span the subject's own duty cycle before calling a window representative, and ask
  what the subject does *between* its events, not only during them. **And when the floor cannot be
  measured, the comparison is INCONCLUSIVE — never a direction.** With `floor = None` a harness of mine
  fell through to "the mix DROPPED" on +0.70 dB, manufacturing a result from a missing control; the
  same run's real answer was "no change". Make the no-floor branch say so explicitly, because the
  default branch will otherwise say something confident.
- **98** — **A debug selector can be read by a branch the subject never takes, and its reply will
  still say it worked.** With nobody playing, `LobbyCameraMode.Update` returns early into
  `Attract()` — a wander seeded from `RandomPointInBox`, and reseeded on *every* lobby build because
  `ForgetIsland` clears `_wandering`. `island`, `orbit` and `settle` are read only by the orbit
  branch that early return never reaches. So a harness that carefully pinned the camera photographed
  two quite different viewpoints, and the console answered `island 0 'Lost Kingdom'` **both times** —
  the reply echoes the selection, not where the camera is, which is rule 88's shape again. Two
  lobbies in one run cannot be compared at all until the orbit branch is forced. **Assert the state
  the subject actually uses, not the one you set**: `state` reports `cam=`, and an identical
  `cam=452,353,32` in both shots is what finally made the frames comparable. Note also that a
  recipe can go stale without anyone touching it — `lobbyshot.py`'s `pause → island → settle` dates
  from before the attract camera existed and silently stopped pinning anything.
- **99** — **A region's mean and variance can be identical while the texture on it is plainly
  wrong.** The lobby sea drawn with mirrored addressing instead of wrapping is a **diamond lattice**
  where it should be parallel ripples — unmistakable at a glance, across half the screen — and the
  crop's statistics barely moved: mean 55.69 → 55.62, variance 221.83 → 222.79, under half a
  percent. Mirroring rearranges *where* the same texels land without changing *which* texels they
  are, so every summary statistic survives it intact. This is rule 90's "shape, not scalar" in its
  purest form: a verdict resting on those percentages would have reported "no change" for the
  defect it was built to find. **Pair the picture with a number the game reports about itself** —
  here `water: requested Wrap sampler AnisotropicRepeat`, which does discriminate, where nothing
  computed from the pixels did.
- **100** — **A threshold taken from each run's own distribution cannot compare two runs.** Comparing a
  looped scream against a replayed one, the harness cut at "this window's 35th percentile plus 6 dB" —
  a sensible floor for *one* recording and worthless across two. It landed at **−29.95 dBFS** for the
  control and **−69.17 dBFS** for the subject, 39 dB apart, so the duty cycles, burst counts and gap
  medians computed from them were three confident numbers measuring two different things: it reported
  "155 onsets against 47" and "median gap 0.30 s against 1.30 s", which reads exactly like a result.
  The same two captures against **fixed** thresholds answered cleanly — sounding above −60 dBFS, 93.3%
  against 52.4%, and a 10th percentile of −55.1 dBFS against −180.0, the latter being true digital
  silence. **An adaptive threshold is an instrument that re-calibrates itself to whatever it is shown**,
  which is the one thing a control exists to prevent. This is rule 5's saturating measure wearing a
  percentile. Note also what the census did instead: `plays 1` against `plays 8` needed no threshold.
- **101** — **A `trap … EXIT` restore with a relative path dies if the script has changed directory,
  and it still prints as though it ran.** A control substitution wrapped its restore in a trap —
  correct, per rule 50 — then `cd`'d elsewhere to launch the harness. On exit the trap fired, `cp`
  failed with "No such file or directory", `md5sum` printed nothing, and the line read `restored:`
  with an empty value: a restore that reported success by saying nothing. The mutation stayed on disk
  and the next build compiled it. **Use absolute paths in anything that runs at exit**, and make the
  restore *assert* — print the md5 and compare it against the one taken before, so a silent failure
  cannot pass for a quiet success. Rule 50 already says to verify the md5 after every control; this is
  why it says "after **every**" rather than "at the end".
- **104** — **A control can fire because its EXPECTED value is wrong, and "the control failed" is then the
  wrong conclusion to draw from it.** Reading a saved script struct, the alignment was checked against the
  speed word, which every shipped `.RSE` carries as 50. Thirteen of the fourteen records read 50 and one
  read **60**, and the honest-looking conclusion — "the control fails, so the alignment is wrong, so the
  program counter I just read is noise" — would have thrown away a correct decode. The 60 was real: 50 is
  what the *loader* writes, and the engine pushes an object's own operating speed over it
  (`FUN_0055a300`), and that object's `mOperatingSpeed` was independently measured at 60. **Before
  believing a control, ask what WRITES the field it reads** — a field with more than one writer cannot
  have one expected value. What actually settled the alignment was a second control with only one
  possible answer: the struct's length field equalled the body block's own word count for all fourteen.
  The lesson is rule 12's, one turn further on: a control that fires is a reason to check the control,
  and checking it means checking its expectation, not just its arithmetic.
- **105** — **A four-character tag compared as a dword is stored BACKWARDS, so searching for it the right
  way round finds nothing and reads as proof the thing is absent.** Looking for the seventeen module tags
  in a park save's inflated payload, a search for `WRLD`, `GSYS`, `RSYS` and the rest returned **not found
  for sixteen of seventeen**, with the one hit being unrelated. The natural reading — "this file is not
  written by that code path at all" — was wrong and would have redirected the whole task. The executable
  compares a 4-byte read against a constant like `0x57524c44`, so the bytes on disk spell `DLRW`; searched
  reversed, **all seventeen** appeared, in the exact order the code reads them. This is rule 17's "say what
  you measured, not what it means" applied to endianness: a dword constant in a decompiler listing is not
  a byte string, and one of the two orders is a fact about the file while the other is a fact about how the
  constant was printed. Check both orders before recording an absence.
- **106** — **A before/after on the thing you CHANGED cannot see something else going missing.** A park's
  things were replaying the clip that builds them. The fix was verified by watching one ride across a
  load — "role 0 never appears" — before and after, with the number predicted first and then confirmed.
  It was also a net regression: **ten of the fourteen placed things stopped animating at all, for good**,
  and the single ride that was watched happened to be among the four that survived. The instrument was
  sound, the control was real, and the prediction was right. **The population was wrong.** The tell was
  there to be read in the change's own justification: the mechanism being altered — how every script
  resumes — applied to every script, while the measurement named one ride, so the scope of the claim and
  the scope of the evidence never matched and only the narrower one was checked. **Before believing a
  before/after, ask what else runs through the code being changed and measure THAT set.** Where the game
  can census a whole population as cheaply as one member — every thing, not the thing in question —
  census all of it; the run costs the same either way. This is rule 85's "a census that showed only the
  survivors" one level up: there the instrument did the filtering, here the author did. And the other
  half of it: the regression was found by an adversarial review rather than by the verification, because
  a check the author designs inherits the author's blind spot about where to point it.
- **107** — **A view steered by where the pointer IS drifts for as long as the clock runs, and one of its
  two axes cannot be set from a console at all.** The camcorder's `Steer` reads the pointer's *position*,
  not its movement, and the two axes are not alike: yaw **accumulates**
  (`Yaw -= Beyond(x) * YawRate * Time.Delta`) so it winds on every frame the pointer sits outside the
  dead zone — which is wherever X last left it — while pitch is **assigned**
  (`Pitch = Beyond(y) * PitchScale`) so anything a console writes to it is gone by the next frame. Both
  bit in one session. A four-sided walk test came back with "east" having moved the viewer three cells
  **north**, because the heading had spun between the approaches and every one of them resolved against
  a different one; and a screenshot meant to show a ride came back as 41.3 degrees of empty sky. The
  fixes are different for each: freeze the drift with `pause` (which zeroes `Time.Delta`, and is *not*
  rule 91 — that forbids **grabbing a frame** while held, not measuring), and for the picture put the
  pointer itself inside the dead zone with **XTEST motion**, not a warp, since a warp with no motion
  behind it reaches X and never reaches SDL. **Then read the heading back and assert it**, per rule 98:
  had the reply not carried `yaw=`, all four readings would have looked perfectly well-formed and been
  measurements of nothing.
- **108** — **A facing-relative command means a different world direction at every heading, so aiming the
  camera silently re-aims the controls.** `walk <forward> <right>` resolves as
  `dx = forward * -sin(Yaw) + right * cos(Yaw)`. At yaw 0 `right` is exactly `+x`, which is why a census
  harness could walk east with `walk 0 1` and be right. The moment a second harness set the heading to
  face that same ride — yaw `-pi/2` — `right` became `-y`, the identical command walked the viewer away
  along a different axis, and the census it printed was internally consistent the whole way. Nothing was
  wrong with the game, the command or the reply. **When a harness sets an orientation, recompute what
  its own movement arguments mean in that orientation**, and prefer a command whose arguments are in the
  frame you are reasoning in — or assert the destination, not merely that something moved.
- **109** — **Sampling faster than the thing you measure turns frame pacing into a rate, and storing
  only your derived statistic means you cannot go back.** Measuring how fast a camera turns, a harness
  polled a pure getter as fast as it could — about 144 Hz — and differenced each reply against the one
  before. Consecutive polls either land in the same frame, giving an angle of nought that drags every
  median down, or straddle one, in which case a whole frame's turning is divided by a 7 ms gap. Its
  headline, "max 143.12 degrees a second", is exactly **1.0 degree in one 7 ms sample**: an artefact of
  the sampling interval wearing the units of the subject. **Difference over a fixed window** (100 ms
  here) so the denominator is yours and not the scheduler's, and deduplicate repeated readings before
  any statistic. The second half cost a whole extra run: the harness wrote only the rate it had
  computed, so when the computation proved aliased there was nothing to re-analyse. **Write the raw
  samples to a file as well as the summary** — they are small, and the question you will want to ask of
  them is not the one you built the harness for.
- **110** — **The start of a run is not an event, and counting it as one can invert the verdict.** Asked
  whether a camera swings when it changes which island it looks at, a harness bucketed every sample by
  whether it fell within 1.5 s of a change. The **first** sample necessarily "changes" island — there
  was no previous value — so the run's own settling landed squarely in the treatment bucket, and **20
  of 20** of the fastest turns were inside the first 5 s. It reported near-a-change 13.48 against
  away 10.18 and a maximum of 143 against 48: a swing, confidently. Discarding a warm-up reversed it —
  6.66 against 8.99, with the treatment now *quieter* than the control. **Discard a warm-up before
  anything is measured, and ignore transitions inside it**, because the first observation of any state
  is a transition from nothing. This is rule 103's shape again: the segmentation, not the subject,
  produced the result.
- **111** — **Read the units off the call site before aiming a capture, and check the reply agrees.**
  `camcorder x y [yaw]` takes **world units and radians** — its own site says "an optional heading, in
  radians" — and four frames were lost passing cells and degrees. The game said so both times and was
  not read: `camcorder 44 25` replied `stand=(44,25) cell=(4.4,2.5) at=(4,2)`, the camera parked in the
  far corner of the map, and every shot came back empty terrain at **mean 110–111**, indistinguishable
  from the shots it was meant to differ from. A heading of `180` was twenty-eight turns. **The reply
  carries `stand=`, `cell=`, `at=` and `yaw=` precisely so a misaimed capture can be caught before it is
  believed** — and brightness cannot catch it, which is rule 99 again.
- **112** — **Establish a facing from something you can recognise in the frame, never from the formula
  alone.** Rule 108's `dx = forward * -sin(Yaw) + right * cos(Yaw)` gives the x component, and reading
  forward off it as `-y` at yaw 0 was backwards: a camera at cell (42,28) on yaw 0 photographed the
  Drinks Shop at (43,30), which is **+y**. Four more frames were spent on the inverted pair before a
  landmark settled it. **Photograph a thing whose cell you already know, name it in the frame, and only
  then aim at the subject.** The side headings `±π/2` were right throughout; only the pair I had
  reasoned about was wrong.
- **113** — **A test step that can run without a build will happily test the previous build.**
  `dotnet test --no-build` runs the assembly already on disk, so a compile error does not fail the run -
  it hands back the last good build's numbers. A mutation harness that built with `capture_output=True`
  and never read the exit code reported an identical clean **894 passed** four times over, against a
  binary that predated every edit in the turn, and each one read exactly like a mutation surviving. The
  only thing that caught it was the build line printed above the results. **Check the build's exit code
  before believing any number below it, and make the harness refuse to return a verdict when the build
  failed** - a fabricated survival is worse than no measurement, because it gets written down as
  evidence that a fix rests on nothing.
- **114** — **A mutation is only tested by a test that calls the mutated code.** Keying a footprint's
  owner on the wrong corner survived the whole suite twice. The diagnosis was right the first time - no
  test built a *turned* thing, where anchor and corner differ - but the fix was to add a turned case to a
  file whose own helper *rebuilt* the stamping logic instead of calling `ParkBuilding.Stamp`, so the
  mutation stayed invisible and survived a second time. **Before adding a test to kill a mutation, name
  the function the mutation edits and check the test reaches it**; a fixture that re-derives the subject
  proves the fixture. Making the real function internal, the way `ParkPicking.ThingOn` already is, is
  what closed it - and it immediately exposed a second defect in the same shape, in the sell path.
- **115** — **Editing a structured document by matching a fragment silently destroys structure.** Twice
  in two sessions an edit to `docs/QUEUE.md` replaced text that ran *into the middle of an item*: once a
  heading was written over the fragment "not a path.", severing Q3's body so its tile-piece half and its
  whole Confirm clause became a phantom ticked "Q1b" sixty lines away; once an item's heading line was
  replaced while its original body was left dangling underneath, so the entry read as done and
  not-started at once. Neither was noticed by the edit, the build, or the tests - **only counting the
  headings found them** (`grep -c` on the item pattern, two `Q1b`). **Match the whole entry, or anchor
  on a line you can see is a boundary; then count the headings afterwards and check each number appears
  once.** For a block with awkward characters, replace it by line range with an assertion on the first
  and last line rather than by string match - an off-by-one there fired the assert and wrote nothing,
  where a fuzzy string match would have eaten the next item's heading.
- **116** — **A mutation harness that restores the SOURCE leaves the last mutation's BINARY on disk,
  and the next run of the game silently tests it.** Rule 113 says check the build's exit code before
  believing a number below it; this is its other half, and it bit on the very next task. A harness put
  four mutations through build-test-restore, ending with one that disabled a new click handler, and its
  `finally` copied the file back without rebuilding. The game harness run minutes later drove a binary
  in which that handler did not exist, so the feature reported as not working - and every other part of
  the same run was valid, which is what made it convincing. **Rebuild after the final restore, inside
  the harness**, and have it print the md5 of the built assembly as well as of the sources; better, make
  the game harness refuse to start unless the build is newer than every source file it cares about. A
  restored working tree is not a restored program.
- **117** — **A count quoted from someone else's measurement names THEIR population, not your test's.**
  A decode reported that its reading predicted "all fourteen" of the shipped save's placed objects, and
  a test was written predicting fourteen. It compared eleven: the decoder had walked every object record,
  and the test walked the ones the buy catalogue describes, which leaves out the bus, the gates and the
  lights. Every compared object matched, so the reading was right and the prediction was not. **Before
  writing a number you did not measure into an assertion, say what was counted and check your loop
  counts the same thing** - and when the two disagree, find the difference before changing either.
- **118** — **A change to what a scene LOADS shows up as a within-run `save/` change the first time,
  and that is the loading bar, not damage.** The bar learns each situation's step count into
  `save/opentpw.cfg`; a park that starts loading four more textures takes more steps, so the first run
  after the change rewrites that file as the park loads - inside the run, which is the one place a
  checksum difference is supposed to mean something. **Name the file that changed before calling it
  either way**, and re-run: the second run of the same build must be unchanged, and it was.
- **119** — **Whether something is still in memory is the collector's to say, and a root list read from the
  code misses links through a base class.** Q10 asked which statics keep a left park's save alive. Of four
  readings of the code, two said `ParkRides.Current` reached no save, since it has no `ParkWorld` field. The
  other two found that it does: every `Entity` keeps the `Level` it was made in, so any never-cleared static
  of an entity type holds a whole level. **Measure it first:** hold the object in a `WeakReference`, force a full
  blocking collection, ask whether it is alive, and only then name the roots as the explanation. Print
  "nothing named" when it is alive and no root you know of holds it. In a test, make the object in a
  non-inlined helper, because a debug build keeps a method's locals alive until the method returns.
- **120** — **A test stepped at one frame rate cannot tell a rate taken per second from one taken per
  frame.** Q12 stepped the lobby's leave sequence with `Time.Delta` at 1/60 and pinned every count exactly,
  and `HomingRate * Time.Delta` rewritten as `HomingRate / 60f` still passed: at that one rate the two are
  the same number. **Step it at a second rate** with its own predicted counts - 30 a second was enough, and
  the mutation then failed that row only. The same holds for any `Time.SmoothingFactor` ease a test drives.
- **121** — **To prove a build is fresh, look at the file the test will load, not at the test assembly.** Q44's
  mutation harness refused every result as stale, rightly by its own rule and wrongly in fact: it watched
  `OpenTPW.Tests.dll`, and in an incremental build (`dotnet build` without `--no-incremental`, as a mutation
  harness runs it) a change to a method body in `OpenTPW` rebuilds only `OpenTPW`, whose copy beside the tests
  (`OpenTPW.Tests/bin/.../OpenTPW.dll`) is what moves - the test assembly is not recompiled, because the reference
  assembly it compiles against did not change. `--no-incremental` rebuilds every project, so there the test
  assembly moves too and proves nothing. Watch the copy under either build. The guard failed safe, which is the
  point of having one; a guard watching the wrong file the other way round would have passed stale results.

---

When appending to this file, anchor on the last **content** and add after it — never on a trailing block
you intend to keep, because a replacement silently drops whatever the anchor matched.
