using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace TerrainFlow.Controllers
{
    public class AccountController : Controller
    {

        [HttpGet("~/Signin")]
        public IActionResult Signin(string returnUrl = "/")
        {
            // Note: the "returnUrl" parameter corresponds to the endpoint the user agent
            // will be redirected to after a successful authentication and not
            // the redirect_uri of the requesting client application.
            ViewBag.ReturnUrl = returnUrl;

            // Note: in a real world application, you'd probably prefer creating a specific view model.
            return View("Signin");
        }

        [HttpPost("~/Signin")]
        public IActionResult Signin(string provider, string returnUrl)
        {
            Request.Scheme = "https";

            // Note: the "provider" parameter corresponds to the external
            // authentication provider choosen by the user agent.
            if (string.IsNullOrEmpty(provider))
            {
                return BadRequest();
            }

            // Note: the "returnUrl" parameter corresponds to the endpoint the user agent
            // will be redirected to after a successful authentication and not
            // the redirect_uri of the requesting client application.
            if (string.IsNullOrEmpty(returnUrl))
            {
                return BadRequest();
            }

            var redirect = Url.Action("ExternalCallback", "Account");
            return new ChallengeResult(provider, new AuthenticationProperties
            {
                
                RedirectUri = redirect 
            });
        }

        [HttpGet("~/Signout"), HttpPost("~/Signout")]
        public async Task<IActionResult> Signout()
        {
            // Instruct the cookies middleware to delete the local cookie created
            // when the user agent is redirected from the external identity provider
            // after a successful authentication flow (e.g Google or Facebook).

            await HttpContext.Authentication.SignOutAsync("Cookie");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ExternalCallback(string returnUrl = null)
        {
            // User is now logged in. Do things if you want to do things here.
            return RedirectToAction("Index", "Home");
        }
    }
}
