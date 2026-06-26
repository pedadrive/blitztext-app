using System.Windows;
using Blitztext.App.Platform;
using Blitztext.Core;
using Blitztext.Core.Abstractions;
using Blitztext.Core.Models;

namespace Blitztext.App.UI;

public partial class SettingsWindow : Window
{
    private readonly AppCoordinator _coordinator;
    private readonly ICredentialStore _credentials;
    private bool _loading;

    public SettingsWindow(AppCoordinator coordinator, ICredentialStore credentials)
    {
        _coordinator = coordinator;
        _credentials = credentials;
        InitializeComponent();
        LoadModelList();
        LoadFromSettings();
        LocalModelBox.SelectionChanged += (_, _) => { if (!_loading) UpdateModelStatus(); };
    }

    private void LoadModelList()
    {
        LocalModelBox.Items.Clear();
        foreach (var model in LocalModelCatalog.SupportedModelNames)
            LocalModelBox.Items.Add(LocalModelCatalog.DisplayName(model));
    }

    private void LoadFromSettings()
    {
        _loading = true;
        var s = _coordinator.Settings;

        if (_credentials.HasValue(CredentialKey.OpenAiApiKey))
            ApiKeyBox.Password = "";

        LanguageBox.Text = s.Transcription.Language;
        HotkeyModeBox.SelectedIndex = s.App.HotkeyMode == HotkeyMode.Toggle ? 1 : 0;
        ToneBox.SelectedIndex = (int)s.TextImprovement.Tone;
        EmojiDensityBox.SelectedIndex = (int)s.EmojiText.EmojiDensity;
        CustomTermsBox.Text = string.Join(", ", s.TextImprovement.CustomTerms);
        SecureLocalModeBox.IsChecked = s.App.SecureLocalModeEnabled;

        var modelIndex = LocalModelCatalog.SupportedModelNames
            .ToList()
            .IndexOf(_coordinator.SelectedLocalModelName);
        LocalModelBox.SelectedIndex = modelIndex >= 0 ? modelIndex : 0;

        UpdateModelStatus();
        _loading = false;
    }

    private void ApplyToSettings()
    {
        var s = _coordinator.Settings;
        s.Transcription.Language = LanguageBox.Text.Trim();
        s.App.HotkeyMode = HotkeyModeBox.SelectedIndex == 1 ? HotkeyMode.Toggle : HotkeyMode.Hold;
        s.TextImprovement.Tone = (TextTone)Math.Max(0, ToneBox.SelectedIndex);
        s.EmojiText.EmojiDensity = (EmojiDensity)Math.Max(0, EmojiDensityBox.SelectedIndex);
        s.TextImprovement.CustomTerms = CustomTermsBox.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        s.App.SecureLocalModeEnabled = SecureLocalModeBox.IsChecked == true;

        if (LocalModelBox.SelectedIndex >= 0)
            s.App.SelectedLocalTranscriptionModelName =
                LocalModelCatalog.SupportedModelNames[LocalModelBox.SelectedIndex];

        _coordinator.SaveSettings();
    }

    private void SaveKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (key.Length == 0)
        {
            System.Windows.MessageBox.Show(this, "Bitte einen API Key eingeben.", "Blitztext");
            return;
        }
        _coordinator.SetApiKey(key);
        ApiKeyBox.Password = "";
        MessageBox.Show(this, "API Key gespeichert.", "Blitztext");
    }

    private async void InstallModelButton_Click(object sender, RoutedEventArgs e)
    {
        if (LocalModelBox.SelectedIndex < 0) return;
        var modelName = LocalModelCatalog.SupportedModelNames[LocalModelBox.SelectedIndex];

        InstallModelButton.IsEnabled = false;
        DownloadProgress.Visibility = Visibility.Visible;
        DownloadProgress.Value = 0;
        DownloadStatus.Text = "Download startet …";

        try
        {
            var downloader = new WhisperModelDownloader();
            var progress = new Progress<double>(p =>
            {
                DownloadProgress.Value = p;
                DownloadStatus.Text = $"Download {(int)(p * 100)} %";
            });
            await downloader.DownloadAsync(modelName, progress);
            DownloadStatus.Text = $"{LocalModelCatalog.DisplayName(modelName)} ist installiert.";
        }
        catch (Exception ex)
        {
            DownloadStatus.Text = "Fehler: " + ex.Message;
        }
        finally
        {
            DownloadProgress.Visibility = Visibility.Collapsed;
            InstallModelButton.IsEnabled = true;
            UpdateModelStatus();
        }
    }

    private void UpdateModelStatus()
    {
        if (LocalModelBox.SelectedIndex < 0) return;
        var modelName = LocalModelCatalog.SupportedModelNames[LocalModelBox.SelectedIndex];
        var installed = new WhisperNetLocalTranscriber().IsModelInstalled(modelName);
        InstallModelButton.Content = installed
            ? $"{LocalModelCatalog.DisplayName(modelName)} ist installiert"
            : $"{LocalModelCatalog.DisplayName(modelName)} herunterladen";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyToSettings();
        Close();
    }
}
