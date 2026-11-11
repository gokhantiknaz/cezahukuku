# Git

> Git kuralları (bootstrap 2026-11-11). Tek geliştirici, lite mod; PR yok, SHIP onayıyla `main`'e birleştirme.

## Branching
- `feature/<spec-no>-<short-name>` — **no branch without a spec.** Also for CHANGE-lane mini-specs.
  Dal adları İngilizce ya da ASCII Türkçe, kısa: `feature/0001-lecture-note-files`.
- Refactors: `refactor/<spec-no>-<short-name>` (mini-spec with Changed behavior = none).
- Fixes: `fix/<report-id>-<short-name>` (bug fixes carry a report, not a spec number);
  incidents: `incident/<date>-<short-name>`; trivial changes: `trivial/<short-name>` (see below).
- Bootstrap: `chore/bootstrap` — the bootstrap output (`workflows/bootstrap.md`), merged like
  any branch.

## Commits
- Conventional Commits, with a plan reference. Önek ve kapsam İngilizce/ASCII, açıklama **Türkçe**:
  `feat(ders-notu): belge ekleme [plan 0001/3]`.
  Review fixes name their fix round instead of the step:
  `fix(catalog): empty page size [plan 0001/fix-2]` — the reviewer counts rounds from these
  (R-06 after round 3).
- Agent commits follow the same standard: the agent writes the message, the human approves it
  through the gate before the commit, not one by one — an approved plan covers its
  `[plan NNNN/n]` and `[plan NNNN/fix-k]` commits, the ship approval the ship commit. Every
  message stays visible in the branch and the PR. Lite and strict alike.
- Only `./scripts/check`'s own exit code gates a commit or a push. Never pipe its output into
  another command in the chain that decides it: in `./scripts/check | grep … && git commit` the
  chain takes grep's exit code, so a red check commits. Keep the output aside and gate on check
  alone: `./scripts/check > check.log 2>&1 && git commit …` (then show `check.log`).
- Commit named paths only: `git commit -- <paths>`, never `git add -A`, `git add .` or
  `commit -a`. Another session or tool may have written or staged files in the same tree.

## Forbidden
- Direct commits to the default branch (`main`). `main`'e yalnız SHIP onayından sonra `git merge --no-ff <dal>` ile
  birleştirilir ve push edilir.
- Force push, history rewriting on shared branches. Undo = `git revert` (see recovery R-11).

## Trivial changes
<!-- Set at bootstrap. Applies only to requests the change-request triage rubric
     (workflows/change-request.md) classifies as TRIVIAL: no acceptance criterion changes, no shared code. -->
- Policy: **(2) direct commit allowed on a `trivial/<short-name>` branch** (bootstrap 2026-11-11). Tek dar commit,
  `scripts/check` yeşil; ajan değişikliği insana gösterir, insanın onayıyla `main`'e `--no-ff` birleştirilir.
  (Alternatifler: (1) PR + bir inceleyici — PR kullanılmadığı için seçilmedi; (3) her şey mini-spec'ten geçer.)
- Always: one narrow commit, `scripts/check` green. A second file or a new test means re-triage.

## Pull requests
- Şimdilik zorunlu değil (tek geliştirici, makinede `gh` yok). Birleştirme SHIP onayıyla yerelde `--no-ff`; CI
  (`.github/workflows/check.yml`) her push'ta `scripts/check` ve `scripts/doctor --strict` çalıştırır.
- Ekip büyürse: PR zorunlu, `.github/pull_request_template.md` kontrol listesi, CI yeşil, `main` korumalı + CODEOWNERS.
