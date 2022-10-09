﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard
{
    public class PowerModeControl : AbstractComboBoxFeatureCardControl<PowerModeState>
    {
        private readonly PowerModeListener _powerModeListener = IoCContainer.Resolve<PowerModeListener>();
        private readonly PowerPlanListener _powerPlanListener = IoCContainer.Resolve<PowerPlanListener>();
        private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();

        private readonly Button _configButton = new()
        {
            Icon = SymbolRegular.Settings24,
            FontSize = 20,
            Margin = new(8, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        private readonly TextBlock _actualPowerModeTextBlock = new()
        {
            Margin = new(8, 4, 0, 0),
            FontSize = 12,
        };

        public PowerModeControl()
        {
            Icon = SymbolRegular.Gauge24;
            Title = "Power Mode";
            Subtitle = "Select your preferred power mode.\nYou can switch mode with Fn+Q.";

            _powerModeListener.Changed += PowerModeListener_Changed;
            _powerPlanListener.Changed += PowerPlanListener_Changed;
        }

        private void PowerModeListener_Changed(object? sender, PowerModeState e) => Dispatcher.Invoke(async () =>
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        });

        private async Task UpdateActualPowerModeTextBlock()
        {
            var targetState = await _powerModeFeature.GetStateAsync();
            var currentState = await _powerModeFeature.GetActualStateAsync();
            bool isGodModeActive = targetState == PowerModeState.GodMode && currentState == PowerModeState.Performance;
            bool isStateSupported = targetState == currentState || isGodModeActive;

            _actualPowerModeTextBlock.Visibility = isStateSupported ? Visibility.Collapsed : Visibility.Visible;

            if (isStateSupported)
                return;

            _actualPowerModeTextBlock.Inlines.Clear();

            var text = new List<(Run, string?)>
            {
                (new Run("Active mode: "), "TextFillColorTertiaryBrush"),
                (new Run(currentState.GetDisplayName()) { FontWeight = FontWeights.DemiBold }, null),
            };

            if (await Power.IsPowerAdapterConnectedAsync() != PowerAdapterStatus.Connected)
                text.Add((new Run("\nThe selected mode will take effect when plugged in."), "TextFillColorTertiaryBrush"));

            foreach (var (run, foreground) in text)
            {
                _actualPowerModeTextBlock.Inlines.Add(run);
                if (foreground != null)
                    run.SetResourceReference(ForegroundProperty, foreground);
            }
        }

        protected override async Task OnRefreshAsync()
        {
            await base.OnRefreshAsync();
            await UpdateActualPowerModeTextBlock();
        }

        private void PowerPlanListener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        });

        protected override async Task OnStateChange(ComboBox comboBox, IFeature<PowerModeState> feature, PowerModeState? newValue, PowerModeState? oldValue)
        {
            try
            {
                await base.OnStateChange(comboBox, feature, newValue, oldValue);

                if (!comboBox.TryGetSelectedItem(out PowerModeState state))
                    return;

                var mi = await Compatibility.GetMachineInformationAsync();

                switch (state)
                {
                    case PowerModeState.Balance when mi.Properties.SupportsIntelligentSubMode:
                        _configButton.ToolTip = "Settings";
                        _configButton.Visibility = Visibility.Visible;
                        break;
                    case PowerModeState.GodMode:
                        _configButton.ToolTip = "Settings";
                        _configButton.Visibility = Visibility.Visible;
                        break;
                    default:
                        _configButton.ToolTip = null;
                        _configButton.Visibility = Visibility.Collapsed;
                        break;
                }
            }
            catch (InvalidOperationException ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"State change failed.", ex);

                await SnackbarHelper.ShowAsync("Couldn't change Power Mode", ex.Message, true);
            }
        }

        protected override FrameworkElement? GetAccessory(ComboBox comboBox)
        {
            _configButton.Click += ConfigButton_Click;

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            stackPanel.Children.Add(_configButton);
            stackPanel.Children.Add(comboBox);

            var verticalStackPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center
            };
            verticalStackPanel.Children.Add(stackPanel);
            verticalStackPanel.Children.Add(_actualPowerModeTextBlock);

            return verticalStackPanel;
        }

        private void ConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_comboBox.TryGetSelectedItem(out PowerModeState state))
                return;

            if (state == PowerModeState.Balance)
            {
                var window = new BalanceModeSettingsWindow
                {
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                };
                window.ShowDialog();
            }

            if (state == PowerModeState.GodMode)
            {
                var window = new GodModeSettingsWindow
                {
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ShowInTaskbar = false,
                };
                window.ShowDialog();
            }
        }
    }
}
