using CommunityToolkit.Mvvm.ComponentModel;
using EquipmentBorrowing.Domain;

namespace EquipmentBorrowing.Desktop.ViewModels;

public partial class EquipmentSelectionOption(Equipment equipment) : ObservableObject
{
    public Equipment Equipment { get; } = equipment;

    [ObservableProperty]
    private bool isSelected;
}
