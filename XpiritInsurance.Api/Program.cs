using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.IdentityModel.Tokens.Jwt;
using System.Collections.Concurrent;

namespace XpiritInsurance.Api;

public class Program
{
    private const string CorsPolicyName = "CorsPolicy";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        var configurationManagers = new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>();

        //authentication config
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(options =>
                    {
                        builder.Configuration.Bind("AzureAdB2C", options);
                        options.TokenValidationParameters.NameClaimType = "name";
                        options.TokenValidationParameters.ValidateIssuerSigningKey = false;
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
