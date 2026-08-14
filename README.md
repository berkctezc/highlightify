# Highlightify

Highlightify turns the music in Instagram Story Highlights into a reviewed Spotify playlist.

The repository includes two clients:

- A responsive web app built with React, TypeScript, Vite, Tailwind CSS and shadcn/ui primitives.
- The original .NET command-line client.

The ASP.NET Core backend reuses the existing Instagram extraction logic, manages Spotify PKCE sessions, performs track matching, creates playlists, and stores the most recent import history locally.

## Web app

### Requirements

- .NET 10 SDK
- Node.js 22+
- pnpm 11+
- `yt-dlp` for Highlights that require a local Instagram browser session
- ImageMagick (`magick`) for album-cover similarity when several Spotify releases share the same metadata
- A Spotify developer application

Add this redirect URI to the Spotify developer application:

```text
http://127.0.0.1:5086/api/auth/spotify/callback
```

Configure the Spotify client ID with .NET user secrets:

```bash
dotnet user-secrets set \
  --project src/Highlightify.Web/Highlightify.Web.csproj \
  Spotify:ClientId "YOUR_SPOTIFY_CLIENT_ID"
```

Environment variables are also supported:

```bash
export SPOTIFY_CLIENT_ID="YOUR_SPOTIFY_CLIENT_ID"
export SPOTIFY_REDIRECT_URI="http://127.0.0.1:5086/api/auth/spotify/callback"
export HIGHLIGHTIFY_FRONTEND_URL="http://127.0.0.1:5173"
```

Install dependencies and start both the API and Vite server:

```bash
cd web
pnpm install
pnpm dev:full
```

Open [http://127.0.0.1:5173](http://127.0.0.1:5173).

### Production-style local run

Build the frontend into the ASP.NET Core static web root, then run the API:

```bash
cd web
pnpm build:full
cd ..
dotnet run --project src/Highlightify.Web/Highlightify.Web.csproj --no-build
```

Open [http://127.0.0.1:5086](http://127.0.0.1:5086).

### Web flow

1. Paste one or more Instagram Highlight URLs or upload saved HTML exports.
2. Optionally select a local browser session for private Highlights.
3. Review the Spotify match, confidence, and alternatives for every extracted song.
4. Select an existing playlist or create a private/public playlist.
5. Open the completed playlist directly in Spotify.

Raw Instagram cookies are never accepted by the web API. When browser access is selected, `yt-dlp` reads the chosen profile locally. Import history is stored in `src/Highlightify.Web/App_Data` and is ignored by Git.

## Architecture

```text
web/                          React + Vite SPA
src/Highlightify.Web/         ASP.NET Core API and local web host
src/Highlightify.Core/        Shared domain models and source resolution
src/Highlightify.Integrations Instagram and Spotify integrations
src/Highlightify.Application/ Original CLI orchestration
src/Highlightify.Console/     CLI entry point
tests/Highlightify.Tests/     .NET extraction and domain tests
```

The frontend and backend are deliberately separated so the same web interface can later be packaged with Tauri for macOS and Windows without rebuilding the product UI.

## Verification

```bash
dotnet test highlightify.sln --nologo -m:1 -nr:false

cd web
pnpm typecheck
pnpm lint
pnpm test
pnpm build
```

## CLI

The existing CLI remains available:

```bash
dotnet run --project src/Highlightify.Console -- \
  --highlight "HIGHLIGHT_ID" \
  --spotify-client-id "YOUR_SPOTIFY_CLIENT_ID" \
  --playlist-name "IG Highlights" \
  --instagram-cookies-from-browser "firefox:/path/to/profile"
```

```text
Usage:
  highlightify --highlight <id> [--highlight <id> ...] --spotify-client-id <id> [options]
  highlightify --html <file> [--html <file> ...] --spotify-client-id <id> [options]

Required:
  --spotify-client-id <id>     Spotify app client ID

Highlight input:
  --highlight <id>             Instagram Highlight ID or URL
  --html <file>                Saved HTML export
  --instagram-cookie <value>   Raw Cookie header (CLI only)
  --instagram-cookie-file <f>  Cookie header or Netscape cookie file (CLI only)
  --instagram-cookie-path <f>  Firefox profile or cookies.sqlite path
  --instagram-cookies-from-browser <spec>
                               Browser source such as firefox or firefox:/profile/path

Spotify output:
  --playlist-name <name>       Playlist name; defaults to Instagram Highlights
  --playlist-id <id>           Existing playlist ID to append to
  --redirect-uri <uri>         OAuth callback; defaults to 127.0.0.1:54321
  --no-browser                 Print the authorization URL

Other:
  --dry-run                    Print candidates without changing Spotify
  --help, -h                   Show help
```

Instagram payload formats change frequently, so extraction is intentionally best-effort and covered by regression fixtures.

#### CONTRIBUTORS / MAINTAINERS
[@berkctezc](https://github.com/berkctezc)
[@sametirkoren](https://github.com/sametirkoren)
