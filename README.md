[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/8943/badge)](https://www.bestpractices.dev/projects/8943)

# BasicAuth
A library which includes a dotnet Basic Authentication middleware component which can be added to dotnet web and API apps on Azure to enable classic/old RFC 2617 Basic Authentication. Please note, Basic Auth is one of the oldest forms of web authentication and is [not known for being the most secure](https://datatracker.ietf.org/doc/html/rfc2617). Use and implement at your own risk and only over HTTPS/TLS to prevent sending user names and passwords unencrypted over the Internet.

## Changelog
- See [CHANGELOG.md](CHANGELOG.md) for review history and recorded security findings.

## Security Requirements
- Use HTTPS/TLS only. This middleware rejects non-HTTPS requests.
- Do not store credentials directly in source code.
- Add host-level rate limiting and monitoring for internet-facing workloads.
- Prefer modern auth (OIDC/OAuth/JWT) for public-facing production apps where possible.

## Install - Leveraging [NuGet Package](https://www.nuget.org/packages/joelbyford.BasicAuth/)
Assuming you would like to add the library to your project via a NuGet package, the following are the steps required:
1. Have an existing `dotnet webapp` or `dotnet apiapp` created (if starting from scratch, simply type `dotnet new webapp --name "myWebApp"` to create a new one).
2. From the command line change the directory the the directory with a `.csproj` file in it (`cd myWebApp` in the example above).
3. Add the package to your project by typing `dotnet add package joelbyford.BasicAuth`.
4. Modify your code startup code as appropriate (see later in this readme).

## Install - Leveraging Source Code
If you would rather use the raw source code, just copy the BasicAuth.cs file into your existing project instead.

## Code Modifications
Once installed, to use the library, simply modify the `Configure` method in your `startup.cs` to call the library in any *one* of *two* ways:

### Authorize a Single User
For simple use cases, this may satisfy your need. Source credentials from environment variables or a secret store.
```
using joelbyford;
using System;

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        ...
        string basicAuthRealm = "mywebsite.com";
        string basicAuthUser = Environment.GetEnvironmentVariable("BASIC_AUTH_USER");
        string basicAuthPass = Environment.GetEnvironmentVariable("BASIC_AUTH_PASS");
        app.UseMiddleware<joelbyford.BasicAuth>(basicAuthRealm, basicAuthUser, basicAuthPass);
    }
```

### Authorize a Dictionary of Users
If you would like to control how and where you get the users and passwords from, this method is best (e.g. you are obtaining from a database or secure configuration source).
```
using joelbyford;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        ...
        Dictionary<string, string> myUsers = new Dictionary<string, string>();
        var packageJson = File.ReadAllText("authorizedUsers.secure.json");
        myUsers = JsonSerializer.Deserialize<Dictionary<string, string>>(packageJson);
        string basicAuthRealm = "mywebsite.com";
        app.UseMiddleware<joelbyford.BasicAuth>(basicAuthRealm, myUsers);
    }
```
In this example, credentials are loaded from a controlled configuration source. Avoid committing credential files to source control and rotate passwords regularly.

If you must use a local file for development, do not store production credentials in that file and exclude it from git.

Example format:
```
{    
    "testUser" : "testPassword",
    "devUser" : "devPassword"
}
```
This can of course be loaded in from a database call instead as long as users and passwords are loaded into a `Dictionary<string, string>`

To see an example of this in use, please see `startup.cs` in the https://github.com/joelbyford/CSVtoJSONcore repo.

## Local Authentication Test Harness
To quickly verify middleware behavior (including a bogus endpoint auth check), a runnable sample is included at `harness/BasicAuthHarness`.

Run the harness:
```
dotnet run --project harness/BasicAuthHarness/BasicAuthHarness.csproj
```

In a second terminal, run the included curl test script:
```
powershell -ExecutionPolicy Bypass -File .\harness\BasicAuthHarness\testing\test-auth.ps1
```

Alternatively you may call the tests via the [REST Client VSCode plugin](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) in the testing.http file.  

Expected results:
- Missing Authorization header -> `401 Unauthorized`
- Invalid credentials -> `401 Unauthorized`
- Valid credentials to `POST /bogus` -> `404 Not Found` (auth passed, route missing)
- Valid credentials to `GET /health` -> `200 OK`

Note: The harness runs on HTTP for convenience and uses `X-Forwarded-Proto: https` in curl commands to simulate TLS termination at a reverse proxy.
