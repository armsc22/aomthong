using GoldShopWeb.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Security.Cryptography;

namespace GoldShopWeb.Data;
public partial class MemberStore
{
    private static void InitializeSavingsRequests(SqliteConnection db, SqliteTransaction tx)
    {
        Execute(db,tx,"""
        CREATE TABLE IF NOT EXISTS SavingsRequests(
        Id TEXT PRIMARY KEY, CustomerId INTEGER NOT NULL REFERENCES Customers(Id), AccountId INTEGER NOT NULL REFERENCES Accounts(Id),
        AccountNumber TEXT NOT NULL, MemberCode TEXT NOT NULL, CustomerName TEXT NOT NULL, Phone TEXT NOT NULL, Address TEXT NOT NULL,
        AmountMinor INTEGER NOT NULL CHECK(AmountMinor>0), Created TEXT NOT NULL, Status TEXT NOT NULL,
        ReceiptNumber TEXT NOT NULL, ReviewedAt TEXT NOT NULL, ReviewedBy TEXT NOT NULL, Note TEXT NOT NULL, BalanceMinor INTEGER NOT NULL);
        CREATE UNIQUE INDEX IF NOT EXISTS SavingsReceiptNumber ON SavingsRequests(ReceiptNumber) WHERE ReceiptNumber<>'';
        CREATE TABLE IF NOT EXISTS SavingsSlips(Id TEXT PRIMARY KEY REFERENCES SavingsRequests(Id), Image BLOB NOT NULL, Mime TEXT NOT NULL, Hash TEXT NOT NULL UNIQUE);
        CREATE TABLE IF NOT EXISTS SavingsMessages(Id TEXT NOT NULL REFERENCES SavingsRequests(Id), Stage TEXT NOT NULL, State TEXT NOT NULL, PRIMARY KEY(Id,Stage));
        """);
        using var cmd=Command(db,tx,"SELECT COUNT(*) FROM pragma_table_info('SavingsRequests') WHERE name='TransactionNumber'");
        if(Convert.ToInt64(cmd.ExecuteScalar())==0) Execute(db,tx,"ALTER TABLE SavingsRequests ADD COLUMN TransactionNumber TEXT NOT NULL DEFAULT ''");
    }

