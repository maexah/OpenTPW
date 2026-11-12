# Verifying

Every rule here is a postmortem of an instrument that **agreed with the author while being wrong**: a test, a grep, a
control run, a harness, a decode or a screenshot that answered confidently about something it never examined. They are
numbered as they were written, because other pages cite them by number ("see rule 75"); a number never changes meaning.
Each rule is one line of why. Its story, as it was written, is under the same number in
[`history/verifying-stories.md`](history/verifying-stories.md): grep `\*\*N\*\*` there. Read the group that matches your symptom.

Prose did not stop these lessons recurring; a mechanism did (a test that fails the suite, a `default` case that records
the value, a report on the console). If you break a rule you have already read, do not read it again: build the check.

## Start here

The ones that have bitten more than once.

- **3** — check that the control is a control; a difference of exactly zero is a smell, not a result.
- **5** — a measure that saturates everywhere distinguishes nothing; hold the confound constant.
- **11** — a literal-substring filter is blind to the formatting of the text it searches.
- **43** — never let a check's failure mode be an empty string; pair "nothing found" with a positive control.
- **75** — coverage is decided by fixtures as much as by assertions.
- **81** — a check whose alphabet is also the content's syntax will invent defects.
- **84** — a test that a state was reached says nothing about whether anything happens in it.
- **88** — a debug command that looks like a query can be a setter; read the handler before polling.
- **99** — a region's statistics can survive a plainly wrong picture; pair them with a number the game reports.
- **103** — never segment a statistic by the quantity under test; the verdict inverts.
- **113** — a test step that can run without a build tests the previous build; check the build's exit code.
- **116** — a restored source is not a restored binary; rebuild after the final restore, and print the assembly's hash.

## Before you trust a measurement

- **1** — Drive every ordering of a multi-step gesture, and say which you covered: one ordering can work while another does nothing.
- **2** — An instrument that reads the same state as the thing under test cannot falsify it; check against a number from a different path.
- **4** — Show the predicate can be FALSE on a case that must fail, and assert on a value the system prints, not a word you expect in the log.
- **5** — A measure that scores every region the same measures the frame; compare two runs that both carry the confound and differ only in the subject.
- **6** — A harness must tell its own reply from the rest of the log: match a prefix nothing else uses and a marker only that answer carries.
- **29** — A lookup that can fail reports its failure count on the same line as its results, or the summary covers an unknown fraction.
- **30** — State the unit the payoff is measured in before quoting a number; a large count in the wrong unit persuades more than a right one.
- **34** — When three sound refutations leave the fault in place, the kind of explanation is wrong: build an instrument that partitions the space.
- **38** — Never pipe a run you will keep through a truncating filter; write it to a file and label every row uniquely.
- **39** — An offset scan over a memory region finds every struct type there; bound it to code known to use the type and plant a known answer.
- **45** — A warning count is the project's only when every project compiled: use `--no-incremental` for any number reported.
- **52** — Never assert on the endpoints of something periodic: sampling at the cycle shows a still park at any speed.
- **53** — Parity is not period: to pin a rate, assert the interval between events.
- **54** — "The first one" is no choice of subject; select on the property the measurement needs, or measure the population.
- **55** — Simplify an expression with a bias and a rounding term symbolically, then ask what inputs the caller can really give it.
- **56** — An invariant over differences cannot see an offset, a scale or a mirror: it proves consistency, never correctness.
- **59** — Ask whether a measured magnitude is sensible before writing prose; where a name and a value disagree, the value is evidence.
- **69** — Decide in advance what result supports and what refutes; if an outcome fits both, the instrument does not discriminate.
- **85** — Print a field's distinct values before filtering a census on it, and write the sampling interval beside "never observed".
- **86** — A pause that stops the world may stop the thing you measure: under `pause` anything on `Time.Delta` freezes; advance it deliberately with `step <n>`, and stepping advances everything on that clock.
- **87** — A capture shows one side of the subject: confirm the subject is in the frame and pin the viewpoint before differencing.
- **90** — Measure the control floor before quoting a pixel percentage, then look at the shape of the difference image.
- **93** — Check a threshold against the region's reachable maximum, and judge a mostly-empty control by its parts.
- **95** — Every numeric check can pass while the display is wrong: look at the picture, every time.
- **99** — A region's statistics can survive a plainly wrong picture; pair them with a number the game reports.
- **100** — A threshold taken from each run's own distribution cannot compare two runs; use fixed thresholds.
- **103** — Never segment a statistic by the quantity under test; the verdict inverts.
- **106** — A before/after on the thing you changed cannot see something else going missing; measure everything the change runs through.
- **109** — Sampling faster than the subject turns frame pacing into a rate; difference over a fixed window and keep the raw samples.
- **110** — The start of a run is not an event: discard a warm-up before measuring anything.
- **119** — Whether an object is still in memory is the collector's to say; measure with a `WeakReference` before naming roots.
- **123** — A warning diff keyed on file and line reads a line shift as defects; compare the per-code counts as well.
- **127** — A timing taken on the original is only as true as its park clock: run `tpwmem.py clock` first.
- **128** — A reference frame of the original comes from the real GPU (off-screen drew the gate without its sign), and the first grab after a scene change can repeat the last.

