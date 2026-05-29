# String Theory Diagnostics Endpoint

This Cloudflare Worker receives the game's diagnostics ZIP through a raw `application/zip` POST and forwards it to private Discord webhooks.

The Discord webhook URLs should be configured as Worker secrets, not committed into the Unity client.

## Deploy

1. Create a Cloudflare Worker.
2. Add `DISCORD_WEBHOOK_URL` as a Worker secret for user-submitted bug reports.
3. Add `DISCORD_AUTO_WEBHOOK_URL` as a Worker secret for automatic session logs.
4. Optional: add `MAX_UPLOAD_BYTES`, for example `9437184`.
5. Deploy `cloudflare-worker.js`.
6. Set `StringTheoryBuildInfo.DiagnosticsUploadEndpoint` to the Worker URL.
7. Keep `StringTheoryBuildInfo.DiagnosticsUploadEndpointKind` as `raw-zip`.

The Unity client can also post directly to a Discord webhook if `DiagnosticsUploadEndpointKind` is `discord-webhook`, but that exposes the webhook URL inside the build. The Worker path is the safer default.