    public SavingsRequest? SubmitSavings(string id,int customerId,decimal amount,byte[] image,string mime)
    {
        if(!Guid.TryParseExact(id,"N",out _) || amount<=0 || amount>1_000_000_000m || decimal.Round(amount,2)!=amount || image.Length==0 || image.Length>5*1024*1024) return null;
        if(Services.PaymentSlipStore.DetectImage(image)!=mime) return null;
        using var db=Open(); using var tx=db.BeginTransaction(deferred:false);
        var existing=Read<SavingsRequest>(db,tx,"SELECT * FROM SavingsRequests WHERE Id=$id",("$id",id)).FirstOrDefault();
        if(existing is not null) return existing.CustomerId==customerId ? existing : null;
        var hash=Convert.ToHexString(SHA256.HashData(image));
        if(Scalar(db,tx,"SELECT COUNT(*) FROM SavingsSlips WHERE Hash=$hash",("$hash",hash))>0) return null;
        var customer=Read<Customer>(db,tx,"SELECT * FROM Customers WHERE Id=$id",("$id",customerId)).FirstOrDefault();
        var account=Read<DepositAccount>(db,tx,"SELECT * FROM Accounts WHERE CustomerId=$id AND IsActive=1",("$id",customerId)).FirstOrDefault();
        if(customer is null || account is null) return null;
        var request=new SavingsRequest {Id=id,CustomerId=customerId,AccountId=account.Id,AccountNumber=account.AccountNumber,MemberCode=customer.MemberCode,CustomerName=customer.FullName,Phone=customer.Phone,Address=customer.Address,AmountMinor=checked((long)(amount*100)),Created=DateTimeOffset.UtcNow.ToString("O")};
        request.TransactionNumber=Services.TransactionNumbers.Next(new SqliteConnectionStringBuilder(_connectionString).DataSource);
        Insert(db,tx,"SavingsRequests",request);
        Execute(db,tx,"INSERT INTO SavingsSlips VALUES($id,$image,$mime,$hash)",("$id",id),("$image",image),("$mime",mime),("$hash",hash));
        tx.Commit(); return request;
    }
    public IReadOnlyList<SavingsRequest> SavingsRequests(int? customerId=null)
    {
        using var db=Open();
        return Read<SavingsRequest>(db,null,"SELECT * FROM SavingsRequests WHERE $customer=0 OR CustomerId=$customer ORDER BY Created DESC LIMIT 200",("$customer",customerId??0));
    }
    public SavingsRequest? SavingsRequest(string id)
    {
        using var db=Open(); return Read<SavingsRequest>(db,null,"SELECT * FROM SavingsRequests WHERE Id=$id",("$id",id)).FirstOrDefault();
    }
    public (byte[] Image,string Mime)? SavingsSlip(string id)
    {
        using var db=Open(); using var cmd=Command(db,null,"SELECT Image,Mime FROM SavingsSlips WHERE Id=$id",("$id",id));
        using var reader=cmd.ExecuteReader(); return reader.Read() ? ((byte[])reader[0],reader.GetString(1)) : null;
    }
    public bool ReviewSavings(string id,bool approve,string reviewer,string note)
    {
        if(string.IsNullOrWhiteSpace(reviewer)||note.Length>500||(!approve&&string.IsNullOrWhiteSpace(note))) return false;
        using var db=Open(); using var tx=db.BeginTransaction(deferred:false);
        var request=Read<SavingsRequest>(db,tx,"SELECT * FROM SavingsRequests WHERE Id=$id AND Status='Pending'",("$id",id)).FirstOrDefault();
        if(request is null) return false;
        var now=DateTimeOffset.UtcNow;
        var receipt=""; long balance=0;
        if(approve)
        {
            if(Scalar(db,tx,"SELECT COUNT(*) FROM Accounts WHERE Id=$id AND CustomerId=$customer AND IsActive=1",("$id",request.AccountId),("$customer",request.CustomerId))!=1) return false;
            var entries=Read<LedgerEntry>(db,tx,"SELECT * FROM Ledger WHERE AccountId=$id ORDER BY Seq",("$id",request.AccountId));
            balance=checked((long)(BuildSummary(entries).TotalBalance*100)+request.AmountMinor);
            Insert(db,tx,"Ledger",new LedgerEntry {AccountId=request.AccountId,Seq=entries.Count==0?1:checked(entries[^1].Seq+1),Date=now.ToOffset(TimeSpan.FromHours(7)).Date,Deposit=request.Amount,BalanceAfter=balance/100m,Status=1});
            var prefix="D"+now.ToOffset(TimeSpan.FromHours(7)).ToString("MMdd",CultureInfo.InvariantCulture);
            var sequence=Scalar(db,tx,"SELECT COALESCE(MAX(CAST(substr(ReceiptNumber,6) AS INTEGER)),0)+1 FROM SavingsRequests WHERE substr(ReceiptNumber,1,5)=$prefix",("$prefix",prefix));
            receipt=prefix+sequence.ToString("D4",CultureInfo.InvariantCulture);
        }
        Execute(db,tx,"UPDATE SavingsRequests SET Status=$status,ReceiptNumber=$receipt,ReviewedAt=$at,ReviewedBy=$by,Note=$note,BalanceMinor=$balance WHERE Id=$id",
            ("$status",approve?"Approved":"Rejected"),("$receipt",receipt),("$at",now.ToString("O")),("$by",reviewer),("$note",note.Trim()),("$balance",balance),("$id",id));
        tx.Commit(); return true;
    }
    public bool ClaimSavingsMessage(string id,string stage)
    {
        using var db=Open(); using var tx=db.BeginTransaction(deferred:false);
        if(Scalar(db,tx,"SELECT COUNT(*) FROM SavingsRequests WHERE Id=$id AND Status=$stage",("$id",id),("$stage",stage))!=1) return false;
        var inserted=Execute(db,tx,"INSERT OR IGNORE INTO SavingsMessages VALUES($id,$stage,'Sending')",("$id",id),("$stage",stage))==1;
        tx.Commit(); return inserted;
    }
    public void CompleteSavingsMessage(string id,string stage,bool sent)
    {
        using var db=Open(); Execute(db,null,sent?"UPDATE SavingsMessages SET State='Sent' WHERE Id=$id AND Stage=$stage AND State='Sending'":"DELETE FROM SavingsMessages WHERE Id=$id AND Stage=$stage AND State='Sending'",("$id",id),("$stage",stage));
    }
}
