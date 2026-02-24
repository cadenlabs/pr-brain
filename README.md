# 🧠 PR Brain

Context-aware PR reviews inside VS Code Copilot Chat.

Most PR reviews catch nullable warnings and spelling mistakes. PR Brain reads the **full picture** before it says anything — the diff, the linked ticket, your team's standards, the interface contracts the code must honour, and the test files that already exist.

Built by [CadenLabs](https://github.com/cadenlabs). Runs as a local MCP server, zero cloud setup.

---

## What a review looks like

```
### 🔴 Critical Issues
PaymentService.ProcessAsync does not wrap the charge + order insert in a
transaction. If the charge succeeds but the insert fails, the customer is
billed with no order created.

### 🟡 Important Issues
Ticket #204 requires idempotency support — this PR adds no idempotency key
check on POST /payments.

### 🟢 Test Coverage Gaps
No test covers the case where the external payment gateway returns a 429.

### 💡 Suggestions
Consider moving the retry policy into a shared Polly extension rather than
inlining it here — three other services do the same thing.

### ✅ What's Good
Clean separation between the gateway adapter and the domain service.
The new DTO mapping is consistent with the rest of the codebase.

### 📋 Ticket Coverage
Ticket #204 asked for idempotent payments and a receipt email on success.
The email dispatch is implemented. The idempotency key is missing.
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- VS Code + [GitHub Copilot](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot) (Chat, Agent mode)
- A **GitHub Personal Access Token** with `repo` read scope
  → [Create one here](https://github.com/settings/tokens) (classic or fine-grained)

The same PAT works for both the GitHub API and GitHub Models.

---

## Setup

### 1. Clone and build

```bash
git clone https://github.com/cadenlabs/pr-brain.git
cd pr-brain
dotnet build
```

### 2. Configure VS Code

Open your MCP config — `Cmd+Shift+P` → **MCP: Open User Configuration** — and add:

```json
{
  "servers": {
    "pr-brain": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/absolute/path/to/pr-brain/src/PrBrain.Mcp"
      ],
      "env": {
        "GitHub__Token": "YOUR_GITHUB_PAT",
        "GitHubModels__Token": "YOUR_GITHUB_PAT",
        "GitHubModels__Model": "gpt-4o",
        "GitHubModels__Endpoint": "https://models.inference.ai.azure.com"
      }
    }
  }
}
```

Replace `/absolute/path/to/pr-brain` with the actual path on your machine.
`GitHub__Token` and `GitHubModels__Token` can be the same PAT.

### 3. Restart the MCP server

`Cmd+Shift+P` → **MCP: Restart Server** → `pr-brain`

---

## Usage

Open Copilot Chat in **Agent mode** and ask:

```
Review this PR: https://github.com/owner/repo/pull/42
```

or shorthand:

```
Review owner/repo #42
```

That's it.

---

## Team standards (optional but recommended)

Add a `.github/review-brain.md` file to any repo you want PR Brain to review. It will be fetched automatically and injected into every review for that repo.

A starter template is in [`.github/review-brain.md`](.github/review-brain.md) — copy it into your repo and edit it to match your team's rules.

If the file doesn't exist, PR Brain falls back to general engineering best practices.

---

## How the context is assembled

PR Brain makes several API calls in parallel before generating a single token:

| Layer | What it fetches |
|---|---|
| 1 | PR metadata (title, author, description) |
| 2 | Full diff |
| 3 | Linked ticket — auto-detected from `closes #N` / `fixes #N` in the PR body |
| 4 | `.github/review-brain.md` from the target repo |
| 5 | Interface files (`I*.cs`) touched by the PR |
| 6 | Test files related to changed code |

All of it goes into one prompt. `gpt-4o` via [GitHub Models](https://github.com/marketplace/models) does the rest.

---

## Configuration reference

| Environment variable | Description | Default |
|---|---|---|
| `GitHub__Token` | PAT for GitHub REST API calls | required |
| `GitHubModels__Token` | PAT for GitHub Models (gpt-4o) | required |
| `GitHubModels__Model` | Model name | `gpt-4o` |
| `GitHubModels__Endpoint` | Models endpoint | `https://models.inference.ai.azure.com` |

---

## License

[MIT](LICENSE)
