using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace StitchLens.Core.Services;

public class PdfGenerationService : IPdfGenerationService {
    public PdfGenerationService() {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GeneratePatternPdfAsync(PatternPdfData data) {
        return await Task.Run(() => {
            var document = Document.Create(container => {
                container.Page(page => {
                    page.Size(PageSizes.Letter);
                    page.Margin(0.5f, Unit.Inch);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(column => {
                        column.Item().Text("StitchLens Needlepoint Pattern")
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);

                        column.Item().PaddingTop(10).Text(data.Title).FontSize(14).SemiBold();

                        column.Item().PaddingTop(10).Row(row => {
                            row.RelativeItem().Column(col => {
                                col.Item().Text("Canvas Specifications").Bold();
                                col.Item().Text($"Mesh: {data.MeshCount} count");
                                col.Item().Text($"Size: {data.WidthInches:F1}\" × {data.HeightInches:F1}\"");
                                col.Item().Text($"Stitches: {data.WidthStitches} × {data.HeightStitches}");
                            });

                            row.RelativeItem().Column(col => {
                                col.Item().Text("Materials").Bold();
                                col.Item().Text($"Brand: {data.YarnBrand}");
                                col.Item().Text($"Colors: {data.YarnMatches.Count}");
                                col.Item().Text($"Total Skeins: {data.YarnMatches.Sum(m => m.EstimatedSkeins)}");
                            });
                        });

                        column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingTop(10).Column(column =>
                    {
                        // Make image larger - full page width
                        column.Item().Text("Pattern Preview").FontSize(12).Bold();

                        if (data.QuantizedImageData != null && data.QuantizedImageData.Length > 0) {
                            column.Item().PaddingTop(5).PaddingBottom(10)
                                .Height(4, Unit.Inch)  // Larger image
                                .AlignCenter()
                                .Image(data.QuantizedImageData, ImageScaling.FitArea);
                        }
                        else {
                            column.Item().PaddingTop(5).PaddingBottom(10)
                                .Height(4, Unit.Inch)
                                .Background(Colors.Grey.Lighten3)
                                .AlignCenter().AlignMiddle()
                                .Text("Image not available").FontSize(10).FontColor(Colors.Grey.Medium);
                        }

                        // Shopping List Table
                        column.Item().PaddingTop(10).Table(table =>
                        {
                            // Define columns - adjusted widths to fit with color swatch
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);  // Color swatch
                                columns.ConstantColumn(50);  // Code (reduced from 60)
                                columns.RelativeColumn(2);   // Name (reduced relative size)
                                columns.ConstantColumn(60);  // Stitches (reduced from 70)
                                columns.ConstantColumn(45);  // Yards (reduced from 50)
                                columns.ConstantColumn(45);  // Skeins (reduced from 50)
                            });

                            // Header - smaller padding
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken3)
                                    .Padding(3).Text("Color").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3)
                                    .Padding(3).Text("Code").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3)
                                    .Padding(3).Text("Name").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3)
                                    .Padding(3).Text("Stitches").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3)
                                    .Padding(3).Text("Yards").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3)
                                    .Padding(3).Text("Skeins").FontColor(Colors.White).FontSize(8).Bold();
                            });

                            // Rows - smaller padding and font
                            foreach (var yarn in data.YarnMatches) {
                                var bgColor = data.YarnMatches.IndexOf(yarn) % 2 == 0
                                    ? Colors.White
                                    : Colors.Grey.Lighten4;

                                // Color swatch cell
                                table.Cell().Background(bgColor).Padding(3)
                                    .Background(yarn.HexColor)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Medium);

                                table.Cell().Background(bgColor).Padding(3)
                                    .Text(yarn.Code).FontSize(8);
                                table.Cell().Background(bgColor).Padding(3)
                                    .Text(yarn.Name).FontSize(8);
                                table.Cell().Background(bgColor).Padding(3)
                                    .Text(yarn.StitchCount.ToString("N0")).FontSize(8);
                                table.Cell().Background(bgColor).Padding(3)
                                    .Text(yarn.YardsNeeded.ToString()).FontSize(8);
                                table.Cell().Background(bgColor).Padding(3)
                                    .Text(yarn.EstimatedSkeins.ToString()).FontSize(8).Bold();
                            }

                            // Footer
                            table.Footer(footer =>
                            {
                                footer.Cell().Background(Colors.Grey.Lighten2).Padding(3); // Empty for color column
                                footer.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten2)
                                    .Padding(3).Text("TOTALS:").FontSize(8).Bold();
                                footer.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(3).Text(data.YarnMatches.Sum(m => m.StitchCount).ToString("N0")).FontSize(8).Bold();
                                footer.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(3).Text(data.YarnMatches.Sum(m => m.YardsNeeded).ToString()).FontSize(8).Bold();
                                footer.Cell().Background(Colors.Grey.Lighten2)
                                    .Padding(3).Text(data.YarnMatches.Sum(m => m.EstimatedSkeins).ToString()).FontSize(8).Bold();
                            });
                        });

                        // Instructions
                        column.Item().PageBreak();
                        column.Item().Text("Stitching Instructions").FontSize(14).Bold();

                        column.Item().PaddingTop(10).Column(instructions =>
                        {
                            instructions.Item().Text("Getting Started:").Bold();
                            instructions.Item().PaddingLeft(15).Text("1. Cut canvas 2-3 inches larger on all sides");
                            instructions.Item().PaddingLeft(15).Text("2. Bind edges with masking tape");
                            instructions.Item().PaddingLeft(15).Text("3. Mark the center of your canvas");

                            instructions.Item().PaddingTop(10).Text("Stitching Tips:").Bold();
                            instructions.Item().PaddingLeft(15).Text("• Work from center outward");
                            instructions.Item().PaddingLeft(15).Text("• Use 18-inch strands of yarn");
                            instructions.Item().PaddingLeft(15).Text("• Keep consistent tension");

                            instructions.Item().PaddingTop(10).Text("Finishing:").Bold();
                            instructions.Item().PaddingLeft(15).Text("• Block by dampening and pinning to shape");
                            instructions.Item().PaddingLeft(15).Text("• Allow to dry completely");
                            instructions.Item().PaddingLeft(15).Text("• Professional framing recommended");
                        });

                        // Color Legend with Symbols
                        column.Item().PageBreak();
                        column.Item().Text("Color Reference Guide").FontSize(14).Bold();
                        column.Item().PaddingTop(5).Text("Use this guide to identify colors while stitching").FontSize(9);

                        column.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(30);  // Color swatch
                                columns.ConstantColumn(45);  // Code
                                columns.RelativeColumn(2);   // Name
                                columns.ConstantColumn(40);  // Symbol
                                columns.ConstantColumn(60);  // Usage %
                            });

                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(3)
                                    .Text("Color").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(3)
                                    .Text("Code").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(3)
                                    .Text("Name").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(3)
                                    .Text("Symbol").FontColor(Colors.White).FontSize(8).Bold();
                                header.Cell().Background(Colors.Blue.Darken3).Padding(3)
                                    .Text("Usage").FontColor(Colors.White).FontSize(8).Bold();
                            });

                            var totalStitches = data.YarnMatches.Sum(m => m.StitchCount);
                            var symbols = "•○◆◇■□▲△★☆●◉▪▫◘◙▼▽◊◈";

                            for (int i = 0; i < data.YarnMatches.Count; i++) {
                                var yarn = data.YarnMatches[i];
                                var symbol = i < symbols.Length ? symbols[i].ToString() : (i + 1).ToString();
                                var percentage = totalStitches > 0
                                    ? (yarn.StitchCount * 100.0 / totalStitches).ToString("F1")
                                    : "0";

                                var rowColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                                // Color swatch
                                table.Cell().Background(rowColor).Padding(3)
                                    .Background(yarn.HexColor)
                                    .Border(1)
                                    .BorderColor(Colors.Grey.Medium);

                                table.Cell().Background(rowColor).Padding(3)
                                    .Text(yarn.Code).FontSize(8);

                                table.Cell().Background(rowColor).Padding(3)
                                    .Text(yarn.Name).FontSize(8);

                                table.Cell().Background(rowColor).Padding(3)
                                    .Text(symbol).FontSize(11).Bold().AlignCenter();

                                table.Cell().Background(rowColor).Padding(3)
                                    .Text($"{percentage}%").FontSize(8);
                            }
                        });
                    });

                    page.Footer().AlignCenter().Text(text => {
                        text.Span("Created with ");
                        text.Span("StitchLens").Bold();
                        text.Span(" - Page ");
                        text.CurrentPageNumber();
                    });
                });

                // Add stitch grid pages
                RenderStitchGridPages(container, data);

            });

            return document.GeneratePdf();
        });
    }

    private void RenderStitchGridPages(IDocumentContainer container, PatternPdfData data) {
        if (data.StitchGrid == null) return;

        // Determine grid page size (how many stitches fit per page)
        // At 10 stitches per inch, we can fit about 70 stitches width, 90 height on letter with margins
        const int stitchesPerPageWidth = 50;
        const int stitchesPerPageHeight = 65;

        int pagesWide = (int)Math.Ceiling((double)data.StitchGrid.Width / stitchesPerPageWidth);
        int pagesHigh = (int)Math.Ceiling((double)data.StitchGrid.Height / stitchesPerPageHeight);

        for (int pageY = 0; pageY < pagesHigh; pageY++) {
            for (int pageX = 0; pageX < pagesWide; pageX++) {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter.Landscape()); // Landscape for more width
                    page.Margin(0.4f, Unit.Inch);
                    page.PageColor(Colors.White);

                    int startX = pageX * stitchesPerPageWidth;
                    int startY = pageY * stitchesPerPageHeight;
                    int endX = Math.Min(startX + stitchesPerPageWidth, data.StitchGrid.Width);
                    int endY = Math.Min(startY + stitchesPerPageHeight, data.StitchGrid.Height);

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Stitch Chart - Section {pageX + 1},{pageY + 1}")
                                .FontSize(12).Bold();
                            row.RelativeItem().AlignRight()
                                .Text($"Rows {startY + 1}-{endY}, Columns {startX + 1}-{endX}")
                                .FontSize(10);
                        });
                        col.Item().PaddingTop(3).LineHorizontal(1);
                    });

                    page.Content().Table(table =>
                    {
                        // Column for row numbers + one column per stitch
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(25); // Row number column
                            for (int x = startX; x < endX; x++) {
                                columns.ConstantColumn(12); // Each stitch cell
                            }
                        });

                        // Header row with column numbers
                        table.Header(header =>
                        {
                            header.Cell().Text("").FontSize(6); // Empty corner
                            for (int x = startX; x < endX; x++) {
                                bool isTenthCol = (x + 1) % 10 == 0;
                                var colNum = (x + 1).ToString();
                                var bgColor = isTenthCol ? Colors.Grey.Lighten2 : Colors.White;

                                header.Cell().Background(bgColor).Padding(1).AlignCenter()
                                    .Text(colNum).FontSize(5).Bold();
                            }
                        });

                        // Grid rows
                        for (int y = startY; y < endY; y++) {
                            // Determine if this is a 10th row (bolder line below)
                            bool isTenthRow = (y + 1) % 10 == 0;

                            // Row number
                            var rowBg = isTenthRow ? Colors.Grey.Lighten2 : Colors.Grey.Lighten3;
                            table.Cell().Background(rowBg).Padding(2)
                                .AlignCenter().Text((y + 1).ToString()).FontSize(6).Bold();

                            // Stitch cells
                            for (int x = startX; x < endX; x++) {
                                var cell = data.StitchGrid.Cells[x, y];

                                // Determine if this is a 10th column (bolder line on right)
                                bool isTenthCol = (x + 1) % 10 == 0;

                                // Determine border widths
                                float rightBorder = isTenthCol ? 1.5f : 0.5f;
                                float bottomBorder = isTenthRow ? 1.5f : 0.5f;

                                // Build the cell in one fluent chain
                                var cellBuilder = table.Cell()
                                    .Border(0.5f)
                                    .BorderRight(rightBorder)
                                    .BorderBottom(bottomBorder)
                                    .BorderColor(Colors.Grey.Lighten1);

                                // Add background color if option is enabled
                                if (data.UseColoredGrid) {
                                    cellBuilder = cellBuilder.Background(cell.HexColor);
                                }

                                // Complete the cell with padding and content
                                cellBuilder.Padding(1)
                                    .AlignCenter().AlignMiddle()
                                    .Text(cell.Symbol).FontSize(8);
                            }
                        }
                    });

                    page.Footer().AlignCenter().Text($"Page {pageX + 1 + (pageY * pagesWide)} of {pagesWide * pagesHigh}")
                        .FontSize(8);
                });
            }
        }
    }
}