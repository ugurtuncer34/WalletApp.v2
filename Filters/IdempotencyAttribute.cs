using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace WalletApp.Filters;

public class IdempotencyAttribute : Attribute, IAsyncActionFilter
{
    private const string IdempotencyHeader = "X-Idempotency-Key";
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // 1- is idempotencyKey present in request header?
        if(!context.HttpContext.Request.Headers.TryGetValue(IdempotencyHeader, out var idempotencyKey))
        {
            // if not, decline request since it is mandatory
            context.Result = new BadRequestObjectResult($"Security breach: Missing header '{IdempotencyHeader}'");
            return;
        }

        // action filters do not take DI from constructor, thus this service locator is used
        var cache = context.HttpContext.RequestServices.GetRequiredService<IDistributedCache>();
        var cacheKey = $"Idempotency_{idempotencyKey}";

        // 2- this key used before?
        var cachedResult = await cache.GetStringAsync(cacheKey);

        if (!string.IsNullOrEmpty(cachedResult))
        {
            // this request has already come. do not proceed again, return old response as Json
            context.Result = new OkObjectResult(JsonSerializer.Deserialize<object>(cachedResult));
            return;
        }

        // 3- if new request, proceed to controller
        var executedContext = await next();

        // 4- after process finished, if success and no error, save response to cache
        if(executedContext.Exception == null && executedContext.Result is ObjectResult objectResult)
        {
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            };

            await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(objectResult.Value), cacheOptions);
        }

    }
}