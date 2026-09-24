using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// Nedräkning mot en påminnelses avgångstid ("Gå 13:30" - se MinDag.razor:s DepartureLabel,
/// docs/PRODUCT.md §11), uttryckt som ett fast antal 5-minutersstaplar i stället för en klocka
/// man själv måste läsa av mot nuet. Läser aldrig klockan själv - "nu" skickas alltid in som
/// parameter (CLAUDE.md §5), så både avrundningen och 60-minutersfönstret går att testa med
/// fasta tidpunkter.
///
/// Två separata rena funktioner, med avsikt: <see cref="Calculate"/> avgör HUR en enskild
/// avgångstid räknas om till staplar, <see cref="SelectReminder"/> avgör VILKEN av flera
/// påminnelser som överhuvudtaget ska visa en nedräkning ("bara den närmaste"). Att hålla isär
/// dem låter gränsfallen i <see cref="Calculate"/> (avrundningen, 60-minutersgränsen, passerad
/// tid) testas utan att bygga upp en lista påminnelser, och urvalsregeln testas utan att räkna
/// staplar.
/// </summary>
public static class DepartureCountdown
{
    /// <summary>En stapel är exakt 5 minuter, alltid - ett fast kvantum, inte en andel av
    /// kvarvarande tid. Det är just att takten aldrig ändras som gör staplarna läsbara.</summary>
    public const int MinutesPerBar = 5;

    /// <summary>Nedräkningen visas bara det sista fönstret före avgång: 60 minuter, alltså som
    /// mest <see cref="TotalBars"/> staplar.</summary>
    public const int WindowMinutes = 60;

    /// <summary><see cref="WindowMinutes"/> / <see cref="MinutesPerBar"/> - alltid 12 med
    /// dagens fasta kvantum och fönster.</summary>
    public const int TotalBars = WindowMinutes / MinutesPerBar;

    /// <summary>Resultatet av <see cref="Calculate"/>: hur många av <see cref="TotalBars"/>
    /// staplar som är fyllda, det fasta totalantalet staplar (alltid <see cref="TotalBars"/> i
    /// dagens utformning, men en del av resultatet så anroparen aldrig behöver gissa), och hur
    /// många hela minuter som är kvar - avrundat uppåt med exakt samma avrundning som staplarna,
    /// så att gränssnittets siffra ("om 25 min") och antalet fyllda staplar alltid stämmer
    /// överens med varandra.</summary>
    public readonly record struct Countdown(int FilledBars, int TotalBars, int MinutesRemaining);

    /// <summary>
    /// Räknar om avståndet mellan <paramref name="now"/> och <paramref name="departure"/> till
    /// fyllda staplar. <c>null</c> är det NORMALA resultatet, inte ett felläge - de allra flesta
    /// tidpunkter ligger antingen mer än 60 minuter bort eller har redan passerat, och båda ger
    /// <c>null</c>: en passerad avgångstid ska bli tyst (docs/PRODUCT.md §8/§11 - "En passerad
    /// tid blir tyst"), och en nedräkning visas bara den sista timmen. Avrundar alltid uppåt -
    /// en påbörjad femminutersperiod räknas (21 minuter kvar → 5 staplar).
    /// </summary>
    /// <remarks>
    /// <paramref name="now"/> och <paramref name="departure"/> måste båda vara lokal
    /// väggklockstid (samma slags värde som <c>TimeProvider.GetLocalNow().DateTime</c> i
    /// MinDag.razor, eller <see cref="DepartureTime"/>s eget resultat) - aldrig UTC. Skickas en
    /// UTC-tidpunkt in ger subtraktionen ett tyst FELAKTIGT svar snarare än ett undantag, eftersom
    /// <see cref="DateTime"/> inte bär med sig vilken sort tid den representerar
    /// (<see cref="DateOnly.ToDateTime(TimeOnly)"/>, som <see cref="DepartureTime"/> bygger på,
    /// ger <see cref="DateTimeKind.Unspecified"/>). Samma fälla som
    /// <c>Hemordna.Application.Time.HouseholdClock</c> varnar för i sin egen doc, av samma skäl.
    /// </remarks>
    public static Countdown? Calculate(DateTime now, DateTime departure)
    {
        var minutesRemaining = (departure - now).TotalMinutes;
        if (minutesRemaining <= 0 || minutesRemaining > WindowMinutes)
        {
            return null;
        }

        var filledBars = (int)Math.Ceiling(minutesRemaining / MinutesPerBar);
        return new Countdown(filledBars, TotalBars, (int)Math.Ceiling(minutesRemaining));
    }

