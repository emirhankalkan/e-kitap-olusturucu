using EKitap.Api.Services;
using Xunit;

namespace EKitap.Tests;

public sealed class ContactInfoCleanerTests
{
    private readonly ContactInfoCleaner cleaner = new();

    [Theory]
    [InlineData("E-posta: elif.kaya@example.org | Tel: 0500 000 00 01 | ORCID: 0000-0001-1000-0001", "ORCID: 0000-0001-1000-0001")]
    [InlineData("Email: mert.demir@example.org | Telefon: +90 (500) 000 00 02 | ORCID: 0000-0001-1000-0002", "ORCID: 0000-0001-1000-0002")]
    [InlineData("E-posta derya.akin@example.org / GSM 0 (500) 000 00 12", "")]
    [InlineData("İletişim: selin.arslan@example.org - 0500-000-00-03 - ORCID 0000-0001-1000-0003", "ORCID 0000-0001-1000-0003")]
    [InlineData("kerem.celik@example.org | Cep: +90 500 000 00 04", "")]
    [InlineData("zeynep.sen@example.org | 0500 000 10 04", "")]
    [InlineData("Eposta: burcu.ozturk@example.org; Telefon: (0500) 000 00 05", "")]
    [InlineData("E-mail onur.sahin@example.org | Mobile +90-500-000-00-06", "")]
    [InlineData("nazli.er@example.org / Tel.No: 0 500 000 00 07", "")]
    [InlineData("emre.topal@example.org / GSM: +90 (500) 000-10-07", "")]
    [InlineData("Mail: ipek.yalcin@example.org | İrtibat: 0500.000.00.08", "")]
    [InlineData("tolga.gunes@example.org | Telefon 0500 000 00 09", "")]
    [InlineData("E-posta: asli.cetin@example.org | Cep telefonu: +90 500 000 00 10", "")]
    [InlineData("İletişim: test+case@sub.example.com.tr", "")]
    [InlineData("Phone: +1 (202) 555-0123", "")]
    [InlineData("Tel: 312 343 10 33", "")]
    [InlineData("Telefon: 0312 343 10 33", "")]
    [InlineData("GSM: 0090 500 000 00 01", "")]
    public void ContactFormats_AreRemoved(string input, string expected)
    {
        Assert.Equal(expected, cleaner.Clean(input));
    }

    [Theory]
    [InlineData("ORCID: 0000-0002-1825-0097")]
    [InlineData("ORCID 0000-0001-5109-370X")]
    [InlineData("2026-09-27 tarihinde 120 katılımcı ve %24 tasarruf; 3.14 ve 1.000,50 TL.")]
    [InlineData("Proje kodu: 3123431033; örneklem 5000000001.")]
    [InlineData("Dr. Elif KAYA | Ankara Örnek Üniversitesi, TÜRKİYE")]
    public void OtherInformation_IsPreservedExactly(string input)
    {
        Assert.Equal(input, cleaner.Clean(input));
    }

    [Fact]
    public void Cleaning_IsIdempotent()
    {
        const string input = "Yazar | E-posta: a@example.org | Telefon: +90 (500) 000 00 01 | ORCID 0000-0002-1825-0097";
        var cleaned = cleaner.Clean(input);
        Assert.Equal("Yazar | ORCID 0000-0002-1825-0097", cleaned);
        Assert.Equal(cleaned, cleaner.Clean(cleaned));
    }
}
