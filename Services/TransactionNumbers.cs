using Microsoft.Data.Sqlite;
using System.Globalization;
namespace GoldShopWeb.Services;
public static class TransactionNumbers
{
    public static string Next(string databasePath)
    {
        var path=Path.Combine(Path.GetDirectoryName(databasePath)!,"transaction-numbers.db");
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,DefaultTimeout=30}.ToString()); db.Open();
        using var tx=db.BeginTransaction(deferred:false); using var cmd=db.CreateCommand(); cmd.Transaction=tx;
        cmd.CommandText="CREATE TABLE IF NOT EXISTS DailyNumbers(Day TEXT PRIMARY KEY,NextValue INTEGER NOT NULL)";cmd.ExecuteNonQuery();
        var day=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy/MM/dd",CultureInfo.InvariantCulture);
        cmd.Parameters.AddWithValue("$day",day);
        cmd.CommandText="INSERT INTO DailyNumbers VALUES($day,1) ON CONFLICT(Day) DO UPDATE SET NextValue=NextValue+1 RETURNING NextValue-1";
        var number=Convert.ToInt64(cmd.ExecuteScalar()); tx.Commit();
        return "AT"+day+"SE"+number.ToString("D4",CultureInfo.InvariantCulture);
    }
}
