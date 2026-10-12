namespace GoldShopWeb.Models;
public record RegistrationImage(byte[] Bytes,string Mime);
public record RegistrationDocuments(string BankName,string BankAccountNumber,RegistrationImage CitizenCard,RegistrationImage Portrait,RegistrationImage BankBook);
public record MemberBankDetails(string BankName,string BankAccountNumber);
