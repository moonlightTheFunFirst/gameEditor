using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEditor;

public static class AnimationSerializer
{
    private const int SchemaVersion = 1;
    private const string Format = "gameEditor.animation";
    private const string FrameTrackType = "frame";
    private const string EventTrackType = "event";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Save(string path, AnimationDocument document)
    {
        var animationFile = CreateAnimationFile(document);
        var json = JsonSerializer.Serialize(animationFile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static AnimationDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        var animationFile = JsonSerializer.Deserialize<AnimationFile>(json, JsonOptions)
            ?? throw new InvalidOperationException("Animation file is empty or invalid.");

        ValidateHeader(animationFile.Header);
        var document = new AnimationDocument(CreateClip(animationFile))
        {
            FilePath = path,
            IsDirty = false
        };
        return document;
    }

    private static AnimationFile CreateAnimationFile(AnimationDocument document)
    {
        var clip = document.Clip;
        return new AnimationFile
        {
            Header = new AnimationFileHeader
            {
                Format = Format,
                SchemaVersion = SchemaVersion,
                CreatedWith = "gameEditor",
                EditorVersion = Application.ProductVersion,
                SavedAt = DateTimeOffset.Now.ToString("O")
            },
            Clip = new AnimationFileClip
            {
                Name = clip.Name,
                DurationMs = clip.DurationMs,
                Fps = clip.Fps,
                Loop = clip.Loop,
                Tracks =
                [
                    new AnimationFileTrack
                    {
                        Type = FrameTrackType,
                        Target = "sprite",
                        Keys = clip.Frames.Select(frame => new AnimationFileKey
                        {
                            TimeMs = frame.TimeMs,
                            TileSetId = frame.TileSetId,
                            TileId = frame.TileId
                        }).ToList()
                    },
                    new AnimationFileTrack
                    {
                        Type = EventTrackType,
                        Target = "timeline",
                        Keys = clip.Events
                            .Where(animationEvent => !string.IsNullOrWhiteSpace(animationEvent.Name))
                            .Select(animationEvent => new AnimationFileKey
                            {
                                TimeMs = animationEvent.TimeMs,
                                Name = animationEvent.Name,
                                Memo = animationEvent.Memo
                            }).ToList()
                    }
                ]
            }
        };
    }

    private static AnimationClip CreateClip(AnimationFile file)
    {
        var clip = new AnimationClip
        {
            Name = string.IsNullOrWhiteSpace(file.Clip.Name) ? "NewAnimation" : file.Clip.Name,
            Fps = Math.Clamp(file.Clip.Fps, 1, 60),
            Loop = file.Clip.Loop
        };

        var frameTrack = file.Clip.Tracks.FirstOrDefault(track => string.Equals(track.Type, FrameTrackType, StringComparison.OrdinalIgnoreCase));
        if (frameTrack is not null)
        {
            foreach (var key in frameTrack.Keys.OrderBy(key => key.TimeMs))
            {
                if (string.IsNullOrWhiteSpace(key.TileSetId) || key.TileId is not { } tileId)
                {
                    continue;
                }

                clip.Frames.Add(new AnimationFrameKey
                {
                    TimeMs = Math.Max(0, key.TimeMs),
                    TileSetId = key.TileSetId,
                    TileId = tileId
                });
            }
        }

        var eventTrack = file.Clip.Tracks.FirstOrDefault(track => string.Equals(track.Type, EventTrackType, StringComparison.OrdinalIgnoreCase));
        if (eventTrack is not null)
        {
            foreach (var key in eventTrack.Keys.OrderBy(key => key.TimeMs))
            {
                if (string.IsNullOrWhiteSpace(key.Name))
                {
                    continue;
                }

                clip.Events.Add(new AnimationEventKey
                {
                    TimeMs = Math.Max(0, key.TimeMs),
                    Name = key.Name,
                    Memo = key.Memo ?? ""
                });
            }
        }

        if (clip.Frames.Count == 0)
        {
            throw new InvalidOperationException("Animation has no frame keys.");
        }

        NormalizeFrameTimes(clip);
        return clip;
    }

    public static void NormalizeFrameTimes(AnimationClip clip)
    {
        for (var i = 0; i < clip.Frames.Count; i++)
        {
            clip.Frames[i].TimeMs = i * clip.FrameDurationMs;
        }

        foreach (var animationEvent in clip.Events)
        {
            var frameIndex = Math.Clamp(
                (int)Math.Round(animationEvent.TimeMs / (double)clip.FrameDurationMs),
                0,
                Math.Max(0, clip.Frames.Count - 1));
            animationEvent.TimeMs = frameIndex * clip.FrameDurationMs;
        }
    }

    private static void ValidateHeader(AnimationFileHeader header)
    {
        if (!string.Equals(header.Format, Format, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported animation format: {header.Format}");
        }

        if (header.SchemaVersion > SchemaVersion)
        {
            throw new InvalidOperationException($"Unsupported animation schema version: {header.SchemaVersion}");
        }
    }
}
