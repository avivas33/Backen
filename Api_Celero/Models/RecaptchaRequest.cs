namespace Api_Celero.Models
{
    public class RecaptchaRequest
    {
        public string? Token { get; set; }
        public string? ExpectedAction { get; set; } // Opcional, depende de tu implementación frontend
    }
}
