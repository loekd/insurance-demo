using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using XpiritInsurance.Claims.Client;

//uncomment to debug this method.
//#if DEBUG
//await Task.Delay(5000);
//#endif

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped<CustomAuthorizationMessageHandler>();
//builder.Services.AddHttpClient("XpiritInsurance.Claims.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
builder.Services.AddHttpClient("XpiritInsurance.Claims.ServerAPI", client => client.BaseAddress = new Uri("https://localhost:7008/"))
    .AddHttpMessageHandler<CustomAuthorizationMessageHandler>();

// Supply HttpClient instances that include access tokens when making requests to the server project
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("XpiritInsurance.Claims.ServerAPI"));

builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAdB2C", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://xpiritinsurance.onmicrosoft.com/3b551417-548e-4e8e-80c3-44bb06f3aa64/API.Access");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("openid");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("offline_access");
    options.ProviderOptions.LoginMode = "redirect";
});

await builder.Build().RunAsync();
