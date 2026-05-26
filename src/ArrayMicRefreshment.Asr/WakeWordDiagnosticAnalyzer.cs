using ArrayMicRefreshment.Core;

namespace ArrayMicRefreshment.Asr;

internal static class WakeWordDiagnosticAnalyzer
{
    public static string AnalyzeListenPath(WakeWordListenStats listen, string detectorId, bool detectorRunning)
    {
        if (detectorId.Contains("stub", StringComparison.OrdinalIgnoreCase))
        {
            return "KWS 模型未加载（stub 检测器），无法从真实语音识别唤醒词。"
                + "请确认 models/sherpa-kws 存在并运行 download-models.ps1 -IncludeKws。";
        }

        if (!listen.Listening)
        {
            return "当前未处于唤醒监听状态（可能被 PTT 占用麦克风或已停止监听）。";
        }

        if (listen.DictationActive)
        {
            return "唤醒后会话进行中，KWS 已暂停；此窗口统计的是 dictation 而非监听。";
        }

        if (!detectorRunning)
        {
            return "麦克风有数据但 KWS 检测器未运行（Start/Stop 时序异常或 dictation 未正确恢复监听）。";
        }

        if (listen.CaptureBytes > 0 && listen.ChunksFed == 0)
        {
            return "麦克风有原始数据但未产生 16kHz mono 块送入 KWS（重采样或 chunk 过短）。";
        }

        if (listen.CapturePeakRms < 0.003)
        {
            return "麦克风信号过弱（peakRms < 0.003）。检查录音设备、系统麦克风权限与输入音量。";
        }

        if (listen.CapturePeakRms >= 0.015)
        {
            return "检测到较大音量但未触发唤醒。可能原因：唤醒词发音/文本不匹配、KWS 阈值偏高、"
                + "或 wake-keywords.txt 编码与短语不一致（见日志中的 keywordLine）。";
        }

        if (listen.CapturePeakRms >= 0.006)
        {
            return "有中等音量输入但未触发唤醒。请更清晰地说出完整唤醒词，或提高灵敏度。";
        }

        return "环境较安静或尚未说出唤醒词；继续监听中。";
    }
}
