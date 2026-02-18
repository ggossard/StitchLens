# StitchLens

This repository contains the StitchLens solution: an ASP.NET Core9 application that converts photos into needlepoint patterns.

Projects
- `StitchLens.Web` � web application (Razor views)
- `StitchLens.Core` � core services (image processing, quantization, PDF generation)
- `StitchLens.Data` � EF Core models and database

Quick start (local)
1. Install .NET9 SDK: https://dotnet.microsoft.com/download
2. Restore dependencies and build:
 ```
 dotnet restore
 dotnet build
 ```
3. Ensure `appsettings.json` connection string points to a writable location. By default the SQLite file `stitchlens.db` will be created in the web project's working directory.
4. Run the web app:
 ```
 cd StitchLens.Web
 dotnet run
 ```
5. Open https://localhost:5001 (or the URL shown in the console)

Database
- The solution includes EF Core migrations. To create or update the database run:
 ```
 cd StitchLens.Web
 dotnet ef database update --project ../StitchLens.Data --startup-project .
 ```

Uploads
- Uploaded images are saved to the `uploads/` directory under the web project's content root. This path is exposed as `/uploads/` static files.

Contributing
- Use feature branches and open PRs against `main`.
- Keep `SeedData` in the repo for reproducible initialization.
- AI coding session guidance: `StitchLens.Web/docs/AI_INSTRUCTIONS.md`.

Security
- Do not commit secrets or production connection strings. Use user secrets or environment variables for production configuration.
