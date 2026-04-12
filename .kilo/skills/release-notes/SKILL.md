---
name: release-notes
description: Write release notes for Redisboard.NET from git history. Use this skill whenever the user asks to write release notes, changelog entries, release summaries, or version notes based on commits. Always ask for starting point first, using commit id or date, then read git history and build release notes from that range.
---

# Release Notes

Canonical shared guidance should live at `ai/skills/release-notes.md` if project later promotes this skill beyond Kilo-only use.

You write release notes from actual git history. Do not invent changes. Do not summarize code you did not verify in commit messages or git metadata.

## Standard Release Notes Shape

Standard release notes should answer four things fast:

1. What version or release this is.
2. What changed that matters to users.
3. What changed internally that matters to maintainers.
4. What range of commits or time window this note covers.

Default structure:

```md
## [Version or Release Name]

### Highlights
- Biggest user-facing improvement.
- Second most important change.
- Third most important change if needed.

### Added
- New features.

### Changed
- Behavior updates, refinements, workflow updates, documentation improvements.

### Fixed
- Bug fixes, validation fixes, broken examples, formatting corrections when relevant.

### Internal
- Test refactors, CI changes, chores, version bumps, non-user-facing maintenance.
```

Rules:
- Lead with user-facing value.
- Group similar commits into one note when they describe same theme.
- Keep raw commit language out of final note when better product language exists.
- Preserve accuracy. If commit scope is unclear, stay conservative.
- Do not create empty sections unless user asks for full template.
- Mention docs-only release clearly if most commits are documentation.

## Required Workflow

When user asks for release notes:

1. Ask exactly one targeted question first:
   - "From which commit or date should I start reading history?"
2. Accept either:
   - commit SHA or short SHA
   - date like `2026-04-01`
   - relative reference like `last tag` only if user says so
3. After user answers, run git commands to collect history from that point to `HEAD` on current branch.
4. Read commit subjects first. If needed, inspect commit bodies for ambiguous commits.
5. Build release notes from verified history only.

## Git Commands

Preferred patterns:

- If start point is commit:
  - `git log <start>..HEAD --pretty=format:'%H%x09%ad%x09%s' --date=iso-strict`
- If start point is date:
  - `git log --since='<date> 00:00:00' --pretty=format:'%H%x09%ad%x09%s' --date=iso-strict`
- To include bodies for ambiguous commits:
  - `git show --stat --summary <sha>`
  - `git log --format=fuller -n 1 <sha>`

If branch name matters, get it with:

- `git rev-parse --abbrev-ref HEAD`

## Classification Rules

Map commits into release note buckets:

- `feat:` -> `Highlights` and usually `Added`
- `fix:` -> `Fixed`
- `docs:` -> `Changed` unless docs-only release, then make that explicit in `Highlights`
- `test:` -> `Internal` unless test change fixes public regression and commit says so
- `chore:` -> `Internal`
- version bump -> `Internal` or final line in intro if release version obvious
- CI/workflow changes -> `Internal` unless directly affects package consumers

Combine repetitive commits:

- Multiple README/doc cleanup commits -> one bullet
- Several test cleanup commits -> one bullet
- Feature commit plus follow-up docs commit -> one feature bullet and maybe one docs bullet if docs materially improved usage

## Writing Rules

- Write normal prose. Do not use caveman mode inside final release notes.
- Be concise. Usually 5-12 bullets total.
- Prefer user language over implementation language.
- Mention technical identifiers like `AddLeaderboard`, `IDatabase`, `IConnectionMultiplexer` when they matter to consumers.
- Do not copy commit messages verbatim if they are long, awkward, or too implementation-heavy.
- If commit history suggests no user-facing changes, say that directly.

## Output Format

Default behavior:

1. Write release notes to separate markdown file in repository root.
2. File should contain only final release notes content. No range summary. No commit appendix. No extra commentary.
3. File name should be stable and date-oriented unless user provides exact target name.

Preferred file naming:

- `release-notes-YYYY-MM-DD.md` for date-based requests
- `release-notes-<version>.md` if release version is explicit and safer than date

Chat response should stay minimal:

- confirm file path written
- optionally include 1-2 lines noting commit range used

Only print release notes inline in chat if user explicitly asks for inline output instead of file output.

Example:

```md
## Redisboard.NET 1.0.2

### Highlights
- Expanded dependency injection support in `AddLeaderboard`, including better Redis service fallback behavior.
- Improved usage documentation for ranking flows and service registration.

### Changed
- Clarified README examples and ranking guidance.
- Documented default batch size limits and DI extension behavior more clearly.

### Internal
- Updated publish workflow to use `artifacts` output.
- Improved test consistency with Arrange-Act-Assert structure.
- Bumped package version to `1.0.2`.
```

## Guardrails

- Always ask for start commit or date first. No exceptions.
- Never fabricate scope beyond git evidence.
- If commit messages are too vague, say so and offer conservative notes.
- If no commits exist in requested range, say that directly and do not generate fake content.
- Default deliverable is markdown file containing only release notes body.
