namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// A repository seeded with one violation per link and line-ending rule. The expected findings are the output of the
/// Python link linter and line-ending script that <c>verify-links</c> and <c>verify-line-endings</c> reproduce.
/// </summary>
internal static class LinkCorpus
{
    public const string Configuration = """
        {
          "version": 1,
          "links": {
            "include": ["AGENTS.md", "CONTRIBUTING.md", "README.md", "SECURITY.md", "LICENSE.txt", ".agents/*.md", ".devcontainer/*.md", ".github/*.md", "benchmarks/*.md", "docs/*.md", "samples/*.md", "src/*.md", "tests/*.md"],
            "exclude": [".git/*", "*/.git/*", ".worktrees/*", "*/.worktrees/*", "bin/*", "*/bin/*", "obj/*", "*/obj/*", "TestResults/*", "*/TestResults/*", ".github/ISSUE_TEMPLATE/*", "docs/pull_request_template.md", "docs/GENERATED*.md", "tests/ISSUE-*.md"]
          }
        }
        """;

    private const string Tracker = "summarize durable context or link a maintained doc instead";

    public static readonly string[] ExpectedLinkFindings =
    [
        "Link validation failed:",
        $"- LICENSE.txt:1: bare issue/PR reference is not allowed; {Tracker}",
        "- docs/bom.md:3: missing Markdown anchor 'title' in docs/bom.md",
        "- docs/code.md:3: missing local link target: missing-inline.md",
        "- docs/code.md:14: missing local link target: missing-comment.md",
        "- docs/crlf.md:3: missing local link target: missing-crlf.md",
        "- docs/crlf.md:6: missing local link target: missing-cr.md",
        "- docs/crlf.md:8: missing local link target: missing-ff.md",
        "- docs/deleted.md:1: missing local link target: gone.md",
        "- docs/escape.md:1: link escapes repository: /docs/escape.md",
        "- docs/escape.md:2: link escapes repository: ../../outside.md",
        "- docs/escape.md:4: link escapes repository: outside-link.md",
        "- docs/headings.md:16: missing Markdown anchor 'version-12--cafe' in docs/headings.md",
        "- docs/headings.md:20: missing Markdown anchor 'fenced' in docs/headings.md",
        "- docs/headings.md:24: missing Markdown anchor 'nope' in sub/target.md",
        "- docs/parens.md:1: missing local link target: file(1",
        $"- docs/policy.md:1: direct GitHub issue/PR link is not allowed; {Tracker}",
        $"- docs/policy.md:2: direct GitHub issue/PR link is not allowed; {Tracker}",
        $"- docs/policy.md:3: repo-relative issue/PR link is not allowed; {Tracker}",
        $"- docs/policy.md:4: bare issue/PR reference is not allowed; {Tracker}",
        $"- docs/policy.md:5: bare issue/PR reference is not allowed; {Tracker}",
        $"- docs/policy.md:6: bare issue/PR reference is not allowed; {Tracker}",
        $"- docs/policy.md:7: bare issue/PR reference is not allowed; {Tracker}",
        $"- docs/ref.md:3: direct GitHub issue/PR link is not allowed; {Tracker}",
        "- docs/ref.md:1: missing local link target: missing-ref.md",
        "- docs/ref.md:3: missing Markdown anchor 'nope' in docs/ref.md",
        $"- docs/ref.md:3: direct GitHub issue/PR link is not allowed; {Tracker}",
        "- notes/aliased.md:1: missing local link target: missing-aliased.md",
    ];

    /// <summary>The arguments of the explicit-path run, which replace <c>include</c> but not <c>exclude</c>.</summary>
    public static readonly string[] ExplicitPaths = ["notes", "docs/GENERATED_x.md", "README.md", "docs/linked"];

    public static readonly string[] ExpectedExplicitPathFindings =
    [
        "Link validation failed:",
        "- notes/aliased.md:1: missing local link target: missing-aliased.md",
        "- shared/shared.md:3: missing local link target: missing-shared.md",
    ];

    public static readonly string[] ExpectedLineEndingFindings =
    [
        "Repository line-ending verification failed:",
        "- data/blob.txt: Expected LF line endings.",
        "- data/mixed.txt: Expected LF line endings.",
        "- data/script.cmd: Expected CRLF line endings.",
        "- docs/worktree.md: Expected LF line endings.",
    ];

