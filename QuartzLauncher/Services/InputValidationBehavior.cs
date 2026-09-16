using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QuartzLauncher.Services;

public static class InputValidationBehavior
{
    public static readonly DependencyProperty IdentifierOnlyProperty =
        DependencyProperty.RegisterAttached(
            "IdentifierOnly",
            typeof(bool),
            typeof(InputValidationBehavior),
            new PropertyMetadata(false, OnIdentifierOnlyChanged));

    private static readonly DependencyProperty IsNormalizingProperty =
        DependencyProperty.RegisterAttached(
            "IsNormalizing",
            typeof(bool),
            typeof(InputValidationBehavior),
            new PropertyMetadata(false));

    public static void SetIdentifierOnly(DependencyObject element, bool value) =>
        element.SetValue(IdentifierOnlyProperty, value);

    public static bool GetIdentifierOnly(DependencyObject element) =>
        (bool)element.GetValue(IdentifierOnlyProperty);

    private static void OnIdentifierOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox) return;

        if ((bool)e.NewValue)
        {
            textBox.PreviewTextInput += OnPreviewTextInput;
            textBox.PreviewKeyDown += OnPreviewKeyDown;
            textBox.TextChanged += OnTextChanged;
            DataObject.AddPastingHandler(textBox, OnPasting);
            Normalize(textBox);
        }
        else
        {
            textBox.PreviewTextInput -= OnPreviewTextInput;
            textBox.PreviewKeyDown -= OnPreviewKeyDown;
            textBox.TextChanged -= OnTextChanged;
            DataObject.RemovePastingHandler(textBox, OnPasting);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Any(character => !IsAllowed(character)))
            e.Handled = true;
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || !IsPasteGesture(e)) return;

        e.Handled = true;
        if (Clipboard.ContainsText())
            ReplaceSelection(textBox, Filter(Clipboard.GetText()));
    }

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox || !e.DataObject.GetDataPresent(DataFormats.Text))
        {
            e.CancelCommand();
            return;
        }

        e.CancelCommand();
        ReplaceSelection(textBox, Filter(e.DataObject.GetData(DataFormats.Text)?.ToString() ?? ""));
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            Normalize(textBox);
    }

    private static void Normalize(TextBox textBox)
    {
        if ((bool)textBox.GetValue(IsNormalizingProperty)) return;

        var filtered = Filter(textBox.Text);
        if (filtered == textBox.Text) return;

        var caret = Math.Min(textBox.SelectionStart, filtered.Length);
        textBox.SetValue(IsNormalizingProperty, true);
        try
        {
            textBox.Text = filtered;
            textBox.SelectionStart = caret;
            textBox.SelectionLength = 0;
        }
        finally
        {
            textBox.ClearValue(IsNormalizingProperty);
        }
    }

    private static void ReplaceSelection(TextBox textBox, string value)
    {
        var start = Math.Clamp(textBox.SelectionStart, 0, textBox.Text.Length);
        var length = Math.Clamp(textBox.SelectionLength, 0, textBox.Text.Length - start);
        var text = textBox.Text.Remove(start, length).Insert(start, value);
        textBox.Text = Filter(text);
        textBox.SelectionStart = Math.Min(start + value.Length, textBox.Text.Length);
        textBox.SelectionLength = 0;
    }

    private static bool IsPasteGesture(KeyEventArgs e) =>
        (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        || (e.Key == Key.Insert && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));

    private static string Filter(string? value) =>
        new((value ?? "").Where(IsAllowed).ToArray());

    private static bool IsAllowed(char character) =>
        char.IsLetterOrDigit(character) || character == '_';
}
