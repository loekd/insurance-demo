using Microsoft.Identity.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;

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

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapControllers();

        app.Run();
    }
}
