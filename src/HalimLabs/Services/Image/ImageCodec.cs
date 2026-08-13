using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HalimLabs.Services.Image;

public static class ImageCodec
{
    public const int MaxLongSide = 1024;

    public static byte[] NormalizeJpeg(byte[] bytes, int maxLongSide = MaxLongSide)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count == 0)
            throw new InvalidOperationException("No image frames.");

        var frame = decoder.Frames[0];
        BitmapSource source = frame;
        var longSide = Math.Max(frame.PixelWidth, frame.PixelHeight);
        if (longSide > maxLongSide)
        {
            var scale = maxLongSide / (double)longSide;
            source = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
        }

        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var output = new MemoryStream();
        encoder.Save(output);
        return output.ToArray();
    }

    public static string ToJpegDataUri(byte[] bytes) =>
        "data:image/jpeg;base64," + Convert.ToBase64String(NormalizeJpeg(bytes));

    /// <summary>
    /// Places images left-to-right so a single-image editor (Kontext / Qwen-Edit)
    /// can see person + clothing together.
    /// </summary>
    public static byte[] StitchHorizontal(IReadOnlyList<byte[]> images, int maxLongSide = MaxLongSide)
    {
        if (images is null || images.Count == 0)
            throw new ArgumentException("No images to stitch.");
        if (images.Count == 1)
            return NormalizeJpeg(images[0], maxLongSide);

        const int gap = 12;
        var frames = images.Select(LoadDisplay).ToList();
        var targetH = frames.Max(f => f.PixelHeight);
        var widths = frames
            .Select(f => Math.Max(1, (int)Math.Round(f.PixelWidth * (targetH / (double)f.PixelHeight))))
            .ToList();
        var totalW = widths.Sum() + gap * (frames.Count - 1);

        var scale = 1.0;
        if (Math.Max(totalW, targetH) > maxLongSide)
            scale = maxLongSide / (double)Math.Max(totalW, targetH);

        var outW = Math.Max(1, (int)Math.Round(totalW * scale));
        var outH = Math.Max(1, (int)Math.Round(targetH * scale));
        var outStride = outW * 4;
        var output = new byte[outH * outStride];
        for (var i = 0; i < output.Length; i += 4)
        {
            output[i] = 255;
            output[i + 1] = 255;
            output[i + 2] = 255;
            output[i + 3] = 255;
        }

        var x = 0;
        for (var n = 0; n < frames.Count; n++)
        {
            var dw = Math.Max(1, (int)Math.Round(widths[n] * scale));
            var scaled = new TransformedBitmap(
                frames[n],
                new ScaleTransform(dw / (double)frames[n].PixelWidth, outH / (double)frames[n].PixelHeight));
            if (scaled.CanFreeze)
                scaled.Freeze();

            dw = scaled.PixelWidth;
            var dh = Math.Min(outH, scaled.PixelHeight);
            var srcStride = dw * 4;
            var src = new byte[scaled.PixelHeight * srcStride];
            scaled.CopyPixels(src, srcStride, 0);

            var copyWidth = Math.Min(dw, Math.Max(0, outW - x)) * 4;
            if (copyWidth <= 0)
                break;
            for (var row = 0; row < dh; row++)
                Buffer.BlockCopy(src, row * srcStride, output, row * outStride + x * 4, copyWidth);

            x += dw + Math.Max(1, (int)Math.Round(gap * scale));
        }

        var bitmap = BitmapSource.Create(outW, outH, 96, 96, PixelFormats.Bgra32, null, output, outStride);
        var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    public static BitmapSource LoadDisplay(byte[] bytes)
    {
        if (bytes is null || bytes.Length < 24)
            throw new InvalidOperationException("Image data is empty.");

        var copy = new byte[bytes.Length];
        Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);

        using var stream = new MemoryStream(copy, writable: false);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        if (decoder.Frames.Count == 0)
            throw new InvalidOperationException("No image frames.");

        var frame = decoder.Frames[0];
        BitmapSource display = frame;
        if (frame.Format != PixelFormats.Bgra32 && frame.Format != PixelFormats.Pbgra32)
            display = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

        if (display.CanFreeze)
            display.Freeze();

        if (display.PixelWidth <= 0 || display.PixelHeight <= 0)
            throw new InvalidOperationException("Decoded image has invalid size.");

        return display;
    }

    public static BitmapSource CreateThumbnail(BitmapSource source, int size = 72)
    {
        var scale = size / (double)Math.Max(source.PixelWidth, source.PixelHeight);
        if (scale >= 1)
        {
            if (source.CanFreeze)
                source.Freeze();
            return source;
        }

        var thumb = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        if (thumb.CanFreeze)
            thumb.Freeze();
        return thumb;
    }
}
