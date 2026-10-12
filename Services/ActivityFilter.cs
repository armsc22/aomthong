using GoldShopWeb.Data;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
namespace GoldShopWeb.Services;
public class ActivityFilter(MemberStore store,AccountService account,ILogger<ActivityFilter> logger) : IAsyncActionFilter
{
    private string Actor(HttpContext context) => context.User.Identity?.IsAuthenticated==true ? "staff:"+context.User.Identity.Name : account.CurrentCustomerId() is int id ? "customer:"+id : "anonymous";
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var http=context.HttpContext;
        var actor=Actor(http);
        var action=string.Join("/",new[]{context.RouteData.Values["area"],context.RouteData.Values["controller"],context.RouteData.Values["action"]}.Where(v=>v is not null));
        // Only allow-listed internal identifiers. Never capture forms, URLs, headers or exception messages.
        var target=string.Join(" ",context.ActionArguments.Where(x=>x.Key is "id" or "customerId").Select(x=>x.Key+"="+x.Value));
        var outcome="Completed";
        try
        {
            if(http.User.IsInRole("Viewer") && !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method) && context.RouteData.Values["action"]?.ToString()!="Logout")
            {outcome="Forbidden:ReadOnlyStaff";context.Result=new StatusCodeResult(403);return;}
            var result=await next();
            if(actor=="anonymous")actor=Actor(http);
            if(result.Exception is not null)outcome="Error:"+result.Exception.GetType().Name;
            else if(http.Items["AuditOutcome"] is string explicitOutcome)outcome=explicitOutcome;
            else if(result.Result is StatusCodeResult code)outcome="HTTP "+code.StatusCode;
            else if(result.Result is ObjectResult obj && obj.StatusCode>=400)outcome="HTTP "+obj.StatusCode;
            else if(!context.ModelState.IsValid)outcome="ValidationFailed";
        }
        catch(Exception ex){outcome="Error:"+ex.GetType().Name;throw;}
        finally
        {
            try{store.RecordActivity(actor,action,target.Length>200?target[..200]:target,outcome,http.TraceIdentifier);}
            catch(Exception ex){logger.LogError("Activity log write failed: {Type}; trace={Trace}",ex.GetType().Name,http.TraceIdentifier);}
        }
    }
}
