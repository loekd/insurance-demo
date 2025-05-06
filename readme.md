# Insurance Demo App

Demo app to go with talk about modern Identity Platforms.

- Features AAD B2C integration, using custom policies and user flows.
- API has token validation that supports both.
- Uses Blazor WebAssembly for the frontends.
- Uses Dapr with Redis for state storages.


## Getting started

- Configure your own instance of AAD B2C in the appsettings files by following the walkthrough here:
    - https://learn.microsoft.com/en-us/aspnet/core/blazor/security/webassembly/standalone-with-azure-active-directory-b2c?view=aspnetcore-7.0
- Run three Redis containers for state storage:
    - `docker run --name quote_state -d -p 6380:6379 redislabs/redisearch:latest`
    - `docker run --name insurance_state -d -p 6381:6379 redislabs/redisearch:latest`
    - `docker run --name damageclaim_state -d -p 6382:6379 redislabs/redisearch:latest`

- Build the .NET Tool 
  - Build XpiritInsurance.DaprLauncher
  - `dotnet pack --no-build --configuration DEBUG --output nupkg`
  - Install the tool from the build output folder
  - `dotnet tool install --add-source C:\MyProjects\XpiritInsurance.DaprLauncher\nupkg xpirit.daprlauncher`
- Select as startup projects:
    - XpiritInsurance.Api
    - XpiritInsurance.DamagClaims.Server
    - XpiritInsurance.Sales.Server
- Run the projects