## Before you believe an absence

- **17** — A range measured across the corpus is not an engine invariant until an instruction enforces it: say "no shipped file does X".
- **21** — "Nothing writes X" means "nothing in [a,b) writes X"; write the range beside the finding, because the writer lives at the edge.
- **33** — A negative result describes the instrument's reach, not the data; say which encodings and ranges were swept.
- **36** — Prove a search could find a presence, the same way, before believing an absence; a four-character tag has two spellings.
- **43** — Never let a check's failure mode be an empty string: print a count and pair "nothing found" with a positive control; check a tool exists, anchor a search for a short or comma-formatted number, and test its own `$?` or `PIPESTATUS`.
- **79** — A loop that collects runs its whole budget; only a loop that waits may exit early, or the truncation reads as a finding.
- **102** — A reader that says "there is no such memory" can be wrong; read a load-bearing number by a second path, best from the file itself.
- **105** — A four-character tag compared as a dword is stored backwards; search both byte orders before recording an absence.

## Controls and mutations

- **3** — Assert the manipulation took effect, with one thing that must change and one that must not: a zero difference from a no-op manufactured a bug; make the fixed probe report INCONCLUSIVE when the thing that must change did not.
- **9** — When a control fails twice, suspect the predicate, not the threshold; an unexplained 0.00 or 100.00 is explained or distrusted.
- **26** — Restore from a copy, never from version control, and keep an md5 before and after every control. Commit before running a control.
- **48** — Include a mutation you expect to survive, and say so: a survivor names an unpinned claim; re-run, as a pin is not a pin until the mutation it was written for fails.
- **50** — Neither side of a control substitution may be empty; wrap mutate-measure-restore so the restore always runs, and check the md5 after every control.
- **65** — Evaluate every guard before a band edge at the test's inputs, and land a control somewhere different from what it controls.
- **66** — Give the two sides of an "these lookups agree" assertion different not-found fallbacks, or two failures pass.
- **77** — Classify an unknown beside one case that certainly does and one that certainly does not, chosen before looking.
- **92** — Aim at the built park and photograph something already there first, so a missing control blames the harness.
- **97** — A control window can fall in a real silence of the subject; when the floor cannot be measured, the result is INCONCLUSIVE.
- **104** — A control can fire because its expected value is wrong; ask what writes the field it reads.
- **114** — A mutation is tested only by a test that calls the mutated code; a fixture that re-derives the subject proves the fixture.
- **116** — A restored source is not a restored binary; rebuild after the final restore, and print the assembly's hash.
- **118** — A change to what a scene loads rewrites `save/opentpw.cfg` the first run; name the changed file and re-run before calling it damage.
- **121** — To prove a build is fresh, watch the file the test will load, not the test assembly.
- **125** — A mutation is red when its named test fails on its named assertion; an exit code is not that.

## Predictions

