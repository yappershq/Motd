namespace Motd.Shared;

/// <summary>
/// An immutable MOTD payload — the URL to open in CS2's in-game InfoPanel. Pointer-safe (only
/// primitives, no native ModSharp types) so it can cross the plugin boundary freely.
///
/// CS2 only navigates absolute <c>http(s)</c> URLs in this panel — inline HTML is not rendered, and
/// the panel uses the page's own <c>&lt;title&gt;</c> — so the URL is all we carry.
/// </summary>
/// <param name="Url">An absolute <c>http://</c> / <c>https://</c> URL.</param>
public readonly record struct MotdContent(string Url)
{
    /// <summary>Create a MOTD payload for an absolute http(s) URL.</summary>
    public static MotdContent ForUrl(string url) => new(url);
}
