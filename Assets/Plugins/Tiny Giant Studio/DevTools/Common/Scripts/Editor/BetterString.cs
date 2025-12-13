using System.Globalization;

namespace TinyGiantStudio.DevTools
{
    public static class BetterString
    {
        public static string Number(int number) => number.ToString("N0", CultureInfo.InvariantCulture);
    }
}