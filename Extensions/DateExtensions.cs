using System.Globalization;

namespace GoldShopWeb.Extensions;

public static class DateExtensions
{
    private static readonly ThaiBuddhistCalendar Calendar = new();

    /// <summary>2022-03-15 -> "15/03/2565" (Buddhist era, as shown on the back office).</summary>
    public static string ToThaiDate(this DateTime date) =>
        $"{date.Day:D2}/{date.Month:D2}/{Calendar.GetYear(date)}";
}
