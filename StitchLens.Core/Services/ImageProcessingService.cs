using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Threading.Tasks;
using Image = SixLabors.ImageSharp.Image;

namespace StitchLens.Core.Services;

public class ImageProcessingService : IImageProcessingService
{
    private readonly string _uploadPath;

    public ImageProcessingService(string uploadPath)
    {
        _uploadPath = uploadPath;
        Directory.CreateDirectory(_uploadPath);
    }

    public async Task<ProcessedImage> ProcessUploadAsync(Stream imageStream, CropData? cropData = null)
    {
        using var image = await Image.LoadAsync<Rgba32>(imageStream);

        // CRITICAL: Auto-orient the image based on EXIF data FIRST
        image.Mutate(x => x.AutoOrient());

        // Apply crop if specified
        if (cropData != null)
        {
            // Validate and constrain crop rectangle to image bounds
            var cropX = Math.Max(0, Math.Min(cropData.X, image.Width - 1));
            var cropY = Math.Max(0, Math.Min(cropData.Y, image.Height - 1));
            var cropWidth = Math.Min(cropData.Width, image.Width - cropX);
            var cropHeight = Math.Min(cropData.Height, image.Height - cropY);

            // Ensure minimum dimensions
            if (cropWidth > 0 && cropHeight > 0)
            {
                image.Mutate(x => x.Crop(new Rectangle(
                    cropX,
                    cropY,
                    cropWidth,
                    cropHeight
                )));

                if (cropData.Shape == CropShape.Circle || cropData.Shape == CropShape.Oval)
                {
                    ApplyEllipseMask(image, cropData.Shape);
                }
            }
        }

        // Limit max dimensions to prevent huge processing times
        const int maxDimension = 2000;
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            var ratio = Math.Min(
                (double)maxDimension / image.Width,
                (double)maxDimension / image.Height
            );
            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            image.Mutate(x => x.Resize(newWidth, newHeight));
        }

        // Convert to byte array
        using var ms = new MemoryStream();
        await image.SaveAsync(ms, new PngEncoder());

        return new ProcessedImage
        {
            ImageData = ms.ToArray(),
            Width = image.Width,
            Height = image.Height,
            Format = "png"
        };
    }

    public async Task<string> SaveImageAsync(ProcessedImage image, string fileName)
    {
        var filePath = Path.Combine(_uploadPath, fileName);
        await File.WriteAllBytesAsync(filePath, image.ImageData);
        return filePath;
    }

    private static void ApplyEllipseMask(Image<Rgba32> image, CropShape shape)
    {
        float centerX = (image.Width - 1) / 2f;
        float centerY = (image.Height - 1) / 2f;

        float radiusX;
        float radiusY;

        if (shape == CropShape.Circle)
        {
            var radius = Math.Min(image.Width, image.Height) / 2f;
            radiusX = radius;
            radiusY = radius;
        }
        else
        {
            radiusX = image.Width / 2f;
            radiusY = image.Height / 2f;
        }

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);

                for (int x = 0; x < accessor.Width; x++)
                {
                    float dx = (x - centerX) / radiusX;
                    float dy = (y - centerY) / radiusY;

                    if ((dx * dx) + (dy * dy) > 1f)
                    {
                        var pixel = row[x];
                        row[x] = new Rgba32(pixel.R, pixel.G, pixel.B, 0);
                    }
                }
            }
        });
    }
}
