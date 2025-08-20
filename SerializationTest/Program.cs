using System;
using System.Text.Json;
using Api_Celero.Models;

namespace SerializationTest
{
    class TestProgram
    {
        static void Main()
        {
            var payload = new GoogleRecaptchaVerificationRequest
            {
                Event = new EventPayload
                {
                    Token = "test-token",
                    SiteKey = "test-site-key",
                    ExpectedAction = "test-action"
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var jsonPayload = JsonSerializer.Serialize(payload, jsonOptions);
            Console.WriteLine("JSON serializado:");
            Console.WriteLine(jsonPayload);
        }
    }
}
