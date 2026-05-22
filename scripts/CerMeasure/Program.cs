using System.Globalization;
using System.Speech.Synthesis;
using System.Text;
using System.Text.Json;
using ArrayMicRefreshment.Asr;
using ArrayMicRefreshment.Core;
using ArrayMicRefreshment.Core.Audio;

var repoRoot = FindRepoRoot();
var testSetPath = Path.Combine(repoRoot, "scripts", "cer-test-set.json");
var audioDir = Path.Combine(repoRoot, "scripts", "cer-audio");
var modelsDir = Path.Combine(repoRoot, "models");
var outputPath = Path.Combine(repoRoot, "docs", "CER_BASELINE.md");

Directory.CreateDirectory(audioDir);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var entries = JsonSerializer.Deserialize<List<CerEntry>>(
    await File.ReadAllTextAsync(testSetPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidOperationException("cer-test-set.json is empty.");

using var asr = SenseVoiceAsr.CreateFromSettings(new AppSettings { ModelsDirectory = modelsDir });

var rows = new List<CerRow>();
for (var i = 0; i < entries.Count; i++)
{
    var entry = entries[i];
    var wavPath = Path.Combine(audioDir, $"{i}.wav");
    if (!File.Exists(wavPath))
    {
        SynthesizeTo16KHzWav(entry.Text, wavPath);
    }

    var utterance = LoadWavUtterance(wavPath);
    var predicted = await asr.RecognizeUtteranceAsync(utterance, CancellationToken.None);
    var plain = SenseVoiceTextExtractor.ExtractPlainText(predicted);
    var cer = ComputeCer(entry.Text, plain);
    rows.Add(new CerRow(entry.Text, plain, cer, entry.Lang));
    Console.WriteLine($"[{i + 1}/{entries.Count}] CER={cer:P1} ref={entry.Text} hyp={plain}");
}

var meanCer = rows.Average(r => r.Cer);
var codeTermRows = rows.Where(r => r.Lang.Contains("mix", StringComparison.OrdinalIgnoreCase)
    || r.Reference.Contains("Api", StringComparison.OrdinalIgnoreCase)
    || r.Reference.Contains("TypeScript", StringComparison.OrdinalIgnoreCase)
    || r.Reference.Contains("async", StringComparison.OrdinalIgnoreCase)
    || r.Reference.Contains("migration", StringComparison.OrdinalIgnoreCase)).ToList();
var codeMean = codeTermRows.Count > 0 ? codeTermRows.Average(r => r.Cer) : meanCer;

var sb = new StringBuilder();
sb.AppendLine("# CER Baseline — SenseVoice int8 (Phase 5)");
sb.AppendLine();
sb.AppendLine($"Measured on **{DateTime.Now:yyyy-MM-dd}** with `scripts/Measure-Cer.ps1` / `scripts/CerMeasure`.");
sb.AppendLine($"Model: `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-int8-2025-09-09` under `models/`.");
sb.AppendLine();
sb.AppendLine("| # | Lang | Reference | Recognized | CER % |");
sb.AppendLine("|---|------|-----------|------------|-------|");
for (var i = 0; i < rows.Count; i++)
{
    var r = rows[i];
    sb.AppendLine(CultureInfo.InvariantCulture,
        $"| {i + 1} | {entries[i].Lang} | {EscapeMd(r.Reference)} | {EscapeMd(r.Hypothesis)} | {r.Cer * 100:F1} |");
}

sb.AppendLine();
sb.AppendLine($"**Mean CER:** {meanCer * 100:F1}%");
sb.AppendLine($"**Code-term subset mean CER:** {codeMean * 100:F1}% ({codeTermRows.Count} utterances)");
sb.AppendLine();
sb.AppendLine("## Conclusion");
if (codeMean > 0.25)
{
    sb.AppendLine(
        "Code-mixed / programming terminology shows elevated CER (>25%). README future-work suggests evaluating **Qwen3-ASR** for code-heavy dictation.");
}
else
{
    sb.AppendLine(
        "Overall CER is within expected range for SenseVoice on TTS-synthesized prompts. Code-mixed terms remain somewhat harder but below the 25% escalation threshold.");
}

await File.WriteAllTextAsync(outputPath, sb.ToString(), Encoding.UTF8);
Console.WriteLine($"Wrote {outputPath}");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "ArrayMicRefreshment.sln")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    return Directory.GetCurrentDirectory();
}

