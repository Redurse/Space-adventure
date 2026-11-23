using System.Globalization;

namespace Anabiosis.Shared.Model;

// W6 of wiring-plan.md - signals are strings, as in Barotrauma: "1"/"0" for on/off, numbers as
// text, anything else as itself. No signal at all (null) is different from "0".
public static class SignalValues
{
    public const string True = "1";
    public const string False = "0";

    // Barotrauma's reading of a signal as a condition: present, not empty and not "0".
    public static bool IsTruthy(string? value) => !string.IsNullOrEmpty(value) && value != False;

    public static string Of(bool value) => value ? True : False;

    // A signal as a number: "12.5" is, "" and "abc" are not (a missing number reads as 0 where one is needed).
    public static bool TryNumber(string? value, out float number) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && float.IsFinite(number);

    public static float NumberOr(string? value, float fallback = 0f) => TryNumber(value, out var number) ? number : fallback;

    public static string Of(float value) => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
