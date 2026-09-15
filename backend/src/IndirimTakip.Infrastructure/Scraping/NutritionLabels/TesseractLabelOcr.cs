using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace IndirimTakip.Infrastructure.Scraping.NutritionLabels;

/// <summary>Besin etiketi görselini metne çevirir.</summary>
public interface INutritionLabelOcr
{
    /// <summary>OCR motoru bu makinede kurulu mu.</summary>
    bool IsAvailable { get; }

    /// <summary>Görselin metni; OCR başarısızsa null.</summary>
    /// <param name="upscale">Hazırlıkta 2 kat büyütülsün mü (bkz. <see cref="NutritionLabelReader"/>).</param>
    Task<string?> ReadAsync(byte[] image, int pageSegmentationMode, bool upscale, CancellationToken cancellationToken);
}

/// <summary>
/// Sunucudaki Tesseract ile Türkçe etiket okuma — ücretsiz, dış servis yok.
/// </summary>
/// <remarks>
/// <b>Hazırlık ölçülerek seçildi (Nois, 55 görsel).</b> 29'u saydam zeminli:
/// düz griye çevrilince saydam pikselin arkasındaki renk ortaya çıkıyor ve
/// açık renkli yazı KAYBOLUYOR (Whey Rex etiketi okunamaz bir kahverengi
/// lekeye döndü). Bu yüzden görünen piksellerin parlaklığına bakılıyor:
/// yazı açıksa siyah zemine bindirilip ters çevriliyor (koyu yazı, beyaz
/// zemin), koyuysa beyaz zemine bindiriliyor. Ardından gri ton + 2 kat büyütme.
///
/// <b>Tek OCR iş parçacığı.</b> Sunucu iki siteyi birden çalıştırıyor;
/// Tesseract aksi hâlde her görsel için bütün çekirdekleri alırdı.
/// </remarks>
public sealed class TesseractLabelOcr(ILogger<TesseractLabelOcr> logger) : INutritionLabelOcr
{
    private const string Executable = "tesseract";
    private const int MaxOcrEdge = 4096;
    private static readonly TimeSpan OcrTimeout = TimeSpan.FromSeconds(60);

    // Konteyner çalışırken kurulu olup olmadığı değişmiyor, bir kez bakılıyor.
    private static bool? installed;

    public bool IsAvailable => installed ??= CheckInstalled();

    public async Task<string?> ReadAsync(byte[] image, int pageSegmentationMode, bool upscale, CancellationToken cancellationToken)
    {
        var pngPath = Path.Combine(Path.GetTempPath(), $"etiket-{Guid.NewGuid():N}.png");
        try
        {
            try
            {
                await using var png = File.Create(pngPath);
                await PrepareAsync(image, png, upscale, cancellationToken);
            }
            catch (Exception ex) when (ex is ImageFormatException or UnknownImageFormatException or InvalidImageContentException)
            {
                return null;
            }

            return await RunAsync(pngPath, pageSegmentationMode, cancellationToken);
        }
        finally
        {
            try
            {
                File.Delete(pngPath);
            }
            catch (IOException)
            {
                // Artık geçici dosya zararsız; konteyner yenilenince /tmp sıfırlanıyor.
            }
        }
    }

    /// <summary>
    /// Saydamlığı yazının rengine göre düzleştirir, gri tona çevirip büyütür.
    /// Ağdan ve süreçten ayrı: asıl karar teste bağlanabilsin diye.
    /// </summary>
    internal static async Task PrepareAsync(byte[] bytes, Stream output, bool upscale, CancellationToken cancellationToken)
    {
        using var image = Image.Load<Rgba32>(bytes);

        var (hasTransparency, lightText) = Inspect(image);
        var scale = upscale ? Math.Min(2.0, MaxOcrEdge / (double)Math.Max(image.Width, image.Height)) : 1.0;

        image.Mutate(x =>
        {
            if (hasTransparency)
                x.BackgroundColor(lightText ? Color.Black : Color.White);
            x.Grayscale();
            if (hasTransparency && lightText)
                x.Invert();
            if (scale > 1)
                x.Resize((int)(image.Width * scale), (int)(image.Height * scale), KnownResamplers.Lanczos3);
        });

        await image.SaveAsync(output, new PngEncoder(), cancellationToken);
    }

    // Görünen (alfa > 128) piksellerin ortalama parlaklığı. Her pikseli
    // gezmeye gerek yok; seyrek örnek kararı değiştirmiyor.
    private static (bool HasTransparency, bool LightText) Inspect(Image<Rgba32> image)
    {
        var step = Math.Max(1, Math.Min(image.Width, image.Height) / 300);
        var transparent = false;
        double sum = 0;
        long count = 0;

        for (var y = 0; y < image.Height; y += step)
        {
            for (var x = 0; x < image.Width; x += step)
            {
                var p = image[x, y];
                if (p.A < 255)
                    transparent = true;
                if (p.A > 128)
                {
                    sum += 0.299 * p.R + 0.587 * p.G + 0.114 * p.B;
                    count++;
                }
            }
        }

        return (transparent, count > 0 && sum / count > 128);
    }

    private async Task<string?> RunAsync(string path, int mode, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(Executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("stdout");
        start.ArgumentList.Add("-l");
        start.ArgumentList.Add("tur+eng");
        start.ArgumentList.Add("--psm");
        start.ArgumentList.Add(mode.ToString(CultureInfo.InvariantCulture));
        start.Environment["OMP_THREAD_LIMIT"] = "1";

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            logger.LogError(ex, "Tesseract başlatılamadı.");
            return null;
        }

        if (process is null)
            return null;

        using (process)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(OcrTimeout);

            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
                var text = await output;
                await errors;
                return process.ExitCode == 0 ? text : null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                logger.LogWarning("Tesseract bir etikette {Saniye} sn'yi aştı; atlandı.", OcrTimeout.TotalSeconds);
                return null;
            }
        }
    }

    private static bool CheckInstalled()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Executable, "--list-langs")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null)
                return false;

            // Türkçe dil paketi yoksa "kurulu" sayılmıyor: İngilizce modelle
            // okunan Türkçe etiket harf harf bozuluyor.
            var langs = process.StandardOutput.ReadToEnd();
            return process.WaitForExit(10_000) && process.ExitCode == 0
                && langs.Split('\n').Any(l => l.Trim() == "tur");
        }
        catch (Exception)
        {
            return false;
        }
    }
}
