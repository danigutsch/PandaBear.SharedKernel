# Riverbank Hall / Casa da Margem

A fictional community venue in English and Portuguese. The public pages use static server rendering and the packaged
site layout and stylesheet. The demo editor has its own interactive server component and keeps changes in its draft
preview.

- `VenueCatalog` validates the JSON import once before building a read-only site snapshot. Published routes and
  redirects, per-translation dates, canonical addresses, and editor copy belong to the venue.
- `VenueSite` uses the published translations for navigation, page metadata, and the reciprocal sitemap. Each group's
  unlocalized redirect supplies its `x-default` address.
- The host renders the theme once and uses the same version for its links and stylesheet endpoint. Versioned
  requests use immutable caching. Other stylesheet requests receive the host's one-minute freshness.
- The supplied image is atomically installed under its SHA-256 filename. The media endpoint approves only the
  published image.
- The editor uses the key `pt.sections[0].heading` with the named field and an ordered error summary. Submit a blank
  heading, follow the summary link to the control, and verify that the help and error are both announced for that
  input. Enter a draft heading to preview it. The public translation stays read-only.

## Run an isolated packed consumer

Use .NET SDK 11.0.100 and a local SharedKernel candidate directory containing its unchanged `candidate.json` and
`.nupkg` files. The sample targets the same framework as the libraries at its introduction. Start from a copy outside
the library repository so its build cannot import repository props or reference library source. The template generates
a lock for the selected candidate in that copy.

From the repository root, with `candidate` set to the absolute directory of the packed artifacts:

```sh
workspace=$(mktemp -d)
cp -R samples/CommunityVenue "$workspace/sample"
cd "$workspace/sample"
cp CommunityVenue.csproj.template CommunityVenue.csproj
version=$(jq -er .packageVersion "$candidate/candidate.json")
jq -r '.packageSha256 | to_entries[] | "\(.value)  \(.key)"' "$candidate/candidate.json" > candidate.sha256
(cd "$candidate" && sha256sum --check "$workspace/sample/candidate.sha256")
cat > NuGet.Config <<'CONFIG'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <add key="candidate" value="../feed" />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
    <packageSourceMapping>
        <packageSource key="candidate"><package pattern="PandaBear.SharedKernel.*" /></packageSource>
        <packageSource key="nuget.org"><package pattern="*" /></packageSource>
    </packageSourceMapping>
</configuration>
CONFIG
mkdir ../feed
cp "$candidate"/*.nupkg ../feed/
export NUGET_PACKAGES="$workspace/packages"
dotnet restore --configfile NuGet.Config -p:CandidateVersion="$version"
dotnet restore --configfile NuGet.Config --locked-mode -p:CandidateVersion="$version"
dotnet build -c Release --no-restore -p:CandidateVersion="$version"
dotnet bin/Release/net11.0/CommunityVenue.dll --urls http://127.0.0.1:5085
```

Open `http://127.0.0.1:5085/` and switch languages on both pages. The HTML links a versioned `/theme.css?v=…` URL.
GET and HEAD at that URL carry immutable caching, HEAD has an empty body, and a stale version receives the
one-minute policy. The `/sitemap.xml` response contains all four published URLs with their reciprocal language links
and two distinct unlocalized fallbacks. The image URL is immutable and any other media filename returns 404.

The venue, page and editor fixtures are fictional. `Fixtures/hall.webp` was made with the built-in image generator
using the prompt below, then resized and encoded as WebP.

> One photorealistic landscape documentary photograph for a fictional Portuguese community arts venue, Riverbank Hall /
> Casa da Margem. A small gracious brick and plaster former neighborhood meeting hall beside a river path and planted
> courtyard, welcoming teal-painted timber entrance, modest warm daylight, late summer shade, natural earthy colors,
> ground-level wide composition, entirely invented place, no people, no visible names, no text, no logos, no borders.
> Wide 3:2 image suited to a responsive website photo.
