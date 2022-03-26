using SDL2;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows.Threading;

namespace SoHConfig
{
    class ControllerInfo : IDisposable
    {
        public int Id { get; }
        public IntPtr Gamepad { get; private set; }
        public IntPtr Joystick { get; private set; }
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

        public void Dispose()
        {
            if (Joystick.ToInt64() != 0)
            {
                SDL.SDL_JoystickClose(Joystick);
                Joystick = IntPtr.Zero;
            }
            if (Gamepad.ToInt64() != 0)
            {
                SDL.SDL_GameControllerClose(Gamepad);
                Gamepad = IntPtr.Zero;
            }
        }
    }

    class SDLGamepadListener
    {
        private Thread _inputThread;
        private CancellationTokenSource _cancellationTokenSource;
        private Dispatcher _dispatcher;

        public delegate void ControllerDeviceAddedDelegate(object sender, int id);
        public delegate void ControllerDeviceRemovedDelegate(object sender, int id);
        public delegate void ControllerButtonPressedDelegate(object sender, int id, byte button);
        public delegate void ControllerAxisButtonMotionDelegate(object sender, int id, byte button, short value);

        public event ControllerDeviceAddedDelegate ControllerDeviceAdded;
        public event ControllerDeviceRemovedDelegate ControllerDeviceRemoved;
        public event ControllerButtonPressedDelegate ControllerButtonPressed;
        public event ControllerAxisButtonMotionDelegate ControllerAxisButtonMotion;

        public SDLGamepadListener(Dispatcher dispatcher)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _dispatcher = dispatcher;
        }

        public void Start()
        {
            if (_inputThread == null)
            {
                _inputThread = new Thread(new ThreadStart(InputThread));
                _inputThread.Start();
            }
        }

        public void Stop()
        {
            if (_inputThread != null)
            {
                _cancellationTokenSource.Cancel();
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
                if (SDL.SDL_WaitEvent(out var sdlEvent) == 1)
                {
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
                            // Not all controllers reliably hit the max or min values. We don't
                            // want just any movement to trigger a binding, so we'll pick an 
                            // arbitrary threshold that most controllers should be able to meet.
                            var threshold = 1200;
                            if (sdlEvent.caxis.axisValue >= (short.MaxValue - threshold) ||
                                sdlEvent.caxis.axisValue <= (short.MinValue + threshold))
                            {
                                _dispatcher.Invoke(OnControllerAxisButtonMotion, sdlEvent.cdevice.which, sdlEvent.caxis.axis, sdlEvent.caxis.axisValue);
                            }
                            break;
                    }
                }
                else
                {
                    // TODO: Bubble the error up and close the application.
                    Debug.WriteLine(SDL.SDL_GetError());
                }
            }
        }

        private void OnControllerDeviceAdded(int id)
        {
            ControllerDeviceAdded?.Invoke(this, id);
        }

        private void OnControllerDeviceRemoved(int id)
        {
            ControllerDeviceRemoved?.Invoke(this, id);
        }

        private void OnControllerButtonPressed(int id, byte button)
        {
            ControllerButtonPressed?.Invoke(this, id, button);
        }

        private void OnControllerAxisButtonMotion(int id, byte axis, short value)
        {
            ControllerAxisButtonMotion?.Invoke(this, id, axis, value);
        }
    }
}
