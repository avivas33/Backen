using System.Text;

namespace Api_Celero.Models
{
    public class HansaSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int WebPort { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string Usuario { get; set; } = string.Empty;
        public string Clave { get; set; } = string.Empty;
        public bool UseBasicAuth { get; set; }
        public int TimeoutSeconds { get; set; }
        public List<HansaCompany> Companies { get; set; } = new List<HansaCompany>();

        public string GetFullBaseUrl()
        {
            return $"{BaseUrl}:{WebPort}";
        }

        public string GetAuthHeader()
        {
            if (UseBasicAuth)
            {
                var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Usuario}:{Clave}"));
                return $"Basic {authValue}";
            }
            return string.Empty;
        }
    }

    public class HansaCompany
    {
        public string CompCode { get; set; } = string.Empty;
        public string CompName { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public string ActiveStatus { get; set; } = string.Empty;
        public PaymentMethodsConfig PaymentMethods { get; set; } = new PaymentMethodsConfig();
    }

    public class PaymentMethodsConfig
    {
        public PaymentMethod CreditCard { get; set; } = new PaymentMethod();
        public PaymentMethod Yappy { get; set; } = new PaymentMethod();
        public PaymentMethod ACH { get; set; } = new PaymentMethod();
        public PaymentMethod PayPal { get; set; } = new PaymentMethod();
    }

    public class PaymentMethod
    {
        public bool Enabled { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
