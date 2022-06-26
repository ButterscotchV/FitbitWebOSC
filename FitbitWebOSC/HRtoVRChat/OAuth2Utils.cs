using System.Diagnostics;
using System.Net;
using System.Text;
using Fitbit.Api.Portable.OAuth2;

namespace FitbitWebOSC.HRtoVRChat
{
    public class OAuth2Utils
    {
        public static readonly string OAuthEndpoint = "http://localhost:8080/";
        public static readonly string OAuthResponse = "<html><body>FitbitWebOSC has successfully been connected! You can close this page.</body><script type=\"text/javascript\">self.close();</script></html>";

        public static string? GetOAuth2CodeFromUrl(string authUrl)
        {
            using var httpListener = new HttpListener();

            httpListener.Prefixes.Add(OAuthEndpoint);
            httpListener.Start();
            var oAuthResponse = httpListener.GetContextAsync();

            // Open the auth URL in the default web browser
            Process.Start(new ProcessStartInfo() { FileName = authUrl, UseShellExecute = true });

            var responseContext = oAuthResponse.GetAwaiter().GetResult();

            var oauthResponse = responseContext.Request;
            using var response = responseContext.Response;

            byte[] responseBytes = Encoding.UTF8.GetBytes(OAuthResponse);

            // Get a response stream and write the response to it
            response.ContentLength64 = responseBytes.Length;
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

            return oauthResponse.QueryString["code"];
        }

        public static bool TryExchangeAuthCodeForAccessToken(OAuth2Helper oAuth2, string authCode, out OAuth2AccessToken? outToken)
        {
            try
            {
                outToken = oAuth2.ExchangeAuthCodeForAccessTokenAsync(authCode).GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                // Don't print any exceptions
            }

            outToken = null;
            return false;
        }
    }
}
