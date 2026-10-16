using DineMaster.Application.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DineMaster.Api.Services;

/// <summary>Restoran hisobotini PDF ko'rinishida tayyorlaydi (QuestPDF, Community litsenziya).</summary>
public static class ReportPdf
{
    private static readonly string Accent = "#B45309";

    public static byte[] Generate(RestaurantReport r)
    {
        var periodName = r.Period switch
        {
            "day" => "Kunlik", "week" => "Haftalik", "month" => "Oylik", "year" => "Yillik", _ => r.Period
        };
        var breakdownTitle = r.Period switch
        {
            "day" => "Soatlar bo'yicha", "year" => "Oylar bo'yicha", _ => "Kunlar bo'yicha"
        };

        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(32);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

            page.Header().Column(h =>
            {
                h.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(EmailTemplates.Brand).FontSize(20).Bold().FontColor(Accent);
                        c.Item().Text($"{periodName} hisobot — {r.RestaurantName}").FontSize(13).SemiBold();
                        c.Item().Text($"Davr: {r.Label}  ({r.From:dd.MM.yyyy} – {r.To:dd.MM.yyyy})").FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(150).AlignRight().Text($"Tayyorlandi:\n{r.GeneratedAt:dd.MM.yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
                h.Item().PaddingTop(8).LineHorizontal(1).LineColor(Accent);
            });

            page.Content().PaddingTop(14).Column(col =>
            {
                col.Spacing(16);

                // KPI kartochkalari
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c => { for (var i = 0; i < 4; i++) c.RelativeColumn(); });
                    void Kpi(string label, string value)
                        => t.Cell().Padding(3).Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5)
                            .Padding(8).Column(c =>
                            {
                                c.Item().Text(value).FontSize(16).Bold();
                                c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                    var x = r.Totals;
                    Kpi("Jami bronlar", x.Reservations.ToString());
                    Kpi("Mehmonlar", x.Guests.ToString());
                    Kpi("Yakunlangan", x.Completed.ToString());
                    Kpi("Faol (kelgusi)", x.Active.ToString());
                    Kpi("Bekor qilingan", $"{x.Cancelled} ({x.CancelRate}%)");
                    Kpi("Kelmagan", $"{x.NoShow} ({x.NoShowRate}%)");
                    Kpi("To'langan depozit", Money(x.DepositsPaid));
                    Kpi("Oldindan buyurtma", Money(x.PreOrderTotal));
                    Kpi("O'rtacha mehmon", x.AvgGuests.ToString("0.#"));
                    Kpi("Noyob mijozlar", $"{x.UniqueGuests} (qaytgan: {x.ReturningGuests})");
                    Kpi("Sharhlar", x.Reviews == 0 ? "0" : $"{x.Reviews} (o'rt. {x.AvgRating}/10)");
                    Kpi("Kutish ro'yxati", x.WaitlistEntries.ToString());
                });

                if (r.Totals.DepositsUnpaid > 0)
                    col.Item().Text($"To'lanmagan depozitlar (faol bronlar): {Money(r.Totals.DepositsUnpaid)}")
                        .FontColor(Colors.Red.Darken2);

                // Taqsimot jadvali
                col.Item().Text(breakdownTitle).FontSize(12).SemiBold();
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                    });
                    t.Header(h =>
                    {
                        foreach (var title in new[] { "Davr", "Bronlar", "Mehmonlar", "Yakunlangan", "Bekor", "Kelmagan" })
                            h.Cell().Background(Accent).Padding(5).Text(title).FontColor(Colors.White).SemiBold().FontSize(9);
                    });
                    var i = 0;
                    foreach (var row in r.Breakdown)
                    {
                        var bg = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                        t.Cell().Background(bg).Padding(4).Text(row.Label);
                        t.Cell().Background(bg).Padding(4).AlignRight().Text(row.Reservations.ToString());
                        t.Cell().Background(bg).Padding(4).AlignRight().Text(row.Guests.ToString());
                        t.Cell().Background(bg).Padding(4).AlignRight().Text(row.Completed.ToString());
                        t.Cell().Background(bg).Padding(4).AlignRight().Text(row.Cancelled.ToString());
                        t.Cell().Background(bg).Padding(4).AlignRight().Text(row.NoShow.ToString());
                    }
                    if (r.Breakdown.Count == 0)
                        t.Cell().ColumnSpan(6).Padding(6).Text("Bu davrda bronlar yo'q.").FontColor(Colors.Grey.Darken1);
                });

                col.Item().Row(row =>
                {
                    row.Spacing(16);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Eng gavjum soatlar").FontSize(12).SemiBold();
                        foreach (var h in r.TopHours)
                            c.Item().PaddingTop(3).Text($"{h.Label} — {h.Reservations} ta bron, {h.Guests} mehmon");
                        if (r.TopHours.Count == 0) c.Item().Text("—");
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Ko'p buyurtma qilingan taomlar").FontSize(12).SemiBold();
                        foreach (var m in r.TopMenuItems)
                            c.Item().PaddingTop(3).Text($"{m.Name} — {m.Quantity} ta ({Money(m.Revenue)})");
                        if (r.TopMenuItems.Count == 0) c.Item().Text("Oldindan buyurtmalar yo'q.");
                    });
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Darken1));
                t.Span($"{EmailTemplates.Brand} · ");
                t.CurrentPageNumber();
                t.Span(" / ");
                t.TotalPages();
            });
        })).GeneratePdf();
    }

    private static string Money(decimal v) => $"{v:N0} so'm".Replace(',', ' ');
}
