using CarAutoParts.Domain.Entities;

namespace CarAutoParts.Infrastructure.Data.Seed;

/// <summary>Per-vertical seed packs for categories and brands.</summary>
public static class VerticalSeedPacks
{
    public sealed record Pack(string[] Brands, (string Name, string Description)[] Categories);

    public static Pack For(string? verticalKey) =>
        (verticalKey ?? "auto-parts").ToLowerInvariant() switch
        {
            "bike-parts" => new Pack(
                ["Honda", "Yamaha", "Suzuki", "Kawasaki", "Hero", "Road Prince", "United", "Unique"],
                [
                    ("Engine", "Bike engine parts"),
                    ("Brakes", "Brake shoes, pads, cables"),
                    ("Electrical", "CDI, coils, bulbs"),
                    ("Body", "Panels, mirrors, seats"),
                    ("Filters", "Air and oil filters"),
                    ("Tyres", "Tyres and tubes"),
                    ("Transmission", "Chains, sprockets, clutches"),
                    ("Accessories", "Helmets, covers, locks")
                ]),
            "general-retail" => new Pack(
                ["Generic", "Local", "Premium", "Value"],
                [
                    ("Stationery", "Pens, paper, notebooks"),
                    ("Office", "Folders, clips, organizers"),
                    ("Art", "Colors, brushes, craft"),
                    ("School", "Bags, geometry, supplies"),
                    ("Consumables", "Toner, ink, tape"),
                    ("Gifts", "Cards and gift items"),
                    ("Electronics", "Cables, batteries, accessories"),
                    ("Other", "Miscellaneous retail")
                ]),
            _ => new Pack(
                ["Toyota", "Honda", "Suzuki", "Hyundai", "Kia", "Nissan", "BMW", "Audi"],
                [
                    ("Engine Parts", "Engine components and assemblies"),
                    ("Brake System", "Brake pads, discs, and fluids"),
                    ("Electrical", "Batteries, alternators, and wiring"),
                    ("Suspension", "Shocks, struts, and bushings"),
                    ("Filters", "Oil, air, and fuel filters"),
                    ("Body Parts", "Panels, bumpers, and mirrors"),
                    ("Transmission", "Gears, clutches, and fluids"),
                    ("Cooling System", "Radiators, hoses, and thermostats")
                ])
        };

    public static string DefaultCompanyName(string? verticalKey) =>
        (verticalKey ?? "auto-parts").ToLowerInvariant() switch
        {
            "bike-parts" => "Bike Auto Parts",
            "general-retail" => "Retail POS",
            _ => "Car Auto Parts"
        };
}
