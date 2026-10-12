namespace GoldShopWeb.Services;

/// <summary>Shared presentation for LINE notifications; no transaction logic.</summary>
public static class LineFlexStyle
{
    public const string Leaf = "#D8EB9D";
    public const string Ink = "#29451F";
    public const string Muted = "#697568";
    public const string Accent = "#527A2F";
    public const string Surface = "#F5F8EE";

    public static string? AssetUrl(string? baseUrl, string fileName)
        => Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https"
            ? new Uri(uri, "/images/line/" + fileName).AbsoluteUri : null;

    public static object Header(string title, string? subtitle = null, string? logoUrl = null)
    {
        var lines = new List<object>
        {
            new { type = "text", text = "ออมทองทวีทรัพย์", size = "sm", weight = "bold", color = Accent, wrap = true },
            new { type = "text", text = title, size = "xl", weight = "bold", color = Ink, wrap = true, margin = "sm" }
        };
        if (!string.IsNullOrWhiteSpace(subtitle))
            lines.Add(new { type = "text", text = subtitle, size = "xs", color = Ink, wrap = true, margin = "md" });
        object heading = new { type = "box", layout = "vertical", flex = 1, contents = lines };
        var content = new List<object>();
        if (!string.IsNullOrWhiteSpace(logoUrl))
            content.Add(new { type = "image", url = logoUrl, size = "64px", aspectRatio = "1:1", aspectMode = "fit", flex = 0 });
        content.Add(heading);
        return new
        {
            type = "box", layout = "horizontal", spacing = "md", paddingAll = "xl", backgroundColor = Leaf,
            background = new { type = "linearGradient", angle = "135deg", startColor = "#EAF4CD", endColor = "#D8EB9D" },
            contents = content
        };
    }

    public static object Paper(object content, string? backgroundUrl)
    {
        if (string.IsNullOrWhiteSpace(backgroundUrl)) return content;
        var foreground = System.Text.Json.JsonSerializer.SerializeToNode(content)!.AsObject();
        foreground.Remove("backgroundColor");
        return new
        {
            type = "box", layout = "vertical", paddingAll = "none", backgroundColor = "#FFFFFF",
            contents = new object[]
            {
                new
                {
                    type = "box", layout = "vertical", contents = new object[]
                    {
                        new
                        {
                            type = "box", layout = "vertical", position = "absolute",
                            offsetTop = "0px", offsetBottom = "0px", offsetStart = "0px", offsetEnd = "0px",
                            contents = new[] { new { type = "image", url = backgroundUrl, size = "full", aspectRatio = "1:3", aspectMode = "cover", flex = 1 } }
                        },
                        foreground
                    }
                }
            }
        };
    }

    public static object Row(string label, string? value, string color = Ink, bool bold = false) => new
    {
        type = "box", layout = "horizontal", spacing = "md", margin = "sm",
        contents = new object[]
        {
            new { type = "text", text = label, color = Muted, size = "sm", flex = 4, wrap = true },
            new { type = "text", text = string.IsNullOrWhiteSpace(value) ? "—" : value, color, size = "sm", align = "end", weight = bold ? "bold" : "regular", flex = 6, wrap = true }
        }
    };
}
