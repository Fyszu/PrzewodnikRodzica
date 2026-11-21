using PrzewodnikRodzica.Core.Models;

namespace PrzewodnikRodzica.Core.Services;

public static class CharitySupportCatalog
{
    public static List<CharityOrganization> GetAll() => new()
    {
        new CharityOrganization
        {
            Name = "Fundacja Ernesta",
            FocusArea = "Poronienie, urodzenie martwego dziecka, strata okołoporodowa",
            Description = "Bezpłatne wsparcie psychologiczne, grupy wsparcia i praktyczna pomoc formalna dla rodziców po stracie ciąży lub noworodka.",
            WebsiteUrl = "https://fundacjaernesta.pl"
        },
        new CharityOrganization
        {
            Name = "Fundacja Tęczowy Kocyk",
            FocusArea = "Strata dziecka, wsparcie szpitalne i formalne",
            Description = "Pomoc rodzicom po poronieniu i martwym urodzeniu, edukacja w zakresie praw pacjenta i formalności w USC oraz ZUS.",
            WebsiteUrl = "https://teczowykocyk.pl"
        },
        new CharityOrganization
        {
            Name = "Fundacja Wcześniak Rodzice-Rodzicom",
            FocusArea = "Przedwczesny poród i narodziny wcześniaka",
            Description = "Wsparcie merytoryczne, materiały edukacyjne dla rodziców dzieci na oddziałach neonatologicznych oraz programy rehabilitacyjne.",
            WebsiteUrl = "https://wczesniak.pl"
        },
        new CharityOrganization
        {
            Name = "Koalicja dla Wcześniaka",
            FocusArea = "Wcześniaki i hospitalizacja poporodowa",
            Description = "Organizacja działająca na rzecz praw wcześniaków i ich rodziców (m.in. inicjator prac nad uzupełniającym urlopem macierzyńskim).",
            WebsiteUrl = "https://koalicjadlawczesniaka.pl"
        },
        new CharityOrganization
        {
            Name = "Fundacja Gajusz",
            FocusArea = "Ciężko chore dzieci, zaświadczenie „Za życiem”, hospicjum perinatalne",
            Description = "Opieka hospicjum perinatalnego i domowego, wsparcie psychologiczne i socjalne rodzin dzieci z ciężkimi schorzeniami.",
            WebsiteUrl = "https://gajusz.org.pl"
        },
        new CharityOrganization
        {
            Name = "Fundacja Warszawskie Hospicjum dla Dzieci",
            FocusArea = "Dzieci z nieuleczalnymi chorobami, wsparcie perinatalne",
            Description = "Hospicjum perinatalne i domowe oraz bezpłatne poradnictwo dla rodziców w najtrudniejszych diagnozach prenatalnych.",
            WebsiteUrl = "https://hospicjum.waw.pl"
        }
    };

    public static List<ExternalLinkItem> GetOfficialLinks() => new()
    {
        new ExternalLinkItem
        {
            Title = "Zasiłki i wzory formularzy (ZAM, Z-3, Z-3a, ZAS-53)",
            Institution = "Zakład Ubezpieczeń Społecznych (ZUS)",
            Url = "https://www.zus.pl"
        },
        new ExternalLinkItem
        {
            Title = "Urlop rodzicielski i uprawnienia rodzinne",
            Institution = "Ministerstwo Rodziny, Pracy i Polityki Społecznej",
            Url = "https://www.gov.pl/web/rodzina/urlop-rodzicielski"
        },
        new ExternalLinkItem
        {
            Title = "Porady prawne: ochrona w ciąży i rodzicielstwo (Kodeks pracy)",
            Institution = "Państwowa Inspekcja Pracy (PIP)",
            Url = "https://www.pip.gov.pl/dla-pracodawcow/porady-prawne/rodzicielstwo"
        },
        new ExternalLinkItem
        {
            Title = "Wniosek o odpis aktu urodzenia (Rejestr Stanu Cywilnego)",
            Institution = "Gov.pl / Ministerstwo Cyfryzacji",
            Url = "https://www.gov.pl/web/cyfryzacja/rejestr-stanu-cywilnego"
        }
    };
}