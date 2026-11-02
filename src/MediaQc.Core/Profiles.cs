using System.Text.Json;
using System.Text.Json.Serialization;

namespace MediaQc;

/// <summary>
/// The profiles mediaqc ships and those a delivery describes in a file (docs/decisions/2026-11-01-profiles.md): each
/// named one holds a file to its spec's loudness and true peak, the other checks as <see cref="Profile.EbuR128"/>.
/// </summary>
public static class Profiles
{
    /// <summary>ATSC A/85 (US): -24 LKFS within ±2 LU, true peak at most -2 dBTP (A/85's own document).</summary>
    public static Profile AtscA85 { get; } = Profile.EbuR128 with
    {
        Name = "atsc-a85",
        TargetLoudness = -24,
        LoudnessPass = 2,
        LoudnessWarning = 2,
        MaxTruePeak = -2,
    };

    /// <summary>
    /// Free TV OP-59 (Australia), Issue 4 (October 2018): -24 LKFS within ±1 LU (4(c)), true peak at most -2 dBTP (5(h)),
    /// full mix with BS.1770's gate. Its exception for programmes "where exact normalisation to target level is not
    /// practicable" is left to a profile file (docs/decisions/2026-11-02-op59-arib-figures.md).
    /// </summary>
    public static Profile Op59 { get; } = Profile.EbuR128 with
    {
        Name = "op-59",
        TargetLoudness = -24,
        LoudnessPass = 1,
        LoudnessWarning = 1,
        MaxTruePeak = -2,
    };

    /// <summary>
    /// ARIB TR-B32 (Japan), version 1.5-E1 (2016-09-29): -24 LKFS within ±1 LU (2.1.4, 2.1.5), true peak at most
    /// -1 dBTP on every channel (2.2.4), full mix with BS.1770's gate. Its exception for programmes made quieter for
    /// "creative production requirements" (2.1.6) is left to a profile file
    /// (docs/decisions/2026-11-02-op59-arib-figures.md).
    /// </summary>
    public static Profile AribTrB32 { get; } = Profile.EbuR128 with
    {
        Name = "arib-tr-b32",
        TargetLoudness = -24,
        LoudnessPass = 1,
        LoudnessWarning = 1,
        MaxTruePeak = -1,
    };

    /// <summary>Every profile mediaqc ships, the default first.</summary>
    public static IReadOnlyList<Profile> All { get; } = [Profile.EbuR128, AtscA85, Op59, AribTrB32];

    /// <summary>
    /// The profile <paramref name="nameOrFile"/> names (one of <see cref="All"/>), else the one the JSON file at that
    /// path describes (<see cref="Read"/>).
    /// </summary>
    /// <exception cref="ArgumentException">Neither a profile's name nor a profile file.</exception>
    public static Profile Get(string nameOrFile)
    {
        ArgumentNullException.ThrowIfNull(nameOrFile);
        var named = All.FirstOrDefault(p => string.Equals(p.Name, nameOrFile, StringComparison.OrdinalIgnoreCase));
        if (named is not null)
            return named;
        if (!File.Exists(nameOrFile))
            throw new ArgumentException($"No profile {nameOrFile}: the profiles are {string.Join(", ", All.Select(p => p.Name))}, or a profile file (.json).", nameof(nameOrFile));
        return Read(File.ReadAllText(nameOrFile), nameOrFile);
    }

    /// <summary>
    /// A profile from JSON: <c>name</c>, optionally the profile it starts from (<c>basedOn</c>, default ebu-r128), and
    /// any of its thresholds and settings to change (camelCase: <c>targetLoudness</c>, <c>loudnessPass</c>,
    /// <c>loudnessWarning</c>, <c>maxTruePeak</c>, <c>minBlack</c>, <c>blackPixelThreshold</c>,
    /// <c>blackPictureThreshold</c>, <c>minSilence</c>, <c>silenceNoise</c>, <c>maxOutOfRange</c>,
    /// <c>rangeMergeGap</c>).
    /// </summary>
    /// <exception cref="ArgumentException">Not such JSON, or a value out of its range.</exception>
    public static Profile Read(string json, string source = "the profile")
    {
        ArgumentNullException.ThrowIfNull(json);
        ProfileFile file;
        try
        {
            file = JsonSerializer.Deserialize(json, ProfileFileContext.Default.ProfileFile)
                ?? throw new ArgumentException($"{source} is empty.", nameof(json));
        }
        catch (JsonException e)
        {
            throw new ArgumentException($"{source} isn't a profile: {e.Message}", nameof(json), e);
        }

        if (string.IsNullOrWhiteSpace(file.Name))
            throw new ArgumentException($"{source} has no name.", nameof(json));
        if (file.BasedOn is { } basis && basis.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{source}: basedOn names one of {string.Join(", ", All.Select(p => p.Name))}.", nameof(json));
        var start = file.BasedOn is null ? Profile.EbuR128 : Get(file.BasedOn);
        var profile = start with
        {
            Name = file.Name,
            TargetLoudness = file.TargetLoudness ?? start.TargetLoudness,
            LoudnessPass = file.LoudnessPass ?? start.LoudnessPass,
            LoudnessWarning = file.LoudnessWarning ?? Math.Max(start.LoudnessWarning, file.LoudnessPass ?? 0),
            MaxTruePeak = file.MaxTruePeak ?? start.MaxTruePeak,
            MinBlack = file.MinBlack ?? start.MinBlack,
            BlackPixelThreshold = file.BlackPixelThreshold ?? start.BlackPixelThreshold,
            BlackPictureThreshold = file.BlackPictureThreshold ?? start.BlackPictureThreshold,
            MinSilence = file.MinSilence ?? start.MinSilence,
            SilenceNoise = file.SilenceNoise ?? start.SilenceNoise,
            MaxOutOfRange = file.MaxOutOfRange ?? start.MaxOutOfRange,
            RangeMergeGap = file.RangeMergeGap ?? start.RangeMergeGap,
        };
        if (profile.LoudnessPass < 0 || profile.LoudnessWarning < profile.LoudnessPass)
            throw new ArgumentException($"{source}: loudnessPass is at least 0 and loudnessWarning at least loudnessPass.", nameof(json));
        if (profile.MinBlack <= 0 || profile.MinSilence <= 0 || profile.RangeMergeGap < 0)
            throw new ArgumentException($"{source}: minBlack and minSilence are positive, rangeMergeGap at least 0.", nameof(json));
        if (profile.BlackPixelThreshold is < 0 or > 1 || profile.BlackPictureThreshold is < 0 or > 1 || profile.MaxOutOfRange is < 0 or > 1)
            throw new ArgumentException($"{source}: blackPixelThreshold, blackPictureThreshold and maxOutOfRange are 0 to 1.", nameof(json));
        return profile;
    }
}

/// <summary>A profile file's fields: its name, the profile it starts from, and what it changes (null: as that one).</summary>
internal sealed record ProfileFile(
    string? Name,
    string? BasedOn,
    double? TargetLoudness,
    double? LoudnessPass,
    double? LoudnessWarning,
    double? MaxTruePeak,
    double? MinBlack,
    double? BlackPixelThreshold,
    double? BlackPictureThreshold,
    double? MinSilence,
    double? SilenceNoise,
    double? MaxOutOfRange,
    double? RangeMergeGap);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ProfileFile))]
internal partial class ProfileFileContext : JsonSerializerContext;
