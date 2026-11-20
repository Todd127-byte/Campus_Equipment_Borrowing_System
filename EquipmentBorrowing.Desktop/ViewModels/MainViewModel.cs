using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EquipmentBorrowing.Application.Exceptions;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using System.Collections.ObjectModel;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly DatabaseInitializer _databaseInitializer;
    private readonly ManageStudentsService _manageStudents;
    private readonly ManageEquipmentService _manageEquipment;
    private readonly BorrowEquipmentService _borrowEquipment;
    private readonly ReturnEquipmentService _returnEquipment;
    private readonly GetActiveBorrowingsService _getActiveBorrowings;
    private readonly BrowseBorrowingsService _browseBorrowings;

    public ObservableCollection<Student> Students { get; } = [];
    public ObservableCollection<Equipment> EquipmentCatalog { get; } = [];
    public ObservableCollection<EquipmentSelectionOption> AvailableEquipment { get; } = [];
    public ObservableCollection<ActiveBorrowingSummary> ActiveBorrowings { get; } = [];
    public ObservableCollection<BorrowingRecordSummary> SearchResults { get; } = [];
    public ObservableCollection<BorrowingRecordSummary> StudentHistory { get; } = [];
    public ObservableCollection<BorrowingRecordSummary> EquipmentHistory { get; } = [];
    public string[] StatusFilterOptions { get; } = ["All", "Active", "Returned"];
    public string[] SortFieldOptions { get; } = ["Checkout date", "Expected return date"];

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Starting database connection…";
    [ObservableProperty] private bool hasNoEquipment;
    [ObservableProperty] private bool hasNoStudents;
    [ObservableProperty] private bool hasNoActiveBorrowings;
    [ObservableProperty] private bool hasNoSearchResults;
    [ObservableProperty] private bool hasNoStudentHistory;
    [ObservableProperty] private bool hasNoEquipmentHistory;

    [ObservableProperty] private Student? selectedStudent;
    [ObservableProperty] private Student? selectedBorrowStudent;
    [ObservableProperty] private Student? selectedHistoryStudent;
    [ObservableProperty] private Equipment? selectedEquipment;
    [ObservableProperty] private Equipment? selectedHistoryEquipment;
    [ObservableProperty] private ActiveBorrowingSummary? selectedBorrowing;

    [ObservableProperty] private string newStudentNumber = string.Empty;
    [ObservableProperty] private string newStudentName = string.Empty;
    [ObservableProperty] private string newStudentContact = string.Empty;
    [ObservableProperty] private string editStudentNumber = string.Empty;
    [ObservableProperty] private string editStudentName = string.Empty;
    [ObservableProperty] private string editStudentContact = string.Empty;

    [ObservableProperty] private string newEquipmentIdentifier = string.Empty;
    [ObservableProperty] private string newEquipmentName = string.Empty;
    [ObservableProperty] private string newEquipmentDescription = string.Empty;
    [ObservableProperty] private string editEquipmentIdentifier = string.Empty;
    [ObservableProperty] private string editEquipmentName = string.Empty;
    [ObservableProperty] private string editEquipmentDescription = string.Empty;

    [ObservableProperty] private DateTimeOffset? expectedReturnDate = DateTimeOffset.Now.Date.AddDays(7);
    [ObservableProperty] private string searchText = string.Empty;
    [ObservableProperty] private string selectedStatusFilter = "All";
    [ObservableProperty] private string selectedSortField = "Checkout date";
    [ObservableProperty] private bool sortDescending = true;

    public string EligibilityActionLabel => SelectedStudent?.IsAllowedToBorrow == true
        ? "Restrict borrowing"
        : "Allow borrowing";

    public MainViewModel(
        DatabaseInitializer databaseInitializer,
        ManageStudentsService manageStudents,
        ManageEquipmentService manageEquipment,
        BorrowEquipmentService borrowEquipment,
        ReturnEquipmentService returnEquipment,
        GetActiveBorrowingsService getActiveBorrowings,
        BrowseBorrowingsService browseBorrowings)
    {
        _databaseInitializer = databaseInitializer;
        _manageStudents = manageStudents;
        _manageEquipment = manageEquipment;
        _borrowEquipment = borrowEquipment;
        _returnEquipment = returnEquipment;
        _getActiveBorrowings = getActiveBorrowings;
        _browseBorrowings = browseBorrowings;
    }

    public async Task InitializeAsync() => await RefreshAsync();

    [RelayCommand]
    private Task RetryAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Connecting to PostgreSQL…";
        try
        {
            await _databaseInitializer.InitializeAsync();
            await LoadCollectionsAsync();
            await SearchBorrowingsCoreAsync();
            StatusMessage = "Records refreshed.";
        }
        catch (Exception exception)
        {
            StatusMessage = GetUserMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RegisterStudentAsync()
    {
        await RunAsync(async () =>
        {
            await _manageStudents.RegisterAsync(NewStudentNumber, NewStudentName, NewStudentContact);
            NewStudentNumber = NewStudentName = NewStudentContact = string.Empty;
        }, "Student registered.");
    }

    [RelayCommand]
    private async Task UpdateStudentAsync()
    {
        if (SelectedStudent is null)
        {
            StatusMessage = "Select a student to edit.";
            return;
        }

        var id = SelectedStudent.Id;
        await RunAsync(
            () => _manageStudents.UpdateAsync(id, EditStudentNumber, EditStudentName, EditStudentContact),
            "Student information updated.");
    }

    [RelayCommand]
    private async Task ToggleEligibilityAsync()
    {
        if (SelectedStudent is null)
        {
            StatusMessage = "Select a student to change borrowing eligibility.";
            return;
        }

        var id = SelectedStudent.Id;
        var isEligible = !SelectedStudent.IsAllowedToBorrow;
        await RunAsync(
            () => _manageStudents.SetEligibilityAsync(id, isEligible),
            isEligible ? "Student can borrow equipment." : "Student is restricted from new checkouts.");
    }

    [RelayCommand]
    private async Task RegisterEquipmentAsync()
    {
        await RunAsync(async () =>
        {
            await _manageEquipment.RegisterAsync(NewEquipmentIdentifier, NewEquipmentName, NewEquipmentDescription);
            NewEquipmentIdentifier = NewEquipmentName = NewEquipmentDescription = string.Empty;
        }, "Equipment registered and marked available.");
    }

    [RelayCommand]
    private async Task UpdateEquipmentAsync()
    {
        if (SelectedEquipment is null)
        {
            StatusMessage = "Select equipment to edit.";
            return;
        }

        var id = SelectedEquipment.Id;
        await RunAsync(
            () => _manageEquipment.UpdateAsync(id, EditEquipmentIdentifier, EditEquipmentName, EditEquipmentDescription),
            "Equipment information updated.");
    }

    [RelayCommand]
    private async Task BorrowSingleAsync()
    {
        var selected = AvailableEquipment.Where(option => option.IsSelected).Select(option => option.Equipment.Id).ToArray();
        if (SelectedBorrowStudent is null || selected.Length != 1 || ExpectedReturnDate is null)
        {
            StatusMessage = "Choose one eligible student, exactly one available item, and an expected return date.";
            return;
        }

        await RunAsync(
            () => _borrowEquipment.BorrowEquipmentAsync(SelectedBorrowStudent.Id, selected[0], ExpectedReturnDate.Value.Date),
            "Equipment borrowed successfully.");
    }

    [RelayCommand]
    private async Task BorrowMultipleAsync()
    {
        var selected = AvailableEquipment.Where(option => option.IsSelected).Select(option => option.Equipment.Id).Distinct().ToArray();
        if (SelectedBorrowStudent is null || selected.Length < 2 || ExpectedReturnDate is null)
        {
            StatusMessage = "Choose one eligible student, at least two distinct available items, and an expected return date.";
            return;
        }

        await RunAsync(
            async () => await _borrowEquipment.BorrowMultipleEquipmentAsync(
                SelectedBorrowStudent.Id,
                selected,
                ExpectedReturnDate.Value.Date),
            $"Checked out {selected.Length} items in one transaction.");
    }

    [RelayCommand]
    private async Task ReturnSelectedAsync()
    {
        if (SelectedBorrowing is null)
        {
            StatusMessage = "Select an active borrowing to return.";
            return;
        }

        var borrowingId = SelectedBorrowing.BorrowingId;
        await RunAsync(() => _returnEquipment.ReturnEquipmentAsync(borrowingId), "Equipment returned successfully.");
    }

    [RelayCommand]
    private async Task LoadStudentHistoryAsync()
    {
        if (SelectedHistoryStudent is null)
        {
            StatusMessage = "Select a student to view borrowing history.";
            return;
        }

        var studentId = SelectedHistoryStudent.Id;
        await RunAsync(async () =>
        {
            var records = await _browseBorrowings.GetStudentHistoryAsync(studentId);
            Replace(StudentHistory, records);
            HasNoStudentHistory = StudentHistory.Count == 0;
        }, "Student borrowing history loaded.", refresh: false);
    }

    [RelayCommand]
    private async Task LoadEquipmentHistoryAsync()
    {
        if (SelectedHistoryEquipment is null)
        {
            StatusMessage = "Select equipment to view borrowing history.";
            return;
        }

        var equipmentId = SelectedHistoryEquipment.Id;
        await RunAsync(async () =>
        {
            var records = await _browseBorrowings.GetEquipmentHistoryAsync(equipmentId);
            Replace(EquipmentHistory, records);
            HasNoEquipmentHistory = EquipmentHistory.Count == 0;
        }, "Equipment borrowing history loaded.", refresh: false);
    }

    [RelayCommand]
    private async Task SearchBorrowingsAsync()
    {
        await RunAsync(SearchBorrowingsCoreAsync, "Borrowing search completed.", refresh: false);
    }

    [RelayCommand]
    private async Task ResetSearchAsync()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = "All";
        SelectedSortField = "Checkout date";
        SortDescending = true;
        await SearchBorrowingsAsync();
    }

    private async Task SearchBorrowingsCoreAsync()
    {
        var status = Enum.TryParse<BorrowingStatusFilter>(SelectedStatusFilter, ignoreCase: true, out var parsedStatus)
            ? parsedStatus
            : BorrowingStatusFilter.All;
        var sort = SelectedSortField == "Expected return date"
            ? BorrowingSortField.ExpectedReturnDate
            : BorrowingSortField.CheckoutDate;
        var records = await _browseBorrowings.SearchAsync(
            new BorrowingSearchCriteria(SearchText, status, sort, SortDescending));
        Replace(SearchResults, records);
        HasNoSearchResults = SearchResults.Count == 0;
    }

    private async Task RunAsync(Func<Task> action, string successMessage, bool refresh = true)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Working…";
        try
        {
            await action();
            if (refresh)
            {
                await LoadCollectionsAsync();
            }

            StatusMessage = successMessage;
        }
        catch (OperationOutcomeUnknownException exception)
        {
            try
            {
                await LoadCollectionsAsync();
            }
            catch
            {
                // Keep the outcome warning visible if the database is still unavailable.
            }

            StatusMessage = GetUserMessage(exception);
        }
        catch (Exception exception)
        {
            StatusMessage = GetUserMessage(exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCollectionsAsync()
    {
        var previousStudentId = SelectedStudent?.Id;
        var previousBorrowStudentId = SelectedBorrowStudent?.Id;
        var previousHistoryStudentId = SelectedHistoryStudent?.Id;
        var previousEquipmentId = SelectedEquipment?.Id;
        var previousHistoryEquipmentId = SelectedHistoryEquipment?.Id;
        var students = await _manageStudents.GetAllAsync();
        var equipment = await _manageEquipment.GetAllAsync();
        var available = await _manageEquipment.GetAvailableAsync();
        var active = await _getActiveBorrowings.ExecuteAsync();

        Replace(Students, students);
        Replace(EquipmentCatalog, equipment);
        Replace(AvailableEquipment, available.Select(item => new EquipmentSelectionOption(item)));
        Replace(ActiveBorrowings, active);

        SelectedStudent = previousStudentId.HasValue ? Students.FirstOrDefault(item => item.Id == previousStudentId) : null;
        SelectedBorrowStudent = previousBorrowStudentId.HasValue ? Students.FirstOrDefault(item => item.Id == previousBorrowStudentId) : null;
        SelectedHistoryStudent = previousHistoryStudentId.HasValue ? Students.FirstOrDefault(item => item.Id == previousHistoryStudentId) : null;
        SelectedEquipment = previousEquipmentId.HasValue ? EquipmentCatalog.FirstOrDefault(item => item.Id == previousEquipmentId) : null;
        SelectedHistoryEquipment = previousHistoryEquipmentId.HasValue ? EquipmentCatalog.FirstOrDefault(item => item.Id == previousHistoryEquipmentId) : null;
        if (SelectedBorrowing is not null && !ActiveBorrowings.Any(item => item.BorrowingId == SelectedBorrowing.BorrowingId))
        {
            SelectedBorrowing = null;
        }
        HasNoStudents = Students.Count == 0;
        HasNoEquipment = EquipmentCatalog.Count == 0;
        HasNoActiveBorrowings = ActiveBorrowings.Count == 0;
    }

    private static void Replace<T>(ObservableCollection<T> collection, IEnumerable<T> records)
    {
        collection.Clear();
        foreach (var record in records)
        {
            collection.Add(record);
        }
    }

    private static string GetUserMessage(Exception exception) => exception switch
    {
        OperationOutcomeUnknownException => exception.Message,
        InvalidOperationException => exception.Message,
        _ => "The database operation failed. Check the PostgreSQL connection, then refresh and try again."
    };

    partial void OnSelectedStudentChanged(Student? value)
    {
        EditStudentNumber = value?.InstitutionalStudentNumber ?? string.Empty;
        EditStudentName = value?.Name ?? string.Empty;
        EditStudentContact = value?.ContactInformation ?? string.Empty;
        OnPropertyChanged(nameof(EligibilityActionLabel));
    }

    partial void OnSelectedEquipmentChanged(Equipment? value)
    {
        EditEquipmentIdentifier = value?.EquipmentIdentifier ?? string.Empty;
        EditEquipmentName = value?.Name ?? string.Empty;
        EditEquipmentDescription = value?.Description ?? string.Empty;
    }
}
