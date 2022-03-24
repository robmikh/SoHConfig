using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public Dictionary<N64ControllerButton, byte> Bindings { get; }

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
            var guidString = Encoding.UTF8.GetString(guidBuffer);
            var name = SDL.SDL_JoystickName(joystick);

            Id = id;
            Gamepad = gamepad;
            Joystick = joystick;
            GuidString = guidString;
            Name = name;

            // TODO: Load bindings from ini
            Bindings = new Dictionary<N64ControllerButton, byte>();
        }
    }

    enum N64ControllerButton
    {
        A,
        B,
        Start,
    }


    public partial class MainWindow : Window
    {
        private Thread _inputThread;
        private Dispatcher _dispatcher;

        private ObservableCollection<ControllerInfo> _controllers;
        private Dictionary<int, ControllerInfo> _controllerMap;

        private int? _currentController;
        private N64ControllerButton? _activeButton;

        public MainWindow()
        {
            InitializeComponent();

            _dispatcher = Dispatcher;
            _controllers = new ObservableCollection<ControllerInfo>();
            _controllerMap = new Dictionary<int, ControllerInfo>();
            _currentController = null;
            _activeButton = null;

            ControllerComboBox.ItemsSource = _controllers;
            BindingGrid.Visibility = Visibility.Collapsed;

            _inputThread = new Thread(new ThreadStart(InputThread));
            _inputThread.Start();
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
                var displalyString = sdlButton.ToString().Replace("SDL_CONTROLLER_BUTTON_", "");
                uiButton.Content = displalyString;
                var controllerInfo = _controllerMap[id];
                controllerInfo.Bindings.Add(_activeButton.Value, button);
                _activeButton = null;
                uiButton.IsChecked = false;
            }
        }

        private void InputThread()
        {
            SDL.SDL_SetHint(SDL.SDL_HINT_JOYSTICK_THREAD, "1");
            SDL.SDL_Init(SDL.SDL_INIT_GAMECONTROLLER);

            bool quit = false;
            while (!quit)
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
            var comboBox = (ComboBox)sender;
            var controllerInfo = comboBox.SelectedItem as ControllerInfo;
            if (controllerInfo != null)
            {
                BindingGrid.Visibility = Visibility.Visible;
                _currentController = controllerInfo.Id;
            }
            else
            {
                BindingGrid.Visibility = Visibility.Collapsed;
                _currentController = null;
            }
        }
    }
}
