using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;

namespace XpiritInsurance.Api;

public class Program
{
    private const string CorsPolicyName = "CorsPolicy";
    public const string DefaultPrivilegesPolicyName = "DefaultPrivilegesPolicy";
    public const string ElevatedPrivilegesPolicyName = "ElevatedPrivilegesPolicy";

    public static void Main(string[] args)
    {
        if (args.Any(a => a.Contains("dapr")))
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

#if DEBUG
                        IdentityModelEventSource.ShowPII = true; //don't enable this on prod
                        options.RequireHttpsMetadata = false;
                        //for debugging, put breakpoints on callbacks if needed
                        options.Events = new JwtBearerEvents
                        {
                            OnAuthenticationFailed = ctx =>
                            {
                                return Task.CompletedTask;
                            },

                            OnTokenValidated = ctx =>
                            {
                                AddPrincipalClaimsFromToken(ctx);
                                return Task.CompletedTask;
                            },

                            OnForbidden = ctx =>
                            {
                                return Task.CompletedTask;
                            }
                        };
#endif
                    },
                    options => { builder.Configuration.Bind("AzureAdB2C", options); });

        //configure authorization policies
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(DefaultPrivilegesPolicyName, policy =>
            {
                policy.RequireAuthenticatedUser(); //authenticated users only
                policy.RequireClaim(ClaimConstants.Scp, "API.ReadWrite");
            })
            .AddPolicy(ElevatedPrivilegesPolicyName, policy =>
            {
                policy.RequireAuthenticatedUser(); //authenticated users only
                policy.RequireClaim(ClaimConstants.Scp, "IdentityVerified");
            });

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
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        app.Run();
    }

    private static void AddPrincipalClaimsFromToken(TokenValidatedContext ctx)
    {
        //check if user is identified for health insurance, by checking a custom claim named 'idVerified'
        //this is set to 'true' when a 'DigiD' identity is connected to the current user account.
        IEnumerable<Claim> claimSet = ((JsonWebToken)ctx.SecurityToken).Claims;
        bool idVerified = claimSet.SingleOrDefault(c => c.Type == "idVerified")?.Value == "true";
        if (idVerified)
        {
            var claim = new Claim(ClaimConstants.Scp, "IdentityVerified");
            ((ClaimsIdentity)ctx.Principal!.Identity!).AddClaim(claim);
        }

        //check if the user has a scope claim with value API.ReadWrite
        bool apiAccess = claimSet.SingleOrDefault(c => c.Type == ClaimConstants.Scp)?.Value == "API.ReadWrite";
        if (apiAccess)
        {
            var claim = new Claim(ClaimConstants.Scp, "API.ReadWrite");
            ((ClaimsIdentity)ctx.Principal!.Identity!).AddClaim(claim);
        }
    }
}
