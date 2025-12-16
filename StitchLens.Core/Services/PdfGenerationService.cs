using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StitchLens.Data.Models; // For CraftType enum

namespace StitchLens.Core.Services;

public class PdfGenerationService : IPdfGenerationService {
 public PdfGenerationService() {
 QuestPDF.Settings.License = LicenseType.Community;
 }

 public async Task<byte[]> GeneratePatternPdfAsync(PatternPdfData data) {
 return await Task.Run(() => {
 // Determine craft-specific terminology
 var craftName = data.CraftType == Data.Models.CraftType.Needlepoint ? "Needlepoint" : "Cross-Stitch";
 var materialName = data.CraftType == Data.Models.CraftType.Needlepoint ? "Canvas" : "Fabric";
 var countLabel = data.CraftType == Data.Models.CraftType.Needlepoint ? "Mesh" : "Count";
 var threadType = data.CraftType == Data.Models.CraftType.Needlepoint ? "Yarn" : "Floss";

 // Precompute totals for use in header/footer/rows
 var totalStitches = data.YarnMatches.Sum(m => m.StitchCount);
 var totalYards = data.YarnMatches.Sum(m => m.YardsNeeded);
 var totalSkeinsFractional = data.YarnMatches.Sum(m => m.EstimatedSkeins);
 var totalSkeinsRoundedUp = data.YarnMatches.Sum(m => (int)Math.Ceiling(m.EstimatedSkeins));

 var document = Document.Create(container => {
 container.Page(page => {
 page.Size(PageSizes.Letter);
 page.Margin(0.5f, Unit.Inch);
 page.PageColor(Colors.White);
 page.DefaultTextStyle(x => x.FontSize(10));

 page.Header().Column(column => {
 column.Item().Text($"StitchLens {craftName} Pattern") // UPDATED
 .FontSize(20).Bold().FontColor(Colors.Blue.Darken3);

 column.Item().PaddingTop(10).Text(data.Title).FontSize(14).SemiBold();

 column.Item().PaddingTop(10).Row(row => {
 row.RelativeItem().Column(col => {
 col.Item().Text($"{materialName} Specifications").Bold(); // UPDATED
 col.Item().Text($"{countLabel}: {data.MeshCount} count"); // UPDATED
 col.Item().Text($"Size: {data.WidthInches:F1}\" × {data.HeightInches:F1}\"");
 col.Item().Text($"Stitches: {data.WidthStitches} × {data.HeightStitches}");
 });

 row.RelativeItem().Column(col => {
 col.Item().Text("Materials").Bold();
 col.Item().Text($"{threadType} Brand: {data.YarnBrand}"); // UPDATED
 col.Item().Text($"Colors: {data.YarnMatches.Count}");
 // Show whole skein total only (no fractional number)
 col.Item().Text($"Total Skeins: {totalSkeinsRoundedUp}");
 });
 });

 column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
 });

 page.Content().PaddingTop(10).Column(column =>
 {
 // Make image larger - full page width
 column.Item().Text("Pattern Preview").FontSize(12).Bold();

 if (data.QuantizedImageData != null && data.QuantizedImageData.Length >0) {
 column.Item().PaddingTop(5).PaddingBottom(10)
 .Height(4, Unit.Inch)
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
 table.ColumnsDefinition(columns =>
 {
 columns.ConstantColumn(30);
 columns.ConstantColumn(50);
 columns.RelativeColumn(2);
 columns.ConstantColumn(60);
 columns.ConstantColumn(45);
 columns.ConstantColumn(70); // wider for skeins + percent
 });

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
 // Use two spaces between Skeins and (% used) to match preview
 header.Cell().Background(Colors.Blue.Darken3)
 .Padding(3).Text("Skeins (% used)").FontColor(Colors.White).FontSize(8).Bold();
 });

 foreach (var yarn in data.YarnMatches) {
 var bgColor = data.YarnMatches.IndexOf(yarn) %2 ==0
 ? Colors.White
 : Colors.Grey.Lighten4;

 var percentage = totalStitches >0
 ? (yarn.StitchCount *100.0 / totalStitches).ToString("F0")
 : "0";

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
 .Text(yarn.YardsNeeded.ToString("F2")).FontSize(8);
 // Show integer skeins and percentage like preview: "1 (40%)"
 table.Cell().Background(bgColor).Padding(3)
 .Text($"{(int)Math.Ceiling(yarn.EstimatedSkeins)} ({percentage}%)").FontSize(8).Bold();
 }

 table.Footer(footer =>
 {
 footer.Cell().Background(Colors.Grey.Lighten2).Padding(3);
 footer.Cell().ColumnSpan(2).Background(Colors.Grey.Lighten2)
 .Padding(3).Text("TOTALS:").FontSize(8).Bold();
 footer.Cell().Background(Colors.Grey.Lighten2)
 .Padding(3).Text(totalStitches.ToString("N0")).FontSize(8).Bold();
 // Round yards footer to2 decimals
 footer.Cell().Background(Colors.Grey.Lighten2)
 .Padding(3).Text(totalYards.ToString("F2")).FontSize(8).Bold();
 // Show integer total skeins only
 footer.Cell().Background(Colors.Grey.Lighten2)
 .Padding(3).Text(totalSkeinsRoundedUp.ToString()).FontSize(8).Bold();
 });
 });

 // Instructions - CRAFT SPECIFIC
 column.Item().PageBreak();
 column.Item().Text("Stitching Instructions").FontSize(14).Bold();

 column.Item().PaddingTop(10).Column(instructions =>
 {
 if (data.CraftType == Data.Models.CraftType.Needlepoint) {
 // Needlepoint instructions
 instructions.Item().Text("Getting Started:").Bold();
 instructions.Item().PaddingLeft(15).Text("1. Cut canvas2-3 inches larger on all sides");
 instructions.Item().PaddingLeft(15).Text("2. Bind edges with masking tape");
 instructions.Item().PaddingLeft(15).Text("3. Mark the center of your canvas");

 instructions.Item().PaddingTop(10).Text("Stitching Tips:").Bold();
 instructions.Item().PaddingLeft(15).Text("• Work from center outward");
 instructions.Item().PaddingLeft(15).Text("• Use18-inch strands of yarn");
 instructions.Item().PaddingLeft(15).Text("• Keep consistent tension");

 instructions.Item().PaddingTop(10).Text("Finishing:").Bold();
 instructions.Item().PaddingLeft(15).Text("• Block by dampening and pinning to shape");
 instructions.Item().PaddingLeft(15).Text("• Allow to dry completely");
 instructions.Item().PaddingLeft(15).Text("• Professional framing recommended");
 }
 else {
 // Cross-stitch instructions
 instructions.Item().Text("Getting Started:").Bold();
 instructions.Item().PaddingLeft(15).Text("1. Find the center of your fabric by folding in half both ways");
 instructions.Item().PaddingLeft(15).Text("2. Mark center with a water-soluble pen or by basting");
 instructions.Item().PaddingLeft(15).Text("3. Use an embroidery hoop to keep fabric taut");

 instructions.Item().PaddingTop(10).Text("Stitching Tips:").Bold();
 instructions.Item().PaddingLeft(15).Text("• Start from the center and work outward");
 instructions.Item().PaddingLeft(15).Text("• Use2 strands of floss (6-strand divisible)");
 instructions.Item().PaddingLeft(15).Text("• Keep all top stitches facing the same direction");
 instructions.Item().PaddingLeft(15).Text("• Work in rows for best coverage");

 instructions.Item().PaddingTop(10).Text("Finishing:").Bold();
 instructions.Item().PaddingLeft(15).Text("• Gently hand wash in cool water if needed");
 instructions.Item().PaddingLeft(15).Text("• Press face-down on a towel while damp");
 instructions.Item().PaddingLeft(15).Text("• Frame or mount as desired");
 }
 });

 // Color Legend with Symbols
 column.Item().PageBreak();
 column.Item().Text("Color Reference Guide").FontSize(14).Bold();
 column.Item().PaddingTop(5).Text("Use this guide to identify colors while stitching").FontSize(9);

 column.Item().PaddingTop(10).Table(table =>
 {
 table.ColumnsDefinition(columns =>
 {
 columns.ConstantColumn(30);
 columns.ConstantColumn(45);
 columns.RelativeColumn(2);
 columns.ConstantColumn(40);
 columns.ConstantColumn(60);
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

 var symbols = "•○◆◇■□▲△★☆●◉▪▫◘◙▼▽◊◈";

 for (int i =0; i < data.YarnMatches.Count; i++) {
 var yarn = data.YarnMatches[i];
 var symbol = i < symbols.Length ? symbols[i].ToString() : (i +1).ToString();
 var percentage = totalStitches >0
 ? (yarn.StitchCount *100.0 / totalStitches).ToString("F1")
 : "0";

 var rowColor = i %2 ==0 ? Colors.White : Colors.Grey.Lighten4;

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
 // At10 stitches per inch, we can fit about70 stitches width,90 height on letter with margins
 const int stitchesPerPageWidth =50;
 const int stitchesPerPageHeight =65;

 int pagesWide = (int)Math.Ceiling((double)data.StitchGrid.Width / stitchesPerPageWidth);
 int pagesHigh = (int)Math.Ceiling((double)data.StitchGrid.Height / stitchesPerPageHeight);

 for (int pageY =0; pageY < pagesHigh; pageY++) {
 for (int pageX =0; pageX < pagesWide; pageX++) {
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
 row.RelativeItem().Text($"Stitch Chart - Section {pageX +1},{pageY +1}")
 .FontSize(12).Bold();
 row.RelativeItem().AlignRight()
 .Text($"Rows {startY +1}-{endY}, Columns {startX +1}-{endX}")
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
 bool isTenthCol = (x +1) %10 ==0;
 var colNum = (x +1).ToString();
 var bgColor = isTenthCol ? Colors.Grey.Lighten2 : Colors.White;

 header.Cell().Background(bgColor).Padding(1).AlignCenter()
 .Text(colNum).FontSize(5).Bold();
 }
 });

 // Grid rows
 for (int y = startY; y < endY; y++) {
 // Determine if this is a10th row (bolder line below)
 bool isTenthRow = (y +1) %10 ==0;

 // Row number
 var rowBg = isTenthRow ? Colors.Grey.Lighten2 : Colors.Grey.Lighten3;
 table.Cell().Background(rowBg).Padding(2)
 .AlignCenter().Text((y +1).ToString()).FontSize(6).Bold();

 // Stitch cells
 for (int x = startX; x < endX; x++) {
 var cell = data.StitchGrid.Cells[x, y];

 // Determine if this is a10th column (bolder line on right)
 bool isTenthCol = (x +1) %10 ==0;

 // Determine border widths
 float rightBorder = isTenthCol ?1.5f :0.5f;
 float bottomBorder = isTenthRow ?1.5f :0.5f;

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

 page.Footer().AlignCenter().Text($"Page {pageX +1 + (pageY * pagesWide)} of {pagesWide * pagesHigh}")
 .FontSize(8);
 });
 }
 }
 }
}