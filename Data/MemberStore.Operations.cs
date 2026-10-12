using GoldShopWeb.Models;
using Microsoft.AspNetCore.Identity;

namespace GoldShopWeb.Data;
public record StaffInfo(int Id,string Username,string Role,bool Active,int Version);
public record ActivityEntry(string Time,string Actor,string Action,string Target,string Outcome,string TraceId);
public partial class MemberStore
{
    private static readonly PasswordHasher<string> StaffHasher=new();
    public void RecordActivity(string actor,string action,string target,string outcome,string trace="")
    {
        using var db=Open();
        Execute(db,null,"INSERT INTO ActivityLog(OccurredAt,Actor,Action,Target,Outcome,TraceId) VALUES($time,$actor,$action,$target,$outcome,$trace)",
            ("$time",DateTimeOffset.UtcNow.ToString("O")),("$actor",actor),("$action",action),("$target",target),("$outcome",outcome),("$trace",trace));
    }
    public IReadOnlyList<ActivityEntry> Activities(string? actor,string? action)
    {
        using var db=Open(); using var cmd=Command(db,null,"SELECT OccurredAt,Actor,Action,Target,Outcome,TraceId FROM ActivityLog WHERE ($actor='' OR instr(Actor,$actor)>0) AND ($action='' OR instr(Action,$action)>0) ORDER BY Id DESC LIMIT 500",("$actor",(actor??"")[..Math.Min(actor?.Length??0,100)]),("$action",(action??"")[..Math.Min(action?.Length??0,100)]));
        using var r=cmd.ExecuteReader();var rows=new List<ActivityEntry>();
        while(r.Read())rows.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5)));return rows;
    }
    public IReadOnlyList<StaffInfo> Staff()
    {
        using var db=Open();using var cmd=Command(db,null,"SELECT Id,Username,Role,Active,Version FROM Staff ORDER BY Id");using var r=cmd.ExecuteReader();var rows=new List<StaffInfo>();
        while(r.Read())rows.Add(new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetBoolean(3),r.GetInt32(4)));return rows;
    }
    public bool CreateStaff(string username,string password,string role)
    {
        username=username.Trim();
        if(!System.Text.RegularExpressions.Regex.IsMatch(username,@"^[a-zA-Z0-9._-]{3,40}$")||password.Length<12||password.Length>128||role is not ("Owner" or "Operator" or "Viewer"))return false;
        var hash=StaffHasher.HashPassword(username,password);
        using var db=Open();using var tx=db.BeginTransaction(deferred:false);
        if(Scalar(db,tx,"SELECT COUNT(*) FROM Staff WHERE Role='Owner' AND Active=1")==0 && role!="Owner")return false;
        var ok=Execute(db,tx,"INSERT OR IGNORE INTO Staff(Username,Hash,Role,Active,Version,Failures,LockedUntil) VALUES($name,$hash,$role,1,1,0,0)",("$name",username),("$hash",hash),("$role",role))==1;tx.Commit();return ok;
    }
    public bool SetStaffActive(int id,bool active)
    {
        using var db=Open();using var tx=db.BeginTransaction(deferred:false);
        if(!active && Scalar(db,tx,"SELECT COUNT(*) FROM Staff WHERE Id=$id AND Role='Owner' AND Active=1",("$id",id))==1 && Scalar(db,tx,"SELECT COUNT(*) FROM Staff WHERE Role='Owner' AND Active=1")<=1)return false;
        var ok=Execute(db,tx,"UPDATE Staff SET Active=$active,Version=Version+1 WHERE Id=$id",("$active",active?1:0),("$id",id))==1;tx.Commit();return ok;
    }
    public StaffInfo? AuthenticateStaff(string username,string password)
    {
        using var db=Open();using var tx=db.BeginTransaction(deferred:false);
        using var cmd=Command(db,tx,"SELECT Id,Username,Hash,Role,Active,Version,Failures,LockedUntil FROM Staff WHERE Username=$name",("$name",username.Trim()));
        int id,version,failures;string name,hash,role;bool active;long locked;
        using(var r=cmd.ExecuteReader()){
            if(!r.Read()){StaffHasher.VerifyHashedPassword("",DummyHash,password);return null;}
            id=r.GetInt32(0);name=r.GetString(1);hash=r.GetString(2);role=r.GetString(3);active=r.GetBoolean(4);version=r.GetInt32(5);failures=r.GetInt32(6);locked=r.GetInt64(7);
        }
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();if(!active||locked>now)return null;
        if(locked!=0)failures=0;
        if(StaffHasher.VerifyHashedPassword(name,hash,password)==PasswordVerificationResult.Failed){failures++;Execute(db,tx,"UPDATE Staff SET Failures=$failed,LockedUntil=$locked WHERE Id=$id",("$failed",failures),("$locked",failures>=5?now+900:0),("$id",id));tx.Commit();return null;}
        Execute(db,tx,"UPDATE Staff SET Failures=0,LockedUntil=0 WHERE Id=$id",("$id",id));tx.Commit();return new(id,name,role,true,version);
    }
}
