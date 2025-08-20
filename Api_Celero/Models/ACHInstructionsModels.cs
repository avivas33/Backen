namespace Api_Celero.Models
{
    public class ACHInstructionsSettings
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new List<string>();
        public BankDetailsSettings BankDetails { get; set; } = new BankDetailsSettings();
        public Dictionary<string, CompanyACHInstructions> Companies { get; set; } = new Dictionary<string, CompanyACHInstructions>();
    }

    public class CompanyACHInstructions
    {
        public string Title { get; set; } = string.Empty;
        public List<string> Steps { get; set; } = new List<string>();
        public BankDetailsSettings BankDetails { get; set; } = new BankDetailsSettings();
    }

    public class BankDetailsSettings
    {
        public string Title { get; set; } = string.Empty;
        public List<BankInfo> Banks { get; set; } = new List<BankInfo>();
    }

    public class BankInfo
    {
        public string Beneficiary { get; set; } = string.Empty;
        public string Bank { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
    }
}