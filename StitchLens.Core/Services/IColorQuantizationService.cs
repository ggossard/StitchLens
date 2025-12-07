using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StitchLens.Core.Services;

public interface IColorQuantizationService
{
    Task<QuantizedResult> QuantizeAsync(byte[] imageData, int maxColors);
}

public class QuantizedResult
{
    public byte[] QuantizedImageData { get; set; } = Array.Empty<byte>();
    public List<ColorInfo> Palette { get; set; } = new();
}

public class ColorInfo
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }
    public double Lab_L { get; set; }
    public double Lab_A { get; set; }
    public double Lab_B { get; set; }
    public int PixelCount { get; set; }
}