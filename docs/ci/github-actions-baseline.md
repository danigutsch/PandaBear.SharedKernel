# GitHub Actions timing baseline

This snapshot records SharedKernel's hosted workflow and job timing before the solution-scope and CI reuse changes.
It covers all 38 repository workflow runs created during the fixed UTC interval on November 25, 2026,
from 19:15:00 inclusive to 20:45:00 exclusive. The workflow list was observed at 2026-11-25T20:47:34Z.
There were 35 pull request runs and three push runs across four workflows. All were terminal with one
attempt each. The push runs reported one trunk revision. This 90-minute sample contains a concentrated
native-stack review wave. Use comparable triggers and runner labels when comparing later changes.
[#267](https://github.com/danigutsch/PandaBear.SharedKernel/issues/267)

The 202 job records include 141 assigned jobs, 25 skipped jobs and 36 cancellations before assignment.
Of the assigned jobs, 128 succeeded, four failed and nine were cancelled. Assigned jobs used 456.20
elapsed runner minutes overall. Cancelled workflow runs accounted for 55.05 minutes.

## Measurement

- Run counts use distinct GitHub run IDs grouped by workflow and trigger event. A replacement created by a
  new pull request event is a separate run. A manual rerun keeps its run ID and increases `run_attempt`.
- Workflow wall time is the terminal run's `updated_at` minus `created_at`. All records in
  this snapshot were single-attempt runs and reported the same `run_started_at` and creation time.
  Success-only medians exclude cancelled runs so cancellation does not make completed runs appear faster.
- Job records report the exact job and matrix names. Times are reported for jobs with a positive `runner_id`.
  Job wall time uses `completed_at − created_at` and includes time waiting after the job was created.
  Runner elapsed uses `completed_at − started_at`. This measures elapsed time on the assigned runner.
  A workflow's runner total sums these intervals across its jobs, including overlapping parallel jobs.
- Skips and cancellations before assignment count as observed job records and consume no measured runner time.
  Assigned jobs retain their elapsed time whether they passed, failed or were cancelled.
- Compare equal workflow and job definitions when measuring a later change. Keep trigger counts by run and
  retain every job attempt if a rerun occurs. For elapsed time, report originally triggered run completion
  and each attempt separately when reruns occur.

Use GitHub's repository workflow-run list filtered by the time window, then each run attempt's job list.
Apply the half-open window to `created_at` locally. Job IDs link to the corresponding GitHub run logs.
[Workflow runs API](https://docs.github.com/en/rest/actions/workflow-runs#list-workflow-runs-for-a-repository) ·
[Workflow jobs API](https://docs.github.com/en/rest/actions/workflow-jobs#list-jobs-for-a-workflow-run-attempt)

## Workflow summary

Times are minutes. Counts in parentheses are observed runs per hour. Runner minutes for cancelled runs are
included in the total. Wall median and range include all terminal runs.

| Workflow | PR / push | Runs (per hour) | Pass / fail / cancel | Wall median (range) | Success wall median | Runner total | Runner for cancelled | Example |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Actionlint | 0 / 1 | 1 (0.67) | 1 / 0 / 0 | 0.23 (0.23–0.23) | 0.23 | 0.17 | 0.00 | [Run 45683907384](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907384) |
| PR Metadata | 13 / 0 | 13 (8.67) | 7 / 0 / 6 | 1.33 (0.03–2.00) | 1.52 | 11.25 | 1.52 | [Run 45683580567](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580567) |
| Security baseline | 11 / 1 | 12 (8.00) | 8 / 0 / 4 | 3.23 (0.03–5.28) | 3.72 | 63.13 | 2.18 | [Run 45683580316](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580316) |
| SharedKernel validation | 11 / 1 | 12 (8.00) | 7 / 0 / 5 | 14.22 (0.03–19.15) | 16.63 | 381.65 | 51.35 | [Run 45683907024](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907024) |

## Job summary

Counts in parentheses are recorded job appearances per hour. Pass, fail, cancel and skip columns use
GitHub conclusions. Wall and runner medians use assigned jobs only. A dash identifies a job with no
assigned runner in the sample.

Repository quality, library validation and Windows package consumption used the most runner minutes in
this window. They accounted for 90.98, 72.92 and 56.22 minutes respectively.

### Actionlint

| Job | PR / push | Records (per hour) | Assigned | Pass / fail / cancel / skip | Wall median | Runner median | Runner total | Example |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Actionlint / Actionlint | 0 / 1 | 1 (0.67) | 1 | 1 / 0 / 0 / 0 | 0.22 | 0.17 | 0.17 | [Job 139451322373](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907384/job/139451322373) |

### PR Metadata

| Job | PR / push | Records (per hour) | Assigned | Pass / fail / cancel / skip | Wall median | Runner median | Runner total | Example |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PR Metadata / Validate squash header | 12 / 0 | 12 (8.00) | 9 | 7 / 0 / 5 / 0 | 1.42 | 1.30 | 11.25 | [Job 139450324847](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580567/job/139450324847) |

### Security baseline

| Job | PR / push | Records (per hour) | Assigned | Pass / fail / cancel / skip | Wall median | Runner median | Runner total | Example |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| baseline (ubuntu-latest) | 11 / 1 | 12 (8.00) | 9 | 8 / 0 / 4 / 0 | 2.03 | 1.88 | 16.05 | [Job 139450336188](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580316/job/139450336188) |
| baseline (windows-latest) | 11 / 1 | 12 (8.00) | 9 | 8 / 0 / 4 / 0 | 3.57 | 3.50 | 27.98 | [Job 139450336611](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580316/job/139450336611) |
| secrets | 11 / 1 | 12 (8.00) | 9 | 8 / 0 / 4 / 0 | 2.48 | 2.18 | 19.10 | [Job 139450335962](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580316/job/139450335962) |

### SharedKernel validation

| Job | PR / push | Records (per hour) | Assigned | Pass / fail / cancel / skip | Wall median | Runner median | Runner total | Example |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| candidate | 10 / 1 | 11 (7.33) | 8 | 8 / 0 / 3 / 0 | 5.49 | 4.63 | 38.23 | [Job 139451206263](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139451206263) |
| coverage-gate | 10 / 1 | 11 (7.33) | 5 | 5 / 0 / 4 / 2 | 2.62 | 2.57 | 14.17 | [Job 139460523109](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45684357962/job/139460523109) |
| linux-package-consumption | 10 / 1 | 11 (7.33) | 8 | 8 / 0 / 3 / 0 | 2.42 | 2.38 | 18.95 | [Job 139453685000](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139453685000) |
| published-candidate-comparison | 10 / 1 | 11 (7.33) | 0 | 0 / 0 / 0 / 11 | — | — | 0.00 | [Job 139449860165](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683492077/job/139449860165) |
| published-candidate-consumption | 10 / 1 | 11 (7.33) | 0 | 0 / 0 / 0 / 11 | — | — | 0.00 | [Job 139449859702](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683492077/job/139449859702) |
| repository-quality | 10 / 1 | 11 (7.33) | 8 | 7 / 0 / 4 / 0 | 11.07 | 10.35 | 90.98 | [Job 139452195766](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907024/job/139452195766) |
| selection | 10 / 1 | 11 (7.33) | 9 | 8 / 0 / 3 / 0 | 1.48 | 1.35 | 12.27 | [Job 139450383449](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139450383449) |
| SharedKernel Validation | 10 / 1 | 11 (7.33) | 11 | 7 / 4 / 0 / 0 | 0.10 | 0.05 | 0.60 | [Job 139461227731](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907024/job/139461227731) |
| source-build | 3 / 0 | 3 (2.00) | 0 | 0 / 0 / 3 / 0 | — | — | 0.00 | [Job 139450319267](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683492077/job/139450319267) |
| source-build (ubuntu-latest, SharedKernel.CI.Tool) | 7 / 1 | 8 (5.33) | 8 | 8 / 0 / 0 / 0 | 1.45 | 1.09 | 9.35 | [Job 139451206451](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139451206451) |
| source-build (ubuntu-latest, SharedKernel.RepoConfig.Tool) | 7 / 1 | 8 (5.33) | 8 | 8 / 0 / 0 / 0 | 1.74 | 1.32 | 11.00 | [Job 139451206415](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139451206415) |
| source-build (windows-latest, SharedKernel.CI.Tool) | 7 / 1 | 8 (5.33) | 8 | 8 / 0 / 0 / 0 | 2.02 | 1.57 | 13.05 | [Job 139451206517](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139451206517) |
| source-build (windows-latest, SharedKernel.RepoConfig.Tool) | 7 / 1 | 8 (5.33) | 8 | 8 / 0 / 0 / 0 | 2.06 | 1.24 | 12.15 | [Job 139451206812](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139451206812) |
| validate | 4 / 0 | 4 (2.67) | 0 | 0 / 0 / 3 / 1 | — | — | 0.00 | [Job 139450318920](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683492077/job/139450318920) |
| validate (browser) | 3 / 1 | 4 (2.67) | 4 | 4 / 0 / 0 / 0 | 3.58 | 3.54 | 14.13 | [Job 139451206396](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139451206396) |
| validate (library) | 6 / 1 | 7 (4.67) | 7 | 6 / 0 / 1 / 0 | 11.23 | 9.37 | 72.92 | [Job 139452195860](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907024/job/139452195860) |
| validate (services) | 3 / 1 | 4 (2.67) | 4 | 4 / 0 / 0 / 0 | 4.50 | 4.45 | 17.63 | [Job 139451206360](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301/job/139451206360) |
| windows-package-consumption | 10 / 1 | 11 (7.33) | 8 | 7 / 0 / 4 / 0 | 7.13 | 7.05 | 56.22 | [Job 139455424363](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907024/job/139455424363) |

## Run inventory

This inventory includes all in-window run IDs and the exact evidence links for reproducing the sample.

| Workflow | Event | Created UTC | Conclusion | Wall min | Runner min | Evidence |
| --- | --- | --- | --- | --- | --- | --- |
| SharedKernel validation | `pull_request` | 19:16:07 | cancelled | 0.93 | 0.80 | [Run 45683492077](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683492077) |
| Security baseline | `pull_request` | 19:16:07 | cancelled | 0.82 | 2.18 | [Run 45683492115](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683492115) |
| PR Metadata | `pull_request` | 19:16:07 | cancelled | 0.82 | 0.75 | [Run 45683492137](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683492137) |
| PR Metadata | `pull_request` | 19:16:39 | cancelled | 0.03 | 0.00 | [Run 45683578625](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683578625) |
| SharedKernel validation | `pull_request` | 19:16:40 | cancelled | 14.78 | 50.43 | [Run 45683580301](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580301) |
| Security baseline | `pull_request` | 19:16:40 | success | 4.07 | 7.70 | [Run 45683580316](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580316) |
| PR Metadata | `pull_request` | 19:16:40 | success | 1.73 | 1.42 | [Run 45683580567](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683580567) |
| SharedKernel validation | `push` | 19:18:38 | success | 17.25 | 58.85 | [Run 45683907024](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907024) |
| Security baseline | `push` | 19:18:38 | success | 2.90 | 6.42 | [Run 45683907164](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907164) |
| Actionlint | `push` | 19:18:38 | success | 0.23 | 0.17 | [Run 45683907384](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45683907384) |
| SharedKernel validation | `pull_request` | 19:21:18 | success | 18.12 | 61.05 | [Run 45684357962](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45684357962) |
| Security baseline | `pull_request` | 19:21:18 | success | 3.55 | 8.77 | [Run 45684358266](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45684358266) |
| PR Metadata | `pull_request` | 19:21:18 | success | 1.37 | 1.30 | [Run 45684358371](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45684358371) |
| PR Metadata | `pull_request` | 19:29:40 | cancelled | 0.03 | 0.00 | [Run 45685728848](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685728848) |
| Security baseline | `pull_request` | 19:29:40 | cancelled | 0.03 | 0.00 | [Run 45685728865](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685728865) |
| SharedKernel validation | `pull_request` | 19:29:40 | cancelled | 0.15 | 0.07 | [Run 45685728916](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685728916) |
| SharedKernel validation | `pull_request` | 19:29:40 | success | 13.65 | 30.53 | [Run 45685729144](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729144) |
| PR Metadata | `pull_request` | 19:29:40 | success | 1.52 | 1.45 | [Run 45685729145](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729145) |
| Security baseline | `pull_request` | 19:29:40 | success | 3.58 | 6.87 | [Run 45685729152](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729152) |
| Security baseline | `pull_request` | 19:29:40 | cancelled | 0.03 | 0.00 | [Run 45685729300](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729300) |
| SharedKernel validation | `pull_request` | 19:29:40 | cancelled | 0.38 | 0.05 | [Run 45685729413](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729413) |
| SharedKernel validation | `pull_request` | 19:29:40 | cancelled | 0.03 | 0.00 | [Run 45685729523](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729523) |
| Security baseline | `pull_request` | 19:29:40 | cancelled | 0.05 | 0.00 | [Run 45685729564](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729564) |
| PR Metadata | `pull_request` | 19:29:40 | cancelled | 0.03 | 0.00 | [Run 45685729569](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729569) |
| PR Metadata | `pull_request` | 19:29:40 | cancelled | 0.05 | 0.00 | [Run 45685729629](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685729629) |
| SharedKernel validation | `pull_request` | 19:29:40 | success | 15.13 | 40.93 | [Run 45685730522](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685730522) |
| Security baseline | `pull_request` | 19:29:40 | success | 2.52 | 7.10 | [Run 45685730526](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685730526) |
| PR Metadata | `pull_request` | 19:29:41 | success | 1.33 | 1.22 | [Run 45685730711](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685730711) |
| SharedKernel validation | `pull_request` | 19:29:41 | success | 19.15 | 53.38 | [Run 45685731093](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685731093) |
| Security baseline | `pull_request` | 19:29:41 | success | 3.85 | 7.97 | [Run 45685731279](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685731279) |
| PR Metadata | `pull_request` | 19:29:41 | success | 1.82 | 1.40 | [Run 45685731440](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685731440) |
| Security baseline | `pull_request` | 19:29:41 | success | 5.28 | 7.62 | [Run 45685732571](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685732571) |
| SharedKernel validation | `pull_request` | 19:29:41 | success | 16.63 | 42.95 | [Run 45685732631](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685732631) |
| PR Metadata | `pull_request` | 19:29:41 | success | 1.47 | 1.28 | [Run 45685732774](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45685732774) |
| SharedKernel validation | `pull_request` | 20:30:15 | success | 13.30 | 42.60 | [Run 45695572072](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45695572072) |
| PR Metadata | `pull_request` | 20:30:15 | cancelled | 0.83 | 0.77 | [Run 45695572144](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45695572144) |
| Security baseline | `pull_request` | 20:30:15 | success | 3.90 | 8.52 | [Run 45695572317](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45695572317) |
| PR Metadata | `pull_request` | 20:30:49 | success | 2.00 | 1.67 | [Run 45695670505](https://github.com/danigutsch/PandaBear.SharedKernel/actions/runs/45695670505) |

Use these workflow and job definitions when comparing the later changes.
[Solution scope #268](https://github.com/danigutsch/PandaBear.SharedKernel/issues/268) ·
[CI efficiency #272](https://github.com/danigutsch/PandaBear.SharedKernel/issues/272)
