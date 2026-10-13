# SwiftlyS2 Docs

[![Build API Docs](https://img.shields.io/github/actions/workflow/status/swiftly-solution/swiftlys2.net-docs/build-api-docs.yml?label=api%20docs)](https://github.com/swiftly-solution/swiftlys2.net-docs/actions/workflows/build-api-docs.yml)
[![Update Docs](https://img.shields.io/github/actions/workflow/status/swiftly-solution/swiftlys2.net-docs/update-docs-agent.yml?label=docs%20agent)](https://github.com/swiftly-solution/swiftlys2.net-docs/actions/workflows/update-docs-agent.yml)
[![Discord](https://img.shields.io/discord/1178027657594687608?color=7289da&logo=discord&logoColor=white)](https://swiftlys2.net/discord)

Source of the documentation published at [swiftlys2.net](https://swiftlys2.net): guides, plugin development pages, resource references and the generated C# API reference for [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2).

Pages are MDX. A scheduled agent keeps them in sync with the SwiftlyS2 source, and the API reference is regenerated for both the `stable` (`master`) and `beta` branches whenever SwiftlyS2 changes.

## Repository layout

| Path                    | Contents                                                                                                                      |
| ----------------------- | ----------------------------------------------------------------------------------------------------------------------------- |
| `docs/`                 | The documentation pages (`.mdx`), grouped into categories and ordered by `_meta.json` files.                                  |
| `api/`                  | Generated API reference, `stable.yaml` and `beta.yaml`. Produced by the *Build API Docs* workflow, do not edit by hand.        |
| `tools/docs-agent/`     | Agent that updates one docs page at a time from the SwiftlyS2 source. `pages.json` lists the pages it maintains.              |
| `.github/workflows/`    | `build-api-docs.yml` and `update-docs-agent.yml`.                                                                             |

## docs/ layout

```
docs/
├── _meta.json            # top-level navigation
├── index.mdx             # introduction
├── installation.mdx
├── guides/               # terminologies, chat and HTML styling, dependency injection, porting from CSS
├── development/          # plugin API: commands, events, menus, database, entities, ...
└── resources/            # server-side references: core config, CLI options, console filter, command overrides
```

Each folder has a `_meta.json` that sets page order and titles. A page entry points at its file, and a category entry points at a subfolder:

```json
{
    "commands": { "page": "commands.mdx" },
    "development": { "category": "development", "title": "Development" }
}
```

New pages need an entry in the folder's `_meta.json` and a `title` in the frontmatter:

```mdx
---
title: Scheduler
---
```

Pages in `development/` reference API types with the `ApiRef` component, for example `<ApiRef name="ISchedulerService" />` or `<ApiRef name="ISwiftlyCore" member="Scheduler" />`. Use it instead of plain backticks when a type or member exists in the API reference.

## API reference

`build-api-docs.yml` runs on `workflow_dispatch` and on the `swiftlys2-updated` repository dispatch. For both `master` (`stable`) and `beta` it:

1. Checks out SwiftlyS2 and runs `docfx metadata` with the project's `docfx.json`.
2. Compresses the output with `tools/dotnet-yaml-api-compressor` into `api/<label>.yaml`.
3. Commits the result as `docs(api): update <label> API reference`.

## Docs agent

`tools/docs-agent` is a .NET tool that drives a tool-calling LLM (DeepSeek) to bring one page in line with the SwiftlyS2 source. It can read and grep the source, read other docs pages to copy their tone, and write the page it was given. It never invents options: anything it documents has to be verified in code. Features present on `beta` but not on `stable` go into a final `## Beta Changes` section.

### Pages

`tools/docs-agent/pages.json` is a list of pages the agent maintains:

| Field      | Description                                                                                                                      |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `slug`     | Unique id, also the key for change detection.                                                                                    |
| `page`     | Docs page to update, for example `docs/development/commands.mdx`.                                                                |
| `upstream` | `true` lets the agent also edit example configs under `plugin_files/configs/` in SwiftlyS2. Mutually exclusive with `scope`.     |
| `scope`    | Comma-separated source folders or files the agent can see. Used to restrict development pages to the public API.                |
| `task`     | Instructions for this page: what to focus on and what to verify.                                                                 |
| `watch`    | Source paths the page depends on. The run is skipped when they (on `stable` and `beta`) and the `task` are unchanged.           |

If `page` does not exist yet, the agent creates it and adds it to its folder's `_meta.json`. This is how features new on `beta` get their pages.

Development pages use `scope: managed/src/SwiftlyS2.Shared`, so the agent only sees the public plugin API and does not describe internals. Resource pages see the native C++ source.

### Running locally

Requires the **.NET 10 SDK** and a `DEEPSEEK_API_KEY`.

```bash
git clone https://github.com/swiftly-solution/swiftlys2.git swiftlys2-src
git clone -b beta https://github.com/swiftly-solution/swiftlys2.git swiftlys2-beta

export DEEPSEEK_API_KEY=...
dotnet run --project tools/docs-agent -- \
  --page docs/development/scheduler.mdx \
  --src swiftlys2-src --beta-src swiftlys2-beta \
  --scope managed/src/SwiftlyS2.Shared \
  --task "Focus: ISchedulerService, ITimerContext and TimerStep." \
  --summary summary.md
```

Run it from the repository root, since `--page` is a path relative to the working directory and the agent reads other pages from `docs/`. `--summary` receives a short markdown description of the change. The page is only rewritten when something is out of date, and with `--slug`/`--hash-file` an unchanged run exits without writing a summary.

| Option                       | Description                                                                                  |
| ---------------------------- | -------------------------------------------------------------------------------------------- |
| `--page <mdx>`               | Page to update (required).                                                                   |
| `--src <dir>`                | SwiftlyS2 checkout of the stable branch (required).                                          |
| `--beta-src <dir>`           | SwiftlyS2 checkout of the beta branch, used for the `## Beta Changes` section.               |
| `--task <text>`              | What the agent should do (required).                                                         |
| `--summary <file>`           | Where to write the summary (required).                                                       |
| `--scope <dir,dir,...>`      | Restrict every source tool to these repo-relative paths.                                     |
| `--allow-upstream`           | Allow edits to `plugin_files/configs/` in the source checkout.                               |
| `--slug <id> --hash-file <json> [--watch a,b,c] [--force]` | Skip the run when nothing the page depends on has changed. `--force` ignores the stored hash. |

Environment: `DEEPSEEK_API_KEY` (required), `DEEPSEEK_MODEL` (default `deepseek-flash`), `DEEPSEEK_API_URL`.

### Automation

`update-docs-agent.yml` runs on `workflow_dispatch` (with a `force` input) and on the `swiftlys2-updated` repository dispatch. It goes through every entry of `pages.json`, then opens one pull request on this repository (`docs: sync resource pages with SwiftlyS2 source`) and one on SwiftlyS2's `beta` branch for any example config the agent corrected. The branch is `docs-agent/sync`. Generated changes should always be reviewed before merging.

## Contributing

Fixes and new pages are welcome.

1. Edit or add `.mdx` files under `docs/`, and update the folder's `_meta.json` for new pages.
2. Match the tone and structure of neighbouring pages: short sentences, tables for options, fenced code with a language.
3. Only document what the source supports. Check the code samples compile against the current API.
4. Use [Conventional Commits](https://www.conventionalcommits.org/), for example `docs: ...` or `feat(docs): ...`.
5. Open a pull request.

Formatting follows `.prettierrc` (4 spaces, no trailing commas).

If a page maintained by the agent is wrong, fix the `task` or `watch` entry in `pages.json` as well, otherwise the next run may reintroduce the problem.

## Community

- **Discord**: [Join our community](https://swiftlys2.net/discord)
- **Issues**: [Report docs problems](https://github.com/swiftly-solution/swiftlys2.net-docs/issues)
- **Framework**: [swiftly-solution/swiftlys2](https://github.com/swiftly-solution/swiftlys2)
