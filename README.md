# highlightify

`highlightify` is a small .NET CLI that:

1. Reads Instagram story highlight pages or saved HTML exports.
2. Extracts music metadata from the highlight payload.
3. Searches Spotify for matching tracks.
4. Adds the matches to a playlist.

## Requirements

- yt-dlp
- A Spotify developer app with a client id.
- A Spotify account that can create or edit playlists.
- Instagram cookies if the highlight is private or requires login.

## Usage

```bash
dotnet run --project highlightify -- \
  --highlight "17876436264678750" \ # highlight-id from url
  --spotify-client-id "34ab6eeec71b46e2ab27bb021fdc95bc" \ # this is the dev app id you need to use
  --playlist-name "IG Highlights" \ # name of playlist
  --instagram-cookies-from-browser "firefox:/path/to/profile" #also chromium/safari etc. supported
```

Parameters:

```bash
Usage:
  highlightify --highlight <id> [--highlight <id> ...] --spotify-client-id <id> [options]
  highlightify --html <file> [--html <file> ...] --spotify-client-id <id> [options]

Required:
  --spotify-client-id <id>     Spotify app client id

Highlight input:
  --highlight <id>             Instagram highlight ID to fetch
  --html <file>                Local HTML export to parse instead of fetching a URL
  --instagram-cookie <value>    Raw Cookie header for Instagram requests
  --instagram-cookie-file <f>   File containing raw Cookie header or Netscape cookie lines
  --instagram-cookie-path <f>   Firefox profile directory or cookies.sqlite file
  --instagram-cookies-from-browser <spec>  Browser source like firefox or firefox:/path/to/profile (preferred)

Spotify output:
  --playlist-name <name>       Playlist name to create or reuse; defaults to "Instagram Highlights"
  --playlist-id <id>           Existing playlist id to append to
  --redirect-uri <uri>         OAuth redirect URI; defaults to http://127.0.0.1:54321/callback/
  --no-browser                 Print auth URL instead of opening a browser

Other:
  --dry-run                    Print matches without modifying Spotify
  --help, -h                   Show this help
```

### Environment variables

- `SPOTIFY_CLIENT_ID`
- `SPOTIFY_REDIRECT_URI`
- `HIGHLIGHTIFY_PLAYLIST_NAME`
- `HIGHLIGHTIFY_PLAYLIST_ID`
- `INSTAGRAM_COOKIE`
- `INSTAGRAM_COOKIE_FILE`

## Notes

- Spotify authentication uses PKCE and a localhost callback.
- Instagram page formats change often, so song extraction is best-effort and may need tuning for your account data.