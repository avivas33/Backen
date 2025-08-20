using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Api_Celero.Models
{
    // Modelo para el cuerpo de la solicitud a la API de Google reCAPTCHA
    public class GoogleRecaptchaVerificationRequest
    {
        [JsonPropertyName("event")]
        public EventPayload Event { get; set; } = null!;
    }

    public class EventPayload
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
        
        [JsonPropertyName("site_key")]
        public string SiteKey { get; set; } = string.Empty;
        
        [JsonPropertyName("expected_action")]
        public string? ExpectedAction { get; set; } // Opcional
    }

    // Modelo para la respuesta de la API de Google reCAPTCHA
    public class GoogleRecaptchaVerificationResponse
    {
        [JsonPropertyName("tokenProperties")]
        public TokenProperties? TokenProperties { get; set; }
        
        [JsonPropertyName("riskAnalysis")]
        public RiskAnalysis? RiskAnalysis { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; } // Nombre de la evaluación, ej: projects/PROJECT_ID/assessments/ASSESSMENT_ID
        
        [JsonPropertyName("event")]
        public EventPayload? Event { get; set; } // Google puede devolver el evento enviado
    }

    public class TokenProperties
    {
        [JsonPropertyName("valid")]
        public bool Valid { get; set; }
        
        [JsonPropertyName("hostname")]
        public string? Hostname { get; set; }
        
        [JsonPropertyName("action")]
        public string? Action { get; set; } // La acción que coincidió con esta solicitud
        
        [JsonPropertyName("createTime")]
        public string? CreateTime { get; set; } // Timestamp RFC3339 UTC "Zulu" format
    }

    public class RiskAnalysis
    {
        [JsonPropertyName("score")]
        public float Score { get; set; } // Puntuación de riesgo de 0.0 a 1.0
        
        [JsonPropertyName("reasons")]
        public List<string>? Reasons { get; set; }
    }
}
