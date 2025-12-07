using System;
using System.IO;
using System.Threading.Tasks;

namespace StitchLens.Core.Services;

public interface IImageProcessingService
{
    Task<ProcessedImage> ProcessUploadAsync(Stream imageStream, CropData? cropData = null);
    Task<string> SaveImageAsync(ProcessedImage image, string fileName);
}

public class ProcessedImage
{
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
    public int Width { get; set; }
    public int Height { get; set; }
    public string Format { get; set; } = "png";
}

public class CropData
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}