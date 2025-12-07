namespace StitchLens.Core.ColorScience;

public static class ColorConverter {
    /// <summary>
    /// Convert RGB to LAB color space (D65 illuminant, 2° observer)
    /// LAB is perceptually uniform - better for color matching than RGB
    /// </summary>
    public static (double L, double A, double B) RgbToLab(byte r, byte g, byte b) {
        // First convert RGB to XYZ
        var (x, y, z) = RgbToXyz(r, g, b);

        // Then XYZ to LAB
        return XyzToLab(x, y, z);
    }

    /// <summary>
    /// Convert LAB back to RGB
    /// </summary>
    public static (byte R, byte G, byte B) LabToRgb(double l, double a, double b) {
        // LAB to XYZ
        var (x, y, z) = LabToXyz(l, a, b);

        // XYZ to RGB
        return XyzToRgb(x, y, z);
    }

    private static (double X, double Y, double Z) RgbToXyz(byte r, byte g, byte b) {
        // Normalize RGB to 0-1
        double rLinear = r / 255.0;
        double gLinear = g / 255.0;
        double bLinear = b / 255.0;

        // Apply gamma correction (sRGB)
        rLinear = rLinear > 0.04045 ? Math.Pow((rLinear + 0.055) / 1.055, 2.4) : rLinear / 12.92;
        gLinear = gLinear > 0.04045 ? Math.Pow((gLinear + 0.055) / 1.055, 2.4) : gLinear / 12.92;
        bLinear = bLinear > 0.04045 ? Math.Pow((bLinear + 0.055) / 1.055, 2.4) : bLinear / 12.92;

        // Convert to XYZ using D65 illuminant matrix
        double x = rLinear * 0.4124564 + gLinear * 0.3575761 + bLinear * 0.1804375;
        double y = rLinear * 0.2126729 + gLinear * 0.7151522 + bLinear * 0.0721750;
        double z = rLinear * 0.0193339 + gLinear * 0.1191920 + bLinear * 0.9503041;

        return (x * 100, y * 100, z * 100);
    }

    private static (double L, double A, double B) XyzToLab(double x, double y, double z) {
        // D65 reference white point
        const double refX = 95.047;
        const double refY = 100.000;
        const double refZ = 108.883;

        double xr = x / refX;
        double yr = y / refY;
        double zr = z / refZ;

        // Apply LAB conversion function
        xr = xr > 0.008856 ? Math.Pow(xr, 1.0 / 3.0) : (7.787 * xr + 16.0 / 116.0);
        yr = yr > 0.008856 ? Math.Pow(yr, 1.0 / 3.0) : (7.787 * yr + 16.0 / 116.0);
        zr = zr > 0.008856 ? Math.Pow(zr, 1.0 / 3.0) : (7.787 * zr + 16.0 / 116.0);

        double l = (116.0 * yr) - 16.0;
        double a = 500.0 * (xr - yr);
        double b = 200.0 * (yr - zr);

        return (l, a, b);
    }

    private static (double X, double Y, double Z) LabToXyz(double l, double a, double b) {
        const double refX = 95.047;
        const double refY = 100.000;
        const double refZ = 108.883;

        double fy = (l + 16.0) / 116.0;
        double fx = a / 500.0 + fy;
        double fz = fy - b / 200.0;

        double xr = fx * fx * fx > 0.008856 ? fx * fx * fx : (fx - 16.0 / 116.0) / 7.787;
        double yr = fy * fy * fy > 0.008856 ? fy * fy * fy : (fy - 16.0 / 116.0) / 7.787;
        double zr = fz * fz * fz > 0.008856 ? fz * fz * fz : (fz - 16.0 / 116.0) / 7.787;

        return (xr * refX, yr * refY, zr * refZ);
    }

    private static (byte R, byte G, byte B) XyzToRgb(double x, double y, double z) {
        x /= 100.0;
        y /= 100.0;
        z /= 100.0;

        // XYZ to linear RGB
        double r = x * 3.2404542 + y * -1.5371385 + z * -0.4985314;
        double g = x * -0.9692660 + y * 1.8760108 + z * 0.0415560;
        double b = x * 0.0556434 + y * -0.2040259 + z * 1.0572252;

        // Apply inverse gamma correction (sRGB)
        r = r > 0.0031308 ? 1.055 * Math.Pow(r, 1.0 / 2.4) - 0.055 : 12.92 * r;
        g = g > 0.0031308 ? 1.055 * Math.Pow(g, 1.0 / 2.4) - 0.055 : 12.92 * g;
        b = b > 0.0031308 ? 1.055 * Math.Pow(b, 1.0 / 2.4) - 0.055 : 12.92 * b;

        // Clamp to valid range and convert to byte
        byte rByte = (byte)Math.Clamp(r * 255.0, 0, 255);
        byte gByte = (byte)Math.Clamp(g * 255.0, 0, 255);
        byte bByte = (byte)Math.Clamp(b * 255.0, 0, 255);

        return (rByte, gByte, bByte);
    }

