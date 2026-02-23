# 🧠 PR Brain

**PR Brain** is a context-aware GitHub Pull Request reviewer built by [CadenLabs](https://github.com/cadenlabs). It goes beyond just reading the diff — it cross-references the linked ticket, team standards, interface contracts, and test coverage to give you deep, actionable code reviews.

It ships as two integration surfaces:

| Mode | How it works |
|---|---|
| **GitHub Copilot Extension** (`PrBrain.Api`) | A Copilot Extension you chat with directly in GitHub Copilot (`@pr-brain review ...`) |
| **MCP Server** (`PrBrain.Mcp`) | A Model Context Protocol server that works inside VS Code (or any MCP-compatible client) |

---

## How it works

PR Brain assembles a rich review context before calling the AI model:

1. **PR metadata** — title, author, description
2. **Full diff** — every changed file
3. **Linked ticket** — auto-detected issue linked in the PR body
4. **Team standards** — reads `.github/review-brain.md` from the target repo
5. **Interface contracts** — fetches interface files (`I*.cs`) touched by the PR
6. **Test coverage** — fetches test files related to changed code

All of this is fed into a streaming `gpt-4o` call via [GitHub Models](https://github.com/marketplace/models).

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- A **GitHub Personal Access Token** (PAT) with `repo` read scope — used to call the GitHub API
- A **GitHub Models token** (can be the same PAT) — used to call `gpt-4o` via `https://models.inference.ai.azure.com`

---

## Project structure

```
pr-brain/
├── src/
│   ├── PrBrain.Api/          # GitHub Copilot Extension (ASP.NET Core API)
│   │   ├── Endpoints/        # Copilot chat endpoint + health check
│   │   ├── Middleware/       # GitHub request signature verification
│   │   ├── Models/           # Request/response and review context models
│   │   ├── Prompts/          # System prompts
│   │   └── Services/
│   │       ├── Ai/           # GitHub Models streaming client + review generator
│   │       ├── Context/      # PR context assembly (diff, ticket, standards, interfaces, tests)
│   │       └── GitHub/       # GitHub REST API wrapper (Octokit)
│   └── PrBrain.Mcp/          # MCP server (stdio transport)
│       └── Tools/            # PrReviewTool — exposes review_pr as an MCP tool
└── PrBrain.sln
```

---

## Option 1 — MCP Server (VS Code / Copilot Chat)

This is the quickest way to get started. The MCP server runs as a local process and exposes a `review_pr` tool that any MCP-compatible client can call.

### 1. Add to your VS Code `mcp.json`

Open your global MCP config (`Cmd+Shift+P` → **MCP: Open User Configuration**) and add:

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
        "GitHubModels__Token": "YOUR_GITHUB_MODELS_TOKEN",
        "GitHubModels__Model": "gpt-4o",
        "GitHubModels__Endpoint": "https://models.inference.ai.azure.com"
      }
    }
  }
}
```

> Replace `/absolute/path/to/pr-brain` with the actual path where you cloned this repo.  
> `GitHub__Token` and `GitHubModels__Token` can be the same GitHub PAT.

### 2. Use it in Copilot Chat

Once VS Code restarts (or you reload the MCP server), open Copilot Chat in **Agent mode** and run:

```
@pr-brain Review this PR: https://github.com/owner/repo/pull/42
```

or use the shorthand:

```
@pr-brain review owner/repo #42
```

---

## Option 2 — GitHub Copilot Extension (`PrBrain.Api`)

This deploys PR Brain as a hosted GitHub Copilot Extension that any GitHub user can invoke from the Copilot chat on GitHub.com or in their editor.

### 1. Configure secrets

The API needs two secrets. Set them via [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets) for local development:

```bash
cd src/PrBrain.Api

dotnet user-secrets set "GitHubModels:Token" "YOUR_GITHUB_MODELS_TOKEN"
```

Or set environment variables (for production / Docker):

```bash
export GitHubModels__Token="YOUR_GITHUB_MODELS_TOKEN"
export GitHubModels__Model="gpt-4o"                                      # optional, default
export GitHubModels__Endpoint="https://models.inference.ai.azure.com"   # optional, default
```

> The GitHub user token is forwarded automatically by the Copilot platform via the `X-GitHub-Token` header on every request — you do not need to configure it here.

### 2. Run locally

```bash
cd src/PrBrain.Api
dotnet run
```

The API starts at `http://localhost:5000`. Signature verification is **skipped** in `Development` mode so you can test with curl or Postman.

**Health check:**

```bash
curl http://localhost:5000/health
# {"status":"ok","service":"pr-brain"}
```

### 3. Register as a GitHub Copilot Extension

1. Go to **GitHub → Settings → Developer Settings → GitHub Apps** and create a new App.
2. Under **Copilot**, set the **Callback URL** to your publicly accessible API URL (e.g. via [ngrok](https://ngrok.com/) for local testing).
3. Install the GitHub App on your account or organisation.
4. Chat with `@pr-brain` in GitHub Copilot:

```
@pr-brain review https://github.com/owner/repo/pull/42
```

---

## Team standards file

PR Brain will look for a `.github/review-brain.md` file in the **target repository** (the repo containing the PR being reviewed). If found, its contents are injected into the review prompt as team standards.

Example `.github/review-brain.md`:

```markdown
## Our Engineering Standards

- All public methods must have XML doc comments
- No raw SQL — use the repository pattern only
- Every new service must have a corresponding unit test
- Prefer `IOptions<T>` over `IConfiguration` for settings
```

If the file is not present, PR Brain falls back to general engineering best practices.

---

## Configuration reference

| Key | Description | Required |
|---|---|---|
| `GitHubModels:Token` | GitHub PAT used to call GitHub Models (gpt-4o) | ✅ |
| `GitHubModels:Model` | Model name (default: `gpt-4o`) | ❌ |
| `GitHubModels:Endpoint` | Models endpoint (default: `https://models.inference.ai.azure.com`) | ❌ |
| `GitHub:Token` *(MCP only)* | GitHub PAT used to call the GitHub REST API | ✅ |

Environment variables use double-underscore as the separator: `GitHubModels__Token`, `GitHub__Token`.

---

## Building

```bash
# Restore + build the entire solution
dotnet build PrBrain.sln

# Run the API
dotnet run --project src/PrBrain.Api

# Run the MCP server (stdio — normally launched by the MCP client)
dotnet run --project src/PrBrain.Mcp
```

---

## License

[MIT](LICENSE)
