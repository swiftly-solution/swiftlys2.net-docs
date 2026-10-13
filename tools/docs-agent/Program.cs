// DeepSeek tool-calling agent that updates one docs page from the SwiftlyS2 source.
// Usage: docs-agent --page <mdx> --src <dir> --task <text> --summary <file> [--allow-upstream]
//   [--beta-src <dir>] [--scope <dir,dir,...>] [--slug <id> --hash-file <json> [--watch a,b,c] [--force]]
// With --slug/--hash-file the run is skipped when the watched source (stable+beta) and the page's task are unchanged
// since the hash stored for that slug; the hash is stored again after a completed run.
// Env: DEEPSEEK_API_KEY (required), DEEPSEEK_MODEL (default deepseek-flash), DEEPSEEK_API_URL.
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

const int MaxTurns = 40;
const int MaxOut = 20000; // chars returned per tool call
const string UpstreamPrefix = "plugin_files/configs/"; // only place the agent may edit in swiftlys2

const string SystemPrompt = """
    You maintain one documentation page of the SwiftlyS2 docs site (MDX, Docusaurus-like).
    The SwiftlyS2 source code is available through your tools. Your job: make the page accurately reflect the source.

    Rules:
    - Only document what the source supports. Never invent options, defaults or types. Verify each one by reading code.
    - Keep the page's existing frontmatter, structure, tone and table style. Change only what is out of date, wrong or missing.
    - If nothing needs changing, do NOT call write_page; just call finish.
    - The source comes in two branches: `stable` (master, the default) and `beta`. The page body documents stable.
      If beta has something stable lacks (new/renamed options, new commands, changed defaults), call diff_beta to find it, verify in code,
      and put it in ONE section at the very END of the page titled `## Beta Changes`, starting with a short note that these are available on the beta branch only.
      Use the page's existing table style there. Rewrite that section from scratch each run; if beta has nothing new, remove it.
    - Match the voice of the rest of the documentation: before writing, read 2-3 other pages with read_docs
      (list "docs/guides" and "docs/development", e.g. docs/development/configuration.mdx, docs/guides/terminologies.mdx, docs/development/commands.mdx)
      and copy their tone, heading style, sentence length, table/admonition/code-block conventions and terminology.
      Do not introduce a new style, emojis or marketing language.
    - Be efficient: use grep to locate things, read_file with line ranges to confirm.
    - When done, call finish with a short markdown summary of what changed and why (used as the PR description).
    """;

var opt = new Dictionary<string, string>();
for (var i = 0; i < args.Length; i++)
    if (args[i].StartsWith("--")) opt[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
string Opt(string k) => opt.TryGetValue(k, out var v) ? v : throw new ArgumentException($"missing --{k}");

var page = Opt("page");
var docsRoot = Path.GetFullPath("docs");
var src = Path.GetFullPath(Opt("src"));
var task = Opt("task");
var summaryPath = Opt("summary");
var betaSrc = opt.TryGetValue("beta-src", out var bs) ? Path.GetFullPath(bs) : null;
var allowUpstream = opt.ContainsKey("allow-upstream");
// --scope restricts every source tool to these repo-relative files/dirs (e.g. the public API project)
var scopes = (opt.GetValueOrDefault("scope") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(x => x.Replace('\\', '/').Trim('/')).ToArray();
if (scopes.Length > 0 && allowUpstream) throw new ArgumentException("--scope and --allow-upstream are mutually exclusive");

// ---- change detection: skip when nothing the page depends on changed since the last completed run ----
string? inputHash = null;
Dictionary<string, string> hashes = [];
var slug = opt.GetValueOrDefault("slug");
var hashFile = opt.GetValueOrDefault("hash-file");
if (slug is not null && hashFile is not null)
{
    using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    void Feed(string s) => h.AppendData(Encoding.UTF8.GetBytes(s + "\0"));
    Feed($"{page}|{string.Join(",", scopes)}|{allowUpstream}|{task}");
    var watch = (opt.GetValueOrDefault("watch") ?? (scopes.Length > 0 ? string.Join(",", scopes) : ".")).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    foreach (var (label, root) in new[] { ("stable", src), ("beta", betaSrc) })
    {
        if (root is null) continue;
        var sep = Path.DirectorySeparatorChar;
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var w in watch)
        {
            var p = Safe(root, w);
            if (File.Exists(p)) files.Add(p);
            else if (Directory.Exists(p))
            {
                foreach (var f in Directory.EnumerateFiles(p, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint }))
                    if (!f.Contains($"{sep}.git{sep}")) files.Add(f);
            }
            else Feed($"{label}:missing:{w}");
        }
        foreach (var f in files)
        {
            Feed($"{label}:{Path.GetRelativePath(root, f).Replace('\\', '/')}");
            h.AppendData(SHA256.HashData(File.ReadAllBytes(f)));
        }
    }
    inputHash = Convert.ToHexString(h.GetHashAndReset()).ToLowerInvariant();
    if (File.Exists(hashFile))
        hashes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(hashFile)) ?? [];
    if (!opt.ContainsKey("force") && hashes.TryGetValue(slug, out var old) && old == inputHash)
    {
        Console.Error.WriteLine($"[{slug}] unchanged since last update ({inputHash[..12]}); skipping");
        return; // no summary file is written: the caller treats that as "skipped"
    }
}

