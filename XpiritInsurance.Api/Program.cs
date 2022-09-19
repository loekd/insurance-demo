using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.IdentityModel.Tokens.Jwt;
using System.Collections.Concurrent;
using System.Diagnostics;
using System;

namespace XpiritInsurance.Api;

public class Program
{
    private const string CorsPolicyName = "CorsPolicy";

    public static void Main(string[] args)
    {
        if (args.Any(a=> a.Contains("dapr")))
        {
            var thisProces = Process.GetCurrentProcess();

            var psi = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                Arguments = $"tool run daprlauncher --create-sidecar-process --monitored-process-id {thisProces.Id} --monitored-process-port 5142",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var launcher = Process.Start(psi)!;
            launcher.OutputDataReceived += (s, e) => Console.WriteLine($"Launcher: {e.Data}", ConsoleColor.Blue);
            launcher.ErrorDataReceived += (s, e) => Console.WriteLine($"Launcher: {e.Data}", ConsoleColor.Red);
            launcher.BeginErrorReadLine();
            launcher.BeginOutputReadLine();
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
                        
                        options.TokenValidationParameters.IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) =>
                        {
                            //use cached info from metadata endpoint to fetch key info for signature validation
                            var tfpClaim = ((JwtSecurityToken)securityToken).Claims.Single(c => c.Type == "tfp");
                            string stsDiscoveryEndpoint = $"https://xpiritinsurance.b2clogin.com/xpiritinsurance.onmicrosoft.com/{tfpClaim.Value}/v2.0/.well-known/openid-configuration";

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

        builder.Services.AddControllers();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        //Add mock services
        builder.Services.AddSingleton<Services.QuoteAmountService>();
        builder.Services.AddSingleton<Services.InsuranceService>();
        builder.Services.AddSingleton<Services.DamageClaimService>();

        var app = builder.Build();

        app.UseHttpsRedirection();
        app.UseCors(CorsPolicyName);

        app.UseAuthentication();
        app.UseAuthorization();

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
