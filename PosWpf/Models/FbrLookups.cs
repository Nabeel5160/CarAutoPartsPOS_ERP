namespace PosWpf.Models;

/// <summary>Common dropdown values for FBR invoice fields.</summary>
public static class FbrLookups
{
    public static IReadOnlyList<string> Provinces { get; } =
    [
        "Punjab",
        "Sindh",
        "Khyber Pakhtunkhwa",
        "Balochistan",
        "Islamabad Capital Territory",
        "Gilgit-Baltistan",
        "Azad Jammu & Kashmir"
    ];

    public static IReadOnlyList<string> RegistrationTypes { get; } =
    [
        "Unregistered",
        "Registered"
    ];

    /// <summary>FBR sandbox scenario IDs (DI testing).</summary>
    public static IReadOnlyList<string> SandboxScenarios { get; } =
    [
        "",
        "SN001",
        "SN002",
        "SN003",
        "SN004",
        "SN005",
        "SN006",
        "SN007",
        "SN008",
        "SN009",
        "SN010"
    ];

    public static IReadOnlyList<string> SaleTypes { get; } =
    [
        "Goods at standard rate (default)",
        "Goods at Reduced Rate",
        "Exempt goods",
        "Goods at zero-rate",
        "3rd Schedule Goods",
        "Cotton ginners",
        "Telecommunication services",
        "Petroleum Products",
        "Electricity Supply to Retailers",
        "Gas to CNG stations",
        "Mobile Phones",
        "Processing/Conversion of Goods",
        "Goods (FED in ST Mode)",
        "Services (FED in ST Mode)",
        "Services",
        "Electric Vehicle",
        "Cement /Concrete Block",
        "Potassium Chlorate",
        "CNG Sales",
        "Toll Manufacturing",
        "Non-Adjustable Supplies"
    ];
}
