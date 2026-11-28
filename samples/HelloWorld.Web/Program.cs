using NativeUI.Controls;
using NativeUI.Controls.Web;

int clickCount = 0;
Text greeting = new() { Value = "Hello, world" };
Button button = new() { Text = "Click me" };
button.Click += (_, _) =>
{
    clickCount++;
    greeting.Value = $"Hello, world. Clicked {clickCount} times";
};

View view = new()
{
    Children = { greeting, button }
};

await WebHost.RunAsync(view);
