using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace GeminiSuper
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                RunAsync(args).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("OCR Error: " + ex.Message);
                Environment.Exit(1);
            }
        }

        static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.AppendFormat("\\u{0:x4}", (int)c);
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        static async Task RunAsync(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  ocr_helper.exe <imagePath>              -> dumps plain text");
                Console.WriteLine("  ocr_helper.exe json <imagePath>         -> dumps full OCR JSON structure");
                Console.WriteLine("  ocr_helper.exe find <imagePath> <query> -> finds matching words with center coords");
                return;
            }

            string mode = "text";
            string imagePath = "";
            string query = "";

            if (args[0].Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                mode = "json";
                if (args.Length < 2) { Console.Error.WriteLine("Missing image path"); return; }
                imagePath = Path.GetFullPath(args[1]);
            }
            else if (args[0].Equals("find", StringComparison.OrdinalIgnoreCase))
            {
                mode = "find";
                if (args.Length < 3) { Console.Error.WriteLine("Usage: find <imagePath> <query>"); return; }
                imagePath = Path.GetFullPath(args[1]);
                query = args[2];
            }
            else
            {
                imagePath = Path.GetFullPath(args[0]);
                if (args.Length > 1)
                {
                    mode = "find";
                    query = args[1];
                }
            }

            if (!File.Exists(imagePath))
            {
                Console.Error.WriteLine("File not found: " + imagePath);
                Environment.Exit(2);
            }

            StorageFile file = await WindowsRuntimeSystemExtensions.AsTask(StorageFile.GetFileFromPathAsync(imagePath));
            using (IRandomAccessStream stream = await WindowsRuntimeSystemExtensions.AsTask(file.OpenAsync(FileAccessMode.Read)))
            {
                BitmapDecoder decoder = await WindowsRuntimeSystemExtensions.AsTask(BitmapDecoder.CreateAsync(stream));
                SoftwareBitmap bitmap = await WindowsRuntimeSystemExtensions.AsTask(decoder.GetSoftwareBitmapAsync());

                OcrEngine engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine == null)
                {
                    Console.Error.WriteLine("No OCR engine available on this system.");
                    Environment.Exit(3);
                }

                OcrResult result = await WindowsRuntimeSystemExtensions.AsTask(engine.RecognizeAsync(bitmap));

                if (mode == "text")
                {
                    Console.WriteLine("=== Windows Native WinRT OCR Result (" + result.Lines.Count + " lines) ===");
                    foreach (var line in result.Lines)
                    {
                        Console.WriteLine(line.Text);
                    }
                }
                else if (mode == "find")
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("[\n");
                    bool first = true;
                    int count = 0;

                    foreach (var line in result.Lines)
                    {
                        foreach (var word in line.Words)
                        {
                            if (word.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                int cx = (int)Math.Round(word.BoundingRect.X + word.BoundingRect.Width / 2.0);
                                int cy = (int)Math.Round(word.BoundingRect.Y + word.BoundingRect.Height / 2.0);

                                if (!first) sb.Append(",\n");
                                sb.Append(string.Format(CultureInfo.InvariantCulture,
                                    "  {{ \"text\": \"{0}\", \"x\": {1}, \"y\": {2}, \"width\": {3}, \"height\": {4}, \"centerX\": {5}, \"centerY\": {6}, \"line\": \"{7}\" }}",
                                    EscapeJson(word.Text),
                                    (int)word.BoundingRect.X,
                                    (int)word.BoundingRect.Y,
                                    (int)word.BoundingRect.Width,
                                    (int)word.BoundingRect.Height,
                                    cx, cy,
                                    EscapeJson(line.Text)
                                ));
                                first = false;
                                count++;
                            }
                        }
                    }
                    sb.Append("\n]");
                    Console.WriteLine(sb.ToString());
                }
                else if (mode == "json")
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("{\n");
                    sb.Append(string.Format("  \"text\": \"{0}\",\n", EscapeJson(result.Text)));
                    sb.Append(string.Format("  \"lineCount\": {0},\n", result.Lines.Count));
                    sb.Append("  \"lines\": [\n");

                    for (int i = 0; i < result.Lines.Count; i++)
                    {
                        var line = result.Lines[i];
                        sb.Append("    {\n");
                        sb.Append(string.Format("      \"text\": \"{0}\",\n", EscapeJson(line.Text)));
                        sb.Append("      \"words\": [\n");

                        for (int j = 0; j < line.Words.Count; j++)
                        {
                            var word = line.Words[j];
                            int cx = (int)Math.Round(word.BoundingRect.X + word.BoundingRect.Width / 2.0);
                            int cy = (int)Math.Round(word.BoundingRect.Y + word.BoundingRect.Height / 2.0);

                            sb.Append(string.Format(CultureInfo.InvariantCulture,
                                "        {{ \"text\": \"{0}\", \"x\": {1}, \"y\": {2}, \"width\": {3}, \"height\": {4}, \"centerX\": {5}, \"centerY\": {6} }}",
                                EscapeJson(word.Text),
                                (int)word.BoundingRect.X,
                                (int)word.BoundingRect.Y,
                                (int)word.BoundingRect.Width,
                                (int)word.BoundingRect.Height,
                                cx, cy
                            ));
                            if (j < line.Words.Count - 1) sb.Append(",");
                            sb.Append("\n");
                        }

                        sb.Append("      ]\n");
                        sb.Append("    }");
                        if (i < result.Lines.Count - 1) sb.Append(",");
                        sb.Append("\n");
                    }

                    sb.Append("  ]\n");
                    sb.Append("}\n");
                    Console.WriteLine(sb.ToString());
                }
            }
        }
    }
}
