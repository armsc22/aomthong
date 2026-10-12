using System.ComponentModel.DataAnnotations;
namespace GoldShopWeb.Areas.Admin.Models;

public class MemberPinViewModel
{
    public int CustomerId { get; set; }
    [Required] public string Phone { get; set; } = "";
    [Required, RegularExpression(@"\A[0-9]{6}\z", ErrorMessage = "PIN ต้องเป็นตัวเลข 6 หลัก")]
    public string Pin { get; set; } = "";
    [Compare(nameof(Pin), ErrorMessage = "PIN ไม่ตรงกัน")]
    public string ConfirmPin { get; set; } = "";
    public bool IdentityVerified { get; set; }
}