- **22** — Predict by code path, not by test name, and write the prediction down first: an over-shoot is as much a miss as an under-shoot.
- **23** — Predict both the assertions that pin behaviour and the guards against an empty test; a guard firing on a broken build is evidence.
- **41** — Derive a prediction from the mechanism, not its tidy case, and order assertions broad first and exact last so one cannot mask the rest.
- **47** — A prediction is confirmed when its reason is confirmed, not when the number matches.
- **58** — Ask whether a confirming assertion could pass with the feature removed, pin non-default values, and chase a predicted failure that never came.
- **61** — When a predicted failure is masked by another, re-check the prediction on the code after clearing the mask.

## Greps, searches and filters

- **11** — A literal-substring filter is blind to the formatting of the text it searches; normalise first and prove it can match.
- **24** — Ask the runner (`dotnet test --list-tests`), not the source: a grep counts what someone wrote, not what runs.
- **25** — `strings` does not know where a string begins; use the reader that walks the archive's own table.
- **63** — Search for data with a fixed-string match, never a regex built from it; a guard that skips an empty lookup turns a broken query into a pass.
- **74** — A grep cannot confirm a symbol you just wrote; re-read the file, not the copy in your context, before correcting it.
- **76** — When a tool takes a pattern, count what it matched and name the single member; a duplicate where none can exist is a contradiction.
- **81** — A check whose alphabet is also the content's syntax will invent defects; walk the structure instead of counting.
- **83** — A folder is not a corpus: name the enumeration's boundary inside the claim, and sweep a small corpus whole.
- **122** — An exclusion filter must not run over prose: filter by path, never by a substring of the matched line.

## Decoding a struct, an offset or a format

- **13** — An archive member is addressed without the archive's extension; derive paths from the walk and read the resolver before editing one.
- **14** — A table that checked out does not vouch for its neighbour; check the dispatcher's bounds and downgrade a source that was wrong once.
- **15** — Use the parser the tree already has, grep the notes before re-deriving, and re-run a delegate's command when its finding carries weight. For "has this been pushed", ask `git ls-remote <remote>`, never a tracking ref.
- **19** — A coverage count nominates; the handler decides: an instruction can park, fault, or need a subsystem that is not there.
- **20** — A block's address range is not its behaviour: follow every exit to a `RET` and compute what is reachable.
- **28** — A float-heavy format with no byte over 0x7f has been through a text decoder; check the extraction before parsing it.
- **31** — Read a field the way the codebase already reads it; values equal to their index are a failed read, not a finding.
- **32** — An unaligned scan over floats always finds plausible hits; require a candidate offset to vary the way ground truth varies.
- **35** — A format's stored index does not say what it indexes; find the code that builds the list, since counts and trailers stay green.
- **37** — A function mentioning X and calling Y is not evidence Y acts on X; name the parameter at the call site and the section of an address.
- **42** — Convert a nested block's offsets to record coordinates in one step and land the last on the block's end; tie the reader's fields to each other.
- **51** — A Ghidra helper's `addr()` reads an integer's decimal digits as hex; pass `api.toAddr(0x...)` or a string.
- **71** — Fields sorted "alphabetically" were sorted then appended to: read the serialiser's order, and use the sort only to name a field.
- **73** — Decode a packed field with an observable that succeeds under one reading and fails under the other, then hunt every place that encoded the old one.
- **80** — Grep every note for a struct offset in hex before decoding it: a serialiser naming its own fields outranks any inference.

## Tests and fixtures

- **16** — Run a new test class alone as well as in the suite, build fixtures as the real format, and read the skipped count.
- **27** — A fixture constant is bytes through a reader, so check its width, sign and tag; assert a run did not stop early before asserting behaviour.
- **44** — A factory that wires something up is tested by asking the built object, not the static behind it.
- **46** — Rigour about the fields is not rigour about the claim; check the thing that would be missing if the claim were false.
- **49** — Before writing a guard, say what its failing would mean; "the two agree" is a corroboration written backwards.
- **62** — A swap has a new mechanism, a consumer and a crutch, and the risk is the consumer: follow the value to the line that reads it.
- **64** — A test count in a commit message is about that commit alone; take it from that commit's own worktree build.
- **67** — Where behaviour is randomised, accumulate observations over the run and assert over the collection; a flaky test is worse than none.
- **68** — Ask which clock a restored timestamp was taken against, name which clock a field is denominated in, and assert position, not state.
- **75** — Coverage is decided by fixtures as much as by assertions: name the construction path that reaches new code and test it, have that test assert the state is LEFT, not merely entered, and build the real thing rather than widening an old assertion.
- **84** — A test that a state was reached says nothing about what happens in it: diff what is set against what is handled.
- **113** — A test step that can run without a build tests the previous build; check the build's exit code, and make the harness refuse to return a verdict when the build failed.
- **117** — A count quoted from someone else's measurement names their population, not your test's.
- **120** — A test stepped at one frame rate cannot tell per-second from per-frame; step it at a second rate.
- **124** — An invariant between events is pinned only if checked between them, and a rare case only if the test forces it.

