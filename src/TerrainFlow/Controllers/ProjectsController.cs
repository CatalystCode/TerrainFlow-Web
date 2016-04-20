using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using GeoTiffSharp;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using TerrainFlow.ViewModels.Projects;

namespace TerrainFlow.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private IConfiguration _configuration;
        private readonly IHostingEnvironment _environment;
        private Storage _storage;

        public ProjectsController(IHostingEnvironment environment, IConfiguration configuration)
        {
            _storage = new Storage(configuration);

            _environment = environment;
            _configuration = configuration;
        }

        private ICollection<ProjectViewModel> GetProjectsFromTables()
        {
            var projects = _storage.GetProjectsForUser(GetEmailFromUser());

            var collection = new Collection<ProjectViewModel>();

            foreach (var entity in projects)
            {
                collection.Add(new ProjectViewModel
                {                    
                    Name = entity.Name,
                    URL =  entity.Url
                });
            }

            return collection;
        }

        [HttpPost]
        public async Task<IActionResult> UploadProjectFiles()
        {
            var files = Request.Form.Files;
            var hashes = new List<string>();

            Trace.TraceInformation("Upload called with {0} files", files.Count());

            try
            {
                return await ProcessUpload(files.First());
            }
            catch (Exception ex)
            {
                Trace.TraceError("Failed to process upload. \n" + ex.ToString());
                return new StatusCodeResult(StatusCodes.Status400BadRequest);
            }
        }

        // /Projects/
        [HttpGet]
        public IActionResult Index()
        {
            var projects = new ProjectsViewModel
            {
                Projects = GetProjectsFromTables()
            };

            return View(projects);
        }

        // /Projects/Add
        [HttpGet]
        public IActionResult Add()
        {
            if (GetEmailFromUser() == null)
            {
                return RedirectToAction("Signin", "Account");
            }

            return View();
        }

        #region Helpers

        private async Task<JsonResult> ProcessUpload(IFormFile file)
        {
            Trace.TraceInformation("Process upload for {0}", file.ToString());

            var t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            var epoch = (int)t.TotalSeconds;
            var sourceName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim('"');
            var filePath = Path.GetTempFileName();
            string url = null;

            // Save locally
            using (var stream = System.IO.File.OpenWrite(filePath))
            {
                await file.CopyToAsync(stream);
            }
            
            try
            {
                var resultPaths = ConvertFiles(filePath, sourceName);
            

                if (resultPaths != null && resultPaths.Any())
                {
                    foreach (var path in resultPaths)
                    {
                        Trace.TraceInformation("Moving to blob store: {0}", path);
                        var blobUri = await _storage.UploadFileToBlob(path, Path.GetFileName(path));

                        // Hack - Grab the destination URI for use later
                        if (blobUri.Contains(".dat"))
                        {
                            url = blobUri.Replace(".dat", string.Empty);
                        }
                    
                    }

                    // update project name if appropriate
                    string projectName = Path.GetFileNameWithoutExtension(sourceName);
                    var projects = _storage.GetProjectsForUser(GetEmailFromUser());

                    if (projects.Where(p => string.Equals(p.Name, projectName, StringComparison.OrdinalIgnoreCase)).Any())
                    {
                        int versionNumber = projects.Count(p => p.Name.Contains(projectName)) + 1;
                        projectName = string.Format("{0} (v{1})", projectName, versionNumber);
                    }
                    _storage.SaveFileToTables(projectName, url, GetEmailFromUser());
                }

            }
            catch (Exception ex)
            {
                var result = new JsonResult(new { error = "The server failed to process this file.  Please verify source data is compatible." });
                result.StatusCode = 500;
                return result;
            }

            return Json(new { thumbnail = "url" + ".jpg" });
        }

        // Rough implementation, support for zipped tiff's
        private IEnumerable<string> ConvertFiles(string filePath, string sourceName)
        {
            string workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workingDirectory);

            // If ZIP, extract first
            if (string.Equals(Path.GetExtension(sourceName), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                Trace.TraceInformation("Found zip file, decompressing.");

                ZipFile.ExtractToDirectory(filePath, workingDirectory);

                var files = Directory.GetFiles(workingDirectory);

                // Lets see if we have a tiff file for now
                var tiff = files.Where(f => string.Equals(Path.GetExtension(f), ".tif", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (!string.IsNullOrEmpty(tiff))
                {
                    Trace.TraceInformation("Found tiff, converting.");

                    var GeoTiff = new GeoTiff();
                    var outputRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

                    var outputBinary = outputRoot + ".dat";
                    var outputMetadata = outputRoot + ".json";
                    var outputThumbnail = outputRoot + ".jpg";

                    GeoTiff.ConvertToHeightMap(tiff, outputBinary, outputMetadata, outputThumbnail);

                    try
                    {
                        if (System.IO.File.Exists(filePath))
                            System.IO.File.Delete(filePath);

                        Directory.Delete(workingDirectory, true);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning("Failed to cleanup working files. {0}", ex.ToString());
                    }

                    return new List<string> { outputBinary, outputMetadata, outputThumbnail };
                }
            }

            return null;
        }

        private string CreateHashFromFile(string filePath)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = System.IO.File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return string.Concat(hash.Select(x => x.ToString("X2")));
                }
            }
        }

        private string GetEmailFromUser()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var email = identity.FindFirst(ClaimTypes.Email).Value;

            return email;
        }
                          
#endregion
    }
}
