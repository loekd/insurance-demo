using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.IdentityModel.JsonWebTokens;

namespace XpiritInsurance.Api;

public class Program
{
    private const string CorsPolicyName = "CorsPolicy";

    public static void Main(string[] args)
    {
        if (args.Any(a=> a.Contains("dapr")))
        {
            //attach dapr sidecar to this process on the fly
            //it watches this process and will stop when it exits
            var thisProces = Process.GetCurrentProcess();

            var psi = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                Arguments = $"tool run daprlauncher --create-sidecar-process --monitored-process-id {thisProces.Id} --app-port 5142 --resources-path {Path.Combine(Environment.CurrentDirectory, "components")}",
            };
            var launcher = Process.Start(psi)!;
            if (launcher.HasExited) throw new InvalidOperationException("Failed to launch Dapr");
        }

        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var configurationManagers = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>();

        //authentication config
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(options =>
                    {
                        builder.Configuration.Bind("AzureAdB2C", options);
                        options.TokenValidationParameters.NameClaimType = "name";
                        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                        options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                        {
                            //used to support both user flows and custom policies
                            //use cached info from metadata endpoint to fetch key info for signature validation
                            var tfpClaim = ((JsonWebToken)securityToken).Claims.Single(c => c.Type == "tfp");
                            string stsDiscoveryEndpoint = $"https://{builder.Configuration["AzureAdB2C:TenantName"]}.b2clogin.com/{builder.Configuration["AzureAdB2C:Domain"]}/{tfpClaim.Value}/v2.0/.well-known/openid-configuration";
                            ConfigurationManager<OpenIdConnectConfiguration> configManager = configurationManagers.GetOrAdd(kid, key => new(stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever()));
                            var config = configManager.GetConfigurationAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();

                            return config.JsonWebKeySet.GetSigningKeys();
                        };
                    },
                    options => { builder.Configuration.Bind("AzureAdB2C", options); });

        //allow both front-ends
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: CorsPolicyName,
                              policy =>
                              {
                                  policy.WithOrigins("https://localhost:7150", "https://localhost:7235")
                                    .AllowAnyHeader()
                                    .AllowAnyMethod();
                              });
        });

        builder.Services.AddControllers()
            .AddDapr();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        //Add mock services
        builder.Services.AddSingleton<Services.QuoteService>();
        builder.Services.AddSingleton<Services.InsuranceService>();
        builder.Services.AddSingleton<Services.DamageClaimService>();

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseCors(CorsPolicyName);

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseCloudEvents();
        app.MapSubscribeHandler();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            IdentityModelEventSource.ShowPII = true;
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        app.Run();
    }
}
