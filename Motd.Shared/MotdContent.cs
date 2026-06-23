namespace Motd.Shared;

/// <summary>
/// What kind of payload a <see cref="MotdContent"/> carries.
/// </summary>
public enum MotdKind
{
    /// <summary>
    /// <see cref="MotdContent.Value"/> is an absolute <c>http://</c> / <c>https://</c> URL.
    /// This is the only mode CS2 reliably renders in the in-game HTML panel.
    /// </summary>
    Url = 0,

    /// <summary>
    /// <see cref="MotdContent.Value"/> is a raw HTML document.
    /// NOTE: CS2 does NOT render inline HTML in the MOTD/InfoPanel — only URLs navigate.
    /// When <see cref="Url"/> is unavailable the implementation falls back to hosting the
    /// HTML at a <c>data:</c>-style or local URL is BLOCKED by the engine, so callers that
    /// truly need HTML must host it themselves and pass the URL with <see cref="MotdKind.Url"/>.
    /// Kept in the contract for forward-compat; current impl logs a warning and no-ops for Html.
    /// </summary>
    Html = 1,
}

/// <summary>
/// An immutable MOTD payload. Pointer-safe: only primitives, no native ModSharp types,
/// so this can cross the plugin boundary freely.
/// </summary>
/// <param name="Kind">URL or HTML. See <see cref="MotdKind"/> for the CS2 limitation.</param>
/// <param name="Value">The URL (or HTML body for <see cref="MotdKind.Html"/>).</param>
/// <param name="Title">Optional title; advisory only — CS2's panel uses the page's own title.</param>
public readonly record struct MotdContent(MotdKind Kind, string Value, string? Title = null)
{
    /// <summary>Convenience factory for the supported URL mode.</summary>
    public static MotdContent ForUrl(string url, string? title = null)
        => new(MotdKind.Url, url, title);

    /// <summary>
    /// Convenience factory for HTML (see <see cref="MotdKind.Html"/> caveat — not rendered by CS2).
    /// </summary>
    public static MotdContent ForHtml(string html, string? title = null)
        => new(MotdKind.Html, html, title);
}