## Shell and harness traps

- **7** — `sys.exit()` in a `finally:` replaces the exception in flight; a harness broken and a subject broken must print different things. Catch `BaseException` before the `finally` and print it.
- **10** — `cmd | tail; echo $?` reports tail's status; capture the command's own exit code or it is decoration.
- **12** — A "must be" banner needs a comparison that can exit non-zero, and one stale path makes `git add` stage nothing. Do not trust `set -e` to enforce a precondition.
- **88** — A debug command that looks like a query can be a setter; read the handler before polling it.
- **89** — A behaviour change can delete the instrument you verify with; prefer an observable the subject cannot switch off.
- **91** — Under `pause` nothing eased on `Time.Delta` moves, so a paused grab can show the picture from before the work; step or resume before grabbing a camera move, a walk or an animation.
- **94** — Drain the reply queue before reading a reply, or you read the answer to an earlier command.
- **96** — `pkill -f PATTERN` kills the shell running it when the pattern is in its own command line.
- **98** — A debug selector can be read by a branch the subject never takes; assert the state the subject uses, not the one you set.
- **101** — A `trap … EXIT` restore with a relative path dies after a `cd` and still prints; use absolute paths and make the restore assert.
- **107** — A view steered by where the pointer is drifts while the clock runs; freeze it, and read the heading back and assert it.
- **108** — A facing-relative command means a different world direction at every heading; recompute your arguments after setting a heading.
- **111** — Read a command's units off its call site before aiming a capture, and check its reply agrees.
- **112** — Establish a facing from something you recognise in the frame, never from the formula alone.
- **126** — A census read on a timer lands between the pulses of anything that plays in pulses; read it on the event's own log line.
- **129** — `pkill -f` and `pgrep -f` match your own shell when the pattern is in your command; kill by pid.
- **130** — A Wine registry write can land in the wrong view or nowhere and say nothing: state `/reg:32` or `/reg:64` and check `system.reg`.

## Delegates and commissioned work

- **8** — A confident negative from an instrument never shown a positive is not evidence; read a task's failures before its result.
- **18** — Read every agent's output before acting, keep the critic stage, and save paid-for verification somewhere durable.

## Memory, documentation and deletion

- **40** — In a multi-branch repo a file's content belongs to a branch; measure which branch owns it before saying "the page doesn't say that".
- **57** — When a fact changes, grep the old claim and edit every sentence that states it, then read the block back in place.
- **60** — A correction is not automatically more accurate than what it replaces: measure what the new sentence asserts before writing it.
- **70** — A deletion instruction is a claim about a file and the file is the better witness: grep shows what is referenced, reading shows what is wanted.
- **72** — Before deleting a type, grep its name, not calls to it, and fix crefs and prose in the same commit.
- **78** — A comment and its code are one edit, code first; never take "the comment says so" as evidence the code does.
- **82** — Doc comments rot as "X is not built" outliving X; sweep for absence phrasings and date the negative with its reason.
- **115** — Editing a structured document by matching a fragment destroys structure; anchor on a boundary and count the headings after.

---

When adding a rule, give it the next free number, put its one line at the end of the group whose symptom it matches, and
its story under the same number and group in `history/verifying-stories.md`. Anchor on that group's last rule and add
after it - never on a trailing block you intend to keep, because a replacement silently drops whatever the anchor
matched. Then check that the numbers still run 1..N with no gaps or duplicates on both pages, and add it to "Start here"
only once it has bitten twice.