    /// <summary>
    /// Avgångstiden ("Gå"-tiden) som en jämförbar tidpunkt: klockslaget minus restiden, på
    /// påminnelsens eget datum. <c>null</c> när klockslag eller restid saknas - samma två villkor
    /// som MinDag.razor:s egen <c>DepartureLabel</c> redan kräver för att visa något alls.
    /// Offentlig så en anropare (t.ex. renderingen) kan räkna om samma avgångstid som
    /// <see cref="SelectReminder"/> redan använt, utan att duplicera uträkningen.
    /// </summary>
    public static DateTime? DepartureTime(ReminderResponse reminder)
    {
        if (reminder.TimeOfDay is not { } time || reminder.TravelMinutes is not { } travelMinutes)
        {
            return null;
        }

        // Att subtrahera minuter från en riktig DateTime rullar datumet bakåt av sig självt när
        // restiden är längre än klockslaget - samma dygnsomslag som MinDag.razor:s egen
        // DepartureLabel räknar fram för hand (för sin textrad), men här utan manuell
        // modulo-aritmetik eftersom resultatet är en jämförbar tidpunkt, inte en text.
        return reminder.Date.ToDateTime(time).AddMinutes(-travelMinutes);
    }

    /// <summary>
    /// Bland flera påminnelser: den vars avgång ligger närmast i tiden OCH inom
    /// 60-minutersfönstret - "bara den närmaste" (annars blir Min dag ett fält av staplar).
    /// <c>null</c> är det normala resultatet: de allra flesta anrop sker utan någon påminnelse
    /// inom fönstret alls. En påminnelse utan klockslag eller utan restid saknar en avgångstid
    /// att räkna mot och kan aldrig väljas (se <see cref="DepartureTime"/>). Bara
    /// <c>"Upcoming"</c> kan väljas - skriven som ett tillåtande filter snarare än att räkna upp
    /// vad som hoppas över, så en framtida status (utöver dagens <c>Cancelled</c>/
    /// <c>CheckedOff</c>) blir tyst som standard i stället för att av misstag råka räknas ner
    /// mot. En avbockad påminnelse ligger fortfarande kvar på dagen
    /// (<c>MinDag.razor</c>:s <c>TodaysReminders</c>/<c>ReminderCheckedRow</c>) men ska inte
    /// fortsätta räknas ner - samma resonemang som redan tystar notiserna för den, se
    /// <c>Hemordna.Application.Push.ReminderNotificationSelector</c> och
    /// <see cref="Hemordna.Domain.Reminders.ReminderStatus.CheckedOff"/>s egen doc: har ägaren
    /// själv sagt "den här är avklarad" ska appen inte motsäga det genom att fortsätta räkna ner
    /// mot avgången.
    /// </summary>
    public static ReminderResponse? SelectReminder(IReadOnlyList<ReminderResponse> reminders, DateTime now)
    {
        ReminderResponse? nearest = null;
        DateTime? nearestDeparture = null;

        foreach (var reminder in reminders)
        {
            if (reminder.Status != "Upcoming")
            {
                continue;
            }

            if (DepartureTime(reminder) is not { } departure || Calculate(now, departure) is null)
            {
                continue;
            }

            // Jämför den verkliga avgångstidpunkten, inte det redan uppåtavrundade
            // MinutesRemaining - två avgångar som båda avrundas till "25 min" (24,1 och 24,9
            // minuter bort) ska ändå avgöras av vilken som faktiskt ligger närmast, inte av
            // listordningen.
            if (nearestDeparture is null || departure < nearestDeparture)
            {
                nearest = reminder;
                nearestDeparture = departure;
            }
        }

        return nearest;
    }
}
