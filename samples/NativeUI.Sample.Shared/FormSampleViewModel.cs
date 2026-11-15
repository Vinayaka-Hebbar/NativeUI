using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NativeUI.Sample;

/// <summary>Supplies application-owned business validation and submission for the form example.</summary>
public partial class FormSampleViewModel : ObservableValidator
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Enter your full name.")]
    [MinLength(2, ErrorMessage = "Use at least two characters.")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    private string _email = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Choose a delivery method.")]
    private string? _delivery = "standard";

    [ObservableProperty]
    private bool _notifications = true;

    [ObservableProperty]
    private string _status = "Enter your details, then Save. Reset restores the form's initial UI values.";

    /// <summary>Creates a model with initial validation results available to bindings.</summary>
    public FormSampleViewModel() => ValidateAllProperties();

    [RelayCommand]
    private void Save() => Status = $"Saved {Name} ({Email}). Notifications: {(Notifications ? "on" : "off")}.";
}
