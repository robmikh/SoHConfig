using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;
using SDL2;
using Xceed.Wpf.Toolkit;

namespace SoHConfig
{
    enum N64ControllerButton
    {
        A,
        B,
        Start,
        CRight,
        CLeft,
        CDown,
        CUp,
        R,
        L,
        DPadRight,
        DPadLeft,
        DPadDown,
        DPadUp,
        StickRight,
        StickLeft,
        StickDown,
        StickUp,
        Z,
    }

    enum ControllerAxisFloat
    {
        LeftX,
        LeftY,
    }

    enum ControllerAxisInt
    {
        RightX,
        RightY,
        TriggerLeft,
        TriggerRight,
    }

    class BackendEntry
    {
        public string DisplayName { get; }
        public string SettingsString { get; }

        public BackendEntry(string displayName, string settings)
        {
            DisplayName = displayName;
            SettingsString = settings;
        }
    }

    public partial class MainWindow : Window
    {
        private SDLGamepadListener _gamepadListener;
        private IniContext? _iniContext;
        private Dictionary<int, ControllerBinding> _bindingMap;

        private ObservableCollection<ControllerInfo> _controllers;
        private Dictionary<int, ControllerInfo> _controllerMap;
        private ObservableCollection<BackendEntry> _backends;

        private int? _currentController;
        private N64ControllerButton? _activeButton;

        public MainWindow()
        {
            InitializeComponent();

            _gamepadListener = new SDLGamepadListener(Dispatcher);
            _gamepadListener.ControllerDeviceAdded += OnControllerDeviceAdded;
            _gamepadListener.ControllerDeviceRemoved += OnControllerDeviceRemoved;
            _gamepadListener.ControllerButtonPressed += OnControllerButtonPressed;
            _gamepadListener.ControllerAxisButtonMotion += OnControllerAxisButtonMotion;

            _controllers = new ObservableCollection<ControllerInfo>();
            _controllerMap = new Dictionary<int, ControllerInfo>();
            _bindingMap = new Dictionary<int, ControllerBinding>();
            _currentController = null;
            _activeButton = null;

            _backends = new ObservableCollection<BackendEntry>();
            _backends.Add(new BackendEntry("Direct3D11", ""));
            _backends.Add(new BackendEntry("OpenGL", "sdl"));

            ConfigGrid.Visibility = Visibility.Collapsed;
            ControllerComboBox.ItemsSource = _controllers;
            BackendComboBox.ItemsSource = _backends;
            BindingGrid.Visibility = Visibility.Collapsed;
        }

        private void OnControllerDeviceAdded(object sender, int id)
        {
            if (!_controllerMap.ContainsKey(id))
            {
                if (SDL.SDL_IsGameController(id) == SDL.SDL_bool.SDL_TRUE)
                {
                    var controllerInfo = new ControllerInfo(id);
                    _controllers.Add(controllerInfo);
                    _controllerMap.Add(id, controllerInfo);

                    var binding = _iniContext.GetBindingForGuidString(controllerInfo.GuidString);
                    if (binding == null)
                    {
                        binding = new ControllerBinding(controllerInfo.GuidString);
                    }
                    _bindingMap.Add(id, binding);
                }
            }
        }

        private void OnControllerDeviceRemoved(object sender, int id)
        {
            if (_controllerMap.ContainsKey(id))
            {
                var controllerInfo = _controllerMap[id];
                _controllerMap.Remove(id);
                _controllers.Remove(controllerInfo);
                _bindingMap.Remove(id);

                controllerInfo.Dispose();
            }
        }

        private void OnControllerButtonPressed(object sender, int id, byte button)
        {
            if (_activeButton.HasValue && _currentController.HasValue && id == _currentController.Value)
            {
                var sdlButton = (SDL.SDL_GameControllerButton)button;
                var displayString = sdlButton.ToString().Replace("SDL_CONTROLLER_BUTTON_", "");
                SetButtonBindingForController(id, _activeButton.Value, button, displayString);
            }
        }

        private void OnControllerAxisButtonMotion(object sender, int id, byte axis, short value)
        {
            if (_activeButton.HasValue && _currentController.HasValue && id == _currentController.Value)
            {
                var sdlAxis = (SDL.SDL_GameControllerAxis)axis;
                var displayString = sdlAxis.ToString().Replace("SDL_CONTROLLER_AXIS_", "");
                var modifier = 0;
                if (value > 0)
                {
                    displayString += "+";
                    modifier = 1;
                }
                else
                {
                    displayString += "-";
                    modifier = -1;
                }
                var bindingValue = (axis + (1 << 9)) * modifier;
                SetButtonBindingForController(id, _activeButton.Value, bindingValue, displayString);
            }
        }

        private void SetButtonBindingForController(int controllerId, N64ControllerButton button, int value, string displayString)
        {
            Debug.Assert(_activeButton.HasValue && _activeButton.Value == button);

            var uiButton = GetUIButtonForN64Button(button);
            uiButton.Content = displayString;
            var binding = _bindingMap[controllerId];
            binding.SetButtonBinding(button, value);
            _activeButton = null;
            uiButton.IsChecked = false;
        }

