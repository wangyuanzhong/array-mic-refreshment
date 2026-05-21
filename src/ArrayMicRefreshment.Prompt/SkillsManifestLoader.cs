using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArrayMicRefreshment.Prompt;

public static class SkillsManifestLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static SkillsManifestDocument LoadFromFile(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Skills manifest not found: {manifestPath}", manifestPath);
        }

        var yaml = File.ReadAllText(manifestPath);
        var doc = Deserializer.Deserialize<SkillsManifestDocument>(yaml);
        if (doc is null)
        {
            throw new InvalidDataException($"Skills manifest is empty or invalid: {manifestPath}");
        }

        if (string.IsNullOrWhiteSpace(doc.Router.SystemPromptFile))
        {
            throw new InvalidDataException("manifest.yaml: router.system_prompt_file is required.");
        }

        if (doc.Router.IntentMap.Count == 0)
        {
            throw new InvalidDataException("manifest.yaml: router.intent_map is required.");
        }

        if (doc.Specialists.Count == 0)
        {
            throw new InvalidDataException("manifest.yaml: specialists is required.");
        }

        return doc;
    }
}
