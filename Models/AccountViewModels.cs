namespace GoldShopWeb.Models;

public class LoginViewModel
{
    public string? ReturnUrl { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Pin { get; set; } = string.Empty;
    public string? Error { get; set; }
}
public class DashboardViewModel
{
    public Customer Customer { get; set; } = new();
    public DepositAccount? Account { get; set; }
    public DepositSummary Summary { get; set; } = new();
    public decimal AnnualDividendPercent { get; set; }
    public decimal DailyDividend { get; set; }
    public Cart Cart { get; set; } = new();
    public IReadOnlyList<LedgerEntry> RecentEntries { get; set; } = [];
}

public class ProfileViewModel
{
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string MemberCode { get; set; } = string.Empty;
    [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
    public string CitizenId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? Address { get; set; } = string.Empty;
    public string? LineUserId { get; set; } = string.Empty;
    public bool Saved { get; set; }
}

public class RegisterViewModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage="กรุณาระบุชื่อธนาคาร")]
    [System.ComponentModel.DataAnnotations.StringLength(100)]
    public string BankName { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage="กรุณาระบุหมายเลขบัญชีธนาคาร")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^[0-9]{6,20}$",ErrorMessage="เลขบัญชีต้องเป็นตัวเลข 6–20 หลัก ไม่ใส่ขีดหรือเว้นวรรค")]
    public string BankAccountNumber { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage="กรุณาแนบรูปบัตรประชาชนด้านหน้า")]
    public IFormFile? CitizenCardImage { get; set; }
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage="กรุณาแนบรูปถ่ายหน้าตรง")]
    public IFormFile? PortraitImage { get; set; }
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage="กรุณาแนบรูปหน้าสมุดบัญชี")]
    public IFormFile? BankBookImage { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string CitizenId { get; set; } = string.Empty;
    public string? Phone { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string? Address { get; set; } = string.Empty;

    /// <summary>LINE user id must match the identity verified by the server.</summary>
    public string LineUserId { get; set; } = string.Empty;
    /// <summary>Server-issued nonce that ties this registration to a session-bound LINE user id.</summary>
    public string LinkNonce { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"\A[0-9]{6}\z", ErrorMessage = "PIN ต้องเป็นตัวเลข 6 หลัก")]
    public string Pin { get; set; } = string.Empty;
    [System.ComponentModel.DataAnnotations.Compare(nameof(Pin), ErrorMessage = "PIN ไม่ตรงกัน")]
    public string ConfirmPin { get; set; } = string.Empty;
    public string? LineDisplayName { get; set; }
}



