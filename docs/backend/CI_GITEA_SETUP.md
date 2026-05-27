# Running CI on Gitea Actions

The workflow under `.github/workflows/migration-check.yml` was written against the GitHub Actions schema, but **Gitea Actions** (Gitea 1.19+) speaks the same dialect with a few caveats. This document walks through everything a team needs to go from "zero Gitea CI" to "PRs are blocked on migration drift just like on GitHub".

If you're using **GitHub** (not Gitea), you can stop reading — the workflow runs as-is.

---

## TL;DR

1. Enable Actions in Gitea (`app.ini` → `[actions] ENABLED=true`).
2. Create a runner registration token from Site Admin or Org / Repo settings.
3. Drop the token into a `.env`, run `docker compose -f tools/ci/gitea-runner.compose.yml up -d`.
4. Push a branch that touches `Migrations/` and watch the check turn green (or red and tell you what's wrong).

The full walk-through with troubleshooting is below.

---

## Prerequisites

| Requirement | Why | How to verify |
|---|---|---|
| Gitea **1.21+** | 1.19 introduced Actions; 1.21 made `DEFAULT_ACTIONS_URL=github` the default and stabilised service containers. | `gitea --version` on the server, or visit `/api/v1/version` in a browser. |
| Docker on the runner host | act_runner spawns job containers via the host's Docker daemon. | `docker version` on the host you'll run the runner on. |
| Network access from runner → Gitea | The runner polls Gitea's API for queued jobs. | `curl -sf https://gitea.example.com/api/v1/version` from the host. |
| (Optional) Network access from runner → github.com | Without it, `uses: actions/checkout@v4` etc. fail. Air-gapped Gitea works too — see §6. | `curl -sfI https://github.com` from the host. |

If any of these are missing, fix them first — the rest of this doc assumes they hold.

---

## 1. Enable Actions on the Gitea server

Edit Gitea's `app.ini` (location varies; commonly `/etc/gitea/app.ini` for systemd or inside the container's volume):

```ini
[actions]
ENABLED = true

# `github` lets workflows reference `uses: actions/checkout@v4` etc. and Gitea
# proxies the action's source from github.com on the runner's behalf.
# Use `self` if you're air-gapped (then you must mirror actions into Gitea
# — see §6).
DEFAULT_ACTIONS_URL = github
```

Restart Gitea:

```bash
systemctl restart gitea            # systemd install
# or
docker compose restart gitea       # docker install
```

Confirm Actions is on: open any repo → there should be a new **Actions** tab.

Each repo can also disable Actions individually via Repo Settings → Actions; the global flag above is the master switch.

---

## 2. Get a runner registration token

Tokens scope the runner to a single level:

| Scope | Path to create token | Best when |
|---|---|---|
| **Instance** | Site Admin → Actions → Runners → Create | One shared runner pool serves every repo on the instance. |
| **Organisation** | Org page → Settings → Actions → Runners → Create | Runners serve every repo in the org but not other orgs. |
| **Repository** | Repo → Settings → Actions → Runners → Create | Per-repo isolation; use when a workflow needs secrets only that repo should see. |

Pick a scope and save the token — it's shown **once** and the registration call below is one-shot.

---

## 3. Deploy the runner

Three deployment shapes; pick one.

### Option A — Docker Compose (recommended)

The fastest path. `tools/ci/gitea-runner.compose.yml` is a drop-in.

```bash
# 1. Create a .env file in the same directory as the compose file.
cat > tools/ci/.env <<EOF
GITEA_INSTANCE_URL=https://gitea.example.com
GITEA_RUNNER_REGISTRATION_TOKEN=<paste-token-here>
GITEA_RUNNER_NAME=ci-runner-1
EOF

# 2. Bring it up.
docker compose -f tools/ci/gitea-runner.compose.yml up -d

# 3. Tail logs to watch registration.
docker compose -f tools/ci/gitea-runner.compose.yml logs -f gitea-runner
```

Successful output ends with `runner: <name> is online`. In the Gitea UI under **Site Admin / Org / Repo → Actions → Runners** the runner appears with status **Idle** and label `ubuntu-latest`.

### Option B — Native binary

If you don't want Docker for the runner itself (you still need it for job containers):

```bash
# 1. Download the binary for your platform.
ARCH=$(uname -m | sed 's/x86_64/amd64/;s/aarch64/arm64/')
curl -L -o act_runner \
  "https://gitea.com/gitea/act_runner/releases/download/v0.2.10/act_runner-0.2.10-linux-${ARCH}"
chmod +x act_runner

# 2. Generate the default config and tune it.
./act_runner generate-config > runner-config.yaml
# (compare with tools/ci/runner-config.example.yaml for recommended values)

# 3. Register against your Gitea.
./act_runner register \
  --no-interactive \
  --instance https://gitea.example.com \
  --token <paste-token-here> \
  --name "$(hostname)-migration" \
  --labels "ubuntu-latest:docker://catthehacker/ubuntu:act-latest"

# 4. Run the daemon (use systemd / supervisord in production).
./act_runner daemon --config runner-config.yaml
```

