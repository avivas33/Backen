using System.Net.Http.Headers;
using System.Text;

namespace Api_Celero.Utils
{
    public static class HttpClientExtensions
    {
        public static void SetBasicAuth(this HttpClient client, string username, string password)
        {
            var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        }
    }
}

