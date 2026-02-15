namespace StitchLens.Core;

/// <summary>
/// Centralized constants for pattern generation.
/// </summary>
public static class PatternConstants {
    /// <summary>
    /// Array of 70 distinct symbols used for color representation in patterns.
    /// Supports up to 60+ colors with fallback to numeric labels beyond that.
    /// </summary>
    public static readonly string[] Symbols = new[]
    {
        // Original 20 symbols (0-19)
        "•", "○", "◆", "◇", "■", "□", "▲", "△", "★", "☆",
        "●", "◉", "▪", "▫", "◘", "◙", "▼", "▽", "◊", "◈",
        // Additional 20 symbols (20-39)
        "♠", "♣", "♥", "♦", "♪", "♫", "✓", "✗", "✚", "✦",
        "✧", "✶", "✹", "❖", "❀", "❁", "❂", "❃", "❄", "◐",
        // Additional 20 symbols (40-59)
        "◑", "◒", "◓", "◔", "◕", "⊕", "⊗", "⊙", "⊚", "⊛",
        "⊜", "⊝", "⊞", "⊟", "⊠", "⊡", "⊢", "⊣", "⊤", "⊥",
        // Extra 10 symbols for safety (60-69)
        "⊦", "⊧", "⊨", "⊩", "⊪", "⊫", "⊬", "⊭", "⊮", "⊯"
    };
}