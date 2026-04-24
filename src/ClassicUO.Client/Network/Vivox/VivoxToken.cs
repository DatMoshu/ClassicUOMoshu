// VivoxToken.cs — Pure C# JWT token generation for Vivox Core SDK
// No external dependencies. Run this FIRST to validate credentials before needing the DLL.
// Vivox docs: https://docs.vivox.com/v5/general/unity/5_15_0/en-us/access-token-guide/

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace ClassicUO.Network.Vivox;

public static class VivoxToken
{
    private static long _serial = 0;

    /// <summary>
    /// Generates a Vivox login token (vxa=login).
    /// Call once per login session.
    /// </summary>
    public static string GenerateLoginToken(
        string issuer,
        string secretKey,
        string userId,
        string domain,        // must match the domain in the login request's acct_name
        int expirySeconds = 90)
    {
        // f = "from" SIP URI — must exactly match the acct_name sent in the login request
        string from = $"sip:.{issuer}.{userId}.@{domain}";
        return BuildToken(issuer, secretKey, "login", from, to: null, expirySeconds);
    }

    /// <summary>
    /// Generates a Vivox channel join token (vxa=join).
    /// Call once per channel per session.
    /// </summary>
    public static string GenerateJoinToken(
        string issuer,
        string secretKey,
        string userId,
        string channelName,
        string domain,
        int expirySeconds = 90)
    {
        string from = $"sip:.{issuer}.{userId}.@{domain}";
        string to   = $"sip:confctl-g-{issuer}.{channelName}@{domain}";
        return BuildToken(issuer, secretKey, "join", from, to, expirySeconds);
    }

    /// <summary>
    /// Generates a Vivox muted join token (vxa=join_muted).
    /// Use for faction-only broadcast channels where most users listen.
    /// </summary>
    public static string GenerateJoinMutedToken(
        string issuer,
        string secretKey,
        string userId,
        string channelName,
        string domain,
        int expirySeconds = 90)
    {
        string from = $"sip:.{issuer}.{userId}.@{domain}";
        string to   = $"sip:confctl-g-{issuer}.{channelName}@{domain}";
        return BuildToken(issuer, secretKey, "join_muted", from, to, expirySeconds);
    }

    /// <summary>
    /// Generates a 3D positional channel join token (vxa=join, channel type positional).
    /// Use for proximity voice chat.
    /// Channel URI for positional uses confctl-d- prefix instead of confctl-g-.
    /// </summary>
    public static string GeneratePositionalJoinToken(
        string issuer,
        string secretKey,
        string userId,
        string channelName,
        string domain,
        int expirySeconds = 90)
    {
        string from = $"sip:.{issuer}.{userId}.@{domain}";
        // positional channel uses "d" (dynamic/3D) prefix vs "g" (group)
        string to   = $"sip:confctl-d-{issuer}.{channelName}@{domain}";
        return BuildToken(issuer, secretKey, "join", from, to, expirySeconds);
    }

    /// <summary>
    /// Generates a join token against an already-formed channel URI. Used when
    /// the URI was built by the SDK (e.g. vx_get_positional_channel_uri, which
    /// encodes 3D attenuation properties into the channel name). The JWT "to"
    /// claim must match the join-request URI EXACTLY, including any embedded
    /// !p-... segment, or the server rejects the join.
    /// </summary>
    public static string GenerateJoinTokenForUri(
        string issuer,
        string secretKey,
        string userId,
        string channelUri,
        string domain,
        int expirySeconds = 90)
    {
        string from = $"sip:.{issuer}.{userId}.@{domain}";
        return BuildToken(issuer, secretKey, "join", from, channelUri, expirySeconds);
    }

    private static string BuildToken(
        string issuer,
        string secretKey,
        string action,
        string from,
        string? to,
        int expirySeconds)
    {
        long exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expirySeconds;
        long vxi = Interlocked.Increment(ref _serial);

        string headerJson = "{\"alg\":\"HS256\",\"typ\":\"JWT\"}";

        var sb = new StringBuilder();
        sb.Append('{');
        sb.Append($"\"iss\":\"{issuer}\",");
        sb.Append($"\"exp\":{exp},");
        sb.Append($"\"vxa\":\"{action}\",");
        sb.Append($"\"vxi\":{vxi},");
        sb.Append($"\"f\":\"{from}\"");
        if (to != null)
        {
            sb.Append($",\"t\":\"{to}\"");
        }
        sb.Append('}');
        
        string payloadJson = sb.ToString();

        string headerB64  = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        string payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        string unsigned = $"{headerB64}.{payloadB64}";

        byte[] key = Encoding.UTF8.GetBytes(secretKey);
        using var hmac = new HMACSHA256(key);
        byte[] sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(unsigned));

        return $"{unsigned}.{Base64UrlEncode(sig)}";
    }

    private static string Base64UrlEncode(byte[] input)
        => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
