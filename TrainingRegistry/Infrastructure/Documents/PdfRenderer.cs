using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TrainingRegistry.Infrastructure.Documents;

public static class PdfRenderer
{
    private const string FontName = "Times New Roman";
    public static byte[] Render(IReadOnlyList<Block> blocks, string footerText)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginTop(2, Unit.Centimetre);
                page.MarginBottom(2, Unit.Centimetre);
                page.MarginLeft(3, Unit.Centimetre);
                page.MarginRight(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily(FontName).FontSize(11));
                page.Content().Column(column =>
                {
                    column.Spacing(6);
                    foreach (var block in blocks)
                    {
                        switch (block)
                        {
                            case TitleBlock t:
                                column.Item().Text(text =>
                                {
                                    text.AlignCenter();
                                    text.Span(t.Text).Bold().FontSize(15);
                                });
                                if (t.Subtitle != null)
                                {
                                    column.Item().Text(text =>
                                    {
                                        text.AlignCenter();
                                        text.Span(t.Subtitle).Bold().FontSize(13);
                                    });
                                }
                                break;
                            case LineBlock l:
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Text(l.Left);
                                    row.RelativeItem().AlignRight().Text(l.Right);
                                });
                                break;
                            case HeadingBlock h:
                                column.Item().PaddingTop(6).Text(text =>
                                {
                                    text.AlignCenter();
                                    text.Span(h.Text).Bold();
                                });
                                break;
                            case ParaBlock p:
                                column.Item().Text(text =>
                                {
                                    text.Justify();
                                    text.Span("      " + p.Text);
                                });
                                break;
                            case TableBlock tb:
                                column.Item().Element(c => RenderTable(c, tb));
                                break;
                            case TwoColumnBlock tc:
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Column(c => RenderSide(c, tc.LeftTitle, tc.LeftLines));
                                    row.ConstantItem(20);
                                    row.RelativeItem().Column(c => RenderSide(c, tc.RightTitle, tc.RightLines));
                                });
                                break;
                        }
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken1));
                    text.Span(footerText + " · стр. ");
                    text.CurrentPageNumber();
                    text.Span(" из ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }
    private static void RenderSide(ColumnDescriptor column, string title, string[] lines)
    {
        column.Spacing(3);
        column.Item().Text(title).Bold();
        foreach (var line in lines)
            column.Item().Text(string.IsNullOrEmpty(line) ? " " : line);
    }
    private static void RenderTable(IContainer container, TableBlock tb)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var w in tb.Widths) columns.RelativeColumn((float)w);
            });
            table.Header(header =>
            {
                foreach (var title in tb.Header)
                    header.Cell().Background(Colors.Grey.Lighten2).Border(0.5f).BorderColor(Colors.Grey.Darken1)
                        .Padding(4).Text(title).Bold();
            });
            foreach (var row in tb.Rows)
                foreach (var value in row)
                    table.Cell().Border(0.5f).BorderColor(Colors.Grey.Darken1).Padding(4).Text(value);
        });
    }
}