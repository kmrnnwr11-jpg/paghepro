using System.Diagnostics;

namespace Paghe.Documenti;

/// <summary>
/// OCR locale con il motore open-source <b>Tesseract</b> (via CLI), <b>senza API esterne</b> e senza
/// costi: legge il testo da foto/scansioni. Richiede il binario <c>tesseract</c> installato
/// (macOS: <c>brew install tesseract tesseract-lang</c>; Linux/Docker:
/// <c>apt-get install tesseract-ocr tesseract-ocr-ita</c>). Lingua di default: italiano (<c>ita</c>).
/// </summary>
public static class OcrTesseract
{
    /// <summary>True se il motore OCR è installato e utilizzabile.</summary>
    public static bool Disponibile => TrovaBinario() is not null;

    /// <summary>
    /// Estrae il testo da un'immagine (PNG/JPG/TIFF…) con Tesseract. <paramref name="lingua"/> è il
    /// codice lingua Tesseract (es. "ita", "ita+eng"). Lancia se Tesseract non è installato.
    /// </summary>
    public static string EstraiTesto(byte[] immagine, string lingua = "ita")
    {
        var bin = TrovaBinario() ?? throw new InvalidOperationException(
            "OCR non disponibile: installa Tesseract (macOS: 'brew install tesseract tesseract-lang').");

        var tmp = Path.Combine(Path.GetTempPath(), $"ocr_{Guid.NewGuid():N}.img");
        File.WriteAllBytes(tmp, immagine);

        // Pre-elaborazione (se ImageMagick è presente): scala di grigi + raddrizza (deskew) +
        // contrasto + upscale + nitidezza. Migliora molto l'OCR su foto/scansioni reali.
        var immaginePerOcr = Preprocessa(tmp) ?? tmp;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = bin,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add(immaginePerOcr);
            psi.ArgumentList.Add("stdout");
            psi.ArgumentList.Add("-l");
            psi.ArgumentList.Add(lingua);
            psi.ArgumentList.Add("--oem");
            psi.ArgumentList.Add("1"); // motore LSTM (rete neurale): più preciso
            psi.ArgumentList.Add("--psm");
            psi.ArgumentList.Add("3");  // pagina intera, layout automatico

            using var p = Process.Start(psi)
                ?? throw new InvalidOperationException("Impossibile avviare Tesseract.");
            var testo = p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(60_000);
            return testo;
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* file temporaneo: ignora */ }
            if (immaginePerOcr != tmp)
                try { File.Delete(immaginePerOcr); } catch { /* ignora */ }
        }
    }

    /// <summary>
    /// Pre-elabora l'immagine con ImageMagick per migliorare l'OCR (se disponibile), restituendo il
    /// percorso del file elaborato; null se ImageMagick non c'è o fallisce (si usa l'originale).
    /// </summary>
    private static string? Preprocessa(string input)
    {
        var magick = TrovaMagick();
        if (magick is null) return null;

        var output = Path.Combine(Path.GetTempPath(), $"ocrpre_{Guid.NewGuid():N}.png");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = magick,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in new[]
                     {
                         input,
                         "-auto-orient",           // orienta secondo i metadati EXIF
                         "-colorspace", "Gray",     // bianco/nero
                         "-deskew", "40%",          // raddrizza il testo storto
                         "-contrast-stretch", "1%x1%",
                         "-resize", "200%",          // upscale: aiuta su testo piccolo
                         "-sharpen", "0x1",
                         output,
                     })
                psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p is null) return null;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(30_000);
            return File.Exists(output) ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TrovaMagick()
    {
        foreach (var c in new[]
                 {
                     "/opt/homebrew/bin/magick", "/usr/local/bin/magick", "/usr/bin/magick",
                     "/opt/homebrew/bin/convert", "/usr/local/bin/convert", "/usr/bin/convert",
                 })
            if (File.Exists(c)) return c;
        return null;
    }

    private static string? TrovaBinario()
    {
        foreach (var c in new[]
                 {
                     "/opt/homebrew/bin/tesseract", // macOS (Apple Silicon)
                     "/usr/local/bin/tesseract",    // macOS (Intel) / Linux
                     "/usr/bin/tesseract",          // Linux / Docker
                 })
            if (File.Exists(c)) return c;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            try
            {
                var f = Path.Combine(dir, "tesseract");
                if (File.Exists(f)) return f;
            }
            catch { /* voce PATH non valida: ignora */ }
        }
        return null;
    }
}