### Option C — Systemd service (production)

Wrap Option B in a systemd unit so the runner survives reboots:

```ini
# /etc/systemd/system/act-runner.service
[Unit]
Description=Gitea Actions runner
Documentation=https://docs.gitea.com/usage/actions/act-runner
After=docker.service network-online.target
Requires=docker.service

[Service]
Type=simple
User=act-runner
WorkingDirectory=/var/lib/act-runner
ExecStart=/usr/local/bin/act_runner daemon --config /etc/act-runner/runner-config.yaml
Restart=on-failure
RestartSec=10s

# act_runner needs the docker socket; add the service user to the docker group
# instead of running as root. Auditable + minimal blast radius.
SupplementaryGroups=docker

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload
systemctl enable --now act-runner
systemctl status act-runner
```

---

## 4. Verify the runner picks up workflows

Open any PR that touches `src/Backend/Infrastructure/Persistence/Migrations/**`, or push a no-op change to `.github/workflows/migration-check.yml` to force a trigger. Within ~10 seconds:

1. The **Actions** tab on the repo shows a new run titled "Migration drift check".
2. The job's "Set up job" step prints the runner name and labels.
3. The `services.postgres` step pulls the image and starts a healthcheck loop.
4. The `Restore + build` step runs `dotnet restore` (cold cache: ~30s).
5. The `Run drift check (full mode)` step executes `tools/backend/migration/check.sh --full`.

A successful run finishes in **2-4 minutes**. The exact "Conclusion: success" event mirrors what GitHub Actions emits.