var model = Environment.GetEnvironmentVariable("DEEPSEEK_MODEL") ?? "deepseek-flash";
var api = Environment.GetEnvironmentVariable("DEEPSEEK_API_URL") ?? "https://api.deepseek.com/chat/completions";

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
    Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? throw new InvalidOperationException("DEEPSEEK_API_KEY not set"));

static JsonObject Tool(string name, string desc, JsonObject props, params string[] required) => new()
{
    ["type"] = "function",
    ["function"] = new JsonObject
    {
        ["name"] = name,
        ["description"] = desc,
        ["parameters"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props,
            ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray()),
        },
    },
};
static JsonObject Str(string d = "") => new() { ["type"] = "string", ["description"] = d };
static JsonObject Src() => new() { ["type"] = "string", ["enum"] = new JsonArray("stable", "beta"), ["description"] = "Default stable" };
static JsonObject Int() => new() { ["type"] = "integer" };

var tools = new JsonArray
{
    Tool("grep", "Recursive regex search in the SwiftlyS2 source. Returns file:line:text.",
        new() { ["pattern"] = Str(), ["path"] = Str("Subdirectory or file, relative to source root. Default '.'"), ["source"] = Src() }, "pattern"),
    Tool("read_file", "Read a file from the SwiftlyS2 source (1-based inclusive line range optional).",
        new() { ["path"] = Str(), ["start"] = Int(), ["end"] = Int(), ["source"] = Src() }, "path"),
    Tool("list_dir", "List a directory in the SwiftlyS2 source.", new() { ["path"] = Str(), ["source"] = Src() }, "path"),
    Tool("diff_beta", "Unified diff of the SwiftlyS2 source, stable vs beta (a/ = stable, b/ = beta). Optional subdirectory or file.",
        new() { ["path"] = Str("Relative to source root. Default '.'") }),
    Tool("read_docs", "Read-only: read a docs file, or list a docs directory, to learn the documentation's writing style. Path relative to the docs folder (e.g. 'guides', 'development/commands.mdx').",
        new() { ["path"] = Str() }, "path"),
    Tool("read_page", "Read the current docs page you are maintaining.", new()),
    Tool("write_page", "Overwrite the docs page with its full new content (must keep frontmatter).",
        new() { ["content"] = Str() }, "content"),
    Tool("write_upstream_file",
        $"Overwrite a file in the SwiftlyS2 repo (only under {UpstreamPrefix}) with full content, e.g. to fix an example config that drifted from the code. The file is edited on the beta branch and a PR against beta is opened, so base the fix on the beta code (source=\"beta\").",
        new() { ["path"] = Str(), ["content"] = Str() }, "path", "content"),
    Tool("finish", "End the task.", new() { ["summary"] = Str() }, "summary"),
};

// Resolve rel under root. Rejects ../, any .git segment, and any symlink along the path.
string Safe(string root, string rel)
{
    var p = Path.GetFullPath(Path.Combine(root, rel));
    var parts = Path.GetRelativePath(root, p).Split(Path.DirectorySeparatorChar);
    if (parts[0] == "..") throw new InvalidOperationException($"path escapes root: {rel}");
    var cur = root;
    foreach (var part in parts.Where(x => x != "."))
    {
        if (part == ".git") throw new InvalidOperationException($"forbidden path: {rel}");
        cur = Path.Combine(cur, part);
        if (new FileInfo(cur).LinkTarget is not null) throw new InvalidOperationException($"symlink not allowed: {rel}");
    }
    return p;
}

static string TryPath(string? args)
{
    try { return JsonNode.Parse(args ?? "{}")?["path"] is { } p ? $" path={p}" : ""; }
    catch { return ""; }
}

static string Clip(string s) => s.Length > MaxOut ? s[..MaxOut] : s;

void RegisterInNav()
{
    var metaPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(page))!, "_meta.json");
    if (!File.Exists(metaPath)) return;
    var name = Path.GetFileNameWithoutExtension(page);
    var meta = JsonNode.Parse(File.ReadAllText(metaPath))!.AsObject();
    if (meta.ContainsKey(name)) return;
    meta[name] = new JsonObject { ["page"] = Path.GetFileName(page) };
    File.WriteAllText(metaPath, meta.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true, IndentSize = 4 }) + "\n", new UTF8Encoding(false));
}

