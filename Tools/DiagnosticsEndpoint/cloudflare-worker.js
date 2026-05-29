const DEFAULT_MAX_UPLOAD_BYTES = 9 * 1024 * 1024;

export default {
  async fetch(request, env) {
    if (request.method === "OPTIONS") {
      return new Response(null, { status: 204, headers: corsHeaders() });
    }

    if (request.method !== "POST") {
      return jsonResponse({ error: "method_not_allowed" }, 405);
    }

    const contentType = request.headers.get("content-type") || "";
    if (!contentType.toLowerCase().includes("application/zip")) {
      return jsonResponse({ error: "unsupported_media_type" }, 415);
    }

    const body = await request.arrayBuffer();
    const maxBytes = parseInt(env.MAX_UPLOAD_BYTES || "", 10) || DEFAULT_MAX_UPLOAD_BYTES;
    if (body.byteLength <= 0) {
      return jsonResponse({ error: "empty_upload" }, 400);
    }

    if (body.byteLength > maxBytes) {
      return jsonResponse({ error: "upload_too_large", maxBytes }, 413);
    }

    const version = safeHeader(request, "x-stringtheory-version", "unknown");
    const channel = safeHeader(request, "x-stringtheory-channel", "unknown");
    const uploadKind = safeHeader(request, "x-stringtheory-upload-kind", "diagnostics");
    const sessionId = safeHeader(request, "x-stringtheory-session-id", "unknown");
    const installId = safeHeader(request, "x-stringtheory-install-id", "unknown");
    const userInitiated = safeHeader(request, "x-stringtheory-user-initiated", "false") === "true";
    const webhookUrl = resolveWebhookUrl(env, userInitiated);
    if (!webhookUrl) {
      return jsonResponse(
        {
          error: "webhook_not_configured",
          expectedSecret: userInitiated ? "DISCORD_WEBHOOK_URL" : "DISCORD_AUTO_WEBHOOK_URL"
        },
        500
      );
    }

    const shortInstallId = sanitizeFilePart(installId).slice(0, 12) || "unknown";
    const fileName = `StringTheory-${sanitizeFilePart(version)}-${shortInstallId}-${sanitizeFilePart(uploadKind)}-${Date.now()}.zip`;
    const messageKind = userInitiated ? "user bug report" : "automatic diagnostics package";

    const payload = {
      username: "String Theory Diagnostics",
      content: `New ${messageKind} from user ${shortInstallId}`,
      allowed_mentions: { parse: [] },
      embeds: [
        {
          title: `${userInitiated ? "User bug report" : "Automatic diagnostics"} - ${shortInstallId}`,
          color: userInitiated ? 3447003 : 5793266,
          fields: [
            { name: "User", value: shortInstallId, inline: true },
            { name: "Version", value: version, inline: true },
            { name: "Channel", value: channel, inline: true },
            { name: "Kind", value: uploadKind, inline: true },
            { name: "Session", value: sessionId, inline: false },
            { name: "Install ID", value: installId, inline: false }
          ]
        }
      ],
      attachments: [
        {
          id: 0,
          filename: fileName,
          description: "String Theory diagnostics package"
        }
      ]
    };

    const form = new FormData();
    form.append("payload_json", JSON.stringify(payload));
    form.append("files[0]", new File([body], fileName, { type: "application/zip" }));

    const discordResponse = await fetch(appendWaitQuery(webhookUrl), {
      method: "POST",
      body: form
    });

    if (!discordResponse.ok) {
      const text = await discordResponse.text();
      return jsonResponse(
        {
          error: "discord_upload_failed",
          status: discordResponse.status,
          details: text.slice(0, 500)
        },
        502
      );
    }

    return jsonResponse({ ok: true, bytes: body.byteLength, fileName }, 200);
  }
};

function resolveWebhookUrl(env, userInitiated) {
  if (userInitiated) {
    return env.DISCORD_WEBHOOK_URL || "";
  }

  return env.DISCORD_AUTO_WEBHOOK_URL || env.DISCORD_WEBHOOK_URL || "";
}

function appendWaitQuery(url) {
  if (url.toLowerCase().includes("wait=")) {
    return url;
  }

  return url.includes("?") ? `${url}&wait=true` : `${url}?wait=true`;
}

function safeHeader(request, name, fallback) {
  const value = request.headers.get(name);
  if (!value) {
    return fallback;
  }

  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed.slice(0, 180) : fallback;
}

function sanitizeFilePart(value) {
  return (value || "unknown")
    .replace(/[^a-zA-Z0-9._-]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 80) || "unknown";
}

function jsonResponse(body, status) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      ...corsHeaders(),
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store"
    }
  });
}

function corsHeaders() {
  return {
    "access-control-allow-origin": "*",
    "access-control-allow-methods": "POST, OPTIONS",
    "access-control-allow-headers": "content-type, x-stringtheory-version, x-stringtheory-channel, x-stringtheory-install-id, x-stringtheory-session-id, x-stringtheory-upload-kind, x-stringtheory-user-initiated"
  };
}
