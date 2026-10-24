using System.Text.Json;
using System.Text.RegularExpressions;
using Galaxy101.Contracts;

namespace Galaxy101.Server.Profile;

/// <summary>Server copy of the client's <c>ProfileValidation</c> / <c>SettingsValidation</c>; the server is the authority.</summary>
public static partial class ProfileRules
{
    // The menu's profile palette (G101-Menu) and the sign-up screen's (the game shell's COLORS: three hues differ).
    public static readonly string[] AvatarColors = ["#FF8A5B", "#FF5E6C", "#E85D9A", "#8B5CF6", "#3D7BFF", "#16B6A8", "#3CCB6E", "#F5A623", "#FF6B6B", "#E8A317", "#E05FA8"];
    public static readonly string[] Planets = ["Satürn", "Mars", "Neptün", "Jüpiter", "Venüs"];
    private static readonly string[] TileSizes = ["Küçük", "Orta", "Büyük"];
    private static readonly string[] Qualities = ["Düşük", "Orta", "Yüksek"];
    private static readonly string[] Languages = ["tr", "en"];

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static readonly string DefaultSettingsJson = JsonSerializer.Serialize(new SettingsDto(), Json);

    [GeneratedRegex(@"\A[\p{L}\p{N} ._]{3,16}\z")]
    private static partial Regex NamePattern();

    public static bool ValidName(string? name) => name is not null && NamePattern().IsMatch(name.Trim());

    public static bool ValidProfile(ProfileUpdateRequest? p) => p is not null && ValidName(p.Name) &&
        AvatarColors.Contains(p.Color) && (p.Status ?? "").Length <= 40 && p.Frame is 0 or 1;

    public static bool ValidSettings(SettingsDto? s) => s is not null &&
        s.Warn is >= 1 and <= 10 && s.Master is >= 0 and <= 100 && s.Music is >= 0 and <= 100 && s.Sfx is >= 0 and <= 100 &&
        TileSizes.Contains(s.Tile) && Qualities.Contains(s.Quality) && Languages.Contains(s.Lang);

    public static int PlanetBit(string planet) => Array.IndexOf(Planets, planet) is var i and >= 0 ? 1 << i : 0;

    public static SettingsDto ReadSettings(string json) => JsonSerializer.Deserialize<SettingsDto>(json, Json) ?? new SettingsDto();
}