    /// <summary>
    /// Calculate perceptual color difference using simple Euclidean distance in LAB space
    /// For more accuracy, use DeltaE2000 (coming in next phase)
    /// </summary>
    public static double CalculateLabDistance(
        double l1, double a1, double b1,
        double l2, double a2, double b2) {
        double dL = l1 - l2;
        double dA = a1 - a2;
        double dB = b1 - b2;

        return Math.Sqrt(dL * dL + dA * dA + dB * dB);
    }

    /// <summary>
    /// Calculate CIEDE2000 color difference - the most accurate perceptual difference formula
    /// Returns a value where 0 = identical, 1 = just noticeable difference, 2+ = noticeable
    /// </summary>
    public static double CalculateDeltaE2000(
        double l1, double a1, double b1,
        double l2, double a2, double b2) {
        // Reference: "The CIEDE2000 Color-Difference Formula" by Sharma, Wu, Dalal

        // Step 1: Calculate C' and h'
        double c1 = Math.Sqrt(a1 * a1 + b1 * b1);
        double c2 = Math.Sqrt(a2 * a2 + b2 * b2);
        double cMean = (c1 + c2) / 2.0;

        double g = 0.5 * (1 - Math.Sqrt(Math.Pow(cMean, 7) / (Math.Pow(cMean, 7) + Math.Pow(25, 7))));

        double a1Prime = a1 * (1 + g);
        double a2Prime = a2 * (1 + g);

        double c1Prime = Math.Sqrt(a1Prime * a1Prime + b1 * b1);
        double c2Prime = Math.Sqrt(a2Prime * a2Prime + b2 * b2);

        double h1Prime = Math.Atan2(b1, a1Prime) * 180.0 / Math.PI;
        if (h1Prime < 0) h1Prime += 360.0;

        double h2Prime = Math.Atan2(b2, a2Prime) * 180.0 / Math.PI;
        if (h2Prime < 0) h2Prime += 360.0;

        // Step 2: Calculate ΔL', ΔC', ΔH'
        double deltaLPrime = l2 - l1;
        double deltaCPrime = c2Prime - c1Prime;

        double deltahPrime;
        if (c1Prime * c2Prime == 0) {
            deltahPrime = 0;
        }
        else {
            double diff = h2Prime - h1Prime;
            if (Math.Abs(diff) <= 180)
                deltahPrime = diff;
            else if (diff > 180)
                deltahPrime = diff - 360;
            else
                deltahPrime = diff + 360;
        }

        double deltaHPrime = 2 * Math.Sqrt(c1Prime * c2Prime) * Math.Sin(deltahPrime * Math.PI / 360.0);

        // Step 3: Calculate CIEDE2000
        double lPrimeMean = (l1 + l2) / 2.0;
        double cPrimeMean = (c1Prime + c2Prime) / 2.0;

        double hPrimeMean;
        if (c1Prime * c2Prime == 0) {
            hPrimeMean = h1Prime + h2Prime;
        }
        else {
            double sum = h1Prime + h2Prime;
            double diff = Math.Abs(h1Prime - h2Prime);
            if (diff <= 180)
                hPrimeMean = sum / 2.0;
            else if (sum < 360)
                hPrimeMean = (sum + 360) / 2.0;
            else
                hPrimeMean = (sum - 360) / 2.0;
        }

        double t = 1 - 0.17 * Math.Cos((hPrimeMean - 30) * Math.PI / 180.0)
            + 0.24 * Math.Cos(2 * hPrimeMean * Math.PI / 180.0)
            + 0.32 * Math.Cos((3 * hPrimeMean + 6) * Math.PI / 180.0)
            - 0.20 * Math.Cos((4 * hPrimeMean - 63) * Math.PI / 180.0);

        double deltaTheta = 30 * Math.Exp(-Math.Pow((hPrimeMean - 275) / 25.0, 2));

        double rC = 2 * Math.Sqrt(Math.Pow(cPrimeMean, 7) / (Math.Pow(cPrimeMean, 7) + Math.Pow(25, 7)));

        double sL = 1 + (0.015 * Math.Pow(lPrimeMean - 50, 2)) / Math.Sqrt(20 + Math.Pow(lPrimeMean - 50, 2));
        double sC = 1 + 0.045 * cPrimeMean;
        double sH = 1 + 0.015 * cPrimeMean * t;

        double rT = -Math.Sin(2 * deltaTheta * Math.PI / 180.0) * rC;

        // Weighting factors (kL = kC = kH = 1 for standard viewing conditions)
        double kL = 1.0;
        double kC = 1.0;
        double kH = 1.0;

        double deltaE = Math.Sqrt(
            Math.Pow(deltaLPrime / (kL * sL), 2) +
            Math.Pow(deltaCPrime / (kC * sC), 2) +
            Math.Pow(deltaHPrime / (kH * sH), 2) +
            rT * (deltaCPrime / (kC * sC)) * (deltaHPrime / (kH * sH))
        );

        return deltaE;
    }
}