If something fails, jump to [§7 Troubleshooting](#7-troubleshooting).

---

## 5. Where the workflow file should live

Gitea reads **both** `.github/workflows/*.yml` and `.gitea/workflows/*.yml`. Choosing between them:

| Location | Pros | Cons |
|---|---|---|
| `.github/workflows/` (current) | Works on both GitHub and Gitea — same file, no duplication. Convenient when mirroring repos. | The folder name is a little misleading for a Gitea-only project. |
| `.gitea/workflows/` | Idiomatic for Gitea-only projects. Clear signal to readers that this is not a GitHub repo. | Breaks if the repo is ever migrated to GitHub without renaming the folder. |
| Both folders | Workflow runs **twice** per push — once via each path. Slower CI for no benefit. | Don't. |

**Recommendation**: leave the file at `.github/workflows/migration-check.yml` until the project is locked into Gitea forever. The migration is one `git mv` away.

---

## 6. Air-gapped Gitea (no GitHub access)

If your Gitea instance can't reach github.com — corporate firewall, classified network, regulatory reasons — `uses: actions/checkout@v4` will fail with "could not download action".

Two paths:

### 6a. Mirror the actions you need into Gitea

```bash
# On any machine with internet access:
for repo in checkout setup-dotnet upload-artifact; do
    git clone --mirror "https://github.com/actions/${repo}.git"
    # Push as a new Gitea repo under an `actions/` org.
    cd "${repo}.git"
    git remote set-url --push origin "https://gitea.example.com/actions/${repo}.git"
    git push --mirror
    cd -
done
```

Then set:

```ini
[actions]
DEFAULT_ACTIONS_URL = self
```

Gitea will resolve `uses: actions/checkout@v4` to `https://gitea.example.com/actions/checkout` at the `v4` ref.

### 6b. Use full URLs (single action only)

Inline a single mirror without changing global config:

```yaml
- uses: https://gitea.example.com/actions/checkout@v4
```

Works without `DEFAULT_ACTIONS_URL=self`, but every workflow that uses it must use the full URL — no half-measures.

---

## 7. Troubleshooting

### "Runner registered but stays in 'Pending' state"

The runner registered but never picked up a job. Most common causes:

1. **Label mismatch.** The workflow says `runs-on: ubuntu-latest`, but the runner registered with a different label. Check Gitea → Runners → click the runner → label list must include `ubuntu-latest`. Re-register with `--labels "ubuntu-latest:docker://catthehacker/ubuntu:act-latest"`.

2. **Runner can't reach Gitea.** Check `docker logs gitea-runner-migration` (or `journalctl -u act-runner`). Connection-refused / TLS errors → fix DNS / certificate on the runner host.

3. **Actions disabled on the repo.** Repo Settings → Actions → "Enable Repository Actions". Org-scoped runners only serve repos that have Actions enabled.

### "`uses: actions/checkout@v4` fails with 'action not found'"

`DEFAULT_ACTIONS_URL=github` is not set, the proxy is disabled, or the runner can't reach github.com. Confirm in Gitea's `app.ini`:

```ini
[actions]
DEFAULT_ACTIONS_URL = github
```

…and that `curl https://github.com` works from the **runner host** (not from your laptop).

### "`services.postgres` never becomes healthy"

The runner is using an old Docker that can't honour `healthcheck` in the service definition, or DNS resolution between job container and service container is broken.

Check:

```bash
docker --version           # need 20.10+
docker network ls          # the runner should have created a temp network per job
```

If healthchecks still don't fire, work around with an in-step wait:

```yaml
- name: Wait for Postgres
  run: |
    for i in {1..30}; do
      pg_isready -h localhost -p 5432 -U ci && break
      sleep 2
    done
```

### "Drift check passes locally but fails in CI"

The CI workflow runs `check.sh --full`, which adds:

- `dotnet build` against the merge result.
- `migrations has-pending-model-changes` (catches entity edits without a migration).
- `database update` against a fresh Postgres.

If only the **--full** mode flags an issue, the most likely cause is unmigrated entity code. Run locally:

```bash
./tools/backend/migration/check.sh --full
# or, faster:
dotnet ef migrations has-pending-model-changes \
  --project src/Backend/Infrastructure/Infrastructure.csproj \
  --startup-project src/Backend/Api/Api.csproj
```

If it reports changes, run `./tools/backend/migration/new.sh <Name>` and re-push.

### "Workflow runs forever / hangs at 'Setting up runner'"

The act_runner config `timeout` is set too high (or unset) and the runner is waiting on a slow image pull. Edit `tools/ci/runner-config.example.yaml`:

```yaml
runner:
  timeout: 30m   # hard limit; jobs above this get killed
```

For first-time pulls of `node:20-bookworm` + `postgres:16-alpine` + .NET SDK on a thin pipe, 5-10 minutes is normal. Subsequent runs reuse the cached images.

---

## 8. Security notes

The runner host has **root-equivalent power over the Docker daemon** (it can spawn arbitrary containers, mount any host path). Treat it accordingly:

1. **Isolate the runner host from production data and secrets.** A compromised workflow on the runner can read anything the host can read.
2. **Pin act_runner version** (the compose file already does this: `gitea/act_runner:0.2.10`). Auto-upgrading silently is fine for dev but not for CI infrastructure.
3. **Audit who can register runners.** Instance-level tokens grant access to every repo on the instance; prefer org- or repo-scoped tokens unless you're running a small team with full mutual trust.
4. **Don't store production secrets in the runner host's environment** — use Gitea's Secrets UI per-repo / per-org. Secrets injected via the UI live only in the job's environment and don't bleed onto the host.
5. **Network-restrict the runner** to only the destinations it needs:
   - Outbound to Gitea (job polling, log upload).
   - Outbound to github.com **only** if `DEFAULT_ACTIONS_URL=github`.
   - Outbound to package registries (NuGet, Docker Hub) — pin these to a private mirror if your security policy requires it.

---

## 9. Multi-runner setup (team scale)

When CI starts taking too long because jobs queue, deploy more runners. They're stateless — adding another container or another machine is the only step.

Two patterns:

1. **Same-host, horizontal**: bump `runner.capacity: 4` in the config and increase the compose container's `cpus` / `memory` allotments. Cheapest path; works until the host saturates.

2. **Multi-host**: deploy the compose file on a second machine with a different `GITEA_RUNNER_NAME`. Gitea balances queued jobs across all idle runners automatically.

For >5 runners or production-grade SLAs, look at the act_runner Kubernetes deployment instead of compose — see https://docs.gitea.com/usage/actions/act-runner#kubernetes.

---

## 10. Cross-references

- Workflow file: [`.github/workflows/migration-check.yml`](../../.github/workflows/migration-check.yml)
- Runner config example: [`tools/ci/runner-config.example.yaml`](../../tools/ci/runner-config.example.yaml)
- Runner compose: [`tools/ci/gitea-runner.compose.yml`](../../tools/ci/gitea-runner.compose.yml)
- Migration workflow (what the CI enforces): [`MIGRATION_WORKFLOW.md`](./MIGRATION_WORKFLOW.md)
- Developer onboarding (broader context): [`DEVELOPER_GUIDE.md`](./DEVELOPER_GUIDE.md)
- Upstream docs: https://docs.gitea.com/usage/actions/overview
