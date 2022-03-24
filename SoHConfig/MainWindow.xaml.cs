using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using SDL2;

namespace SoHConfig
{
    class ControllerInfo
    {
        public int Id { get; }
        public IntPtr Gamepad { get; }
        public IntPtr Joystick { get; }
        public string GuidString { get; }
        public string Name { get; }

        public ControllerInfo(int id)
        {
            var gamepad = SDL.SDL_GameControllerOpen(id);
            if (gamepad.ToInt64() == 0)
            {
                throw new ArgumentException("Invalid controller id");
            }

            var joystick = SDL.SDL_GameControllerGetJoystick(gamepad);
            //var instanceId = SDL.SDL_JoystickInstanceID(joystick);
            var guidBuffer = new byte[33];
            SDL.SDL_JoystickGetGUIDString(SDL.SDL_JoystickGetDeviceGUID(id), guidBuffer, guidBuffer.Length);
            var guidString = Encoding.UTF8.GetString(guidBuffer).Replace("\0", "");
            var name = SDL.SDL_JoystickName(joystick);

            Id = id;
            Gamepad = gamepad;
            Joystick = joystick;
            GuidString = guidString;
            Name = name;
        }
    }

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
    }

    public partial class MainWindow : Window
    {
        private Thread _inputThread;
        private CancellationTokenSource _cancellationTokenSource;
        private Dispatcher _dispatcher;

        private IniContext? _iniContext;
        private Dictionary<int, ControllerBinding> _bindingMap;

        private ObservableCollection<ControllerInfo> _controllers;
        private Dictionary<int, ControllerInfo> _controllerMap;

        private int? _currentController;
        private N64ControllerButton? _activeButton;

        public MainWindow()
        {
            InitializeComponent();

            _cancellationTokenSource = new CancellationTokenSource();
            _dispatcher = Dispatcher;
            _controllers = new ObservableCollection<ControllerInfo>();
            _controllerMap = new Dictionary<int, ControllerInfo>();
            _bindingMap = new Dictionary<int, ControllerBinding>();
            _currentController = null;
            _activeButton = null;

            ConfigGrid.Visibility = Visibility.Collapsed;
            ControllerComboBox.ItemsSource = _controllers;
            BindingGrid.Visibility = Visibility.Collapsed;
        }

        private void OnControllerDeviceAdded(int id)
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
                    // TODO: Set the UI buttons with the current binding
                }
            }
        }

        private void OnControllerDeviceRemoved(int id)
        {
            if (_controllerMap.ContainsKey(id))
            {
                var controllerInfo = _controllerMap[id];
                _controllerMap.Remove(id);
                _controllers.Remove(controllerInfo);
                _bindingMap.Remove(id);

                SDL.SDL_JoystickClose(controllerInfo.Joystick);
                SDL.SDL_GameControllerClose(controllerInfo.Gamepad);
            }
        }

        private void OnControllerButtonPressed(int id, byte button)
        {
            if (_activeButton.HasValue && _currentController.HasValue && id == _currentController.Value)
            {
                var uiButton = GetUIButtonForN64Button(_activeButton.Value);
                var sdlButton = (SDL.SDL_GameControllerButton)button;
                var displayString = sdlButton.ToString().Replace("SDL_CONTROLLER_BUTTON_", "");
                uiButton.Content = displayString;
                var controllerInfo = _controllerMap[id];

                var binding = _bindingMap[id];
                binding.SetButtonBinding(_activeButton.Value, button);

                _activeButton = null;
                uiButton.IsChecked = false;
            }
        }

        private void OnControllerAxisButtonMotion(int id, byte axis, short value)
        {
            if (_activeButton.HasValue && _currentController.HasValue && id == _currentController.Value)
            {
                var uiButton = GetUIButtonForN64Button(_activeButton.Value);
                var sdlAxis = (SDL.SDL_GameControllerAxis)axis;
                var displalyString = sdlAxis.ToString().Replace("SDL_CONTROLLER_AXIS_", "");
                var modifier = 0;
                if (value > 0)
                {
                    displalyString += "+";
                    modifier = 1;
                }
                else
                {
                    displalyString += "-";
                    modifier = -1;
                }
                uiButton.Content = displalyString;
                var binding = _bindingMap[id];
                binding.SetButtonBinding(_activeButton.Value, (axis + (1 << 9)) * modifier);
                _activeButton = null;
                uiButton.IsChecked = false;
            }
        }

        private void InputThread()
        {
            Thread.CurrentThread.Name = "Input Thread";

            SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_THREAD, "1");
            SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);

            var token = _cancellationTokenSource.Token;
            while (!token.IsCancellationRequested)
            {
                SDL.SDL_PollEvent(out var sdlEvent);

                switch (sdlEvent.type)
                {
                    case SDL.SDL_EventType.SDL_CONTROLLERDEVICEADDED:
                        _dispatcher.Invoke(OnControllerDeviceAdded, sdlEvent.cdevice.which);
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERDEVICEREMOVED:
                        _dispatcher.Invoke(OnControllerDeviceRemoved, sdlEvent.cdevice.which);
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERBUTTONUP:
                        _dispatcher.Invoke(OnControllerButtonPressed, sdlEvent.cdevice.which, sdlEvent.cbutton.button);
                        break;
                    case SDL.SDL_EventType.SDL_CONTROLLERAXISMOTION:
                        if (sdlEvent.caxis.axisValue == short.MaxValue || sdlEvent.caxis.axisValue == short.MinValue)
                        {
                            _dispatcher.Invoke(OnControllerAxisButtonMotion, sdlEvent.cdevice.which, sdlEvent.caxis.axis, sdlEvent.caxis.axisValue);
                        }
                        break;
                }
            }
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
            }
        }

        private void AButton_Click(object sender, RoutedEventArgs e)
        {
            UntoggleActiveButton();
            _activeButton = N64ControllerButton.A;
        }

        private void BButton_Click(object sender, RoutedEventArgs e)
        {
            UntoggleActiveButton();
            _activeButton = N64ControllerButton.B;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            UntoggleActiveButton();
            _activeButton = N64ControllerButton.Start;
        }

        private void ControllerComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_iniContext != null)
            {
                var comboBox = (ComboBox)sender;
                var controllerInfo = comboBox.SelectedItem as ControllerInfo;
                if (controllerInfo != null)
                {
                    BindingGrid.Visibility = Visibility.Visible;
                    _currentController = controllerInfo.Id;
                    var temp = _iniContext.GetBindingForGuidString(controllerInfo.GuidString);
                }
                else
                {
                    BindingGrid.Visibility = Visibility.Collapsed;
                    _currentController = null;
                }
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
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
            _inputThread = new Thread(new ThreadStart(InputThread));
            _inputThread.Start();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_inputThread != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }
    }
}