// Safe() plus the --scope allowlist. Only the tools the agent reads source with use this.
string Scoped(string root, string rel)
{
    var p = Safe(root, rel);
    if (scopes.Length == 0) return p;
    var r = Path.GetRelativePath(root, p).Replace('\\', '/');
    if (!scopes.Any(s => r == s || r.StartsWith(s + "/")))
        throw new InvalidOperationException($"path is outside the allowed source scope ({string.Join(", ", scopes)}): {rel}");
    return p;
}

// Paths a tool call covers: the requested one, or every scope when the request is the repo root.
IEnumerable<string> Targets(string? rel) =>
    scopes.Length > 0 && (rel is null or "." or "") ? scopes : [rel ?? "."];

string Root(JsonNode a) =>
    (string?)a["source"] == "beta" ? betaSrc ?? throw new InvalidOperationException("beta source not available") : src;

string Run(string name, JsonNode a)
{
    switch (name)
    {
        case "grep":
        {
            var re = new Regex((string)a["pattern"]!, RegexOptions.None, TimeSpan.FromSeconds(2));
            var top = Root(a);
            var sep = Path.DirectorySeparatorChar;
            IEnumerable<string> files = Targets((string?)a["path"]).Select(t => Scoped(top, t)).SelectMany(root => File.Exists(root)
                ? [root]
                : Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint }).Where(f => !f.Contains($"{sep}.git{sep}")));
            var sb = new StringBuilder();
            var n = 0;
            foreach (var f in files)
            {
                string[] lines;
                try { lines = File.ReadAllLines(f); } catch { continue; }
                if (lines.Take(50).Any(l => l.Contains('\0'))) continue; // binary
                for (var i = 0; i < lines.Length && n < 50; i++)
                    if (re.IsMatch(lines[i])) { sb.AppendLine($"{Path.GetRelativePath(top, f).Replace('\\', '/')}:{i + 1}:{lines[i]}"); n++; }
                if (n >= 50) break;
            }
            return n == 0 ? "(no matches)" : Clip(sb.ToString());
        }
        case "read_file":
        {
            var lines = File.ReadAllLines(Scoped(Root(a), (string)a["path"]!));
            var s = Math.Max((int?)a["start"] ?? 1, 1);
            var e = Math.Min((int?)a["end"] ?? lines.Length, lines.Length);
            return Clip(string.Join("\n", Enumerable.Range(s, Math.Max(e - s + 1, 0)).Select(i => $"{i}: {lines[i - 1]}")));
        }
        case "list_dir":
        {
            var rel = (string)a["path"]!;
            if (scopes.Length > 0 && rel is "." or "") return string.Join("\n", scopes.Select(x => x + "/")); // only the scopes are visible
            var d = Scoped(Root(a), rel);
            return Clip(string.Join("\n", Directory.EnumerateFileSystemEntries(d)
                .Select(p => Path.GetFileName(p) + (Directory.Exists(p) ? "/" : "")).Order()));
        }
        case "diff_beta":
        {
            if (betaSrc is null) return "ERROR: beta source not available.";
            var all = new StringBuilder();
            foreach (var rel in Targets((string?)a["path"]))
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true };
                // --no-index: compare the two checkouts directly
                foreach (var x in new[] { "diff", "--no-index", "--no-color", "--src-prefix=stable/", "--dst-prefix=beta/", "--", Scoped(src, rel), Scoped(betaSrc, rel) }) psi.ArgumentList.Add(x);
                using var p = System.Diagnostics.Process.Start(psi)!;
                all.Append(p.StandardOutput.ReadToEnd().Replace(src + "/", "").Replace(betaSrc + "/", ""));
                p.WaitForExit();
            }
            return all.Length == 0 ? "(no differences)" : Clip(all.ToString());
        }
        case "read_docs":
        {
            var d = Safe(docsRoot, ((string)a["path"]!).Replace("docs/", "", StringComparison.Ordinal));
            return Directory.Exists(d)
                ? string.Join("\n", Directory.EnumerateFileSystemEntries(d).Select(p => Path.GetFileName(p) + (Directory.Exists(p) ? "/" : "")).Order())
                : Clip(File.ReadAllText(d));
        }
        case "read_page":
            return File.Exists(page) ? File.ReadAllText(page) : "(page does not exist yet: create it with write_page)";
        case "write_page":
        {
            var c = (string)a["content"]!;
            if (!c.StartsWith("---")) return "ERROR: page must start with frontmatter (---).";
            var isNew = !File.Exists(page);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(page))!);
            File.WriteAllText(page, c.TrimEnd('\n') + "\n", new UTF8Encoding(false));
            if (isNew) RegisterInNav();
            return "page written";
        }
        case "write_upstream_file":
        {
            if (!allowUpstream) return "ERROR: upstream edits not allowed for this page.";
            // upstream PRs target the beta branch, so edits are made in the beta checkout
            var up = betaSrc ?? throw new InvalidOperationException("--beta-src is required for upstream edits");
            var p = Safe(up, (string)a["path"]!); // resolved first so ../ and symlinks collapse before the check
            var cfg = Safe(up, UpstreamPrefix).TrimEnd(Path.DirectorySeparatorChar); // GetFullPath keeps the prefix's trailing '/'
            if (!p.StartsWith(cfg + Path.DirectorySeparatorChar)
                || Path.GetRelativePath(up, p).Split(Path.DirectorySeparatorChar).Contains(".git"))
                return $"ERROR: '{a["path"]}' (resolved: {Path.GetRelativePath(up, p)}) is outside {UpstreamPrefix}; only paths under it are allowed.";
            File.WriteAllText(p, (string)a["content"]!, new UTF8Encoding(false));
            return "upstream file written";
        }
        default:
            return $"ERROR: unknown tool {name}";
    }
}

