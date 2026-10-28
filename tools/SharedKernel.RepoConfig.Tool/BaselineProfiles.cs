namespace PandaBear.RepoConfig.Tool;

/// <summary>
/// The embedded generic baseline content. It names no forge, owner, host, or registry; the consumer picks each target
/// path. Content may use the variables <c>@@repositoryName@@</c> and <c>@@defaultBranch@@</c>.
/// </summary>
internal static class BaselineProfiles
{
    private const string Gitattributes = """
        * text=auto eol=lf
        *.cmd text eol=crlf
        *.bat text eol=crlf
        *.png binary
        *.jpg binary
        *.jpeg binary
        *.gif binary
        *.ico binary
        *.pdf binary
        *.zip binary
        *.gz binary
        """;

    private const string Editorconfig = """
        root = true

        [*]
        charset = utf-8
        end_of_line = lf
        insert_final_newline = true
        trim_trailing_whitespace = true
        indent_style = space
        indent_size = 4

        [*.{json,yml,yaml}]
        indent_size = 2

        [*.md]
        trim_trailing_whitespace = false

        [{Makefile,*.mk}]
        indent_style = tab
        """;

    private const string Gitignore = """
        .env
        .env.*
        !.env.example
        *.log
        *.tmp
        *.swp
        .DS_Store
        Thumbs.db
        .idea/
        .vs/
        """;

    private const string Readme = """
        # @@repositoryName@@

        Describe what this repository is for.

        ## Getting started

        Describe how to build, test, and run it.

        ## Contributing

        Changes land through pull requests against `@@defaultBranch@@`.
        """;

    private const string PullRequestTemplate = """
        ## Summary

        What changes and why.

        ## Linked issue

        Closes #

        ## Checks

        - [ ] Tests added or updated
        - [ ] Documentation updated
        """;

    private const string IssueFormBug = """
        name: Bug report
        description: Report something that does not work as expected
        body:
          - type: textarea
            id: what-happened
            attributes:
              label: What happened?
              description: Include what you expected instead.
            validations:
              required: true
          - type: textarea
            id: reproduce
            attributes:
              label: Steps to reproduce
            validations:
              required: true
        """;

    private const string IssueFormFeature = """
        name: Feature request
        description: Propose a change or addition
        body:
          - type: textarea
            id: problem
            attributes:
              label: Problem
              description: What cannot be done today?
            validations:
              required: true
          - type: textarea
            id: proposal
            attributes:
              label: Proposal
        """;

    private static readonly Dictionary<string, string> Content = new(StringComparer.Ordinal)
    {
        ["gitattributes"] = Gitattributes,
        ["editorconfig"] = Editorconfig,
        ["gitignore"] = Gitignore,
        ["readme"] = Readme,
        ["pull-request-template"] = PullRequestTemplate,
        ["issue-form-bug"] = IssueFormBug,
        ["issue-form-feature"] = IssueFormFeature,
    };

    /// <summary>Gets the profile IDs.</summary>
    public static IEnumerable<string> Ids => Content.Keys;

    /// <summary>Looks up the content of the profile named <paramref name="id"/>.</summary>
    public static bool TryGet(string id, out string content) => Content.TryGetValue(id, out content!);
}
