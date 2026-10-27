using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Widget;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

namespace PKHeXMobile.Android;

/// <summary>
/// First native Android read-only screen. Uses the system document picker and
/// streams only; it never requests broad storage, opens output streams, or saves.
/// Not yet device-certified or a full PKHeX editor.
/// </summary>
[Activity(Label = "PKHeX Mobile (Test)", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private const int OpenSaveRequest = 1001;
    private readonly CancellationTokenSource _lifetime = new();
    private Button? _openButton;
    private TextView? _status;
    private TextView? _details;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var layout = new LinearLayout(this)
        {
            Orientation = Orientation.Vertical,
        };
        layout.SetPadding(24, 24, 24, 24);

        var heading = new TextView(this)
        {
            Text = "PKHeX Mobile — read-only test",
            TextSize = 22,
        };
        layout.AddView(heading);

        _openButton = new Button(this) { Text = "Open a save (read-only)" };
        _openButton.Click += (_, _) => PickSave();
        layout.AddView(_openButton);

        _status = new TextView(this)
        {
            Text = "No save selected. Files are inspected without modification.",
            TextSize = 16,
        };
        layout.AddView(_status);

        _details = new TextView(this)
        {
            Text = "The experimental reader is not certified for any game format.",
            TextSize = 14,
        };
        layout.AddView(_details);

        var scroll = new ScrollView(this);
        scroll.AddView(layout);
        SetContentView(scroll);
    }

    private void PickSave()
    {
        // ACTION_OPEN_DOCUMENT returns a scoped content URI and read grant.
        // No MANAGE_EXTERNAL_STORAGE, path inference, or write permission.
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
#pragma warning disable CS0618 // A dependency-free Activity result boundary for this prototype.
        StartActivityForResult(intent, OpenSaveRequest);
#pragma warning restore CS0618
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != OpenSaveRequest || resultCode != Result.Ok || data?.Data is null)
            return;

        var uri = data.Data;
        if (_openButton is not null)
            _openButton.Enabled = false;
        if (_status is not null)
            _status.Text = "Inspecting selected document…";
        if (_details is not null)
            _details.Text = string.Empty;

        try
        {
            // A provider's authority/document ID is only an identity hint.
            // The service still requires two independent byte-identical reads.
            DocumentSourceIdentity? identity = null;
            if (!string.IsNullOrWhiteSpace(uri.Authority) &&
                DocumentsContract.IsDocumentUri(this, uri))
            {
                var documentId = DocumentsContract.GetDocumentId(uri);
                if (!string.IsNullOrWhiteSpace(documentId))
                    identity = new DocumentSourceIdentity(uri.Authority!, documentId);
            }

            var resolver = ContentResolver ?? throw new IOException("No content resolver.");
            var inspection = await ReadOnlyDocumentService.InspectWithIdentityAsync(token =>
            {
                token.ThrowIfCancellationRequested();
                Stream stream = resolver.OpenInputStream(uri)
                    ?? throw new IOException("The selected provider returned no read stream.");
                return ValueTask.FromResult(new DocumentReadHandle(stream, identity));
            }, _lifetime.Token);

            if (_lifetime.IsCancellationRequested)
                return;

            if (!inspection.IsRecognized || inspection.Result is null)
            {
                if (_status is not null)
                    _status.Text = "Save refused — no changes made.";
                if (_details is not null)
                    _details.Text = inspection.RejectionReason ?? "Unknown or unsupported save.";
                return;
            }

            if (_status is not null)
                _status.Text = "Experimental read-only projection. No edits or export are enabled.";
            if (_details is not null)
                _details.Text = FormatResult(inspection);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The activity was destroyed; do not publish stale results.
        }
        catch (Exception)
        {
            if (!_lifetime.IsCancellationRequested)
            {
                if (_status is not null)
                    _status.Text = "Could not safely inspect this document.";
                if (_details is not null)
                    _details.Text = "The provider may have revoked access, changed its contents, or returned an unsupported file.";
            }
        }
        finally
        {
            if (!_lifetime.IsCancellationRequested && _openButton is not null)
                _openButton.Enabled = true;
        }
    }

    private static string FormatResult(ReadOnlyDocumentInspection inspection)
    {
        var save = inspection.Result!;
        var text = new StringBuilder();
        text.AppendLine("Provider identity checked twice: " +
            (inspection.SourceIdentityConfirmed ? "yes (not authenticated)" : "unavailable"));
        text.AppendLine("Core type: " + save.CoreSaveType);
        text.AppendLine("Game: " + save.GameVersion + " | Generation: " + save.Generation);
        text.AppendLine("Trainer: " + save.TrainerName);
        text.AppendLine("Trainer ID: " + save.TrainerId + " / " + save.SecretId);
        text.AppendLine("Party: " + save.PartyCount + " reported slots");
        foreach (var pokemon in save.Party)
            AppendPokemon(text, pokemon);
        text.AppendLine("Boxes: " + save.BoxCount + " × " + save.BoxSlotCount);
        foreach (var box in save.Boxes)
        {
            text.AppendLine("Box " + (box.BoxIndex + 1) + ": " +
                box.OccupiedSlots.Count + " occupied");
            foreach (var pokemon in box.OccupiedSlots)
                AppendPokemon(text, pokemon);
        }
        text.AppendLine("Read-only. No format or ROM-hack write compatibility certified.");
        return text.ToString();
    }

    private static void AppendPokemon(StringBuilder text, PokemonSlot pokemon)
    {
        text.Append("  Slot ").Append(pokemon.SlotIndex + 1)
            .Append(": #").Append(pokemon.Species)
            .Append(" ").Append(pokemon.Nickname)
            .Append(" Lv.").Append(pokemon.Level);
        if (pokemon.IsEgg) text.Append(" (Egg)");
        if (pokemon.IsShiny) text.Append(" (Shiny)");
        text.AppendLine();
    }

    protected override void OnDestroy()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.OnDestroy();
    }
}
