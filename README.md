## TerrainFlow WebClient
This is the web client for TerrainFlow, written with MVC 6 (using the first release candidate of 1.0.0), meaning that this puppy is not only using the latest and greatest .NET stuff, but could also run on Linux and Mac OS X.

![Screenshot](Screenshot.jpg)

### Running locally
Before running, please ensure that you run `bower install` inside `src/TerrainFlow` to install bower dependencies.

To properly debug with auth I launch visual studio from a command line, and prior to launching it I set the HTTP_PLATFORM_PORT environment variable to 80.

```
set HTTP_PLATFORM_PORT=80
"C:\Program Files (x86)\Microsoft Visual Studio 14.0\Common7\IDE\devenv.exe"

```

I think update my hostfile to redirect my public facing URL which is trusted by my authentication providers to localhost.  e.g.

```
127.0.0.1 mysite.azurewebsites.net
```

### Extending
* To extend or change the authentication methods, see `src/TerrainFlow/startup.cs`. Authentication is currently enabled via Facebook, Google, and Microsoft; usign a client-side cookie as storage mechanism. We get away without using cookies because we don't really need to interact with the user account - we just need to confirm that our user has a certain unique email address.
* To transform files after being uploaded, check `src/TerrainFlow/Controllers/ProjectsController.cs`, specifically the method `ProcessUpload()`. Simply create an awaitable helper that transforms the file after being uploaded to the server's local disk, right before it's being shipped up to Azure Storage as a blob.
* To change content, edit the files in `src/TerrainFlow/Views` directly.
* To change assets like JavaScript, CSS, or images, go check out `src/TerrainFlow/wwwroot`. Keep in mind that minifaction with Gulp is currently not run on Azure (for performance and compat reasons, mostly), so make sure to run `gulp clean` and `gulp min` before checking in.

### License
MIT, see LICENSE for details.
