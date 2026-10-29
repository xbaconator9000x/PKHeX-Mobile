// Executed against the temporary, official-Core retail-only PKForge Engine.
// Synthetic negative layouts only; this is not save-write or device certification.
using System.Buffers.Binary;
using PKForge.Domain;
using PKForge.Engine;

var engine = new SaveEngine();
var assertions = 0;

void ExpectRefusal(string label, byte[] data)
{
    if (engine.Validate(data))
        throw new Exception($"{label}: Validate incorrectly accepted unsupported bytes");
    assertions++;
    ExpectThrows(label + " auto session", () => engine.OpenSession(data));
    ExpectThrows(label + " explicit retail", () => engine.OpenSession(data, null, SaveFormat.Standard));
    ExpectThrows(label + " snapshot", () => engine.Open(data));
}

void ExpectThrows(string label, Action operation)
{
    try
    {
        operation();
    }
    catch (InvalidDataException)
    {
        assertions++;
        return;
    }
    throw new Exception($"{label}: expected fail-closed InvalidDataException");
}

byte[] SectorStamped(uint signature)
{
    var data = new byte[0x20000];
    for (var sector = 0; sector < 8; sector++)
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(sector * 0x1000 + 0xFF8, 4), signature);
    return data;
}

ExpectRefusal("Unbound CFRU sector signature", SectorStamped(0x01121999));
ExpectRefusal("GS Chronicles sector signature", SectorStamped(0x66290096));

var lumi = new byte[0xEDC20];
BinaryPrimitives.WriteUInt32LittleEndian(lumi, 0xFFFF0001);
ExpectRefusal("Luminescent revision header", lumi);

ExpectRefusal("truncated random input", new byte[37]);

// The explicit hack route must not bypass the retail-only entry guard.
ExpectThrows("explicit Unbound route", () =>
    engine.OpenSession(SectorStamped(0x01121999), null, SaveFormat.Unbound));
ExpectThrows("explicit GS Chronicles route", () =>
    engine.OpenSession(SectorStamped(0x66290096), null, SaveFormat.GsChronicles));

Console.WriteLine($"Retail-only runtime negative checks passed: {assertions} assertions.");
Console.WriteLine("No fixture, output save, Android provider or real write was used.");
