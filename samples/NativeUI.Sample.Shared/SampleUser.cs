namespace NativeUI.Sample;

/// <summary>A person shown by the list and table examples.</summary>
/// <param name="Name">The display name.</param>
/// <param name="Email">The email address.</param>
/// <param name="Status">The account status.</param>
/// <param name="Age">The age in years.</param>
public sealed record SampleUser(string Name, string Email, string Status, int Age);
