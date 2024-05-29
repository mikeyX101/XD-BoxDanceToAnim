using System.Collections.Immutable;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;

if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Input is empty. Needs a path to a file or folder.");
    Console.WriteLine("""
Usage:
    BoxDanceToAnim <input> [output] [all|png|gif]
    
Arguments:
    input: Input file or folder.
    output: Optional, output folder. Defaults to input folder.
    format: Optional, "all", "png" or "gif". Defaults to "all".
    
Notes:
    The animation of GIFs will be ever so slightly slower than normal due to limitations of the GIF format.
""");
    return 1;
}

string input = args[0];
if (!Directory.Exists(input) && !File.Exists(input))
{
    Console.Error.WriteLine("Input is not a path to an existing file or folder.");
    return 1;
}

string format = args.Length > 2 ? args[2] : "all";
if (format != "all" && format != "png" && format != "gif")
{
    Console.Error.WriteLine("Format is invalid. Specify [all|png|gif].");
    return 1;
}

string? outputFolder = args.Length > 1 ? args[1] : null;
ImmutableList<string> filesToProcess;
if (Directory.Exists(input))
{
    filesToProcess = Directory.EnumerateFiles(input, "*.png", SearchOption.TopDirectoryOnly).ToImmutableList();
    outputFolder ??= input;
}
else
{
    filesToProcess = new List<string>{ input }.ToImmutableList();

    string inputFolder = Path.GetDirectoryName(input)!;
    outputFolder ??= inputFolder;
}

string pngDir = Path.Combine(outputFolder, "PNG");
if (!Directory.Exists(pngDir) && format is "png" or "all")
{
    Directory.CreateDirectory(pngDir);
}

string gifDir = Path.Combine(outputFolder, "GIF");
if (!Directory.Exists(gifDir) && format is "gif" or "all")
{
    Directory.CreateDirectory(gifDir);
}

foreach (string file in filesToProcess)
{
    string fileName = Path.GetFileNameWithoutExtension(file);
    
    using Image fileImg = Image.Load(file);
    if (fileImg.Width * 3 != fileImg.Height)
    {
        Console.Error.WriteLine("Invalid size for image: " + fileName + " Skipping.");
        continue;
    }
    
    Console.WriteLine("Processing " + fileName);
    using Image frame1 = GetFrame(fileImg, 0);
    using Image frame2 = GetFrame(fileImg, 1);
    using Image frame3 = GetFrame(fileImg, 2);
    
    if (format is "png" or "all")
    {
        await SaveDanceAnimPngAsync(Path.Combine(pngDir, fileName) + ".png", frame1, frame2, frame3);
    }

    if (format is "gif" or "all")
    {
        await SaveDanceAnimGifAsync(Path.Combine(gifDir, fileName) + ".gif", frame1, frame2, frame3);
    }
}

return 0;

Image GetFrame(Image img, byte idx)
{
    return img.Clone(ctx =>
    {
        Size size = ctx.GetCurrentSize();
        
        Point cropPosition = new Point(0, size.Height / 3 * idx);
        Size cropSize = new Size(size.Width, size.Height / 3);

        ctx.Crop(new Rectangle(cropPosition, cropSize));
    });
}

async Task SaveDanceAnimPngAsync(string output, Image first, Image second, Image third)
{
    const uint repeatCount = 0;
    Rational frameDelay = new Rational(0.166666666666);

    Image png = first.Clone(_ => { });
    PngMetadata pngMetaData = png.Metadata.GetPngMetadata();
    pngMetaData.RepeatCount = repeatCount;
    pngMetaData.AnimateRootFrame = true;
    pngMetaData.ColorType = PngColorType.RgbWithAlpha;
    pngMetaData.TransparentColor = Color.Transparent;

    png.Frames.AddFrame(second.Frames.RootFrame);
    png.Frames.AddFrame(third.Frames.RootFrame);
    png.Frames.AddFrame(second.Frames.RootFrame);

    foreach (ImageFrame frame in png.Frames)
    {
        PngFrameMetadata metadata = frame.Metadata.GetPngMetadata();
        metadata.FrameDelay = frameDelay;
        metadata.BlendMethod = PngBlendMethod.Source;
        metadata.DisposalMethod = PngDisposalMethod.DoNotDispose;
    }
        
    await png.SaveAsPngAsync(output);
}

async Task SaveDanceAnimGifAsync(string output, Image first, Image second, Image third)
{
    const int repeatCount = 0;
    const int frameDelay = 17;

    using Image gif = first.Clone(_ => { });
    GifMetadata gifMetaData = gif.Metadata.GetGifMetadata();
    gifMetaData.RepeatCount = repeatCount;
    gifMetaData.ColorTableMode = GifColorTableMode.Local;

    gif.Frames.AddFrame(second.Frames.RootFrame);
    gif.Frames.AddFrame(third.Frames.RootFrame);
    gif.Frames.AddFrame(second.Frames.RootFrame);

    foreach (ImageFrame frame in gif.Frames)
    {
        GifFrameMetadata metadata = frame.Metadata.GetGifMetadata();
        metadata.FrameDelay = frameDelay;
        metadata.ColorTableMode = GifColorTableMode.Local;
    }
        
    await gif.SaveAsGifAsync(output);
    //await gif.SaveAsGifAsync(output, new GifEncoder { Quantizer = KnownQuantizers.Wu, ColorTableMode = GifColorTableMode.Local, PixelSamplingStrategy = new ExtensivePixelSamplingStrategy() });
}