var pageIsNew = !File.Exists(page);
var messages = new JsonArray
{
    new JsonObject { ["role"] = "system", ["content"] = SystemPrompt },
    new JsonObject
    {
        ["role"] = "user",
        ["content"] = $"Docs page: {page}\n\nTask:\n{task}"
            + (pageIsNew ? "\n\nThe page does not exist yet (the feature is new on beta). Create it with write_page: frontmatter (`---`, `title:`, `---`), then the full content in the same structure as the sibling pages. Document only what the source supports." : "")
            + (allowUpstream ? $"\n\nYou MAY fix drifted files under {UpstreamPrefix} via write_upstream_file. These edits go to the beta branch, so verify against the beta source." : ""),
    },
};

var summary = "";
var finished = false;
var completed = false; // model finished (finish tool or plain reply), not cut off by MaxTurns
for (var turn = 0; turn < MaxTurns && !finished; turn++)
{
    var body = new JsonObject { ["model"] = model, ["temperature"] = 0, ["messages"] = messages.DeepClone(), ["tools"] = tools.DeepClone() };
    using var resp = await http.PostAsync(api, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
    var text = await resp.Content.ReadAsStringAsync();
    if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"{(int)resp.StatusCode}: {text}");
    var msg = JsonNode.Parse(text)!["choices"]![0]!["message"]!.AsObject();
    messages.Add(msg.DeepClone());

    var calls = msg["tool_calls"]?.AsArray();
    if (calls is not { Count: > 0 }) { summary = (string?)msg["content"] ?? ""; completed = true; break; }

    // Read-only tools run in parallel; page/upstream writes and finish run afterwards, in call order.
    static bool Mutates(string n) => n is "write_page" or "write_upstream_file" or "finish";
    string Exec(JsonNode c)
    {
        var fn = c["function"]!;
        var name = (string)fn["name"]!;
        string output;
        try
        {
            var raw = (string?)fn["arguments"];
            var a = JsonNode.Parse(string.IsNullOrEmpty(raw) ? "{}" : raw)!;
            if (name == "finish") { summary = (string)a["summary"]!; output = "ok"; finished = true; }
            else output = Run(name, a);
        }
        catch (Exception e) { output = $"ERROR: {e.Message}"; } // report to the model, let it recover
        Console.Error.WriteLine($"[{name}]{TryPath((string?)fn["arguments"])} {output[..Math.Min(output.Length, 120)].ReplaceLineEndings(" ")}");
        return output;
    }
    var all = calls.Select(c => c!).ToList();
    var outputs = new string[all.Count];
    await Task.WhenAll(all.Select((c, i) => Mutates((string)c["function"]!["name"]!) ? Task.CompletedTask : Task.Run(() => outputs[i] = Exec(c))));
    for (var i = 0; i < all.Count; i++)
        if (Mutates((string)all[i]["function"]!["name"]!)) outputs[i] = Exec(all[i]);
    for (var i = 0; i < all.Count; i++)
        messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = (string)all[i]["id"]!, ["content"] = outputs[i] });
    if (turn == MaxTurns - 1 && !finished) Console.Error.WriteLine("agent hit MaxTurns");
}

File.WriteAllText(summaryPath, summary);
if (inputHash is not null && (finished || completed))
{
    hashes[slug!] = inputHash;
    File.WriteAllText(hashFile!, System.Text.Json.JsonSerializer.Serialize(hashes.OrderBy(k => k.Key).ToDictionary(k => k.Key, k => k.Value),
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
}
