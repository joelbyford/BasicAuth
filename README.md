# BasicAuth
A library which includes a dotnet 5 (dotnet core 5)  Basic Authentication middleware component which can be added to dotnet web and API apps on Azure to enable classic/old RFC 2617 Basic Authentication.  

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
For simple use cases, this may satisfy your need.  ***PLEASE take steps to avoid having credentials in code***
```
using joelbyford;

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        ...
        string basicAuthRealm = "mywebsite.com";
        string basicAuthUser = "testUser"; //hardcoded values here for example only
        string basicAuthPass = "testPass"; //hardcoded values here for example only
        app.UseMiddleware<joelbyford.BasicAuth>(basicAuthRealm, basicAuthUser, basicAuthPass);
    }
```

### Authorize a Dictionary of Users
If you would like to control how and where you get the users and passwords from, this method is best (e.g. you are obtaining from a database). ***PLEASE take steps to avoid having credentials in code***
```
using joelbyford;
using System.IO;
using System.Text.Json;

public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        ...
        Dictionary<string, string> myUsers = new Dictionary<string, string>();
        var packageJson = File.ReadAllText("authorizedUsers.json");
        myUsers = JsonSerializer.Deserialize<Dictionary<string, string>>(packageJson);
        string basicAuthRealm = "mywebsite.com";
        app.UseMiddleware<joelbyford.BasicAuth>(basicAuthRealm, myUsers);
    }
```
In this example, a json file is loaded from the web app's root directory with the following format:
```
{    
    "testUser" : "testPassword",
    "devUser" : "devPassword"
}
```
This can of course be loaded in from a database call instead as long as users and passwords are loaded into a `Dictionary<string, string>`

To see an example of this in use, please see `startup.cs` in the https://github.com/joelbyford/CSVtoJSONcore repo.
