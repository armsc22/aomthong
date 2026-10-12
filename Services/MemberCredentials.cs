using System.Text.RegularExpressions;
namespace GoldShopWeb.Services;

public static class MemberCredentials
{
    public static string NormalizePhone(string? value)
    {
        var phone = Regex.Replace(value ?? "", @"[\s\-()]", "");
        if (phone.StartsWith("+66")) phone = "0" + phone[3..];
        return Regex.IsMatch(phone, @"\A0[0-9]{8,9}\z") ? phone : "";
    }
    public static bool ValidPin(string? pin) => Regex.IsMatch(pin ?? "", @"\A[0-9]{6}\z");
}
