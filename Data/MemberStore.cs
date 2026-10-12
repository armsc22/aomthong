using System.Globalization;
using System.Reflection;
using GoldShopWeb.Models;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Identity;
using GoldShopWeb.Services;

namespace GoldShopWeb.Data;

/// <summary>Durable member accounts and ledger. Each write commits atomically.</summary>
public partial class MemberStore
{
    private static readonly PasswordHasher<string> PinHasher = new();
    private static readonly string DummyHash = PinHasher.HashPassword("", "not-a-real-pin");
    private readonly string _connectionString;

    public MemberStore(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var path = Path.GetFullPath(configuration["Database:Path"] ?? "App_Data/goldshop.db", environment.ContentRootPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path, ForeignKeys = true, DefaultTimeout = 30
        }.ToString();
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        var version = Scalar(connection, transaction, "PRAGMA user_version");
        if (version > 3) throw new InvalidOperationException("Database schema is newer than this application supports.");
        if (version == 0)
        {
            Execute(connection, transaction, """
                CREATE TABLE Customers (
                    Id INTEGER PRIMARY KEY, MemberCode TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    CitizenId TEXT NOT NULL UNIQUE, FirstName TEXT NOT NULL, LastName TEXT NOT NULL,
                    Phone TEXT NOT NULL, Email TEXT NOT NULL, Address TEXT NOT NULL, LineUserId TEXT NOT NULL);
                CREATE TABLE Accounts (
                    Id INTEGER PRIMARY KEY, AccountNumber TEXT NOT NULL UNIQUE,
                    CustomerId INTEGER NOT NULL UNIQUE REFERENCES Customers(Id),
                    DepositTypeCode TEXT NOT NULL, DepositTypeName TEXT NOT NULL,
                    InterestRate TEXT NOT NULL, Branch TEXT NOT NULL, IsActive INTEGER NOT NULL CHECK(IsActive IN (0,1)));
                CREATE TABLE Ledger (
                    AccountId INTEGER NOT NULL REFERENCES Accounts(Id), Seq INTEGER NOT NULL,
                    Date TEXT NOT NULL, Deposit INTEGER NOT NULL CHECK(Deposit >= 0),
                    Withdrawal INTEGER NOT NULL CHECK(Withdrawal >= 0), Interest INTEGER NOT NULL CHECK(Interest >= 0),
                    BalanceAfter INTEGER NOT NULL CHECK(BalanceAfter >= 0), Status INTEGER NOT NULL,
                    Printed INTEGER NOT NULL CHECK(Printed IN (0,1)), PRIMARY KEY(AccountId, Seq));
                PRAGMA user_version = 1;
                """);
            if (configuration.GetValue("Database:SeedDemoData", environment.IsDevelopment()))
            {
                var demo = new DemoMembers();
                foreach (var customer in demo._customers) Insert(connection, transaction, "Customers", customer);
                foreach (var account in demo._accounts) Insert(connection, transaction, "Accounts", account);
                foreach (var entry in demo._ledger) Insert(connection, transaction, "Ledger", entry);
            }
        }
        if (version < 2)
        {
            Execute(connection, transaction, """
                CREATE TABLE MemberCredentials (
                    CustomerId INTEGER PRIMARY KEY REFERENCES Customers(Id),
                    Phone TEXT NOT NULL UNIQUE, PinHash TEXT NOT NULL,
                    FailedAttempts INTEGER NOT NULL DEFAULT 0, LockedUntil INTEGER NOT NULL DEFAULT 0,
                    Version INTEGER NOT NULL DEFAULT 1);
                PRAGMA user_version = 2;
                """);
        }
        if (version < 3)
        {
            Execute(connection, transaction, """
                CREATE TABLE VerifiedLineBindings(CustomerId INTEGER PRIMARY KEY REFERENCES Customers(Id), UserId TEXT NOT NULL UNIQUE);
                PRAGMA user_version = 3;
                """);
        }
        InitializeSavingsRequests(connection, transaction);
        Execute(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS Staff(Id INTEGER PRIMARY KEY,Username TEXT NOT NULL UNIQUE COLLATE NOCASE,Hash TEXT NOT NULL,Role TEXT NOT NULL,Active INTEGER NOT NULL,Version INTEGER NOT NULL,Failures INTEGER NOT NULL,LockedUntil INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS ActivityLog(Id INTEGER PRIMARY KEY,OccurredAt TEXT NOT NULL,Actor TEXT NOT NULL,Action TEXT NOT NULL,Target TEXT NOT NULL,Outcome TEXT NOT NULL,TraceId TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ActivityLogTime ON ActivityLog(OccurredAt);
            """);
        Execute(connection,transaction,"""
            CREATE TABLE IF NOT EXISTS MemberBankDetails(CustomerId INTEGER PRIMARY KEY REFERENCES Customers(Id), BankName TEXT NOT NULL, BankAccountNumber TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS MemberDocuments(CustomerId INTEGER NOT NULL REFERENCES Customers(Id), Kind TEXT NOT NULL, Image BLOB NOT NULL, Mime TEXT NOT NULL, PRIMARY KEY(CustomerId,Kind));
            """);
        transaction.Commit();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    public bool IsConnected
    {
        get
        {
            try { using var connection = Open(); return Scalar(connection, null, "SELECT 1") == 1; }
            catch (SqliteException) { return false; }
        }
    }

    public IReadOnlyList<Customer> Search(string? query)
    {
        using var connection = Open();
        return Read<Customer>(connection, null, """
            SELECT c.* FROM Customers c WHERE
            instr(lower(c.MemberCode),lower($q)) > 0 OR instr(c.CitizenId,$q) > 0 OR
            instr(lower(c.FirstName || ' ' || c.LastName),lower($q)) > 0 OR
            EXISTS(SELECT 1 FROM Accounts a WHERE a.CustomerId=c.Id AND instr(a.AccountNumber,$q)>0)
            ORDER BY c.Id
            """, ("$q", (query ?? "").Trim()));
    }

    public Customer? FindCustomer(int customerId)
    {
        using var connection = Open();
        return Read<Customer>(connection, null, "SELECT * FROM Customers WHERE Id=$id", ("$id", customerId)).FirstOrDefault();
    }

    public IReadOnlyList<CustomerBalance> GetCustomerBalances()
    {
        using var connection = Open();
        using var command = Command(connection, null, """
            SELECT c.MemberCode, COALESCE(a.AccountNumber, ''), c.FirstName, c.LastName,
                COALESCE((SELECT l.BalanceAfter FROM Ledger l WHERE l.AccountId=a.Id ORDER BY l.Seq DESC LIMIT 1), 0), c.Id
            FROM Customers c LEFT JOIN Accounts a ON a.CustomerId=c.Id ORDER BY c.Id
            """);
        using var reader = command.ExecuteReader();
        var rows = new List<CustomerBalance>();
        while (reader.Read())
        {
            rows.Add(new CustomerBalance
            {
                MemberCode = reader.GetString(0), ContractNumber = reader.GetString(1),
                FullName = $"{reader.GetString(2)} {reader.GetString(3)}".Trim(),
                DepositBalance = reader.GetInt64(4) / 100m, CustomerId = reader.GetInt32(5)
            });
        }
        return rows;
    }

    public bool UpdateCustomer(int customerId, string firstName, string lastName, string? phone, string? email, string? address, string? lineUserId)
    {
        using var connection = Open();
        return Execute(connection, null, """
            UPDATE Customers SET FirstName=$first, LastName=$last, Phone=$phone, Email=$email,
            Address=$address, LineUserId=$line WHERE Id=$id
            """, ("$first", firstName.Trim()), ("$last", lastName.Trim()), ("$phone", (phone ?? "").Trim()),
            ("$email", (email ?? "").Trim()), ("$address", (address ?? "").Trim()),
            ("$line", (lineUserId ?? "").Trim()), ("$id", customerId)) == 1;
    }

    public Customer? RegisterCustomer(string firstName, string lastName, string citizenId, string? phone, string? email, string? address, string? lineUserId, decimal interestRate, string pin, RegistrationDocuments? documents = null)
    {
        phone = MemberCredentials.NormalizePhone(phone);
        if (phone.Length == 0 || !MemberCredentials.ValidPin(pin)) return null;
        var pinHash = PinHasher.HashPassword(phone, pin);
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        if (Scalar(connection, transaction, "SELECT COUNT(*) FROM Customers WHERE CitizenId=$id", ("$id", citizenId.Trim())) > 0) return null;
        if (PhoneInUse(connection, transaction, phone, 0)) return null;
        if (Scalar(connection, transaction, "SELECT COUNT(*) FROM VerifiedLineBindings WHERE UserId=$user", ("$user", lineUserId ?? "")) > 0) return null;
        var nextId = checked((int)Scalar(connection, transaction, "SELECT COALESCE(MAX(Id),0)+1 FROM Customers"));
        // Bangkok date; retain sequences across years because the code has no year component.
        var memberPrefix = "M" + DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("MMdd", CultureInfo.InvariantCulture);
        var memberSequence = Scalar(connection, transaction, """
            SELECT COALESCE(MAX(CAST(substr(MemberCode,6) AS INTEGER)),0)+1 FROM Customers
            WHERE substr(MemberCode,1,5)=$prefix AND length(MemberCode)>=9
                AND substr(MemberCode,6) NOT GLOB '*[^0-9]*'
            """, ("$prefix", memberPrefix));
        var customer = new Customer
        {
            Id = nextId, MemberCode = memberPrefix + memberSequence.ToString("D4", CultureInfo.InvariantCulture), FirstName = firstName.Trim(), LastName = lastName.Trim(),
            CitizenId = citizenId.Trim(), Phone = (phone ?? "").Trim(), Email = (email ?? "").Trim(),
            Address = (address ?? "").Trim(), LineUserId = (lineUserId ?? "").Trim()
        };
        Insert(connection, transaction, "Customers", customer);
        Insert(connection, transaction, "Accounts", new DepositAccount
        {
            Id = checked((int)Scalar(connection, transaction, "SELECT COALESCE(MAX(Id),0)+1 FROM Accounts")),
            AccountNumber = Scalar(connection, transaction, "SELECT COALESCE(MAX(CAST(AccountNumber AS INTEGER)),1021000000)+1 FROM Accounts").ToString(CultureInfo.InvariantCulture),
            CustomerId = nextId, DepositTypeCode = "102", DepositTypeName = "ออมทรัพย์/เผื่อเรียก พิเศษ", InterestRate = interestRate
        });
        Execute(connection, transaction, "INSERT INTO MemberCredentials(CustomerId,Phone,PinHash) VALUES($id,$phone,$hash)",
            ("$id", nextId), ("$phone", phone!), ("$hash", pinHash));
        Execute(connection, transaction, "INSERT INTO VerifiedLineBindings(CustomerId,UserId) VALUES($id,$user)", ("$id", nextId), ("$user", lineUserId ?? ""));
        if(documents is not null)
        {
            Execute(connection,transaction,"INSERT INTO MemberBankDetails VALUES($id,$bank,$number)",("$id",nextId),("$bank",documents.BankName.Trim()),("$number",documents.BankAccountNumber));
            foreach(var (kind,file) in new[]{("citizen-card",documents.CitizenCard),("portrait",documents.Portrait),("bank-book",documents.BankBook)})
                Execute(connection,transaction,"INSERT INTO MemberDocuments VALUES($id,$kind,$image,$mime)",("$id",nextId),("$kind",kind),("$image",file.Bytes),("$mime",file.Mime));
        }
        transaction.Commit();
        return customer;
    }

    private static bool PhoneInUse(SqliteConnection connection, SqliteTransaction transaction, string phone, int exceptId) =>
        Read<Customer>(connection, transaction, "SELECT * FROM Customers WHERE Id<>$id", ("$id", exceptId))
            .Any(c => MemberCredentials.NormalizePhone(c.Phone) == phone)
        || Scalar(connection, transaction, "SELECT COUNT(*) FROM MemberCredentials WHERE Phone=$phone AND CustomerId<>$id", ("$phone", phone), ("$id", exceptId)) > 0;

    public bool SetMemberPin(int customerId, string phone, string pin)
    {
        phone = MemberCredentials.NormalizePhone(phone);
        if (phone.Length == 0 || !MemberCredentials.ValidPin(pin)) return false;
        var hash = PinHasher.HashPassword(phone, pin);
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        if (Scalar(connection, transaction, "SELECT COUNT(*) FROM Customers WHERE Id=$id", ("$id", customerId)) != 1 || PhoneInUse(connection, transaction, phone, customerId)) return false;
        Execute(connection, transaction, """
            INSERT INTO MemberCredentials(CustomerId,Phone,PinHash) VALUES($id,$phone,$hash)
            ON CONFLICT(CustomerId) DO UPDATE SET Phone=excluded.Phone,PinHash=excluded.PinHash,
                FailedAttempts=0,LockedUntil=0,Version=MemberCredentials.Version+1
            """, ("$id", customerId), ("$phone", phone), ("$hash", hash));
        Execute(connection, transaction, "UPDATE Customers SET Phone=$phone WHERE Id=$id", ("$phone", phone), ("$id", customerId));
        transaction.Commit();
        return true;
    }

    public (int CustomerId, int Version)? AuthenticatePin(string phone, string pin)
    {
        phone = MemberCredentials.NormalizePhone(phone);
        if (phone.Length == 0 || !MemberCredentials.ValidPin(pin)) return null;
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = Command(connection, transaction, "SELECT CustomerId,PinHash,FailedAttempts,LockedUntil,Version FROM MemberCredentials WHERE Phone=$phone", ("$phone", phone));
        int id, failed, version; long locked; string hash;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read()) { PinHasher.VerifyHashedPassword(phone, DummyHash, pin); return null; }
            id = reader.GetInt32(0); hash = reader.GetString(1); failed = reader.GetInt32(2); locked = reader.GetInt64(3); version = reader.GetInt32(4);
        }
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (locked > now) return null;
        if (locked != 0) failed = 0;
        var result = PinHasher.VerifyHashedPassword(phone, hash, pin);
        if (result == PasswordVerificationResult.Failed)
        {
            failed++;
            Execute(connection, transaction, "UPDATE MemberCredentials SET FailedAttempts=$failed,LockedUntil=$locked WHERE CustomerId=$id",
                ("$failed", failed), ("$locked", failed >= 5 ? now + 900 : 0L), ("$id", id));
            transaction.Commit(); return null;
        }
        Execute(connection, transaction, "UPDATE MemberCredentials SET FailedAttempts=0,LockedUntil=0,PinHash=$hash WHERE CustomerId=$id",
            ("$hash", result == PasswordVerificationResult.SuccessRehashNeeded ? PinHasher.HashPassword(phone, pin) : hash), ("$id", id));
        transaction.Commit(); return (id, version);
    }

    public int CredentialVersion(int customerId)
    {
        using var connection = Open();
        return (int)Scalar(connection, null, "SELECT COALESCE((SELECT Version FROM MemberCredentials WHERE CustomerId=$id),0)", ("$id", customerId));
    }

    public string VerifiedLineUserId(int customerId)
    {
        using var connection = Open();
        using var command = Command(connection, null, "SELECT UserId FROM VerifiedLineBindings WHERE CustomerId=$id", ("$id", customerId));
        return command.ExecuteScalar() as string ?? "";
    }

    public bool BindVerifiedLine(int customerId, string userId)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        if (Scalar(connection, transaction, "SELECT COUNT(*) FROM VerifiedLineBindings WHERE UserId=$user AND CustomerId<>$id", ("$user", userId), ("$id", customerId)) > 0) return false;
        Execute(connection, transaction, "INSERT INTO VerifiedLineBindings(CustomerId,UserId) VALUES($id,$user) ON CONFLICT(CustomerId) DO UPDATE SET UserId=excluded.UserId", ("$id", customerId), ("$user", userId));
        Execute(connection, transaction, "UPDATE Customers SET LineUserId=$user WHERE Id=$id", ("$id", customerId), ("$user", userId));
        transaction.Commit(); return true;
    }

    public DepositAccount? FindAccountByNumber(string accountNumber)
    {
        using var connection = Open();
        return Read<DepositAccount>(connection, null, "SELECT * FROM Accounts WHERE AccountNumber=$number", ("$number", accountNumber)).FirstOrDefault();
    }

    public DepositAccount? FindAccountFor(int customerId)
    {
        using var connection = Open();
        return Read<DepositAccount>(connection, null, "SELECT * FROM Accounts WHERE CustomerId=$id", ("$id", customerId)).FirstOrDefault();
    }

    public DepositSummary GetSummary(int customerId)
    {
        using var connection = Open();
        return BuildSummary(Read<LedgerEntry>(connection, null, """
            SELECT l.* FROM Ledger l JOIN Accounts a ON l.AccountId=a.Id WHERE a.CustomerId=$id ORDER BY l.Seq
            """, ("$id", customerId)));
    }

    public DepositSummary? Deposit(int customerId, decimal amount) => Book(customerId, amount, false);
    public DepositSummary? Withdraw(int customerId, decimal amount) => Book(customerId, amount, true);

    private DepositSummary? Book(int customerId, decimal amount, bool withdraw)
    {
        if (amount <= 0 || amount > 1_000_000_000m || decimal.Round(amount, 2) != amount) return null;
        using var connection = Open();
        // Acquire the database write lock before reading the balance, including across processes.
        using var transaction = connection.BeginTransaction(deferred: false);
        var account = Read<DepositAccount>(connection, transaction, "SELECT * FROM Accounts WHERE CustomerId=$id AND IsActive=1", ("$id", customerId)).FirstOrDefault();
        if (account is null) return null;
        var entries = Read<LedgerEntry>(connection, transaction, "SELECT * FROM Ledger WHERE AccountId=$id ORDER BY Seq", ("$id", account.Id));
        var balance = BuildSummary(entries).TotalBalance;
        if (withdraw && amount > balance) return null;
        var nextBalance = balance + (withdraw ? -amount : amount);
        if (nextBalance > long.MaxValue / 100m) return null;
        var entry = new LedgerEntry
        {
            AccountId = account.Id, Seq = entries.Count == 0 ? 1 : checked(entries[^1].Seq + 1), Date = DateTime.Today,
            Deposit = withdraw ? 0 : amount, Withdrawal = withdraw ? amount : 0,
            BalanceAfter = nextBalance, Status = withdraw ? 2 : 1
        };
        Insert(connection, transaction, "Ledger", entry);
        transaction.Commit();
        entries.Add(entry);
        return BuildSummary(entries);
    }

    private static DepositSummary BuildSummary(IReadOnlyList<LedgerEntry> entries) => new()
    {
        PrincipalBalance = entries.Sum(e => e.Deposit) - entries.Sum(e => e.Withdrawal),
        TotalInterest = entries.Sum(e => e.Interest), TotalDeposit = entries.Sum(e => e.Deposit),
        TotalWithdrawal = entries.Sum(e => e.Withdrawal), Entries = entries
    };

    private static SqliteCommand Command(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return command;
    }

    private static long Scalar(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int Execute(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = Command(connection, transaction, sql, parameters);
        return command.ExecuteNonQuery();
    }

    // Only internal model property names are used as SQL identifiers; all values are parameters.
    private static PropertyInfo[] Properties<T>() => typeof(T).GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray();

    private static void Insert<T>(SqliteConnection connection, SqliteTransaction transaction, string table, T model)
    {
        var properties = Properties<T>();
        var parameters = properties.Select(p =>
        {
            object value = p.GetValue(model)!;
            if (typeof(T) == typeof(LedgerEntry) && value is decimal money) value = checked((long)(money * 100m));
            return ("$" + p.Name, value);
        }).ToArray();
        Execute(connection, transaction, $"INSERT INTO {table} ({string.Join(',', properties.Select(p => p.Name))}) VALUES ({string.Join(',', parameters.Select(p => p.Item1))})", parameters);
    }

    private static List<T> Read<T>(SqliteConnection connection, SqliteTransaction? transaction, string sql, params (string Name, object Value)[] parameters) where T : new()
    {
        using var command = Command(connection, transaction, sql, parameters);
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        var properties = Properties<T>();
        while (reader.Read())
        {
            var item = new T();
            foreach (var property in properties)
            {
                var ordinal = reader.GetOrdinal(property.Name);
                object value = property.PropertyType == typeof(DateTime) ? reader.GetDateTime(ordinal)
                    : property.PropertyType == typeof(decimal) ? (typeof(T) == typeof(LedgerEntry) ? reader.GetInt64(ordinal) / 100m : reader.GetDecimal(ordinal))
                    : Convert.ChangeType(reader.GetValue(ordinal), property.PropertyType, CultureInfo.InvariantCulture);
                property.SetValue(item, value);
            }
            result.Add(item);
        }
        return result;
    }
}

