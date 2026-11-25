namespace SharedKernel.Repositories;

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
        *.webp binary
        *.ico binary
        *.pdf binary
        *.zip binary
        *.gz binary
        """;

    private const string GitattributesShell = Gitattributes + "\n" + """
        *.sh text eol=lf
        *.bash text eol=lf
        *.bats text eol=lf
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

    private const string GitignoreDotnet = Gitignore + "\n" + """
        [Bb]in/
        [Oo]bj/
        TestResults/
        *.user
        *.suo
        *.nupkg
        *.snupkg
        .worktrees/
        .tmp/
        """;

    private const string GitignoreNix = Gitignore + "\n" + """
        result
        result-*
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

    private const string HookPreCommitDiffCheck = """
        #!/bin/sh
        # Rejects staged whitespace errors.
        exec git diff --cached --check
        """;

    private const string HookPrePushDiffCheck = """
        #!/bin/sh
        set -eu

        # Check pushed tips from the worktree with a clean Git environment.
        for variable in $(env | sed -n 's/^\(GIT_[A-Za-z0-9_]*\)=.*/\1/p'); do
            unset "$variable"
        done

        while read -r _local_ref local_oid _remote_ref _remote_oid; do
            case "$local_oid" in
                *[!0]*) git show --format= --check --end-of-options "$local_oid" -- ;;
            esac
        done
        """;

    private const string HookCommitMessage = """
        #!/bin/sh
        exec dotnet tool run ci -- verify-commit-message --file "$1"
        """;

    private const string DotnetSdkBootstrap = """
        #!/bin/sh
        set -eu

        fail() {
            printf 'bootstrap-dotnet: %s\n' "$1" >&2
            exit 1
        }

        check_installed() {
            available=$("$1" --list-sdks) || return 1
            printf '%s\n' "$available" | awk -v expected="$sdk" '$1 == expected { found = 1 } END { exit !found }'
        }

        download() {
            curl --disable --fail --silent --show-error --proto '=https' --connect-timeout 15 \
                --max-time "$3" --max-filesize "$4" --output "$2" -- "$1"
        }

        install=false
        case "$#" in
            0) ;;
            1) [ "$1" = --install ] || fail 'usage: bootstrap-dotnet.sh [--install]'; install=true ;;
            *) fail 'usage: bootstrap-dotnet.sh [--install]' ;;
        esac

        root=$(CDPATH='' cd -- "$(dirname -- "$0")/.." && pwd -P) || fail 'could not find the repository root'
        cd -- "$root"
        command -v jq >/dev/null 2>&1 || fail 'jq is required to read global.json'
        sdk=$(jq -er '.sdk.version | strings | select(test("\\A[0-9]{1,2}\\.[0-9]{1,3}\\.[0-9]{1,4}(-[0-9A-Za-z]+(\\.[0-9A-Za-z]+)*)?\\z"))' global.json) ||
            fail 'global.json must specify an exact SDK version'
        if command -v dotnet >/dev/null 2>&1 && check_installed dotnet; then
            printf 'SDK %s is installed.\n' "$sdk"
            exit 0
        fi

        host_os=$(uname -s)
        host_arch=$(uname -m)
        host=dotnet
        extension=tar.gz
        case "$host_os" in
            Linux)
                os=linux
                libc=$(ldd --version 2>&1 || :)
                case "$libc" in *musl*) os=linux-musl ;; esac
                ;;
            Darwin) os=osx ;;
            MINGW*|MSYS*|CYGWIN*) os=win; host=dotnet.exe; extension=zip ;;
            *) fail 'unsupported .NET SDK bootstrap operating system' ;;
        esac
        case "$host_arch" in
            x86_64|amd64) architecture=x64 ;;
            i?86) architecture=x86 ;;
            aarch64|arm64|ARM64) architecture=arm64 ;;
            armv7*) architecture=arm ;;
            *) fail 'unsupported .NET SDK bootstrap architecture' ;;
        esac

        rid=$os-$architecture
        parent="$root/bin/sharedkernel-dotnet/$sdk"
        target="$parent/$rid"
        if [ -x "$target/$host" ] && check_installed "$target/$host"; then
            printf 'SDK %s is installed at %s.\n' "$sdk" "$target"
            exit 0
        fi
        "$install" || fail "SDK $sdk is missing. To install it side by side, use --install."

        mkdir -p -- "$parent"
        lock="$target.lock"
        mkdir -- "$lock" || fail 'could not reserve the SDK installation directory'
        scratch=
        cleanup() {
            [ -z "$scratch" ] || rm -rf -- "$scratch"
            rmdir -- "$lock"
        }
        trap cleanup EXIT
        trap 'exit 1' HUP INT TERM
        if [ -e "$target" ] || [ -L "$target" ]; then
            fail 'the existing SDK installation directory is occupied'
        fi
        scratch=$(mktemp -d "$parent/.sdk.XXXXXXXX") || fail 'could not stage the SDK download'

        base=https://builds.dotnet.microsoft.com/dotnet
        channel=$(printf '%s\n' "$sdk" | cut -d. -f1,2)
        metadata="$scratch/releases.json"
        download "$base/release-metadata/$channel/releases.json" "$metadata" 60 16777216 ||
            fail 'could not download the SDK release metadata'
        asset=$(jq -ec --arg version "$sdk" --arg rid "$rid" --arg name "dotnet-sdk-$rid.$extension" '
            [.releases[] | (.sdks[]?, .sdk?) | select(.version == $version) | .files[]?
                | select(.rid == $rid and .name == $name) | {url, hash}]
            | unique | if length == 1 then .[0] else empty end
            ' "$metadata") || fail "release metadata has no unique SDK archive for $sdk ($rid)"
        url=$(printf '%s\n' "$asset" | jq -er '.url | strings') || fail 'release metadata has no SDK download URL'
        expected_url="$base/Sdk/$sdk/dotnet-sdk-$sdk-$rid.$extension"
        [ "$url" = "$expected_url" ] || fail 'Unexpected SDK download URL'
        expected_hash=$(printf '%s\n' "$asset" | jq -er '.hash | strings | select(test("\\A[0-9a-fA-F]{128}\\z")) | ascii_downcase') ||
            fail 'release metadata has no valid SHA-512 checksum'

        archive="$scratch/sdk.$extension"
        download "$url" "$archive" 1200 2147483648 || fail 'could not download the SDK archive'
        if command -v sha512sum >/dev/null 2>&1; then
            hash_line=$(sha512sum < "$archive") || fail 'could not hash the SDK archive'
        elif command -v shasum >/dev/null 2>&1; then
            hash_line=$(shasum -a 512 < "$archive") || fail 'could not hash the SDK archive'
        else
            fail 'sha512sum or shasum is required to install the SDK'
        fi
        actual_hash=$(printf '%s\n' "$hash_line" | awk '{ print $1 }')
        [ "$actual_hash" = "$expected_hash" ] || fail 'SHA-512 checksum mismatch'

        payload="$scratch/extracted"
        mkdir -- "$payload"
        case "$extension" in
            tar.gz) tar --extract --gzip --file "$archive" --directory "$payload" || fail 'could not extract the SDK archive' ;;
            zip) unzip -q "$archive" -d "$payload" || fail 'could not extract the SDK archive' ;;
        esac
        if [ ! -x "$payload/$host" ] || ! check_installed "$payload/$host"; then
            fail 'the extracted SDK does not match global.json'
        fi
        mv -- "$payload" "$target"
        printf 'Installed SDK %s at %s.\n' "$sdk" "$target"
        """;

    private static readonly Dictionary<string, string> Content = new(StringComparer.Ordinal)
    {
        ["gitattributes"] = Gitattributes,
        ["gitattributes-shell"] = GitattributesShell,
        ["editorconfig"] = Editorconfig,
        ["gitignore"] = Gitignore,
        ["gitignore-dotnet"] = GitignoreDotnet,
        ["gitignore-nix"] = GitignoreNix,
        ["readme"] = Readme,
        ["pull-request-template"] = PullRequestTemplate,
        ["issue-form-bug"] = IssueFormBug,
        ["issue-form-feature"] = IssueFormFeature,
        ["issue-form-bug-forgejo"] = IssueFormBug.Replace("\ndescription:", "\nabout:", StringComparison.Ordinal),
        ["issue-form-feature-forgejo"] = IssueFormFeature.Replace("\ndescription:", "\nabout:", StringComparison.Ordinal),
        ["pre-commit-diff-check"] = HookPreCommitDiffCheck,
        ["pre-push-diff-check"] = HookPrePushDiffCheck,
        ["commit-msg-conventional"] = HookCommitMessage,
        ["sdk-bootstrap"] = DotnetSdkBootstrap,
    };

    /// <summary>Gets the profile IDs.</summary>
    public static IEnumerable<string> Ids => Content.Keys;

    /// <summary>Looks up the content of the profile named <paramref name="id"/>.</summary>
    public static bool TryGet(string id, out string content) => Content.TryGetValue(id, out content!);
}
