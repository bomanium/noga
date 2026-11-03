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
    /// The folder of delivery templates shipped beside the program (profiles/: profile files with a format to copy and
    /// adjust, docs/decisions/2026-11-03-delivery-format.md).
    /// </summary>
    public static string TemplateFolder => Path.Combine(AppContext.BaseDirectory, "profiles");

    /// <summary>The templates in <see cref="TemplateFolder"/>, by name (the file's, without .json).</summary>
    public static IReadOnlyList<string> Templates =>
        Directory.Exists(TemplateFolder) ? [.. Directory.GetFiles(TemplateFolder, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().Order(StringComparer.Ordinal)] : [];

    /// <summary>
    /// The profile <paramref name="nameOrFile"/> names (one of <see cref="All"/>), else the one the JSON file at that
    /// path describes (<see cref="Read"/>), else the template of that name (<see cref="Templates"/>).
    /// </summary>
    /// <exception cref="ArgumentException">Neither a profile's name, a profile file nor a template.</exception>
    public static Profile Get(string nameOrFile)
    {
        ArgumentNullException.ThrowIfNull(nameOrFile);
        var named = All.FirstOrDefault(p => string.Equals(p.Name, nameOrFile, StringComparison.OrdinalIgnoreCase));
        if (named is not null)
            return named;
        if (File.Exists(nameOrFile))
            return Read(File.ReadAllText(nameOrFile), nameOrFile);
        string template = Path.Combine(TemplateFolder, nameOrFile + ".json");
        if (nameOrFile.IndexOfAny(['/', '\\']) < 0 && !nameOrFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && File.Exists(template))
            return Read(File.ReadAllText(template), template);
        string templates = Templates.Count == 0 ? "" : $"; the templates are {string.Join(", ", Templates)}";
        throw new ArgumentException($"No profile {nameOrFile}: the profiles are {string.Join(", ", All.Select(p => p.Name))}{templates}, or a profile file (.json).", nameof(nameOrFile));
    }

    // A format's values that could never match: a scan or frame rate that isn't one, sizes and counts below 1.
    static void Check(FormatSpec format, string source)
    {
        if (format.Scan is { } scan && !FormatSpec.Scans.Contains(scan, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"{source}: format.scan is one of {string.Join(", ", FormatSpec.Scans)}.", nameof(source));
        if (format.FrameRate is { } rate && VideoFormat.Rational(rate) is not > 0)
            throw new ArgumentException($"{source}: format.frameRate is a rate like 25, 25/1, 30000/1001 or 29.97.", nameof(source));
        if (format.Width is < 1 || format.Height is < 1 || format.BitDepth is < 1 || format.AudioTracks is < 0
            || format.SampleRate is < 1 || format.AudioChannels is < 1 || format.AudioBitDepth is < 1)
        {
            throw new ArgumentException($"{source}: format's sizes, depths, rates and channels are positive, audioTracks at least 0.", nameof(source));
        }

        if (format.MinDuration is < 0 || format.MaxDuration is < 0 || format.MinDuration > format.MaxDuration)
            throw new ArgumentException($"{source}: format's minDuration and maxDuration are at least 0, the first no more than the second.", nameof(source));
    }

    /// <summary>
    /// A profile from JSON: <c>name</c>, optionally the profile it starts from (<c>basedOn</c>, default ebu-r128), and
    /// any of its thresholds and settings to change (camelCase: <c>targetLoudness</c>, <c>loudnessPass</c>,
    /// <c>loudnessWarning</c>, <c>maxTruePeak</c>, <c>minBlack</c>, <c>blackPixelThreshold</c>,
    /// <c>blackPictureThreshold</c>, <c>minSilence</c>, <c>silenceNoise</c>, <c>maxOutOfRange</c>,
    /// <c>rangeMergeGap</c>, <c>minPhase</c>, <c>clipRun</c>), and <c>format</c>, the delivery's format (<see cref="FormatSpec"/>: <c>container</c>,
    /// <c>videoCodec</c>, <c>videoProfile</c>, <c>width</c>, <c>height</c>, <c>frameRate</c>, <c>scan</c>, <c>fieldOrder</c>,
    /// <c>pixelFormat</c>, <c>bitDepth</c>, <c>colourPrimaries</c>, <c>transfer</c>, <c>matrix</c>, <c>colourRange</c>,
    /// <c>displayAspect</c>, <c>audioTracks</c>, <c>audioCodec</c>, <c>sampleRate</c>, <c>audioChannels</c>,
    /// <c>audioBitDepth</c>, <c>minDuration</c>, <c>maxDuration</c>, <c>startTimecode</c>).
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
            MinPhase = file.MinPhase ?? start.MinPhase,
            ClipRun = file.ClipRun ?? start.ClipRun,
            Format = file.Format ?? start.Format,
        };
        if (profile.LoudnessPass < 0 || profile.LoudnessWarning < profile.LoudnessPass)
            throw new ArgumentException($"{source}: loudnessPass is at least 0 and loudnessWarning at least loudnessPass.", nameof(json));
        if (profile.MinBlack <= 0 || profile.MinSilence <= 0 || profile.RangeMergeGap < 0)
            throw new ArgumentException($"{source}: minBlack and minSilence are positive, rangeMergeGap at least 0.", nameof(json));
        if (profile.BlackPixelThreshold is < 0 or > 1 || profile.BlackPictureThreshold is < 0 or > 1 || profile.MaxOutOfRange is < 0 or > 1)
            throw new ArgumentException($"{source}: blackPixelThreshold, blackPictureThreshold and maxOutOfRange are 0 to 1.", nameof(json));
        if (profile.MinPhase is < -1 or > 1 || profile.ClipRun < 1)
            throw new ArgumentException($"{source}: minPhase is -1 to 1, clipRun at least 1.", nameof(json));
        if (profile.Format is { } format)
            Check(format, source);
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
    double? RangeMergeGap,
    double? MinPhase,
    int? ClipRun,
    FormatSpec? Format);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ProfileFile))]
[JsonSerializable(typeof(FormatSpec))]
internal partial class ProfileFileContext : JsonSerializerContext;
