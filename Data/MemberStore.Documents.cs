using GoldShopWeb.Models;
namespace GoldShopWeb.Data;
public partial class MemberStore
{
    public MemberBankDetails? BankDetails(int customerId)
    {
        using var db=Open(); using var cmd=Command(db,null,"SELECT BankName,BankAccountNumber FROM MemberBankDetails WHERE CustomerId=$id",("$id",customerId));
        using var reader=cmd.ExecuteReader(); return reader.Read()?new(reader.GetString(0),reader.GetString(1)):null;
    }
    public RegistrationImage? MemberDocument(int customerId,string kind)
    {
        if(kind is not ("citizen-card" or "portrait" or "bank-book")) return null;
        using var db=Open(); using var cmd=Command(db,null,"SELECT Image,Mime FROM MemberDocuments WHERE CustomerId=$id AND Kind=$kind",("$id",customerId),("$kind",kind));
        using var reader=cmd.ExecuteReader(); return reader.Read()?new((byte[])reader[0],reader.GetString(1)):null;
    }
}