static void SynthesizeTo16KHzWav(string text, string wavPath)
{
    var temp = Path.ChangeExtension(wavPath, ".tmp.wav");
    using (var synth = new SpeechSynthesizer())
    {
        synth.SetOutputToWaveFile(temp);
        synth.Speak(text);
    }

    var pcm = ReadWavPcm(temp, out var sampleRate);
    var resampled = PcmConverters.Ensure16KHzMonoPcm16Le(pcm, sampleRate);
    WritePcm16Wav(wavPath, resampled, PcmConverters.TargetSampleRate);
    File.Delete(temp);
}

static byte[] ReadWavPcm(string path, out int sampleRate)
{
    using var fs = File.OpenRead(path);
    using var reader = new BinaryReader(fs);
    if (new string(reader.ReadChars(4)) != "RIFF")
    {
        throw new InvalidDataException($"Not a RIFF file: {path}");
    }

    _ = reader.ReadInt32();
    if (new string(reader.ReadChars(4)) != "WAVE")
    {
        throw new InvalidDataException($"Not a WAVE file: {path}");
    }

    short channels = 1;
    sampleRate = 16000;
    byte[]? pcm = null;

    while (fs.Position < fs.Length)
    {
        var chunkId = new string(reader.ReadChars(4));
        var chunkSize = reader.ReadInt32();
        switch (chunkId)
        {
            case "fmt ":
                _ = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                fs.Position += chunkSize - 8;
                break;
            case "data":
                pcm = reader.ReadBytes(chunkSize);
                break;
            default:
                fs.Position += chunkSize;
                break;
        }
    }

    if (pcm is null)
    {
        throw new InvalidDataException($"No data chunk in {path}");
    }

    if (channels == 2)
    {
        pcm = StereoToMono(pcm);
    }

    return pcm;
}

static byte[] StereoToMono(byte[] stereo)
{
    var mono = new byte[stereo.Length / 2];
    for (var i = 0; i < mono.Length; i += 2)
    {
        var left = BitConverter.ToInt16(stereo, i * 2);
        var right = BitConverter.ToInt16(stereo, i * 2 + 2);
        var avg = (short)((left + right) / 2);
        mono[i] = (byte)(avg & 0xFF);
        mono[i + 1] = (byte)((avg >> 8) & 0xFF);
    }

    return mono;
}

static void WritePcm16Wav(string path, byte[] pcm, int sampleRate)
{
    using var fs = File.Create(path);
    using var w = new BinaryWriter(fs);
    var dataSize = pcm.Length;
    w.Write(Encoding.ASCII.GetBytes("RIFF"));
    w.Write(36 + dataSize);
    w.Write(Encoding.ASCII.GetBytes("WAVE"));
    w.Write(Encoding.ASCII.GetBytes("fmt "));
    w.Write(16);
    w.Write((short)1);
    w.Write((short)1);
    w.Write(sampleRate);
    w.Write(sampleRate * 2);
    w.Write((short)2);
    w.Write((short)16);
    w.Write(Encoding.ASCII.GetBytes("data"));
    w.Write(dataSize);
    w.Write(pcm);
}

static AudioUtterance LoadWavUtterance(string path)
{
    var pcm = ReadWavPcm(path, out var sampleRate);
    var normalized = PcmConverters.Ensure16KHzMonoPcm16Le(pcm, sampleRate);
    return new AudioUtterance
    {
        Pcm16LeMono = normalized,
        SampleRate = PcmConverters.TargetSampleRate,
        Duration = TimeSpan.FromSeconds((double)normalized.Length / (2 * PcmConverters.TargetSampleRate)),
    };
}

static double ComputeCer(string reference, string hypothesis)
{
    if (string.IsNullOrEmpty(reference))
    {
        return string.IsNullOrEmpty(hypothesis) ? 0 : 1;
    }

    var dist = Levenshtein(reference, hypothesis);
    return (double)dist / reference.Length;
}

static int Levenshtein(string a, string b)
{
    var m = a.Length;
    var n = b.Length;
    var dp = new int[m + 1, n + 1];
    for (var i = 0; i <= m; i++)
    {
        dp[i, 0] = i;
    }

    for (var j = 0; j <= n; j++)
    {
        dp[0, j] = j;
    }

    for (var i = 1; i <= m; i++)
    {
        for (var j = 1; j <= n; j++)
        {
            var cost = a[i - 1] == b[j - 1] ? 0 : 1;
            dp[i, j] = Math.Min(
                Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                dp[i - 1, j - 1] + cost);
        }
    }

    return dp[m, n];
}

static string EscapeMd(string s) => s.Replace("|", "\\|", StringComparison.Ordinal);

internal sealed record CerEntry(string Text, string Lang);

internal sealed record CerRow(string Reference, string Hypothesis, double Cer, string Lang);