        private ToggleButton GetUIButtonForN64Button(N64ControllerButton button)
        {
            switch (button)
            {
                case N64ControllerButton.A:
                    return AButton;
                case N64ControllerButton.B:
                    return BButton;
                case N64ControllerButton.Start:
                    return StartButton;
                case N64ControllerButton.CRight:
                    return CRightButton;
                case N64ControllerButton.CLeft:
                    return CLeftButton;
                case N64ControllerButton.CDown:
                    return CDownButton;
                case N64ControllerButton.CUp:
                    return CUpButton;
                case N64ControllerButton.R:
                    return RButton;
                case N64ControllerButton.L:
                    return LButton;
                case N64ControllerButton.DPadRight:
                    return DPadRightButton;
                case N64ControllerButton.DPadLeft:
                    return DPadLeftButton;
                case N64ControllerButton.DPadDown:
                    return DPadDownButton;
                case N64ControllerButton.DPadUp:
                    return DPadUpButton;
                case N64ControllerButton.StickRight:
                    return StickRightButton;
                case N64ControllerButton.StickLeft:
                    return StickLeftButton;
                case N64ControllerButton.StickDown:
                    return StickDownButton;
                case N64ControllerButton.StickUp:
                    return StickUpButton;
                case N64ControllerButton.Z:
                    return ZButton;
                default:
                    throw new ArgumentException();
            }
        }

        private DecimalUpDown GetUIUpDownForAxisFloat(ControllerAxisFloat axis)
        {
            switch (axis)
            {
                case ControllerAxisFloat.LeftX:
                    return LeftXThreshold;
                case ControllerAxisFloat.LeftY:
                    return LeftYThreshold;
                default:
                    throw new ArgumentException();
            }
        }

        private IntegerUpDown GetUIUpDownForAxisInt(ControllerAxisInt axis)
        {
            switch (axis)
            {
                case ControllerAxisInt.RightX:
                    return RightXThreshold;
                case ControllerAxisInt.RightY:
                    return RightYThreshold;
                case ControllerAxisInt.TriggerLeft:
                    return TriggerLeftThreshold;
                case ControllerAxisInt.TriggerRight:
                    return TriggerRightThreshold;
                default:
                    throw new ArgumentException();
            }
        }

        private void UntoggleActiveButton()
        {
            if (_activeButton.HasValue)
            {
                var button = GetUIButtonForN64Button(_activeButton.Value);
                button.IsChecked = false;
                _activeButton = null;
            }
        }

        private void OnButtonClick(object sender, N64ControllerButton button)
        {
            var uiButton = (ToggleButton)sender;
            if (uiButton.IsChecked == true)
            {
                UntoggleActiveButton();
                _activeButton = button;
            }
            else
            {
                _activeButton = null;
            }
        }

