using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using XpiritInsurance.Api.Services;

namespace XpiritInsurance.Api;

public class Program
{
    private const string CorsPolicyName = "CorsPolicy";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(options =>
                    {
                        builder.Configuration.Bind("AzureAdB2C", options);
                        options.TokenValidationParameters.NameClaimType = "name";
                    },
                    options => { builder.Configuration.Bind("AzureAdB2C", options); });

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

        //Add mock services
        builder.Services.AddSingleton<Services.QuoteAmountService>();
        builder.Services.AddSingleton<Services.InsuranceService>(); 
        builder.Services.AddSingleton<Services.DamageClaimService>();

        builder.Services.AddControllers();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        //app.UseStaticFiles();

        app.UseRouting();
        app.UseCors(CorsPolicyName);

        app.UseAuthentication();
        app.UseAuthorization();

        //app.MapControllerRoute(
        //    name: "default",
        //    pattern: "{controller=Home}/{action=Index}/{id?}");
        //app.MapRazorPages();
        app.MapControllers();


        app.Run();
    }
}