    private static readonly (string Path, string Content)[] Files =
    [
        (".gitattributes", "* text=auto eol=lf\n*.md text eol=lf\n*.txt text eol=lf\n*.cmd text eol=crlf\n*.png binary\n"),
        (".config/sharedkernel-repo.json", Configuration + "\n"),
        ("README.md", "# Corpus\n\nSee [headings](docs/headings.md#version-1.2--caf) and [license](LICENSE.txt).\n"),
        ("LICENSE.txt", "Tracked separately in #945.\n"),
        ("docs/headings.md", "# Version 1.2 \u2014 Caf\u00e9\n\n#hashtag line\n\n# Dup\n\n# Dup\n\n## <b>Tagged</b> `code` *Emph*\n\n```text\n# Fenced\n```\n\n"
            + "- [ok](#version-1.2--caf)\n- [ascii only](#version-12--cafe)\n- [hashtag](#hashtag-line)\n- [dup](#dup-1)\n- [upper](#DUP)\n- [fenced](#fenced)\n"
            + "- [tagged](#tagged-code-emph)\n- [encoded](#version-1.2%2D%2Dcaf)\n- [other file](sub/target.md#target-heading)\n- [other missing](sub/target.md#nope)\n"),
        ("docs/sub/target.md", "# Target heading\n"),
        ("docs/bom.md", "\ufeff# Title\n\n[title](#title)\n"),
        ("docs/crlf.md", "# Crlf\r\n\r\n[x](missing-crlf.md)\r\nline\rcr\r[x](missing-cr.md)\n\f[x](missing-ff.md)\n"),
        ("docs/code.md", "# Code\n\nInline `[x](missing-inline.md)` code.\n\n```md\n[x](missing-fenced.md)\n```\n\n~~~\n```\n[x](missing-tilde.md)\n~~~\n\n<!-- [x](missing-comment.md) -->\n"),
        ("docs/file(1).md", "# Parenthesized\n"),
        ("docs/my file.md", "# Spaced\n"),
        ("docs/parens.md", "[paren](file(1).md)\n[title](my%20file.md \"Title\")\n[empty title]( my%20file.md )\n"),
        ("docs/policy.md", "See https://github.com/example/project/issues/945.\nSee `https://github.com/example/project/pull/945`.\nSee `issues/945`.\n"
            + "Tracked as GH-945.\nTracked as example/project#945.\nOutput: `TestResults/issue-945-benchmark.tsv`.\n- #945\n"
            + "Unicode Standard Annex #15: Unicode Normalization Forms\nUAX #15 defines normalization forms.\nUse a PKCS#12 certificate.\n"
            + "style node fill:#e7f5ff,stroke:#123456\nSee https://example.com/reference/#123.\nRevision 945 remains supported.\n"),
        ("docs/escape.md", "[abs](/docs/escape.md)\n[up](../../outside.md)\n[through dir link](linked/shared.md)\n[outside link](outside-link.md)\n"
            + "[ext](https://example.com/x) [mail](mailto:a@example.com)\n"),
        ("shared/shared.md", "# Shared\n\n[bad](missing-shared.md)\n"),
        ("notes/aliased.md", "[bad](missing-aliased.md)\n"),
        ("docs/ref.md", "[r]: missing-ref.md\n   [s]: <sub/target.md>\n<#nope> <https://github.com/o/r/pull/7>\n"),
        ("docs/deleted.md", "[gone](gone.md)\n"),
        ("docs/gone.md", "# Gone\n"),
        ("docs/GENERATED_x.md", "[bad](missing-generated.md)\n"),
        ("docs/pull_request_template.md", "[bad](missing-template.md)\n"),
        ("src/App/bin/x.md", "[bad](missing-bin.md)\n"),
        ("src/App/readme.md", "[ok](../../docs/sub/target.md)\n"),
        ("data/mixed.txt", "a\r\nb\n"),
        ("data/script.cmd", "echo lf\n"),
        ("data/image.png", "binary\r\n"),
        ("data/ok.txt", "ok\n"),
    ];

    /// <summary>Writes and stages the corpus. Symbolic links require a POSIX file system.</summary>
    public static async Task Write(TemporaryGitRepository repository, CancellationToken cancellationToken)
    {
        foreach (var (path, content) in Files)
        {
            repository.WriteText(path, content);
        }

        File.CreateSymbolicLink(Path.Combine(repository.RootPath, "docs", "linked"), "../shared");
        File.CreateSymbolicLink(Path.Combine(repository.RootPath, "docs", "alias.md"), "../notes/aliased.md");
        File.CreateSymbolicLink(Path.Combine(repository.RootPath, "docs", "outside-link.md"), "/nonexistent-sharedkernel-outside/x.md");
        await Git(repository, ["add", "-A", "--", ":(exclude).home"], cancellationToken);

        // A CRLF blob in the index under eol=lf while the working tree holds LF.
        repository.WriteText("data/blob.txt", "blob\r\n");
        var objectId = (await Git(repository, ["hash-object", "-w", "--no-filters", "data/blob.txt"], cancellationToken)).StandardOutput.Trim();
        await repository.SetIndexEntry("100644", objectId, "data/blob.txt", cancellationToken);
        repository.WriteText("data/blob.txt", "blob\n");

        // A CRLF file only in the working tree, and a file deleted from the working tree but still in the index.
        repository.WriteText("docs/worktree.md", "# Worktree\n");
        await repository.Stage("docs/worktree.md", cancellationToken);
        repository.WriteText("docs/worktree.md", "# Worktree\r\n");
        repository.Delete("docs/gone.md");
    }

    private static async Task<CiTestSelectionTestProcess.ProcessResult> Git(
        TemporaryGitRepository repository,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var result = await repository.RunGit(arguments, cancellationToken);
        return result.ExitCode == 0 ? result : throw new InvalidOperationException("Corpus setup failed: git " + string.Join(' ', arguments));
    }
}
