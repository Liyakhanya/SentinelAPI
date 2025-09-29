namespace SentinelApi.Services
{
    public interface IValidationService
    {
        bool IsValidSuburb(string suburb);
        bool IsValidCategory(string category);
        bool IsValidDuration(int duration);
        List<string> GetPESuburbs();
        List<string> GetValidCategories();
    }
    public class ValidationService : IValidationService
    {
        private readonly List<string> _peSuburbs = new()
        {
            "Walmer", "Korsten", "Bethelsdorp", "Summerstrand", "Newton Park",
            "New Brighton", "Zwide", "Amsterdamhoek", "Mill Park", "Lorraine",
            "Sydenham", "North End", "Central", "Greenacres", "Parsons Hill",
            "Gelvandale", "Schauderville", "Algoa Park", "Malabar", "Kensington",
            "Fernglen", "Linton Grange", "Prospect Hill", "Mount Pleasant", "Booysens Park",
            "Salt Lake", "Cradock Place", "Humerail", "Jabavu", "KwaZakhele",
            "Kwamagxaki", "Kwamaxaka", "Motherwell", "Port Elizabeth Central", "Red Location",
            "Sweden Park", "Windvogel", "Wells Estate", "Chatty", "Colchester"
        };
        private readonly List<string> _validCategories = new()
        {
            "Robbery", "GBV", "Hazard", "General", "Theft", "Accident",
            "Suspicious Activity", "Community Event", "Safety Tip"
        };
        public bool IsValidSuburb(string suburb) => _peSuburbs.Contains(suburb, StringComparer.OrdinalIgnoreCase); // FIXED: Case-insensitive
        public bool IsValidCategory(string category) => _validCategories.Contains(category, StringComparer.OrdinalIgnoreCase); // FIXED: Case-insensitive
        public bool IsValidDuration(int duration) => duration >= 15 && duration <= 120;
        public List<string> GetPESuburbs() => _peSuburbs;
        public List<string> GetValidCategories() => _validCategories;
    }
}