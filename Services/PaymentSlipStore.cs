using System.Text.Json;
using GoldShopWeb.Models;
using Microsoft.Data.Sqlite;

namespace GoldShopWeb.Services;

public sealed class PaymentSlipStore
{
    private readonly string connectionString;
    private readonly string memberDatabasePath;
    public PaymentSlipStore(IConfiguration config, IWebHostEnvironment env)
    {
        var memberDb = Path.GetFullPath(config["Database:Path"] ?? "App_Data/goldshop.db", env.ContentRootPath);
        memberDatabasePath=memberDb;
        var path = Path.Combine(Path.GetDirectoryName(memberDb)!, "payment-slips.db");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS Slips(Id TEXT PRIMARY KEY, CustomerId INTEGER NOT NULL, Snapshot TEXT NOT NULL, Mime TEXT NOT NULL, Image BLOB NOT NULL, Created TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SlipReviews(
                SlipId TEXT PRIMARY KEY REFERENCES Slips(Id), Status TEXT NOT NULL,
                ReceiptNumber TEXT UNIQUE, ReviewedBy TEXT NOT NULL, ReviewedAt TEXT NOT NULL, Note TEXT NOT NULL);
            """;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS SlipNotifications(SlipId TEXT PRIMARY KEY, State TEXT NOT NULL)";
        cmd.ExecuteNonQuery();
    }
    public CashSale? ClaimPendingNotification(string id, int customerId)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT Snapshot FROM Slips WHERE Id=$id AND CustomerId=$customer AND NOT EXISTS(SELECT 1 FROM SlipReviews WHERE SlipId=$id)";
        cmd.Parameters.AddWithValue("$id",id); cmd.Parameters.AddWithValue("$customer",customerId);
        if (cmd.ExecuteScalar() is not string snapshot) return null;
        cmd.CommandText = "INSERT OR IGNORE INTO SlipNotifications VALUES($id,'Sending')";
        if (cmd.ExecuteNonQuery() != 1) return null;
        using var json = JsonDocument.Parse(snapshot); var data = json.RootElement;
        var order = new CashSale { DocumentNumber=SlipReference.Number(snapshot,id), CustomerId=customerId, MemberCode=data.GetProperty("MemberCode").GetString() ?? "", CustomerName=data.GetProperty("FullName").GetString() ?? "" };
        foreach(var item in data.GetProperty("Items").EnumerateArray())
        {
            var productId = item.GetProperty("ProductId").GetInt32();
            order.Lines.Add(new CashSaleLine { ProductId=productId, Sku=item.TryGetProperty("Sku", out var sku) ? sku.GetString() ?? "" : GoldShopWeb.Data.ShopCatalog.FindById(productId)?.Sku ?? productId.ToString(), Name=item.GetProperty("Name").GetString() ?? "", Quantity=item.GetProperty("Quantity").GetInt32(), UnitPrice=item.GetProperty("Price").GetDecimal() });
        }
        tx.Commit(); return order;
    }
    public void CompletePendingNotification(string id, bool sent)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = sent ? "UPDATE SlipNotifications SET State='Sent' WHERE SlipId=$id AND State='Sending'" : "DELETE FROM SlipNotifications WHERE SlipId=$id AND State='Sending'";
        cmd.Parameters.AddWithValue("$id",id); cmd.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var db = new SqliteConnection(connectionString); db.Open(); return db; }
    public static string? DetectImage(byte[] data)
    {
        if (data.Length >= 8 && data.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return "image/png";
        if (data.Length >= 4 && data[0] == 255 && data[1] == 216 && data[2] == 255 && data[^2] == 255 && data[^1] == 217) return "image/jpeg";
        return null;
    }
    public void Save(string id, CheckoutViewModel snapshot, string mime, byte[] image)
    {
        using var db = Open(); using var tx = db.BeginTransaction(deferred:false); using var cmd = db.CreateCommand(); cmd.Transaction=tx;
        cmd.CommandText="SELECT COUNT(*) FROM Slips WHERE Id=$existing";
        cmd.Parameters.AddWithValue("$existing",id);
        if(Convert.ToInt64(cmd.ExecuteScalar())>0) return;
        cmd.Parameters.Clear();
        cmd.CommandText = "INSERT OR IGNORE INTO Slips VALUES($id,$customer,$snapshot,$mime,$image,$created)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$customer", snapshot.Customer.Id);
        // Save only the customer details needed for payment review.
        cmd.Parameters.AddWithValue("$snapshot", JsonSerializer.Serialize(new { TransactionNumber=TransactionNumbers.Next(memberDatabasePath), snapshot.Customer.MemberCode, snapshot.Customer.FullName, snapshot.Cart.Items, snapshot.Shipping, snapshot.Total }));
        cmd.Parameters.AddWithValue("$mime", mime); cmd.Parameters.AddWithValue("$image", image);
        cmd.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
        tx.Commit();
    }
    public List<SlipSummary> List()
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT s.Id,s.Snapshot,s.Created,COALESCE(r.Status,'Pending'),r.ReceiptNumber,COALESCE(r.Note,'') FROM Slips s LEFT JOIN SlipReviews r ON r.SlipId=s.Id ORDER BY s.Created DESC LIMIT 200";
        using var reader = cmd.ExecuteReader(); var rows = new List<SlipSummary>();
        while (reader.Read()) rows.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5)));
        return rows;
    }
    public bool Review(string id, bool approve, string reviewer, string note)
    {
        if (string.IsNullOrWhiteSpace(reviewer) || note.Length > 500 || (!approve && string.IsNullOrWhiteSpace(note))) return false;
        using var db = Open(); using var tx = db.BeginTransaction(deferred: false);
        using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM Slips WHERE Id=$id AND NOT EXISTS(SELECT 1 FROM SlipReviews WHERE SlipId=$id)";
        cmd.Parameters.AddWithValue("$id",id);
        if (Convert.ToInt64(cmd.ExecuteScalar()) != 1) return false;
        string? receipt = null;
        if (approve)
        {
            var prefix = "C" + DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("MMdd", System.Globalization.CultureInfo.InvariantCulture);
            cmd.CommandText = "SELECT COALESCE(MAX(CAST(substr(ReceiptNumber,6) AS INTEGER)),0)+1 FROM SlipReviews WHERE substr(ReceiptNumber,1,5)=$prefix";
            cmd.Parameters.AddWithValue("$prefix",prefix);
            receipt = prefix + Convert.ToInt64(cmd.ExecuteScalar()).ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        }
        cmd.CommandText = "INSERT INTO SlipReviews VALUES($id,$status,$receipt,$by,$at,$note)";
        cmd.Parameters.AddWithValue("$status", approve ? "Approved" : "Rejected");
        cmd.Parameters.AddWithValue("$receipt", (object?)receipt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$by",reviewer); cmd.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$note",note.Trim()); cmd.ExecuteNonQuery(); tx.Commit(); return true;
    }
    public ApprovedSlip? Receipt(string id)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT s.CustomerId,s.Snapshot,r.ReceiptNumber,r.ReviewedAt FROM Slips s JOIN SlipReviews r ON r.SlipId=s.Id WHERE s.Id=$id AND r.Status='Approved'";
        cmd.Parameters.AddWithValue("$id",id); using var reader = cmd.ExecuteReader();
        return reader.Read() ? new(id, reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)) : null;
    }
    public (byte[] Image, string Mime)? Get(string id)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT Image,Mime FROM Slips WHERE Id=$id"; cmd.Parameters.AddWithValue("$id",id);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? ((byte[])reader[0], reader.GetString(1)) : null;
    }
}
public record SlipSummary(string Id, string Snapshot, string Created, string Status, string? ReceiptNumber, string Note)
{
    public string TransactionNumber => SlipReference.Number(Snapshot,Id);
}
public record ApprovedSlip(string Id, int CustomerId, string Snapshot, string ReceiptNumber, string ReviewedAt)
{
    public string TransactionNumber => SlipReference.Number(Snapshot,Id);
}
public static class SlipReference
{
    public static string Number(string snapshot,string fallback){using var json=JsonDocument.Parse(snapshot);return json.RootElement.TryGetProperty("TransactionNumber",out var number)?number.GetString()??fallback:fallback;}
}
