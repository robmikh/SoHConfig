using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SoHConfig
{
    class IniContext
    {
        private string _path;
        private string[] _lines;

        public IniContext(string path)
        {
            _path = path;
            _lines = File.ReadAllLines(path);
        }

        public ControllerBinding? GetBindingForGuidString(string guidString)
        {
            ControllerBinding? binding = null;
            var startIndex = -1;
            var endIndex = -1;
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = _lines[i];
                if (line.StartsWith("[sdl controller binding "))
                {
                    if (startIndex == -1)
                    {
                        var guid = line.Replace("[sdl controller binding ", "").Replace("]", "").Trim();
                        if (guid == guidString)
                        {
                            startIndex = i + 1;
                        }
                    }
                    else
                    {
                        endIndex = i;
                    }
                }
            }
            if (startIndex != -1 && endIndex != -1 && startIndex != endIndex)
            {
                var bindingLines = _lines[startIndex..endIndex];
                binding = new ControllerBinding(guidString, bindingLines);
            }
            return binding;
        }
    }

    class ControllerBinding
    {
        private string _guid;
        private Dictionary<N64ControllerButton, int> _buttonBindings;

        public IReadOnlyDictionary<N64ControllerButton, int> Bindings => _buttonBindings;

        public ControllerBinding(string guid, string[] lines)
        {
            _guid = guid;
            _buttonBindings = new Dictionary<N64ControllerButton, int>();
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                var buttonKey = parts[0].Trim();
                var buttonValue = parts[1].Trim();
                var button = GetButtonFromString(buttonKey);
                if (button != null)
                {
                    if (int.TryParse(buttonValue, out var buttonBinding))
                    {
                        _buttonBindings.Add(button.Value, buttonBinding);
                    }
                }
            }
        }

        public ControllerBinding(string guid)
        {
            _guid = guid;
            _buttonBindings = new Dictionary<N64ControllerButton, int>();
            // TODO: Fill in default bindings
        }

        public void SetButtonBinding(N64ControllerButton button, int value)
        {
            if (_buttonBindings.ContainsKey(button))
            {
                _buttonBindings[button] = value;
            }
            else
            {
                _buttonBindings.Add(button, value);
            }
        }

        private string GetStringFromButton(N64ControllerButton button)
        {
            switch (button)
            {
                case N64ControllerButton.A:
                    return "btn_a";
                case N64ControllerButton.B:
                    return "btn_b";
                case N64ControllerButton.Start:
                    return "btn_start";
                case N64ControllerButton.CRight:
                    return "btn_cright";
                case N64ControllerButton.CLeft:
                    return "btn_cleft";
                case N64ControllerButton.CDown:
                    return "btn_cdown";
                case N64ControllerButton.CUp:
                    return "btn_cup";
                case N64ControllerButton.R:
                    return "btn_r";
                case N64ControllerButton.L:
                    return "btn_l";
                case N64ControllerButton.DPadRight:
                    return "btn_dright";
                case N64ControllerButton.DPadLeft:
                    return "btn_dleft";
                case N64ControllerButton.DPadDown:
                    return "btn_ddown";
                case N64ControllerButton.DPadUp:
                    return "btn_dup";
                case N64ControllerButton.StickRight:
                    return "btn_stickright";
                case N64ControllerButton.StickLeft:
                    return "btn_stickleft";
                case N64ControllerButton.StickDown:
                    return "btn_stickdown";
                case N64ControllerButton.StickUp:
                    return "btn_stickup";
                case N64ControllerButton.Z:
                    return "btn_z";
                default:
                    throw new ArgumentException();
            }
        }

        private N64ControllerButton? GetButtonFromString(string value)
        {
            switch (value)
            {
                case "btn_a":
                    return N64ControllerButton.A;
                case "btn_b":
                    return N64ControllerButton.B;
                case "btn_start":
                    return N64ControllerButton.Start;
                case "btn_cright":
                    return N64ControllerButton.CRight;
                case "btn_cleft":
                    return N64ControllerButton.CLeft;
                case "btn_cdown":
                    return N64ControllerButton.CDown;
                case "btn_cup":
                    return N64ControllerButton.CUp;
                case "btn_r":
                    return N64ControllerButton.R;
                case "btn_l":
                    return N64ControllerButton.L;
                case "btn_dright":
                    return N64ControllerButton.DPadRight;
                case "btn_dleft":
                    return N64ControllerButton.DPadLeft;
                case "btn_ddown":
                    return N64ControllerButton.DPadDown;
                case "btn_dup":
                    return N64ControllerButton.DPadUp;
                case "btn_stickright":
                    return N64ControllerButton.StickRight;
                case "btn_stickleft":
                    return N64ControllerButton.StickLeft;
                case "btn_stickdown":
                    return N64ControllerButton.StickDown;
                case "btn_stickup":
                    return N64ControllerButton.StickUp;
                case "btn_z":
                    return N64ControllerButton.Z;
                default:
                    return null;
            }
        }
    }

}