        private void AButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.A);
        }

        private void BButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.B);
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.Start);
        }

        private string GetDisplayStringForBindingValue(int value)
        {
            if (value < 0 || value >= (1 << 9))
            {
                var axis = Math.Abs(value) - (1 << 9);
                var sdlAxis = (SDL.SDL_GameControllerAxis)axis;
                var displayString = sdlAxis.ToString().Replace("SDL_CONTROLLER_AXIS_", "");
                if (value > 0)
                {
                    displayString += "+";
                }
                else
                {
                    displayString += "-";
                }
                return displayString;
            }
            else
            {
                var sdlButton = (SDL.SDL_GameControllerButton)value;
                var displayString = sdlButton.ToString().Replace("SDL_CONTROLLER_BUTTON_", "");
                return displayString;
            }
        }

        private void ControllerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_iniContext != null)
            {
                UntoggleActiveButton();
                var comboBox = (ComboBox)sender;
                var controllerInfo = comboBox.SelectedItem as ControllerInfo;
                if (controllerInfo != null)
                {
                    BindingGrid.Visibility = Visibility.Visible;
                    SaveButton.IsEnabled = true;
                    ResetButton.IsEnabled = true;
                    _currentController = controllerInfo.Id;
                    UpdateUIToCurrentBinding();
                }
                else
                {
                    BindingGrid.Visibility = Visibility.Collapsed;
                    SaveButton.IsEnabled = false;
                    ResetButton.IsEnabled = false;
                    _currentController = null;
                }
            }
        }

        private void UpdateUIToCurrentBinding()
        {
            if (_currentController != null)
            {
                var binding = _bindingMap[_currentController.Value];
                foreach (var (button, value) in binding.ButtonBindings)
                {
                    var uiButton = GetUIButtonForN64Button(button);
                    uiButton.Content = GetDisplayStringForBindingValue(value);
                }
                foreach (var (axis, value) in binding.FloatThresholdBindings)
                {
                    var uiUpDown = GetUIUpDownForAxisFloat(axis);
                    uiUpDown.Value = (decimal?)value;
                }
                foreach (var (axis, value) in binding.IntThresholdBindings)
                {
                    var uiUpDown = GetUIUpDownForAxisInt(axis);
                    uiUpDown.Value = value;
                }
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "INI files | *.ini";
            if (dialog.ShowDialog() == true)
            {
                var path = dialog.FileName;
                _iniContext = new IniContext(path);
                InitGrid.Visibility = Visibility.Collapsed;
                ConfigGrid.Visibility = Visibility.Visible;
                StartConfig();
            }
        }

        private void StartConfig()
        {
            _gamepadListener.Start();

            var backendSettingsString = _iniContext.GetGfxBackendValue();
            var backendIndex = FindBackendIndexFromSettingsString(backendSettingsString);
            if (backendIndex.HasValue)
            {
                BackendComboBox.SelectedIndex = backendIndex.Value;
            }
        }

        private int? FindBackendIndexFromSettingsString(string value)
        {
            int? index = null;
            for (int i = 0; i < _backends.Count; i++)
            {
                var backend = _backends[i];
                if (backend.SettingsString == value)
                {
                    index = i;
                    break;
                }
            }
            return index;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _gamepadListener.Stop();
        }

        private void DPadUpButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.DPadUp);
        }

        private void DPadDownButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.DPadDown);
        }

        private void DPadLeftButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.DPadLeft);
        }

        private void DPadRightButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.DPadRight);
        }

        private void StickUpButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.StickUp);
        }

        private void StickDownButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.StickDown);
        }

        private void StickLeftButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.StickLeft);
        }

        private void StickRightButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.StickRight);
        }

        private void CUpButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.CUp);
        }

        private void CDownButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.CDown);
        }

        private void CLeftButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.CLeft);
        }

        private void CRightButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.CRight);
        }

        private void ZButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.Z);
        }

        private void LButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.L);
        }

        private void RButton_Click(object sender, RoutedEventArgs e)
        {
            OnButtonClick(sender, N64ControllerButton.R);
        }

        private void OnFloatUpDownChanged(object sender, ControllerAxisFloat axis)
        {
            if (_currentController != null)
            {
                var upDown = (DecimalUpDown)sender;
                if (upDown.Value.HasValue)
                {
                    var binding = _bindingMap[_currentController.Value];
                    binding.SetAxisFloatBinding(axis, (float)upDown.Value.Value);
                }
            }
        }

        private void OnIntUpDownChanged(object sender, ControllerAxisInt axis)
        {
            if (_currentController != null)
            {
                var upDown = (IntegerUpDown)sender;
                if (upDown.Value.HasValue)
                {
                    var binding = _bindingMap[_currentController.Value];
                    binding.SetAxisIntBinding(axis, upDown.Value.Value);
                }
            }
        }

        private void LeftXThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            OnFloatUpDownChanged(sender, ControllerAxisFloat.LeftX);
        }

        private void LeftYThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            OnFloatUpDownChanged(sender, ControllerAxisFloat.LeftY);
        }

        private void RightXThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            OnIntUpDownChanged(sender, ControllerAxisInt.RightX);
        }

        private void RightYThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            OnIntUpDownChanged(sender, ControllerAxisInt.RightY);
        }

        private void TriggerLeftThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            OnIntUpDownChanged(sender, ControllerAxisInt.TriggerLeft);
        }

        private void TriggerRightThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            OnIntUpDownChanged(sender, ControllerAxisInt.TriggerRight);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentController != null)
            {
                var binding = _bindingMap[_currentController.Value];
                binding.ResetToDefault();
                UpdateUIToCurrentBinding();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentController != null)
            {
                var binding = _bindingMap[_currentController.Value];
                _iniContext.SaveBinding(binding);
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            System.Windows.MessageBox.Show(
                $"SoHConfg v{version}", 
                "About", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information, 
                MessageBoxResult.OK);
        }

        private void WindowSaveButton_Click(object sender, RoutedEventArgs e)
        {
            var backendEntry = BackendComboBox.SelectedItem as BackendEntry;
            if (backendEntry != null)
            {
                _iniContext.SaveGfxBackend(backendEntry.SettingsString);
            }
        }

        private void CheckROMButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "N64 ROM files | *.z64";
            if (dialog.ShowDialog() == true)
            {
                var path = dialog.FileName;
                var buffer = File.ReadAllBytes(path);
                using (var cyptoProvider = HashAlgorithm.Create("SHA1"))
                {
                    var hash = BitConverter.ToString(cyptoProvider.ComputeHash(buffer)).Replace("-", "").ToLower();
                    var message = "You're using the wrong ROM!";
                    var icon = MessageBoxImage.Error;
                    if (hash == "cee6bc3c2a634b41728f2af8da54d9bf8cc14099")
                    {
                        message = "You're using the right ROM! Good job!";
                        icon = MessageBoxImage.Information;
                    }
                    System.Windows.MessageBox.Show(
                        $"Hash: {hash}\n{message}",
                        "ROM Checker",
                        MessageBoxButton.OK,
                        icon,
                        MessageBoxResult.OK);
                }
            }
        }
    }
}
