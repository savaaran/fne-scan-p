using System;
using System.Net;

namespace Plugins
{
    /// <summary>
    /// Class deriving from WebClient 
    /// Overrides KeepAlive to false
    /// </summary>
    internal class WebAPIClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            HttpWebRequest httpWebRequest = (HttpWebRequest)base.GetWebRequest(address);
            httpWebRequest.KeepAlive = false;
            return httpWebRequest;
        }
    }
}
