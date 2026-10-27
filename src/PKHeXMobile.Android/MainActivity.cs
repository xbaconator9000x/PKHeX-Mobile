using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Widget;
using PKHeXMobile.Domain;
using PKHeXMobile.ReadOnly;

namespace PKHeXMobile.Android;

/// <summary>
/// Native, least-privilege, read-only save inspector. A selection is transient;
/// private save data is not persisted in Activity state or application storage.
/// All parsing is provisional and all save writes remain disabled.
/// </summary>
[Activity(Label = "PKHeX Mobile (Test)", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private const int OpenSaveRequest = 1001;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _activeInspection;
    private bool _pickerPending;
    private bool _destroyed;
    private Button? _openButton;
    private Button? _cancelButton;
    private TextView? _status;
    private TextView? _trainer;
    private TextView? _party;
    private LinearLayout? _partySlots;
    private TextView? _partyDetails;
    private TextView? _boxDetails;
    private TableLayout? _boxGrid;
    private Spinner? _boxSelector;
    private SaveReadOnlyResult? _currentResult;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        int padding = (int)Math.Ceiling(16 * (Resources?.DisplayMetrics?.Density ?? 1f));
        layout.SetPadding(padding, padding, padding, padding);

        layout.AddView(new TextView(this)
        {
            Text = "PKHeX Mobile — read-only test",
            TextSize = 22,
        });

        _openButton = new Button(this)
        {
            Text = "Open a save (read-only)",
            ContentDescription = "Choose a save document for read-only inspection",
        };
        _openButton.Click += (_, _) => PickSave();
        layout.AddView(_openButton);

        _cancelButton = new Button(this)
        {
            Text = "Cancel inspection",
            ContentDescription = "Cancel the current read-only inspection",
            Visibility = ViewStates.Gone,
        };
        _cancelButton.Click += (_, _) => CancelInspection();
        layout.AddView(_cancelButton);

        _status = new TextView(this)
        {
            Text = savedInstanceState is null
                ? "No save selected. Files are inspected without modification."
                : "Activity recreated. Select the document again; no save data was retained.",
            TextSize = 16,
        };
        layout.AddView(_status);

        _trainer = new TextView(this) { TextSize = 16 };
        layout.AddView(_trainer);

        _party = new TextView(this) { TextSize = 16 };
        layout.AddView(_party);
        _partySlots = new LinearLayout(this) { Orientation = Orientation.Vertical };
        layout.AddView(_partySlots);
        _partyDetails = new TextView(this) { TextSize = 16 };
        layout.AddView(_partyDetails);

        _boxSelector = new Spinner(this)
        {
            Visibility = ViewStates.Gone,
            ContentDescription = "Select one Pokémon storage box to inspect",
        };
        _boxSelector.ItemSelected += (_, args) =>
        {
            if (_currentResult is not null)
                RenderSelectedBox(args.Position);
        };
        layout.AddView(_boxSelector);

        _boxGrid = new TableLayout(this)
        {
            Visibility = ViewStates.Gone,
            StretchAllColumns = true,
            ContentDescription = "Read-only Pokémon storage slots",
        };
        layout.AddView(_boxGrid);

        _boxDetails = new TextView(this) { TextSize = 16 };
        layout.AddView(_boxDetails);

        var scroll = new ScrollView(this);
        scroll.AddView(layout);
        SetContentView(scroll);
    }

    private void PickSave()
    {
        if (_destroyed || _pickerPending || _activeInspection is not null)
            return;

        // Never infer a filesystem path, persist URI grants or request writes.
        var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType("*/*");
        intent.AddFlags(ActivityFlags.GrantReadUriPermission);
        _pickerPending = true;
        if (_openButton is not null)
            _openButton.Enabled = false;
        try
        {
#pragma warning disable CS0618 // Explicit result boundary, no AndroidX dependency.
            StartActivityForResult(intent, OpenSaveRequest);
#pragma warning restore CS0618
        }
        catch (Exception)
        {
            _pickerPending = false;
            if (_openButton is not null) _openButton.Enabled = true;
            if (_status is not null) _status.Text = "Could not open the document picker.";
        }
    }

    private void CancelInspection()
    {
        if (_activeInspection is null)
            return;
        _activeInspection.Cancel();
        if (_cancelButton is not null)
            _cancelButton.Enabled = false;
        if (_status is not null)
            _status.Text = "Cancelling inspection…";
    }

    protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != OpenSaveRequest || _destroyed)
            return;
        _pickerPending = false;
        if (resultCode != Result.Ok || data?.Data is null)
        {
            if (_openButton is not null) _openButton.Enabled = true;
            if (_status is not null) _status.Text = "Selection cancelled. No file was changed.";
            return;
        }
        if (_activeInspection is not null)
            return;

        var uri = data.Data;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _activeInspection = operation;
        var token = operation.Token;
        if (_openButton is not null) _openButton.Enabled = false;
        if (_cancelButton is not null)
        {
            _cancelButton.Enabled = true;
            _cancelButton.Visibility = ViewStates.Visible;
        }
        ClearProjection();
        if (_status is not null) _status.Text = "Inspecting selected document…";

        try
        {
            var resolver = ContentResolver ?? throw new IOException("No content resolver.");
            // The two opens are independent. The URI-derived identity is only
            // a hint, not a provider generation, and never authorizes writes.
            var inspection = await Task.Run(() =>
                ReadOnlyDocumentService.InspectWithIdentityAsync(readToken =>
                {
                    readToken.ThrowIfCancellationRequested();
                    var identity = GetDocumentIdentity(uri);
                    // Unlike OpenInputStream, the descriptor open receives a
                    // CancellationSignal. Providers may still ignore cancellation;
                    // this is not a latency or device-compatibility guarantee.
                    Stream stream = SafReadStream.Open(resolver, uri, readToken);
                    return ValueTask.FromResult(new DocumentReadHandle(stream, identity));
                }, token), token);

            if (!IsCurrent(operation))
                return;
            if (!inspection.IsRecognized || inspection.Result is null)
            {
                if (_status is not null) _status.Text = "Save refused — no changes made.";
                if (_trainer is not null)
                    _trainer.Text = inspection.RejectionReason ?? "Unknown or unsupported save.";
                return;
            }
            _currentResult = inspection.Result;
            if (_status is not null)
                _status.Text = "Read-only preview. No editing or export is enabled.";
            if (_trainer is not null)
                _trainer.Text = ReadOnlyProjectionPresenter.FormatTrainer(
                    inspection.Result, inspection.SourceIdentityConfirmed);
            if (_party is not null)
                _party.Text = ReadOnlyProjectionPresenter.FormatParty(inspection.Result);
            RenderParty(inspection.Result);
            if (_boxSelector is not null && _boxDetails is not null)
            {
                var labels = ReadOnlyProjectionPresenter.BoxLabels(inspection.Result);
                if (labels.Count > 0)
                {
                    var adapter = new ArrayAdapter<string>(
                        this, global::Android.Resource.Layout.SimpleSpinnerItem, labels.ToArray());
                    adapter.SetDropDownViewResource(global::Android.Resource.Layout.SimpleSpinnerDropDownItem);
                    _boxSelector.Adapter = adapter;
                    _boxSelector.Visibility = ViewStates.Visible;
                    _boxSelector.SetSelection(0);
                    RenderSelectedBox(0);
                }
            }
        }
        catch (System.OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_destroyed && ReferenceEquals(_activeInspection, operation))
            {
                ClearProjection();
                if (_status is not null) _status.Text = "Inspection cancelled. No file was changed.";
            }
        }
        catch (global::Android.OS.OperationCanceledException) when (operation.IsCancellationRequested)
        {
            if (!_destroyed && ReferenceEquals(_activeInspection, operation))
            {
                ClearProjection();
                if (_status is not null) _status.Text = "Inspection cancelled. No file was changed.";
            }
        }
        catch (Java.Lang.SecurityException)
        {
            ShowReadFailure(operation, "Document access was denied or revoked.");
        }
        catch (Java.Lang.IllegalArgumentException)
        {
            ShowReadFailure(operation, "The document provider rejected this request.");
        }
        catch (UnauthorizedAccessException)
        {
            ShowReadFailure(operation, "Document access was denied or revoked.");
        }
        catch (InvalidDataException)
        {
            ShowReadFailure(operation, "The document is invalid, incomplete or too large.");
        }
        catch (IOException)
        {
            ShowReadFailure(operation, "The document provider could not be read reliably.");
        }
        catch (Exception)
        {
            ShowReadFailure(operation, "Could not safely inspect this document.");
        }
        finally
        {
            if (ReferenceEquals(_activeInspection, operation))
            {
                _activeInspection = null;
                if (!_destroyed)
                {
                    if (_openButton is not null) _openButton.Enabled = true;
                    if (_cancelButton is not null) _cancelButton.Visibility = ViewStates.Gone;
                }
            }
        }
    }

    // Native touch targets retain the original party slot index. These buttons
    // only reveal immutable scalar details; no PKM or SaveFile reaches the UI.
    private void RenderParty(SaveReadOnlyResult result)
    {
        if (_partySlots is null || _partyDetails is null)
            return;
        _partySlots.RemoveAllViews();
        _partyDetails.Text = "Select a party slot for Core-reported details (read-only).";
        foreach (var cell in ReadOnlyProjectionPresenter.PartyCells(result))
        {
            var detail = ReadOnlyProjectionPresenter.FormatPartySlotDetails(cell);
            var button = new Button(this)
            {
                Text = ReadOnlyProjectionPresenter.FormatPartySlotLabel(cell),
                ContentDescription = detail + ". Read-only.",
            };
            button.Click += (_, _) =>
            {
                if (ReferenceEquals(_currentResult, result) && _partyDetails is not null)
                    _partyDetails.Text = detail;
            };
            _partySlots.AddView(button);
        }
    }

    // Each visible cell represents its original slot index, including empties.
    // This is a navigation-only grid: no drag, drop, import or write affordance.
    private void RenderSelectedBox(int selectedIndex)
    {
        if (_currentResult is null || _boxGrid is null || _boxDetails is null)
            return;
        _boxGrid.RemoveAllViews();
        var cells = ReadOnlyProjectionPresenter.BoxCells(_currentResult, selectedIndex);
        _boxGrid.Visibility = ViewStates.Visible;
        _boxDetails.Text = "Select a slot to inspect it. Editing is disabled.";

        // Recalculate for the current width and font scale. Never reorder cells.
        var configuration = Resources?.Configuration;
        int widthDp = configuration?.ScreenWidthDp ?? 320;
        float fontScale = configuration?.FontScale ?? 1f;
        int columns = ReadOnlyGridLayout.ColumnsForWidth(
            Math.Max(0, widthDp - 32), fontScale);
        int minTouchPx = (int)Math.Ceiling(48 * (Resources?.DisplayMetrics?.Density ?? 1f));
        for (int start = 0; start < cells.Count; start += columns)
        {
            var row = new TableRow(this);
            for (int i = start; i < Math.Min(start + columns, cells.Count); i++)
            {
                var cell = cells[i];
                var pokemon = cell.Pokemon;
                var detail = ReadOnlyProjectionPresenter.FormatCellDetails(cell);
                var slot = new TextView(this)
                {
                    Text = (cell.SlotIndex + 1) + "\n" +
                        (pokemon is null ? "—" : "#" + pokemon.Species),
                    ContentDescription = detail + ". Read-only.",
                    TextSize = 14,
                    Gravity = GravityFlags.Center,
                    Clickable = true,
                    Focusable = true,
                };
                slot.SetMinimumHeight(minTouchPx);
                slot.SetPadding(4, 12, 4, 12);
                slot.Click += (_, _) =>
                {
                    if (_currentResult is not null && _boxDetails is not null)
                        _boxDetails.Text = detail;
                };
                row.AddView(slot, new TableRow.LayoutParams(
                    0, ViewGroup.LayoutParams.WrapContent, 1f));
            }
            _boxGrid.AddView(row);
        }
    }

    private bool IsCurrent(CancellationTokenSource operation) =>
        !_destroyed && !operation.IsCancellationRequested &&
        ReferenceEquals(_activeInspection, operation);

    private void ShowReadFailure(CancellationTokenSource operation, string message)
    {
        if (!IsCurrent(operation))
            return;
        ClearProjection();
        if (_status is not null) _status.Text = message + " No file was changed.";
    }

    private void ClearProjection()
    {
        _currentResult = null;
        if (_trainer is not null) _trainer.Text = string.Empty;
        if (_party is not null) _party.Text = string.Empty;
        if (_partySlots is not null) _partySlots.RemoveAllViews();
        if (_partyDetails is not null) _partyDetails.Text = string.Empty;
        if (_boxDetails is not null) _boxDetails.Text = string.Empty;
        if (_boxGrid is not null)
        {
            _boxGrid.RemoveAllViews();
            _boxGrid.Visibility = ViewStates.Gone;
        }
        if (_boxSelector is not null)
        {
            _boxSelector.Visibility = ViewStates.Gone;
            _boxSelector.Adapter = null;
        }
    }

    private DocumentSourceIdentity? GetDocumentIdentity(global::Android.Net.Uri uri)
    {
        if (string.IsNullOrWhiteSpace(uri.Authority) ||
            !DocumentsContract.IsDocumentUri(this, uri))
            return null;
        var documentId = DocumentsContract.GetDocumentId(uri);
        return string.IsNullOrWhiteSpace(documentId)
            ? null : new DocumentSourceIdentity(uri.Authority!, documentId);
    }

    protected override void OnDestroy()
    {
        _destroyed = true;
        _activeInspection?.Cancel();
        _lifetime.Cancel();
        _lifetime.Dispose();
        _currentResult = null;
        base.OnDestroy();
    }
}
