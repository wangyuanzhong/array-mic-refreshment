using YamlDotNet.Serialization;

namespace ArrayMicRefreshment.Prompt;

public sealed class SkillsManifestDocument
{
    public int Version { get; set; }

    public RouterSection Router { get; set; } = new();

    public Dictionary<string, SpecialistSection> Specialists { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [YamlMember(Alias = "optional_skills")]
    public Dictionary<string, OptionalSkillSection> OptionalSkills { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RouterSection
{
    [YamlMember(Alias = "system_prompt_file")]
    public string SystemPromptFile { get; set; } = string.Empty;

    [YamlMember(Alias = "intent_map")]
    public Dictionary<string, string> IntentMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SpecialistSection
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Stack { get; set; } = new();
}

public sealed class OptionalSkillSection
{
    public string File { get; set; } = string.Empty;

    public string? Note { get; set; }
}
