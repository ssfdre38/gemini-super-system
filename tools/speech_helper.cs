using System;
using System.IO;
using System.Speech.Synthesis;
using System.Text;

namespace GeminiSuperSpeech {
    class Program {
        [STAThread]
        static int Main(string[] args) {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 0) {
                PrintUsage();
                return 1;
            }

            string command = args[0].ToLowerInvariant();

            try {
                using (var synth = new SpeechSynthesizer()) {
                    if (command == "list") {
                        return ListVoices(synth);
                    } else if (command == "speak") {
                        if (args.Length < 2) {
                            Console.WriteLine("{\"success\":false,\"error\":\"Missing text argument for speak\"}");
                            return 1;
                        }
                        string text = args[1];
                        int rate = args.Length > 2 ? ParseInt(args[2], 0) : 0;
                        int volume = args.Length > 3 ? ParseInt(args[3], 100) : 100;
                        string voiceName = args.Length > 4 ? args[4] : null;

                        return SpeakText(synth, text, rate, volume, voiceName);
                    } else if (command == "wav") {
                        if (args.Length < 3) {
                            Console.WriteLine("{\"success\":false,\"error\":\"Missing text or outputPath for wav\"}");
                            return 1;
                        }
                        string text = args[1];
                        string outputPath = args[2];
                        int rate = args.Length > 3 ? ParseInt(args[3], 0) : 0;
                        int volume = args.Length > 4 ? ParseInt(args[4], 100) : 100;
                        string voiceName = args.Length > 5 ? args[5] : null;

                        return SynthesizeToWav(synth, text, outputPath, rate, volume, voiceName);
                    } else {
                        Console.WriteLine("{\"success\":false,\"error\":\"Unknown command: " + command + "\"}");
                        return 1;
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
                return 1;
            }
        }

        static int ListVoices(SpeechSynthesizer synth) {
            var voices = synth.GetInstalledVoices();
            var sb = new StringBuilder();
            sb.Append("{\"success\":true,\"count\":").Append(voices.Count).Append(",\"voices\":[");

            for (int i = 0; i < voices.Count; i++) {
                var info = voices[i].VoiceInfo;
                if (i > 0) sb.Append(",");
                sb.Append("{")
                  .Append("\"name\":\"").Append(EscapeJson(info.Name)).Append("\",")
                  .Append("\"culture\":\"").Append(EscapeJson(info.Culture.Name)).Append("\",")
                  .Append("\"gender\":\"").Append(info.Gender.ToString()).Append("\",")
                  .Append("\"age\":\"").Append(info.Age.ToString()).Append("\",")
                  .Append("\"enabled\":").Append(voices[i].Enabled ? "true" : "false")
                  .Append("}");
            }
            sb.Append("]}");
            Console.WriteLine(sb.ToString());
            return 0;
        }

        static int SpeakText(SpeechSynthesizer synth, string text, int rate, int volume, string voiceName) {
            ConfigureSynth(synth, rate, volume, voiceName);
            synth.Speak(text);

            Console.WriteLine("{\"success\":true,\"action\":\"speak\",\"textLength\":" + text.Length +
                              ",\"voice\":\"" + EscapeJson(synth.Voice.Name) + "\",\"rate\":" + synth.Rate +
                              ",\"volume\":" + synth.Volume + "}");
            return 0;
        }

        static int SynthesizeToWav(SpeechSynthesizer synth, string text, string outputPath, int rate, int volume, string voiceName) {
            ConfigureSynth(synth, rate, volume, voiceName);

            string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }

            synth.SetOutputToWaveFile(outputPath);
            synth.Speak(text);
            synth.SetOutputToDefaultAudioDevice();

            long bytes = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;

            Console.WriteLine("{\"success\":true,\"action\":\"wav\",\"outputPath\":\"" + EscapeJson(outputPath) +
                              "\",\"bytes\":" + bytes + ",\"voice\":\"" + EscapeJson(synth.Voice.Name) + "\"}");
            return 0;
        }

        static void ConfigureSynth(SpeechSynthesizer synth, int rate, int volume, string voiceName) {
            synth.Rate = Math.Max(-10, Math.Min(10, rate));
            synth.Volume = Math.Max(0, Math.Min(100, volume));

            if (!string.IsNullOrEmpty(voiceName)) {
                try {
                    synth.SelectVoice(voiceName);
                } catch {
                    // If exact name fails, try partial match
                    foreach (var v in synth.GetInstalledVoices()) {
                        if (v.Enabled && v.VoiceInfo.Name.IndexOf(voiceName, StringComparison.OrdinalIgnoreCase) >= 0) {
                            synth.SelectVoice(v.VoiceInfo.Name);
                            break;
                        }
                    }
                }
            }
        }

        static int ParseInt(string s, int defaultVal) {
            int res;
            return int.TryParse(s, out res) ? res : defaultVal;
        }

        static string EscapeJson(string s) {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }

        static void PrintUsage() {
            Console.WriteLine("Usage: speech_helper.exe <command> [args]");
            Console.WriteLine("  speech_helper.exe list");
            Console.WriteLine("  speech_helper.exe speak \"Hello World\" [rate: -10..10] [volume: 0..100] [voiceName]");
            Console.WriteLine("  speech_helper.exe wav \"Hello World\" \"output.wav\" [rate] [volume] [voiceName]");
        }
    }
}
